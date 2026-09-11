import { describe, it, expect, vi } from "vitest";
import { setup, tick } from "./helpers";
import { TripRepository } from "../../src/storage/TripRepository";
import { SyncEngine } from "../../src/sync/SyncEngine";
import { parseTrip } from "../../src/protocol/messages";
import { HallzeeError } from "../../src/app/errors";
const id = "HZ-A1B2C3D4E5F6";
const payload = (n: number) => `${n},00123,2026-09-11,09:00:00,09:05:00,300,COMPLETE,0`;
async function fixture() {
  const f = await setup();
  const trips = new TripRepository(f.db);
  f.session.state = "Authenticated";
  f.session.authenticatedId = id;
  const context = () => ({
    terminalId: id,
    receivedWorkspaceId: f.workspace.workspaceId,
    scheduleName: null,
    classSection: null,
    contextSource: "unknown" as const,
  });
  const failed = vi.fn();
  const sync = new SyncEngine(f.session, trips, context, () => {}, failed);
  f.port.onLine((line) => sync.receive(line));
  return { ...f, trips, context, failed, sync };
}
describe("A06–A08 durable ACK and completed-stream cursor", () => {
  it("never ACKs before storage commits, and conflicts are not overwritten", async () => {
    const f = await fixture();
    let commit!: () => void;
    const gate = new Promise<void>((resolve) => {
      commit = resolve;
    });
    const store = f.trips.store.bind(f.trips);
    vi.spyOn(f.trips, "store").mockImplementation(async (...args) => {
      await gate;
      return store(...args);
    });
    const result = f.sync.synchronize();
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(1),
    );
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,1");
    f.sync.receive("TRIP," + payload(1));
    await tick();
    expect(f.port.sent.some((c) => c === "ACK,1")).toBe(false);
    commit();
    await f.sync.drain();
    expect(f.port.sent).toContain("ACK,1");
    f.sync.receive("SYNC_END");
    await result;
    expect((await f.trips.cursor(id)).completedCursor).toBe(1);
    expect(await f.trips.store(parseTrip(payload(1).replace("300", "301")), f.context())).toBe(
      "conflict",
    );
    f.sync.dispose();
    f.db.close();
  });
  it("live 110 cannot skip missing 101–109 after interrupted catch-up from 100", async () => {
    const f = await fixture();
    await f.trips.completeSync(id, 100, false);
    const interrupted = f.sync.synchronize();
    void interrupted.catch(() => {});
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(1),
    );
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,10");
    f.sync.receive("LIVE_TRIP," + payload(110));
    f.sync.receive("TRIP," + payload(101));
    await f.sync.drain();
    f.sync.cancel();
    await expect(interrupted).rejects.toThrow();
    expect((await f.trips.cursor(id)).completedCursor).toBe(100);
    expect(f.port.sent).not.toContain("ACK,110");
    const retry = f.sync.synchronize();
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(2),
    );
    expect(f.port.sent.at(-1)).toMatch(/,100$/);
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,10");
    for (let n = 101; n <= 110; n++) f.sync.receive("TRIP," + payload(n));
    f.sync.receive("SYNC_END");
    await retry;
    expect((await f.trips.all()).length).toBe(10);
    expect((await f.trips.cursor(id)).completedCursor).toBe(110);
    f.sync.dispose();
    f.db.close();
  });
  it("storage failure emits no ACK and does not advance cursor", async () => {
    const f = await fixture();
    vi.spyOn(f.trips, "store").mockRejectedValue(new HallzeeError("STORAGE_FAILED"));
    const result = f.sync.synchronize();
    void result.catch(() => {});
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(1),
    );
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,1");
    f.sync.receive("TRIP," + payload(1));
    await expect(result).rejects.toMatchObject({ code: "STORAGE_FAILED" });
    expect(f.port.sent).not.toContain("ACK,1");
    expect((await f.trips.cursor(id)).completedCursor).toBe(0);
    f.sync.dispose();
    f.db.close();
  });
  it("accepts sparse and growing streams; duplicate live records do not count toward SYNC_BEGIN", async () => {
    const f = await fixture();
    const result = f.sync.synchronize();
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(1),
    );
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,1");
    for (const n of [2, 8]) f.sync.receive("TRIP," + payload(n));
    f.sync.receive("SYNC_END");
    await result;
    expect((await f.trips.cursor(id)).completedCursor).toBe(8);
    f.sync.dispose();
    f.db.close();
  });
  it("rejects premature SYNC_END, and accepts empty sync without resetting cursor", async () => {
    const f = await fixture();
    await f.trips.completeSync(id, 5, false);
    const bad = f.sync.synchronize();
    void bad.catch(() => {});
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(1),
    );
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,1");
    f.sync.receive("SYNC_END");
    await expect(bad).rejects.toThrow();
    expect((await f.trips.cursor(id)).completedCursor).toBe(5);
    const good = f.sync.synchronize();
    await vi.waitFor(() =>
      expect(f.port.sent.filter((c) => c.startsWith("TIME_CURSOR"))).toHaveLength(2),
    );
    f.sync.receive("TIME_ACK,OK");
    f.sync.receive("SYNC_BEGIN,0");
    f.sync.receive("SYNC_END");
    await good;
    expect((await f.trips.cursor(id)).completedCursor).toBe(5);
    f.sync.dispose();
    f.db.close();
  });
  it("disposal discards queued notifications instead of writing to a new session", async () => {
    const f = await fixture();
    f.sync.receive("LIVE_TRIP," + payload(1));
    f.sync.dispose();
    await f.sync.drain();
    expect(await f.trips.all()).toHaveLength(0);
    f.db.close();
  });
});
