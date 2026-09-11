import { describe, it, expect } from "vitest";
import fixtures from "../../../contracts/web-client/v1/identity.json";
import trips from "../../../contracts/web-client/v1/trips.json";
import active from "../../../contracts/web-client/v1/active-passes.json";
import {
  computeClaimProof,
  computeAuthProof,
  deriveOwnerKey,
  passkey,
  terminalName,
} from "../../src/protocol/WebTerminalCrypto";
import { parseIdentity, parseTrip, parsePasses } from "../../src/protocol/messages";
import { LineFramer, commandBytes } from "../../src/transport/LineFramer";
import { ActivePassStore } from "../../src/sync/ActivePassStore";
import { elapsed, wallEpoch, epochLabel } from "../../src/sync/TerminalClock";
describe("A01 literal cross-language crypto contract", () => {
  for (const f of fixtures.cases)
    it(`matches claim, owner auth and commit for ${f.passkey}`, async () => {
      expect(
        await computeClaimProof(
          f.passkey,
          f.clientId.toLowerCase(),
          f.terminalId.toLowerCase(),
          f.nonce.toLowerCase(),
        ),
      ).toBe(f.claimProof);
      const key = await deriveOwnerKey(f.passkey, f.terminalId, f.clientId);
      expect(key.extractable).toBe(false);
      expect(await computeAuthProof(key, f.terminalId, f.clientId, f.nonce)).toBe(f.authProof);
      expect(await computeAuthProof(key, f.terminalId, f.clientId, f.commitNonce)).toBe(
        f.commitProof,
      );
      await expect(crypto.subtle.exportKey("raw", key)).rejects.toThrow();
      expect(
        await computeAuthProof(key, f.terminalId, f.clientId, "00000000000000000000000000000000"),
      ).not.toBe(f.authProof);
    });
  for (const code of ["12345", "1234567", "12A456", "１２３４５６"])
    it(`rejects invalid passkey ${code}`, () => expect(() => passkey(code)).toThrow());
});
describe("A02 strict identity and A03 framing", () => {
  const f = fixtures.cases[0],
    identity = `IDENTITY,2,${f.terminalId},E5F6,CLAIMED,IN_USE,${f.nonce}`;
  it("accepts a credentialed-owner candidate even when occupied", () =>
    expect(parseIdentity(identity)).toMatchObject({ claimed: true, inUse: true }));
  for (const invalid of [
    identity.replace("E5F6,", "0000,"),
    identity.replace("IDENTITY,2", "IDENTITY,1"),
    identity + ",EXTRA",
    identity.replace(f.nonce, "BAD"),
  ])
    it("rejects identity/version/suffix/nonce corruption", () =>
      expect(() => parseIdentity(invalid)).toThrow());
  it("handles every UTF-8 notification split, CRLF and offset views", () => {
    const message = "EVENT,CHECKOUT,00123,12345\r\nText,café\n";
    const bytes = new TextEncoder().encode(message);
    for (let i = 0; i <= bytes.length; i++) {
      const framer = new LineFramer();
      expect([...framer.push(bytes.slice(0, i)), ...framer.push(bytes.slice(i))]).toEqual([
        "EVENT,CHECKOUT,00123,12345",
        "Text,café",
      ]);
    }
    const buffer = new Uint8Array([0, 65, 10, 0]);
    expect(new LineFramer().push(new DataView(buffer.buffer, 1, 2))).toEqual(["A"]);
  });
  it("bounds lines and rejects invalid UTF-8; reset discards partial state", () => {
    expect(() => new LineFramer().push(new Uint8Array(4097).fill(65))).toThrow();
    expect(() => new LineFramer().push(new Uint8Array([255, 10]))).toThrow();
    const f = new LineFramer();
    f.push(new TextEncoder().encode("old"));
    f.reset();
    expect(f.push(new TextEncoder().encode("new\n"))).toEqual(["new"]);
  });
  it("validates command bytes and terminal names", () => {
    expect(commandBytes("ACK,5").at(-1)).toBe(10);
    expect(() => commandBytes("x".repeat(193))).toThrow();
    expect(() => commandBytes("HELLO\nACK,1")).toThrow();
    expect(() => commandBytes("é".repeat(97))).toThrow();
    expect(terminalName(" Room #204 ")).toBe("Room #204");
    for (const v of ["Room,204", "Room\n204", "Salón 204"]) expect(() => terminalName(v)).toThrow();
  });
});
describe("A08 records and A09/A10 pass time", () => {
  for (const f of trips.cases)
    it("accepts firmware fixture including nullable reset fields", () =>
      expect(parseTrip(f.payload)).toMatchObject({
        tripId: f.tripId,
        studentId: f.studentId,
        status: f.status,
      }));
  for (const value of [
    "0,1,,,,,COMPLETE",
    "1,12345678901234567,,,,,COMPLETE",
    "1,1,2026-02-30,09:00:00,,,COMPLETE",
    "1,1,,25:00:00,,,COMPLETE",
    "1,1,,,,-1,COMPLETE",
    "1,1,,,,,BOGUS",
    "1,1,,,,,COMPLETE,0,EXTRA",
  ])
    it("rejects malformed immutable records", () => expect(() => parseTrip(value)).toThrow());
  for (const f of active.cases)
    it("parses active-pass fixture", () => expect(parsePasses(f.line)).toHaveLength(f.count));
  it("keeps leading zeros and removes only the matching student", () => {
    const store = new ActivePassStore();
    store.snapshot("ACTIVE_PASSES,00123,100,00999,110");
    store.event("EVENT,CHECKIN,00123,30");
    expect(store.passes).toEqual([{ studentId: "00999", epoch: 110 }]);
    store.unknown();
    expect(store.fresh).toBe(false);
  });
  it("does not apply a second UTC offset and computes from current wall components", () => {
    const date = new Date(2026, 8, 11, 9, 5, 0);
    const raw = Date.UTC(2026, 8, 11, 9, 0, 0) / 1000;
    expect(elapsed(raw, date)).toBe(300);
    expect(epochLabel(raw)).toBe("09:00:00");
    expect(wallEpoch(date)).toBe(raw + 300);
    expect(elapsed(raw, new Date(2026, 8, 11, 8, 0))).toBe(0);
  });
  it("rejects odd, duplicate, oversized snapshots", () => {
    for (const line of [
      "ACTIVE_PASSES,1",
      "ACTIVE_PASSES,1,1,1,2",
      "ACTIVE_PASSES" + ",1,1".repeat(9),
    ])
      expect(() => parsePasses(line)).toThrow();
  });
});
