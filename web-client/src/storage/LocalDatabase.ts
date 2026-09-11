import { HallzeeError } from "../app/errors";
import { migrate } from "./migrations";
import type { Stores, StoreName } from "./schema";
export function request<T>(req: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}
export class LocalDatabase {
  constructor(
    private db: IDBDatabase,
    onVersionChange: () => void = () => {},
  ) {
    db.onversionchange = () => {
      db.close();
      onVersionChange();
    };
  }
  static open(name = "hallzee-web", onVersionChange?: () => void): Promise<LocalDatabase> {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(name, 1);
      let failed = false;
      req.onupgradeneeded = (event) => migrate(req.result, event.oldVersion);
      req.onerror = () =>
        reject(
          new HallzeeError(
            "STORAGE_UNAVAILABLE",
            "Local storage could not open. Saved data has not been erased.",
          ),
        );
      req.onblocked = () => {
        failed = true;
        reject(new HallzeeError("UPGRADE_BLOCKED", "Close other Hallzee windows, then retry."));
      };
      req.onsuccess = () => {
        if (failed) req.result.close();
        else resolve(new LocalDatabase(req.result, onVersionChange));
      };
    });
  }
  async transaction<T>(
    stores: StoreName[],
    mode: IDBTransactionMode,
    action: (tx: IDBTransaction) => Promise<T> | T,
  ): Promise<T> {
    let tx: IDBTransaction;
    try {
      tx = this.db.transaction(stores, mode, { durability: "strict" });
    } catch {
      throw new HallzeeError(
        "STORAGE_UNAVAILABLE",
        "Local storage is unavailable. Reopen Hallzee before continuing.",
      );
    }
    const done = new Promise<void>((resolve, reject) => {
      tx.oncomplete = () => resolve();
      tx.onabort = tx.onerror = () =>
        reject(
          new HallzeeError(
            "STORAGE_FAILED",
            "Local data could not be saved. No unconfirmed trip was acknowledged.",
          ),
        );
    });
    void done.catch(() => {});
    const timer = setTimeout(() => {
      try {
        tx.abort();
      } catch {
        /* already completed */
      }
    }, 10000);
    try {
      const value = await action(tx);
      await done;
      return value;
    } catch (error) {
      try {
        tx.abort();
      } catch {
        /* already aborted */
      }
      throw error;
    } finally {
      clearTimeout(timer);
    }
  }
  get<K extends StoreName>(store: K, key: IDBValidKey): Promise<Stores[K] | undefined> {
    return this.transaction([store], "readonly", (tx) => request(tx.objectStore(store).get(key)));
  }
  all<K extends StoreName>(store: K): Promise<Stores[K][]> {
    return this.transaction([store], "readonly", (tx) => request(tx.objectStore(store).getAll()));
  }
  put<K extends StoreName>(store: K, value: Stores[K]) {
    return this.transaction([store], "readwrite", (tx) =>
      request(tx.objectStore(store).put(value)),
    );
  }
  remove(store: StoreName, key: IDBValidKey) {
    return this.transaction([store], "readwrite", (tx) =>
      request(tx.objectStore(store).delete(key)),
    );
  }
  async meta<T>(key: string, fallback: T): Promise<T> {
    return ((await this.get("app_meta", key))?.value as T) ?? fallback;
  }
  setMeta(key: string, value: unknown) {
    return this.put("app_meta", { key, value });
  }
  async probe() {
    await this.setMeta("storage-probe", "ok");
    if ((await this.meta("storage-probe", "")) !== "ok") throw new HallzeeError("STORAGE_FAILED");
    await this.remove("app_meta", "storage-probe");
  }
  close() {
    this.db.close();
  }
}
