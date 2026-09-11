import { describe, it, expect, vi, afterEach } from "vitest";
import { IDBFactory } from "fake-indexeddb";
import { ApplicationController } from "../../src/app/ApplicationController";
import { LocalDatabase, request } from "../../src/storage/LocalDatabase";
import { FakePort, tick } from "./helpers";
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});
function environment() {
  let held = false;
  const location = { reload: vi.fn() };
  const win = Object.assign(new EventTarget(), { isSecureContext: true, location });
  const doc = Object.assign(new EventTarget(), { visibilityState: "visible" });
  vi.stubGlobal("window", win);
  vi.stubGlobal("document", doc);
  vi.stubGlobal("indexedDB", new IDBFactory());
  vi.stubGlobal("navigator", {
    locks: {
      request: async (
        _name: string,
        _options: unknown,
        callback: (lock: object | null) => Promise<void>,
      ) => {
        await Promise.resolve();
        if (held) return callback(null);
        held = true;
        try {
          return await callback({});
        } finally {
          held = false;
        }
      },
    },
    storage: {
      estimate: async () => ({}),
      persisted: async () => false,
      persist: async () => false,
    },
  });
  return { win, doc, location };
}
describe("runtime teardown and storage lifecycle", () => {
  it("Strict Mode start/stop/start never lets a disposed controller retain a lock or listeners", async () => {
    const { win } = environment();
    const events = vi.spyOn(win, "addEventListener");
    const first = new ApplicationController(new FakePort());
    const pending = first.start();
    first.stop();
    await pending;
    expect(events).not.toHaveBeenCalled();
    const second = new ApplicationController(new FakePort());
    await second.start();
    expect(second.getSnapshot().ready).toBe(true);
    expect(second.getSnapshot().locked).toBe(false);
    second.stop();
  });
  it("freeze disconnects and releases the lock; resume reloads for fresh storage and reconciliation", async () => {
    const { doc, location } = environment();
    const first = new ApplicationController(new FakePort());
    await first.start();
    doc.dispatchEvent(new Event("freeze"));
    expect(first.getSnapshot().ready).toBe(false);
    await tick();
    const second = new ApplicationController(new FakePort());
    await second.start();
    expect(second.getSnapshot().ready).toBe(true);
    doc.dispatchEvent(new Event("resume"));
    expect(location.reload).toHaveBeenCalledTimes(1);
    first.stop();
    second.stop();
  });
  it("transaction success means complete, not merely request success", async () => {
    vi.stubGlobal("indexedDB", new IDBFactory());
    const db = await LocalDatabase.open("commit");
    await expect(
      db.transaction(["app_meta"], "readwrite", async (tx) => {
        await request(tx.objectStore("app_meta").put({ key: "partial", value: true }));
        tx.abort();
      }),
    ).rejects.toMatchObject({ code: "STORAGE_FAILED" });
    expect(await db.meta("partial", false)).toBe(false);
    db.close();
  });
  it("versionchange closes old handles; older code refuses newer schema without clearing rows", async () => {
    vi.stubGlobal("indexedDB", new IDBFactory());
    const changed = vi.fn();
    const db = await LocalDatabase.open("migration", changed);
    await db.setMeta("saved", "retained");
    const upgraded = await new Promise<IDBDatabase>((resolve, reject) => {
      const r = indexedDB.open("migration", 2);
      r.onsuccess = () => resolve(r.result);
      r.onerror = () => reject(r.error);
    });
    expect(changed).toHaveBeenCalledTimes(1);
    upgraded.close();
    await expect(LocalDatabase.open("migration")).rejects.toMatchObject({
      code: "STORAGE_UNAVAILABLE",
    });
    const raw = await new Promise<IDBDatabase>((resolve) => {
      const r = indexedDB.open("migration", 2);
      r.onsuccess = () => resolve(r.result);
    });
    const saved = await request(raw.transaction("app_meta").objectStore("app_meta").get("saved"));
    expect(saved.value).toBe("retained");
    raw.close();
  });
});
