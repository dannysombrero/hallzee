import { describe, it, expect } from "vitest";
import { setup } from "./helpers";
import { BackupService, validateBackup } from "../../src/storage/BackupService";
import { request } from "../../src/storage/LocalDatabase";
import { deriveOwnerKey } from "../../src/protocol/WebTerminalCrypto";
import { parseCsv, mapRoster, suggestMapping, RosterService } from "../../src/domain/RosterService";
import { csvCell, exportTrips, CSV_HEADER } from "../../src/domain/TripReports";
import { defaultPolicy } from "../../src/storage/schema";
import {
  evaluateWindow,
  resolvePeriod,
  validatePolicy,
} from "../../src/domain/PolicyScheduleService";
import { buildPolicyTransfer } from "../../src/domain/BellPolicyProtocol";
import policies from "../../../contracts/web-client/v1/policies.json";
const terminalId = "HZ-A1B2C3D4E5F6";
describe("local data and backup safety", () => {
  it("rolls back all stores if a later request aborts", async () => {
    const f = await setup();
    await expect(
      f.db.transaction(["app_meta", "workspaces"], "readwrite", async (tx) => {
        await request(tx.objectStore("app_meta").put({ key: "atomic", value: true }));
        await request(tx.objectStore("workspaces").add(f.workspace));
      }),
    ).rejects.toBeDefined();
    expect(await f.db.meta("atomic", false)).toBe(false);
    f.db.close();
  });
  it("exports only classroom data; restore retains nonextractable keys and resets completed cursors", async () => {
    const f = await setup();
    const clientId = await f.credentials.installationId();
    const key = await deriveOwnerKey("807481", terminalId, clientId);
    await f.credentials.savePending(terminalId, clientId, key);
    await f.db.put("terminals", {
      terminalId,
      customName: "Hallzee",
      protocolVersion: 2,
      maxIdLength: 10,
      assignedWorkspaceId: f.workspace.workspaceId,
      deviceIdHint: "private-device",
    });
    await f.db.put("sync_state", {
      terminalId,
      completedCursor: 110,
      recoveryRequired: false,
      historyGeneration: 1,
      lastSuccessfulSyncUtc: new Date().toISOString(),
    });
    const service = new BackupService(f.db);
    const text = await service.export();
    expect(text).not.toMatch(
      /credentials|private-device|completedCursor|pendingCommit|ownerKey|clientId/,
    );
    const backup = validateBackup(text);
    await f.db.put("workspaces", { ...f.workspace, name: "Will be replaced" });
    await service.restore(backup);
    expect((await f.db.get("workspaces", f.workspace.workspaceId))?.name).toBe(f.workspace.name);
    expect(await f.db.get("sync_state", terminalId)).toMatchObject({
      completedCursor: 0,
      recoveryRequired: true,
    });
    const saved = await f.credentials.get(terminalId);
    expect(saved?.key.extractable).toBe(false);
    expect(await f.credentials.installationId()).toBe(clientId);
    await expect(crypto.subtle.exportKey("raw", saved!.key)).rejects.toBeDefined();
    f.db.close();
  });
  it("rejects forbidden fields, bad references, numeric IDs, invalid timezone and malformed data without replacing data", async () => {
    const f = await setup();
    const service = new BackupService(f.db);
    const original = await service.export();
    for (const mutate of [
      (b: any) => (b.credentials = []),
      (b: any) => (b.workspaces[0].timeZone = null),
      (b: any) => (b.workspaces[0].secret = "no"),
      (b: any) => (b.policyRules[0].capacity = 9),
      (b: any) =>
        b.rosterEnrollments.push({
          workspaceId: f.workspace.workspaceId,
          studentId: "404",
          classSection: "A",
        }),
    ]) {
      const b = JSON.parse(original);
      mutate(b);
      expect(() => validateBackup(JSON.stringify(b))).toThrow();
      await expect(service.restore(b)).rejects.toThrow();
      expect((await f.db.all("workspaces"))[0].name).toBe(f.workspace.name);
    }
    f.db.close();
  });
});
describe("roster and reports", () => {
  it("preserves leading zeros and quoted names; merges matching duplicate enrollments", () => {
    const rows = parseCsv(
      '\uFEFFStudent ID,Name,Section\r\n00123,"Rivera, Alex",A\r\n00123,"Rivera, Alex",B\r\n',
    );
    const result = mapRoster(rows, suggestMapping(rows[0]), "w");
    expect(result.errors).toEqual([]);
    expect(result.students).toHaveLength(1);
    expect(result.students[0]).toMatchObject({
      studentId: "00123",
      firstName: "Alex",
      lastName: "Rivera",
    });
    expect(result.enrollments).toHaveLength(2);
    const conflict = mapRoster(
      [...rows, ["00123", "Wrong Name", "C"]],
      suggestMapping(rows[0]),
      "w",
    );
    expect(conflict.errors).toHaveLength(1);
  });
  it("handles embedded newlines and escaped quotes, rejects unterminated fields", () => {
    expect(parseCsv('a,b\r\n"one\ntwo","a""b"')).toEqual([
      ["a", "b"],
      ["one\ntwo", 'a"b'],
    ]);
    expect(() => parseCsv('a,"b')).toThrow();
  });
  it("roster replacement removes omitted enrollments atomically", async () => {
    const f = await setup();
    const service = new RosterService(f.db);
    const rows = parseCsv("ID,Name,Section\n001,A One,A\n002,B Two,B");
    const result = mapRoster(rows, suggestMapping(rows[0]), f.workspace.workspaceId);
    await service.save(f.workspace.workspaceId, result.students, result.enrollments);
    await service.save(f.workspace.workspaceId, result.students.slice(0, 1), [], true);
    expect(await f.db.all("roster_students")).toHaveLength(1);
    expect(await f.db.all("roster_enrollments")).toHaveLength(0);
    f.db.close();
  });
  it("exports stable headers, CRLF and formula-safe cells", () => {
    expect(exportTrips([], [])).toBe("\uFEFF" + CSV_HEADER + "\r\n\r\n");
    for (const s of ["=SUM(1)", "+2", "-2", "@cmd", "\tcmd"]) expect(csvCell(s)).toBe("'" + s);
    expect(csvCell('a,"b"')).toBe('"a,""b"""');
  });
});
describe("shared policy fixtures", () => {
  const p = {
    ...defaultPolicy("w"),
    firstMinutes: 10,
    lastMinutes: 5,
    firstAction: "Lock" as const,
    lastAction: "Warn" as const,
    enforcement: true,
  };
  const period = {
    workspaceId: "w",
    scheduleId: "p",
    periodName: "One",
    scheduleName: "Regular",
    classSection: "A",
    start: "08:00",
    end: "09:00",
    weekdays: ["Mon"],
  };
  it("matches shared decisions and exact firmware transfer", () => {
    for (const c of policies.cases) {
      const at = new Date(2026, 8, 7, c.hour, c.minute);
      expect(evaluateWindow(at, resolvePeriod(at, [period], []), p)).toBe(c.decision);
    }
    expect(buildPolicyTransfer(new Date(2026, 8, 7), p, [period], [])).toContain(policies.transfer);
  });
  it("handles no-school exceptions and rejects overlapping or overnight edits", () => {
    expect(
      resolvePeriod(
        new Date(2026, 8, 7, 8, 5),
        [period],
        [{ workspaceId: "w", date: "2026-09-07", scheduleName: "", isNoSchool: true }],
      ),
    ).toBeUndefined();
    expect(() => validatePolicy(p, [period, { ...period, scheduleId: "q" }], [])).toThrow();
    expect(() => validatePolicy(p, [{ ...period, start: "23:00", end: "01:00" }], [])).toThrow();
  });
});
