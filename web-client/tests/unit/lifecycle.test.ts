import { describe, it, expect, vi, afterEach } from "vitest";
import { AutoReconnectCoordinator } from "../../src/lifecycle/AutoReconnectCoordinator";
import { HallzeeError } from "../../src/app/errors";
afterEach(() => vi.useRealTimers());
describe("bounded automatic reconnect", () => {
  it("coalesces wake events, cancels a hanging attempt at 45 seconds, and needs explicit retry after expiry", async () => {
    vi.useFakeTimers();
    let signal!: AbortSignal;
    const connect = vi.fn((s: AbortSignal) => {
      signal = s;
      return new Promise<void>(() => {});
    });
    const failed = vi.fn();
    const coordinator = new AutoReconnectCoordinator(connect, vi.fn(), failed);
    coordinator.start();
    coordinator.start();
    expect(connect).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(45001);
    expect(signal.aborted).toBe(true);
    expect(failed).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(60000);
    expect(connect).toHaveBeenCalledTimes(1);
    coordinator.stop();
  });
  it("stops on policy denial or ownership failure instead of opening a chooser", async () => {
    for (const error of [
      new DOMException("Denied", "SecurityError"),
      new HallzeeError("OWNER_MISMATCH"),
    ]) {
      vi.useFakeTimers();
      const connect = vi.fn().mockRejectedValue(error),
        failed = vi.fn();
      const coordinator = new AutoReconnectCoordinator(connect, vi.fn(), failed);
      coordinator.start();
      await vi.advanceTimersByTimeAsync(60000);
      expect(connect).toHaveBeenCalledTimes(1);
      expect(failed).toHaveBeenCalledWith(error);
      coordinator.stop();
    }
  });
  it("manual disconnect aborts current attempt and scheduled retries", async () => {
    vi.useFakeTimers();
    const connect = vi.fn().mockRejectedValue(new HallzeeError("DISCONNECTED", "", true));
    const coordinator = new AutoReconnectCoordinator(connect, vi.fn(), vi.fn());
    coordinator.start();
    await vi.advanceTimersByTimeAsync(1);
    coordinator.stop();
    await vi.advanceTimersByTimeAsync(60000);
    expect(connect).toHaveBeenCalledTimes(1);
  });
});
