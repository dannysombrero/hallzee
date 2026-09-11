import { LocalDatabase, request } from "./LocalDatabase";
import { computeAuthProof, clientId } from "../protocol/WebTerminalCrypto";
import type { Credential, Terminal } from "./schema";
import { HallzeeError } from "../app/errors";
export class CredentialRepository {
  constructor(private db: LocalDatabase) {}
  async installationId() {
    return this.db.transaction(["app_meta"], "readwrite", async (tx) => {
      const store = tx.objectStore("app_meta");
      const saved = await request(store.get("installationId"));
      if (saved) return clientId(saved.value);
      const value = clientId(crypto.randomUUID());
      await request(store.put({ key: "installationId", value }));
      return value;
    });
  }
  get(terminal: string) {
    return this.db.get("credentials", terminal);
  }
  async savePending(terminal: string, client: string, key: CryptoKey) {
    const record: Credential = {
      terminalId: terminal,
      clientId: client,
      key,
      state: "pendingCommit",
      createdAtUtc: new Date().toISOString(),
    };
    await this.db.put("credentials", record);
    const saved = await this.get(terminal);
    if (!saved || saved.key.extractable) throw new HallzeeError("CREDENTIAL_STORAGE_FAILED");
    await computeAuthProof(saved.key, terminal, client, "00000000000000000000000000000000");
  }
  async confirm(terminal: Terminal) {
    await this.db.transaction(["credentials", "terminals"], "readwrite", async (tx) => {
      const credential = await request<Credential | undefined>(
        tx.objectStore("credentials").get(terminal.terminalId),
      );
      if (!credential) throw new HallzeeError("CREDENTIAL_MISSING");
      await request(tx.objectStore("credentials").put({ ...credential, state: "confirmed" }));
      await request(tx.objectStore("terminals").put(terminal));
    });
  }
  async release(terminalId: string) {
    await this.db.transaction(["credentials", "terminals"], "readwrite", async (tx) => {
      const terminal = await request<Terminal | undefined>(
        tx.objectStore("terminals").get(terminalId),
      );
      if (terminal) {
        delete terminal.assignedWorkspaceId;
        delete terminal.deviceIdHint;
        await request(tx.objectStore("terminals").put(terminal));
      }
      await request(tx.objectStore("credentials").delete(terminalId));
    });
  }
}
