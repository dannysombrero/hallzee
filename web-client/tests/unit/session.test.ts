import { describe, it, expect, vi } from "vitest";
import { setup } from "./helpers";
import fixtures from "../../../contracts/web-client/v1/identity.json";
import { deriveOwnerKey } from "../../src/protocol/WebTerminalCrypto";
const f = fixtures.cases[0];
describe("A02/A04/A05 secure ownership session", () => {
  it("saves a non-extractable key before commit and verifies literal proofs", async () => {
    const { db, credentials, workspace, port, session } = await setup();
    await db.setMeta("installationId", f.clientId);
    port.onCommand = async (line) => {
      if (line.startsWith("HELLO"))
        port.lines.emit(`IDENTITY,2,${f.terminalId},E5F6,UNCLAIMED,AVAILABLE,${f.nonce}`);
      if (line.startsWith("CLAIM,")) {
        expect(line).toBe(`CLAIM,2,${f.clientId},${f.claimProof}`);
        port.lines.emit(`CLAIM_OK,2,${f.terminalId},${f.commitNonce}`);
      }
      if (line.startsWith("CLAIM_COMMIT")) {
        expect((await credentials.get(f.terminalId))?.state).toBe("pendingCommit");
        expect(line).toBe(`CLAIM_COMMIT,2,${f.clientId},${f.commitProof}`);
        port.lines.emit(`AUTH_OK,2,${f.terminalId},Room 204`);
      }
    };
    await session.open({ id: "test" }, workspace.workspaceId, undefined, f.passkey);
    expect(session.state).toBe("Authenticated");
    expect((await credentials.get(f.terminalId))?.state).toBe("confirmed");
    session.disconnect();
    expect(await credentials.get(f.terminalId)).toBeDefined();
    db.close();
  });
  it("rejects application commands before authentication", async () => {
    const { session, port, db } = await setup();
    await expect(session.send("SYNC_ALL")).rejects.toMatchObject({ code: "AUTH_REQUIRED" });
    expect(port.sent).toEqual([]);
    db.close();
  });
  it("retains pending key when commit may have succeeded, then authenticates occupied owner", async () => {
    const { db, credentials, workspace, port, session } = await setup();
    await db.setMeta("installationId", f.clientId);
    let returning = false;
    port.onCommand = (line) => {
      if (line.startsWith("HELLO"))
        port.lines.emit(
          `IDENTITY,2,${f.terminalId},E5F6,${returning ? "CLAIMED,IN_USE" : "UNCLAIMED,AVAILABLE"},${f.nonce}`,
        );
      if (line.startsWith("CLAIM,")) port.lines.emit(`CLAIM_OK,2,${f.terminalId},${f.commitNonce}`);
      if (line.startsWith("CLAIM_COMMIT")) throw new Error("ACK lost");
      if (line.startsWith("AUTH,")) {
        expect(line).toBe(`AUTH,2,${f.clientId},${f.authProof}`);
        port.lines.emit(`AUTH_OK,2,${f.terminalId},Room 204`);
      }
    };
    await expect(
      session.open({ id: "test" }, workspace.workspaceId, undefined, f.passkey),
    ).rejects.toThrow();
    expect((await credentials.get(f.terminalId))?.state).toBe("pendingCommit");
    returning = true;
    await session.open({ id: "test" }, workspace.workspaceId, f.terminalId);
    expect(session.state).toBe("Authenticated");
    session.dispose();
    db.close();
  });
  it("aborts claim if durable key save fails and sends no commit", async () => {
    const { db, credentials, workspace, port, session } = await setup();
    vi.spyOn(credentials, "savePending").mockRejectedValue(new Error("quota"));
    port.onCommand = (line) => {
      if (line.startsWith("HELLO"))
        port.lines.emit(`IDENTITY,2,${f.terminalId},E5F6,UNCLAIMED,AVAILABLE,${f.nonce}`);
      if (line.startsWith("CLAIM,")) port.lines.emit(`CLAIM_OK,2,${f.terminalId},${f.commitNonce}`);
    };
    await expect(
      session.open({ id: "test" }, workspace.workspaceId, undefined, f.passkey),
    ).rejects.toThrow();
    expect(port.sent.some((c) => c.startsWith("CLAIM_ABORT"))).toBe(true);
    expect(port.sent.some((c) => c.startsWith("CLAIM_COMMIT"))).toBe(false);
    db.close();
  });
  it("rejects wrong AUTH_OK terminal and retains ownership", async () => {
    const { db, credentials, workspace, port, session } = await setup();
    await db.setMeta("installationId", f.clientId);
    await credentials.savePending(
      f.terminalId,
      f.clientId,
      await deriveOwnerKey(f.passkey, f.terminalId, f.clientId),
    );
    port.onCommand = (line) => {
      if (line.startsWith("HELLO"))
        port.lines.emit(`IDENTITY,2,${f.terminalId},E5F6,CLAIMED,AVAILABLE,${f.nonce}`);
      if (line.startsWith("AUTH,")) port.lines.emit("AUTH_OK,2,HZ-000000000000,Room 204");
    };
    await expect(
      session.open({ id: "test" }, workspace.workspaceId, f.terminalId),
    ).rejects.toMatchObject({ code: "IDENTITY_MISMATCH" });
    expect(await credentials.get(f.terminalId)).toBeDefined();
    db.close();
  });
  it("missing credential never starts a claim or AUTH", async () => {
    const { db, workspace, port, session } = await setup();
    port.onCommand = () =>
      port.lines.emit(`IDENTITY,2,${f.terminalId},E5F6,CLAIMED,IN_USE,${f.nonce}`);
    await expect(session.open({ id: "test" }, workspace.workspaceId)).rejects.toMatchObject({
      code: "CREDENTIAL_MISSING",
    });
    expect(port.sent).toHaveLength(1);
    db.close();
  });
});
