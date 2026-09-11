import { LocalDatabase, request } from "./LocalDatabase";
import { defaultSync, type Trip, type WireTrip, type SyncState } from "./schema";
import { immutableTrip } from "../protocol/messages";
export type TripContext = Pick<
  Trip,
  "terminalId" | "receivedWorkspaceId" | "scheduleName" | "classSection" | "contextSource"
>;
export class TripRepository {
  constructor(private db: LocalDatabase) {}
  store(wire: WireTrip, context: TripContext): Promise<"saved" | "duplicate" | "conflict"> {
    return this.db.transaction(["trips"], "readwrite", async (tx) => {
      const store = tx.objectStore("trips");
      const old = await request<Trip | undefined>(store.get([context.terminalId, wire.tripId]));
      if (old) return immutableTrip(old) === immutableTrip(wire) ? "duplicate" : "conflict";
      await request(store.add({ ...wire, ...context, receivedAtUtc: new Date().toISOString() }));
      return "saved";
    });
  }
  async cursor(id: string) {
    return (await this.db.get("sync_state", id)) ?? defaultSync(id);
  }
  async completeSync(id: string, cursor: number, recovery: boolean) {
    await this.db.transaction(["sync_state"], "readwrite", async (tx) => {
      const store = tx.objectStore("sync_state");
      const old = (await request<SyncState | undefined>(store.get(id))) ?? defaultSync(id);
      await request(
        store.put({
          ...old,
          completedCursor: cursor,
          lastSuccessfulSyncUtc: new Date().toISOString(),
          recoveryRequired: recovery ? false : old.recoveryRequired,
        }),
      );
    });
  }
  async recover(id: string) {
    const state = await this.cursor(id);
    await this.db.put("sync_state", { ...state, recoveryRequired: true });
  }
  async resetHistory(id: string) {
    await this.db.transaction(["trips", "sync_state"], "readwrite", async (tx) => {
      const store = tx.objectStore("trips");
      const keys = await request(
        store
          .index("terminalDate")
          .getAllKeys(IDBKeyRange.bound([id, "", 0], [id, "\uffff", 4294967295])),
      );
      for (const key of keys) await request(store.delete(key));
      const state =
        (await request<SyncState | undefined>(tx.objectStore("sync_state").get(id))) ??
        defaultSync(id);
      await request(
        tx
          .objectStore("sync_state")
          .put({ ...defaultSync(id), historyGeneration: state.historyGeneration + 1 }),
      );
    });
  }
  all() {
    return this.db.all("trips");
  }
}
