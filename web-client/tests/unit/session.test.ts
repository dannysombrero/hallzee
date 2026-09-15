import { describe, it, expect, vi, afterEach } from "vitest";
import { setup } from "./helpers";
import fixtures from "../../../contracts/web-client/v1/identity.json";
import { deriveOwnerKey } from "../../src/protocol/WebTerminalCrypto";
const f = fixtures.cases[0];
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });
describe("A02/A04/A05 secure ownership session", () => {
  it.each([
    { claimed: false, saved: false, status: "Not Paired" },
    { claimed: false, saved: true, status: "Not Paired" },
    { claimed: true, saved: false, status: "Paired to other device" },
    { claimed: true, saved: true, status: "Currently Paired" },
  ])("inspects ownership without claiming or retaining a handshake: $status ($claimed/$saved)", async ({ claimed, saved, status }) => {
    const { db, credentials, port, session } = await setup();
    await db.setMeta("installationId", f.clientId);
    if (saved) await credentials.savePending(f.terminalId, f.clientId,
      await deriveOwnerKey(f.passkey, f.terminalId, f.clientId));
    port.onCommand = (line) => port.lines.emit(line.startsWith("HELLO,")
      ? `IDENTITY,2,${f.terminalId},E5F6,${claimed ? "CLAIMED" : "UNCLAIMED"},IN_USE,${f.nonce}`
      : "CLAIM_ABORT_OK");
    expect(await session.inspect({ id: "test", name: "Hallzee-E5F6" })).toEqual({
      device: { id: "test", name: "Hallzee-E5F6" }, name: "Hallzee-E5F6", terminalId: f.terminalId,
      pairingStatus: status, inUse: true,
    });
    expect(port.sent).toHaveLength(2);
    expect(port.sent[0]).toMatch(/^HELLO,/);
    expect(port.sent[1]).toMatch(/^CLAIM_ABORT,/);
    expect(port.connected).toBe(claimed && saved);
    expect(session.state).toBe("Disconnected");
    expect(Boolean(await credentials.get(f.terminalId))).toBe(saved);
    await expect(session.send("SYNC_ALL")).rejects.toMatchObject({ code: "AUTH_REQUIRED" });
    session.dispose(); db.close();
  });
  async function pairingSetup(claimed = false) {
    const context = await setup();
    const { db, credentials, port } = context;
    await db.setMeta("installationId", f.clientId);
    if (claimed) await credentials.savePending(f.terminalId, f.clientId,
      await deriveOwnerKey(f.passkey, f.terminalId, f.clientId));
    let hellos = 0;
    port.onCommand = (line) => {
      if (line.startsWith("HELLO,")) {
        // The discovery nonce must never be used to claim or authenticate.
        const nonce = ++hellos === 1 ? f.commitNonce : f.nonce;
        port.lines.emit(`IDENTITY,2,${f.terminalId},E5F6,${claimed ? "CLAIMED,IN_USE" : "UNCLAIMED,AVAILABLE"},${nonce}`);
      } else if (line.startsWith("CLAIM_ABORT,")) port.lines.emit("CLAIM_ABORT_OK");
      else if (line.startsWith("CLAIM,")) {
        expect(line === `CLAIM,2,${f.clientId},${f.claimProof}`).toBe(true);
        port.lines.emit(`CLAIM_OK,2,${f.terminalId},${f.commitNonce}`);
      } else if (line.startsWith("CLAIM_COMMIT,") || line.startsWith("AUTH,")) {
        const expected = claimed ? `AUTH,2,${f.clientId},${f.authProof}` : `CLAIM_COMMIT,2,${f.clientId},${f.commitProof}`;
        expect(line === expected).toBe(true);
        port.lines.emit(`AUTH_OK,2,${f.terminalId},Test terminal`);
      }
    };
    return context;
  }
  it.each([false, true])("uses one encrypted connection and a fresh challenge for the selected terminal (saved owner: %s)", async (claimed) => {
    const { db, workspace, port, session } = await pairingSetup(claimed);
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    const connect = vi.spyOn(port, "connect");
    const control = new AbortController();
    await session.inspect({ id: "test" }, control.signal);
    connect.mockRejectedValue(new DOMException("synthetic native diagnostic", "NotSupportedError"));
    await vi.advanceTimersByTimeAsync(15000);
    expect(port.connected).toBe(true);
    await expect(session.send("SYNC_ALL")).rejects.toMatchObject({ code: "AUTH_REQUIRED" });
    await session.open({ id: "test" }, workspace.workspaceId, f.terminalId, claimed ? undefined : f.passkey);
    expect(connect).toHaveBeenCalledTimes(1);
    expect(session.state).toBe("Authenticated");
    // Dialog cleanup/old probe expiry must not close the authenticated link.
    control.abort(); session.cancelInspection();
    await vi.advanceTimersByTimeAsync(120000);
    expect(port.connected).toBe(true);
    expect((await db.all("credentials"))[0].state).toBe("confirmed");
    session.dispose(); db.close();
  });
  it.each(["cancel", "back", "expiry", "drop", "different device"])("requires a fresh connection after inspection ends: %s", async (reason) => {
    const { db, workspace, port, session } = await pairingSetup();
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    const connect = vi.spyOn(port, "connect");
    const control = new AbortController();
    await session.inspect({ id: "test" }, control.signal);
    if (reason === "cancel") control.abort();
    if (reason === "back") session.cancelInspection();
    if (reason === "expiry") await vi.advanceTimersByTimeAsync(120000);
    if (reason === "drop") port.drops.emit();
    expect(port.connected).toBe(reason === "different device");
    await session.open({ id: reason === "different device" ? "replacement" : "test" }, workspace.workspaceId, f.terminalId, f.passkey);
    expect(connect).toHaveBeenCalledTimes(2);
    expect(session.state).toBe("Authenticated");
    session.dispose(); db.close();
  });
  it("releases the probe if the firmware does not acknowledge clearing its challenge", async () => {
    const { db, port, session } = await pairingSetup();
    const respond = port.onCommand;
    port.onCommand = (line) => line.startsWith("CLAIM_ABORT,")
      ? port.lines.emit("ERROR,AUTH_REQUIRED") : respond(line);
    await expect(session.inspect({ id: "test" })).rejects.toMatchObject({ code: "AUTH_REQUIRED" });
    expect(port.connected).toBe(false);
    expect(await db.all("credentials")).toEqual([]);
    session.dispose(); db.close();
  });
  it("checks full identity again before claiming on a retained link", async () => {
    const { db, workspace, port, session } = await pairingSetup();
    await session.inspect({ id: "test" });
    port.onCommand = () => port.lines.emit(`IDENTITY,2,HZ-000000000001,0001,UNCLAIMED,AVAILABLE,${f.nonce}`);
    await expect(session.open({ id: "test" }, workspace.workspaceId, f.terminalId, f.passkey))
      .rejects.toMatchObject({ code: "IDENTITY_MISMATCH" });
    expect(port.sent.some((line) => line.startsWith("CLAIM,"))).toBe(false);
    expect(port.connected).toBe(false);
    session.dispose(); db.close();
  });
  it("disconnects a cancelled identity inspection without changing ownership", async () => {
    const { db, port, session } = await setup();
    const control = new AbortController();
    port.onCommand = () => control.abort();
    await expect(session.inspect({ id: "test" }, control.signal)).rejects.toMatchObject({ code: "CANCELLED" });
    expect(session.state).toBe("Disconnected");
    expect(await db.all("credentials")).toEqual([]);
    session.dispose(); db.close();
  });
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
