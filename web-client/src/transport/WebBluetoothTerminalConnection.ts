import type { BluetoothPort, DeviceHandle } from "./BluetoothPort";
import { LineFramer, commandBytes } from "./LineFramer";
import { SERVICE_UUID, TX_UUID, RX_UUID } from "../protocol/constants";
import { Signal } from "../app/events";
import { abortCheck, deadline, HallzeeError } from "../app/errors";
export class WebBluetoothTerminalConnection implements BluetoothPort {
  private device?: BluetoothDevice;
  private tx?: BluetoothRemoteGATTCharacteristic;
  private rx?: BluetoothRemoteGATTCharacteristic;
  private framer = new LineFramer();
  private lines = new Signal<string>();
  private dropped = new Signal<void>();
  private generation = 0;
  private writes: Promise<void> = Promise.resolve();
  onLine = this.lines.subscribe;
  onDisconnect = this.dropped.subscribe;
  requestDevice() {
    return navigator.bluetooth.requestDevice({ filters: [{ services: [SERVICE_UUID] }] });
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
    this.disconnect();
    this.dropped.emit();
  };
  async connect(handle: DeviceHandle, signal?: AbortSignal) {
    this.disconnect();
    const generation = this.generation;
    const device = handle as BluetoothDevice;
    this.device = device;
    device.addEventListener("gattserverdisconnected", this.lost);
    if (!device.gatt) throw new HallzeeError("BLUETOOTH_UNAVAILABLE");
    const valid = () => {
      abortCheck(signal);
      if (generation !== this.generation) throw new HallzeeError("CANCELLED");
    };
    const task = (async () => {
      const server = await device.gatt!.connect();
      if (signal?.aborted || generation !== this.generation) {
        server.disconnect();
        throw new HallzeeError("CANCELLED");
      }
      const service = await server.getPrimaryService(SERVICE_UUID);
      valid();
      const tx = await service.getCharacteristic(TX_UUID);
      valid();
      const rx = await service.getCharacteristic(RX_UUID);
      valid();
      if (!rx.properties.write || !rx.writeValueWithResponse)
        throw new HallzeeError("ACK_WRITES_REQUIRED");
      this.tx = tx;
      this.rx = rx;
      tx.addEventListener("characteristicvaluechanged", this.receive);
      await tx.startNotifications();
      valid();
    })();
    try {
      await deadline(task, signal);
    } catch (error) {
      if (generation === this.generation) this.disconnect();
      throw error;
    }
  }
  sendLine(line: string, signal?: AbortSignal) {
    const bytes = commandBytes(line);
    const generation = this.generation;
    const task = this.writes.then(async () => {
      for (let offset = 0; offset < bytes.length; offset += 20) {
        abortCheck(signal);
        if (!this.rx || generation !== this.generation)
          throw new HallzeeError("DISCONNECTED", "Reconnect to the terminal.", true);
        await deadline(this.rx.writeValueWithResponse(bytes.slice(offset, offset + 20)), signal);
      }
    });
    this.writes = task.catch(() => {});
    return task;
  }
  disconnect() {
    this.generation++;
    this.tx?.removeEventListener("characteristicvaluechanged", this.receive);
    this.device?.removeEventListener("gattserverdisconnected", this.lost);
    this.device?.gatt?.disconnect();
    this.device = undefined;
    this.tx = undefined;
    this.rx = undefined;
    this.framer.reset();
    this.writes = Promise.resolve();
  }
}
