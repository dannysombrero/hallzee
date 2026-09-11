import { errorText } from "../../src/app/errors";
import { bluetoothError } from "../../src/transport/BluetoothErrors";
import { describe, it, expect, vi, afterEach } from "vitest";
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
    control.abort();
    await expect(result).rejects.toBeDefined();
    resolve();
    await Promise.resolve();
    expect(h.rx.writeValueWithResponse).not.toHaveBeenCalled();
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
  expect(errorText(new TypeError("private payload"))).toContain("TypeError");
  expect(errorText(new TypeError("private payload"))).not.toContain("private payload");
});

it("recognizes Chrome's blocked permission NetworkError without exposing arbitrary native messages", () => {
  const blocked = bluetoothError(
    new DOMException("Bluetooth permission has been blocked.", "NetworkError"),
    "connect",
  );
  expect(blocked.message).toContain("Bluetooth permission blocked");
  expect(blocked.message).toContain("Privacy & Security");
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
