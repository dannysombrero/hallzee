import { errorText } from "../../src/app/errors";
import { bluetoothError } from "../../src/transport/BluetoothErrors";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { WebBluetoothTerminalConnection } from "../../src/transport/WebBluetoothTerminalConnection";
import { TX_UUID, SERVICE_UUID } from "../../src/protocol/constants";
import { ActivePassStore } from "../../src/sync/ActivePassStore";
import { buildPolicyTransfer } from "../../src/domain/BellPolicyProtocol";
import { defaultPolicy } from "../../src/storage/schema";
afterEach(() => vi.unstubAllGlobals());
function hardware() {
  const chunks: Uint8Array[] = [];
  let subscribed = false;
  const tx = Object.assign(new EventTarget(), {
    properties: { read: true },
    readValue: vi.fn(async () => new DataView(new ArrayBuffer(0))),
    startNotifications: async () => {
      subscribed = true;
    },
    value: new DataView(new ArrayBuffer(0)),
  });
  const rx = {
    properties: { write: true },
    writeValueWithResponse: vi.fn(async (bytes: Uint8Array) => {
      expect(subscribed).toBe(true);
      chunks.push(bytes);
    }),
  };
  const gatt = {
    connect: vi.fn(async () => gatt),
    getPrimaryService: vi.fn(async () => ({
      getCharacteristic: async (id: string) => (id === TX_UUID ? tx : rx),
    })),
    disconnect: vi.fn(),
  };
  const device = Object.assign(new EventTarget(), { id: "fake-device", gatt });
  return { tx, rx, gatt, device, chunks };
}
describe("browser GATT adapter", () => {
  it("filters service UUID, subscribes before writing, serializes byte chunks and drops stale fragments", async () => {
    const h = hardware(),
      requestDevice = vi.fn(async () => h.device);
    vi.stubGlobal("navigator", { bluetooth: { requestDevice } });
    const port = new WebBluetoothTerminalConnection();
    await port.requestDevice();
    expect(requestDevice).toHaveBeenCalledWith({ filters: [{ services: [SERVICE_UUID] }] });
    await port.connect(h.device);
    await Promise.all([port.sendLine("X".repeat(45)), port.sendLine("SECOND")]);
    expect(h.chunks.map((c) => c.length)).toEqual([20, 20, 6, 7]);
    expect(new TextDecoder().decode(Uint8Array.from(h.chunks.flatMap((c) => [...c])))).toBe(
      "X".repeat(45) + "\nSECOND\n",
    );
    const line = vi.fn();
    port.onLine(line);
    const emit = (text: string) => {
      h.tx.value = new DataView(new TextEncoder().encode(text).buffer);
      h.tx.dispatchEvent(new Event("characteristicvaluechanged"));
    };
    emit("OLD");
    port.disconnect();
    emit("STALE\n");
    await port.connect(h.device);
    emit("NEW\n");
    expect(line).toHaveBeenCalledExactlyOnceWith("NEW");
    port.disconnect();
  });
  it("cancels a late connection and refuses write-without-response-only hardware", async () => {
    const h = hardware(),
      port = new WebBluetoothTerminalConnection();
    h.rx.properties.write = false;
    await expect(port.connect(h.device)).rejects.toMatchObject({ code: "ACK_WRITES_REQUIRED" });
    expect(h.gatt.disconnect).toHaveBeenCalled();
    let resolve!: () => void;
    h.gatt.connect.mockImplementation(
      () =>
        new Promise((done) => {
          resolve = () => done(h.gatt);
        }),
    );
    const control = new AbortController();
    const result = port.connect(h.device, control.signal);
    await vi.waitFor(() => expect(h.gatt.connect).toHaveBeenCalledTimes(2));
    control.abort();
    await expect(result).rejects.toBeDefined();
    resolve();
    await Promise.resolve();
    expect(h.rx.writeValueWithResponse).not.toHaveBeenCalled();
  });
});

describe("native Bluetooth operations outliving their caller", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  for (const stage of ["connect", "read", "write"] as const) {
    for (const interrupt of ["abort", "timeout"] as const) {
      it(`waits for a pending ${stage} after ${interrupt} before reconnecting the same device`, async () => {
        const h = hardware(), port = new WebBluetoothTerminalConnection();
        const control = new AbortController();
        let finish!: () => void;
        if (stage === "connect") h.gatt.connect.mockImplementationOnce(() =>
          new Promise((resolve) => { finish = () => resolve(h.gatt); }));
        if (stage === "read") h.tx.readValue.mockImplementationOnce(() =>
          new Promise((resolve) => { finish = () => resolve(new DataView(new ArrayBuffer(0))); }));
        if (stage === "write") {
          await port.connect(h.device);
          h.rx.writeValueWithResponse.mockImplementationOnce(() =>
            new Promise((resolve) => { finish = () => resolve(); }));
        }
        const first = (stage === "write"
          ? port.sendLine("X".repeat(45), control.signal)
          : port.connect(h.device, control.signal)).catch((error) => error);
        await vi.advanceTimersByTimeAsync(0);
        expect(finish).toBeTypeOf("function");
        if (interrupt === "abort") control.abort();
        else await vi.advanceTimersByTimeAsync(stage === "read" ? 60000 : stage === "connect" ? 15000 : 10000);
        expect(await first).toMatchObject({ code: interrupt === "abort" ? "CANCELLED"
          : `BLUETOOTH_${stage === "read" ? "PAIRING" : stage.toUpperCase()}_TIMEOUTERROR` });

        const second = port.connect(h.device);
        await vi.advanceTimersByTimeAsync(1000);
        expect(h.gatt.connect).toHaveBeenCalledTimes(1);
        finish();
        await vi.advanceTimersByTimeAsync(800);
        await second;
        expect(h.gatt.connect).toHaveBeenCalledTimes(2);
        // A cancelled connect must close its late native result before the
        // replacement starts; a cancelled write must drop its remaining chunks.
        const lastDisconnect = h.gatt.disconnect.mock.invocationCallOrder.at(-1)!;
        expect(lastDisconnect).toBeLessThan(h.gatt.connect.mock.invocationCallOrder[1]);
        await port.sendLine("NEW");
        expect(new TextDecoder().decode(Uint8Array.from(h.chunks.flatMap((c) => [...c])))).toBe("NEW\n");
        port.disconnect();
      });
    }
  }

  it("pauses new attempts when Chrome never settles a cancelled native operation", async () => {
    const h = hardware(), port = new WebBluetoothTerminalConnection();
    let finish!: () => void;
    h.tx.readValue.mockImplementationOnce(() => new Promise((resolve) => {
      finish = () => resolve(new DataView(new ArrayBuffer(0)));
    }));
    const control = new AbortController();
    const first = port.connect(h.device, control.signal).catch((error) => error);
    await vi.advanceTimersByTimeAsync(0);
    control.abort();
    await first;
    const second = port.connect(h.device).catch((error) => error);
    await vi.advanceTimersByTimeAsync(10000);
    expect(await second).toMatchObject({ code: "BLUETOOTH_OPERATION_PENDING", retryable: false });
    expect(h.gatt.connect).toHaveBeenCalledTimes(1);
    finish();
    await vi.advanceTimersByTimeAsync(0);
    await port.connect(h.device);
    expect(h.gatt.connect).toHaveBeenCalledTimes(2);
    port.disconnect();
  });

  it("reports a dropped link during a pending read as a disconnect, not user cancellation", async () => {
    const h = hardware(), port = new WebBluetoothTerminalConnection();
    let finish!: () => void;
    h.tx.readValue.mockImplementationOnce(() => new Promise((resolve) => {
      finish = () => resolve(new DataView(new ArrayBuffer(0)));
    }));
    const connecting = port.connect(h.device).catch((error) => error);
    await vi.advanceTimersByTimeAsync(0);
    h.device.dispatchEvent(new Event("gattserverdisconnected"));
    expect(await connecting).toMatchObject({ code: "DISCONNECTED", retryable: true });
    finish();
    await vi.advanceTimersByTimeAsync(0);
  });

  it("retries a busy encrypted read on the existing connection before sending HELLO", async () => {
    const h = hardware(), port = new WebBluetoothTerminalConnection();
    h.tx.readValue.mockRejectedValueOnce(new DOMException("GATT operation already in progress.", "NetworkError"));
    const connecting = port.connect(h.device);
    await vi.advanceTimersByTimeAsync(0);
    expect(h.rx.writeValueWithResponse).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(800);
    await connecting;
    expect(h.gatt.connect).toHaveBeenCalledTimes(1);
    expect(h.tx.readValue).toHaveBeenCalledTimes(2);
    await port.sendLine("HELLO");
    port.disconnect();
  });

  it("bounds setup retries and never replays a busy command write", async () => {
    const h = hardware(), port = new WebBluetoothTerminalConnection();
    const busy = new DOMException("GATT operation already in progress.", "NetworkError");
    h.tx.readValue.mockRejectedValue(busy);
    const connecting = port.connect(h.device).catch((error) => error);
    await vi.advanceTimersByTimeAsync(1600);
    expect(await connecting).toMatchObject({ code: "BLUETOOTH_PAIRING_NETWORKERROR" });
    expect(h.tx.readValue).toHaveBeenCalledTimes(3);
    h.tx.readValue.mockResolvedValue(new DataView(new ArrayBuffer(0)));
    const second = port.connect(h.device);
    await vi.advanceTimersByTimeAsync(800);
    await second;
    h.rx.writeValueWithResponse.mockRejectedValueOnce(busy);
    await expect(port.sendLine("HELLO")).rejects.toMatchObject({ code: "BLUETOOTH_WRITE_NETWORKERROR" });
    expect(h.rx.writeValueWithResponse).toHaveBeenCalledTimes(1);
  });
});
describe("occupancy and transfer bounds", () => {
  it("keeps other passes on targeted check-in/reset and shows unknown after missing clock or disconnect", () => {
    const store = new ActivePassStore();
    const passes = Array.from({ length: 8 }, (_, i) => `${i + 1},${1789117200 + i}`);
    store.snapshot("ACTIVE_PASSES," + passes.join(","));
    store.event("EVENT,CHECKIN,3,10");
    expect(store.passes).toHaveLength(7);
    store.event("EVENT,RESET,2,0");
    expect(store.passes.map((p) => p.studentId)).not.toContain("2");
    store.event("EVENT,CHECKOUT,1,0");
    expect(store.fresh).toBe(false);
    store.event("EVENT,CHECKOUT,9,1789117200");
    expect(store.passes.map((p) => p.studentId)).not.toContain("9");
  });
  it("accepts 96 dated windows and refuses the 97th without truncation", () => {
    const policy = { ...defaultPolicy("w"), enforcement: true };
    const periods = Array.from({ length: 97 }, (_, i) => ({
      workspaceId: "w",
      scheduleId: String(i),
      periodName: "P",
      scheduleName: "Special",
      classSection: "",
      start: "08:00",
      end: "09:00",
      weekdays: [],
    }));
    const exceptions = [
      { workspaceId: "w", date: "2026-09-07", scheduleName: "Special", isNoSchool: false },
    ];
    expect(
      buildPolicyTransfer(new Date(2026, 8, 7), policy, periods.slice(0, 96), exceptions).at(-1),
    ).toBe("POLICY_COMMIT,96");
    expect(() => buildPolicyTransfer(new Date(2026, 8, 7), policy, periods, exceptions)).toThrow(
      /97 windows/,
    );
  });
});

describe("encrypted pairing before protocol authentication", () => {
  it("waits for encrypted TX read before subscribing; never delivers its stale value as a message", async () => {
    const h = hardware(),
      port = new WebBluetoothTerminalConnection();
    let finish!: () => void;
    h.tx.readValue.mockImplementation(
      () =>
        new Promise((resolve) => {
          finish = () => {
            h.tx.value = new DataView(new TextEncoder().encode("OLD_RESPONSE\n").buffer);
            h.tx.dispatchEvent(new Event("characteristicvaluechanged"));
            resolve(h.tx.value);
          };
        }),
    );
    const notify = vi.spyOn(h.tx, "startNotifications");
    const lines = vi.fn();
    port.onLine(lines);
    const connecting = port.connect(h.device);
    await vi.waitFor(() => expect(h.tx.readValue).toHaveBeenCalledTimes(1));
    expect(notify).not.toHaveBeenCalled();
    expect(h.rx.writeValueWithResponse).not.toHaveBeenCalled();
    finish();
    await connecting;
    expect(notify).toHaveBeenCalledTimes(1);
    expect(lines).not.toHaveBeenCalled();
    port.disconnect();
  });
  it("gives OS pairing more time than the application handshake", async () => {
    vi.useFakeTimers();
    try {
      const h = hardware(),
        port = new WebBluetoothTerminalConnection();
      h.tx.readValue.mockImplementation(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve(new DataView(new ArrayBuffer(0))), 15000),
          ),
      );
      const connecting = port.connect(h.device);
      await vi.advanceTimersByTimeAsync(15001);
      await connecting;
      await port.sendLine("HELLO");
      port.disconnect();
    } finally {
      vi.useRealTimers();
    }
  });
  it("reports a failed native step without exposing browser error payloads", async () => {
    const h = hardware(),
      port = new WebBluetoothTerminalConnection();
    h.tx.readValue.mockRejectedValue(new DOMException("sensitive native payload", "NetworkError"));
    await expect(port.connect(h.device)).rejects.toMatchObject({
      code: "BLUETOOTH_PAIRING_NETWORKERROR",
    });
    try {
      await port.connect(h.device);
    } catch (error) {
      expect((error as Error).message).toContain("pairing / NetworkError");
      expect((error as Error).message).not.toContain("sensitive");
    }
    expect(h.rx.writeValueWithResponse).not.toHaveBeenCalled();
  });
  it("labels missing services and rejected writes instead of reporting a generic failure", async () => {
    const h = hardware(),
      port = new WebBluetoothTerminalConnection();
    h.gatt.getPrimaryService.mockRejectedValueOnce(new DOMException("Missing", "NotFoundError"));
    await expect(port.connect(h.device)).rejects.toMatchObject({
      code: "BLUETOOTH_SERVICE_NOTFOUNDERROR",
    });
    await port.connect(h.device);
    h.rx.writeValueWithResponse.mockRejectedValueOnce(new DOMException("Lost", "NetworkError"));
    await expect(port.sendLine("HELLO")).rejects.toMatchObject({
      code: "BLUETOOTH_WRITE_NETWORKERROR",
    });
    port.disconnect();
  });
});

it("sanitizes chooser and storage diagnostics while preserving useful browser categories", () => {
  for (const name of ["NotFoundError", "SecurityError", "TypeError", "SomethingPrivate"]) {
    const error = new Error("private payload");
    error.name = name;
    const shown = errorText(bluetoothError(error, "chooser"));
    expect(shown).not.toContain("private payload");
    expect(shown).not.toContain("SomethingPrivate");
    expect(shown).toContain("chooser /");
  }
  expect(errorText(new DOMException("private payload", "QuotaExceededError"))).toContain(
    "run out of storage",
  );
  expect(errorText(new DOMException("private payload", "DataCloneError"))).toContain("owner key");
  expect(errorText(new DOMException("private payload", "DataCloneError"))).toContain("Chrome or Edge");
  expect(errorText(new TypeError("private payload"))).toContain("TypeError");
  expect(errorText(new TypeError("private payload"))).not.toContain("private payload");
});

it("recognizes browser blocked permission NetworkError without exposing arbitrary native messages", () => {
  const blocked = bluetoothError(
    new DOMException("Bluetooth permission has been blocked.", "NetworkError"),
    "connect",
  );
  expect(blocked.message).toContain("Bluetooth permission blocked");
  expect(blocked.message).toContain("Privacy & Security");
  expect(blocked.message).toContain("Chrome or Edge");
  expect(blocked.retryable).toBe(false);
  const unknown = bluetoothError(
    new DOMException("Bluetooth permission has been blocked. private payload", "NetworkError"),
    "connect",
  );
  expect(unknown.message).not.toContain("private payload");
  expect(unknown.message).not.toContain("Privacy & Security");
  expect(
    bluetoothError(new DOMException("Authentication failed.", "NetworkError"), "connect").message,
  ).toContain("Bluetooth authentication incomplete");
});

it("explains saved-owner bond recovery for unsupported encrypted reads without suggesting an owner reset", () => {
  const shown = bluetoothError(
    new DOMException("synthetic private payload", "NotSupportedError"),
    "pairing",
  );
  expect(shown.message).toContain("BT REPAIR");
  expect(shown.message).toContain("choose the saved terminal");
  expect(shown.message).toContain("Do not reset ownership");
  expect(shown.message).not.toContain("synthetic private");
  expect(shown.retryable).toBe(false);
});

it("explains that a busy Bluetooth operation can come from this tab", () => {
  const shown = bluetoothError(new DOMException("GATT operation already in progress.", "NetworkError"), "pairing");
  expect(shown.message).toContain("only this Hallzee tab open");
  expect(shown.message).not.toContain("Close other apps or tabs");
});
