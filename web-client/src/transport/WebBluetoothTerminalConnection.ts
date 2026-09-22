import type { BluetoothPort, DeviceHandle } from "./BluetoothPort";
import { LineFramer, commandBytes } from "./LineFramer";
import { SERVICE_UUID, TX_UUID, RX_UUID, SECURITY_REQUEST_UUID } from "../protocol/constants";
import { Signal } from "../app/events";
import { abortCheck, deadline, HallzeeError } from "../app/errors";
import { bluetoothDisconnected, bluetoothError, isBluetoothBusy, type BluetoothStage } from "./BluetoothErrors";
function connectionCheck(signal: AbortSignal) {
  if (signal.aborted && signal.reason instanceof HallzeeError) throw signal.reason;
  abortCheck(signal);
}
export class WebBluetoothTerminalConnection implements BluetoothPort {
  private device?: BluetoothDevice;
  private tx?: BluetoothRemoteGATTCharacteristic;
  private rx?: BluetoothRemoteGATTCharacteristic;
  private framer = new LineFramer();
  private lines = new Signal<string>();
  private dropped = new Signal<void>();
  private generation = 0;
  private connection = new AbortController();
  private writes: Promise<void> = Promise.resolve();
  // A timeout/abort stops our caller, not necessarily Chrome's native operation.
  // Keep the raw operation in this queue until it settles, even across disconnects.
  private operations: Promise<void> = Promise.resolve();
  private activeStage?: BluetoothStage;
  private reconnectAfter = 0;
  onLine = this.lines.subscribe;
  onDisconnect = this.dropped.subscribe;
  async requestDevice() {
    try {
      return await navigator.bluetooth.requestDevice({ filters: [{ services: [SERVICE_UUID] }] });
    } catch (error) {
      throw bluetoothError(error, "chooser");
    }
  }
  getRememberedDevices() {
    return typeof navigator.bluetooth?.getDevices === "function"
      ? navigator.bluetooth.getDevices()
      : Promise.resolve([]);
  }
  private receive = (event: Event) => {
    const value = (event.target as BluetoothRemoteGATTCharacteristic).value;
    if (!value) return;
    try {
      for (const line of this.framer.push(value)) this.lines.emit(line);
    } catch {
      this.disconnect();
      this.dropped.emit();
    }
  };
  private lost = () => {
    this.connection.abort(bluetoothDisconnected(this.activeStage));
    this.disconnect();
    this.dropped.emit();
  };
  private disconnectGatt(server: BluetoothRemoteGATTServer) {
    // disconnect() returns before the OS has necessarily released the link.
    this.reconnectAfter = Date.now() + 800;
    server.disconnect();
  }
  private async step<T>(
    stage: BluetoothStage,
    task: () => Promise<T>,
    valid: () => void,
    signal: AbortSignal,
    ms = 10000,
  ): Promise<T> {
    const operation = this.operations.then(async () => {
      valid();
      this.activeStage = stage;
      try {
        for (let attempt = 0; ; attempt++) {
          valid();
          try {
            const value = await task();
            valid();
            return value;
          } catch (error) {
            valid();
            // Only retry rejected setup operations. Never replay a command write:
            // the terminal may already have received some of its bytes.
            const retry = stage !== "write" && (isBluetoothBusy(error) ||
              (stage === "connect" && bluetoothError(error, stage).retryable));
            if (!retry || attempt >= 2) throw error;
            await new Promise((resolve) => setTimeout(resolve, 800));
          }
        }
      } finally {
        this.activeStage = undefined;
      }
    });
    this.operations = operation.then(() => {}, () => {});
    try {
      return await deadline(operation, signal, ms);
    } catch (error) {
      valid();
      throw bluetoothError(error, stage);
    }
  }
  async connect(handle: DeviceHandle, signal?: AbortSignal) {
    this.disconnect();
    const generation = this.generation;
    const device = handle as BluetoothDevice;
    this.connection = new AbortController();
    const cancelled = AbortSignal.any([this.connection.signal, ...(signal ? [signal] : [])]);
    const valid = () => {
      connectionCheck(cancelled);
      if (generation !== this.generation) throw new HallzeeError("CANCELLED");
    };
    const step = <T>(stage: BluetoothStage, task: () => Promise<T>, ms?: number) =>
      this.step(stage, task, valid, cancelled, ms);
    try {
      valid();
      if (!device.gatt) throw new HallzeeError("BLUETOOTH_UNAVAILABLE");
      try {
        await deadline(this.operations, cancelled, 10000);
      } catch (error) {
        if (error instanceof HallzeeError && error.code === "TIMEOUT")
          throw new HallzeeError("BLUETOOTH_OPERATION_PENDING",
            "Chrome has not finished the previous Bluetooth operation in this tab. Hallzee has paused connection attempts to avoid overlapping them. Wait briefly and retry; if it stays stuck, close and reopen Hallzee. Keep Hallzee site data and saved ownership.");
        throw error;
      }
      valid();
      const pause = this.reconnectAfter - Date.now();
      if (pause > 0)
        await deadline(new Promise((resolve) => setTimeout(resolve, pause)), cancelled);
      valid();
      this.device = device;
      device.addEventListener("gattserverdisconnected", this.lost);
      const server = await step(
        "connect",
        async () => {
          const connected = await device.gatt!.connect();
          if (cancelled.aborted || generation !== this.generation) {
            // This cleanup stays inside the raw queue: it cannot disconnect a
            // replacement connection that has already begun using this handle.
            this.disconnectGatt(connected);
            throw new HallzeeError("CANCELLED");
          }
          return connected;
        },
        15000,
      );
      const service = await step("service", () => server.getPrimaryService(SERVICE_UUID));
      const tx = await step("characteristics", () => service.getCharacteristic(TX_UUID));
      const rx = await step("characteristics", () => service.getCharacteristic(RX_UUID));
      if (!rx.properties.write || !rx.writeValueWithResponse)
        throw new HallzeeError("ACK_WRITES_REQUIRED");
      if (!tx.properties.read || !tx.readValue)
        throw new HallzeeError(
          "ENCRYPTED_READ_REQUIRED",
          "This terminal is missing the readable Hallzee channel needed to establish secure pairing. Verify its firmware.",
        );
      // Firmware protects TX reads with encrypted permissions. Establish the
      // Just Works link before HELLO starts the eight-second application handshake.
      // Read before subscribing: an old TX value is not a new protocol message.
      try {
        await step("pairing", () => tx.readValue(), 60000);
      } catch (error) {
        valid();
        if (!(error instanceof HallzeeError) || error.code !== "BLUETOOTH_PAIRING_NOTSUPPORTEDERROR")
          throw error;
        // Ask updated firmware to initiate encryption before trying another
        // protected operation: ChromeOS may drop the link during that write.
        let securityRequest: BluetoothRemoteGATTCharacteristic | undefined;
        try {
          securityRequest = await step("security-request", () => service.getCharacteristic(SECURITY_REQUEST_UUID));
        } catch (lookupError) {
          // Only an absent optional endpoint selects the legacy write path.
          // Permission failures, cancellation and link loss must still stop.
          if (!(lookupError instanceof HallzeeError) ||
              lookupError.code !== "BLUETOOTH_SECURITY_REQUEST_NOTFOUNDERROR") throw lookupError;
        }
        if (securityRequest) {
          await step("security-request", () => securityRequest.readValue());
          // The empty public read only requests GAP encryption; it proves
          // nothing. Require a successful protected read before HELLO/AUTH.
          await step("pairing-resume", async () => {
            for (let attempt = 0; ; attempt++) {
              await deadline(new Promise((resolve) => setTimeout(resolve, 800)), cancelled);
              valid();
              try {
                return await tx.readValue();
              } catch (resumeError) {
                valid();
                // Retry only the known transient category from this report,
                // never permission/authentication failures or application writes.
                if (!(resumeError instanceof Error) || resumeError.name !== "NotSupportedError" ||
                    resumeError.message !== "GATT Error Unknown." || attempt >= 9) throw resumeError;
              }
            }
          }, 60000);
        } else {
          // Older firmware: RX also requires encryption. Its parser ignores a
          // single blank line, so this starts no HELLO, claim or auth deadline.
          await step("pairing-write", () => rx.writeValueWithResponse(Uint8Array.of(10)), 60000);
        }
      }
      this.tx = tx;
      this.rx = rx;
      tx.addEventListener("characteristicvaluechanged", this.receive);
      await step("notifications", () => tx.startNotifications());
    } catch (error) {
      if (generation === this.generation) this.disconnect();
      throw error;
    }
  }

  sendLine(line: string, signal?: AbortSignal) {
    const bytes = commandBytes(line);
    const generation = this.generation;
    const cancelled = AbortSignal.any([this.connection.signal, ...(signal ? [signal] : [])]);
    const valid = () => {
      connectionCheck(cancelled);
      if (!this.rx || generation !== this.generation)
        throw new HallzeeError("DISCONNECTED", "Reconnect to the terminal.", true);
    };
    const task = this.writes.then(async () => {
      for (let offset = 0; offset < bytes.length; offset += 20) {
        await this.step("write", () => this.rx!.writeValueWithResponse(bytes.slice(offset, offset + 20)), valid, cancelled);
      }
    }).catch((error) => {
      if (generation === this.generation) this.disconnect();
      throw error;
    });
    this.writes = task.catch(() => {});
    return task;
  }
  disconnect() {
    this.generation++;
    this.tx?.removeEventListener("characteristicvaluechanged", this.receive);
    this.device?.removeEventListener("gattserverdisconnected", this.lost);
    this.connection.abort();
    if (this.device?.gatt) this.disconnectGatt(this.device.gatt);
    this.device = undefined;
    this.tx = undefined;
    this.rx = undefined;
    this.framer.reset();
  }
}
