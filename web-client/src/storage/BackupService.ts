import { LocalDatabase, request } from "./LocalDatabase";
import {
  defaultSync,
  type Workspace,
  type Terminal,
  type Trip,
  type Student,
  type Enrollment,
  type Policy,
  type BellPeriod,
  type ScheduleException,
} from "./schema";
import { HallzeeError } from "../app/errors";
import { parseTrip, validDate } from "../protocol/messages";
import { terminalId, terminalName } from "../protocol/WebTerminalCrypto";
import { validatePolicy } from "../domain/PolicyScheduleService";
export interface Backup {
  format: "hallzee-web-data";
  formatVersion: 1;
  schemaVersion: 1;
  appVersion: string;
  exportedAtUtc: string;
  workspaces: Workspace[];
  terminals: Terminal[];
  trips: Trip[];
  rosterStudents: Student[];
  rosterEnrollments: Enrollment[];
  policyRules: Policy[];
  bellPeriods: BellPeriod[];
  scheduleExceptions: ScheduleException[];
}
const mapping = {
  workspaces: "workspaces",
  terminals: "terminals",
  trips: "trips",
  rosterStudents: "roster_students",
  rosterEnrollments: "roster_enrollments",
  policyRules: "policy_rules",
  bellPeriods: "bell_periods",
  scheduleExceptions: "schedule_exceptions",
} as const;
const fields: Record<string, string[]> = {
  workspaces: [
    "workspaceId",
    "name",
    "teacher",
    "school",
    "room",
    "timeZone",
    "createdAtUtc",
    "updatedAtUtc",
  ],
  terminals: ["terminalId", "customName", "protocolVersion", "maxIdLength"],
  trips: [
    "tripId",
    "studentId",
    "tripDate",
    "timeOut",
    "timeIn",
    "durationSeconds",
    "status",
    "terminalId",
    "receivedAtUtc",
    "receivedWorkspaceId",
    "scheduleName",
    "classSection",
    "contextSource",
  ],
  rosterStudents: [
    "workspaceId",
    "studentId",
    "firstName",
    "lastName",
    "grade",
    "createdAtUtc",
    "updatedAtUtc",
  ],
  rosterEnrollments: ["workspaceId", "studentId", "classSection"],
  policyRules: [
    "workspaceId",
    "capacity",
    "warningSeconds",
    "dailyGuideline",
    "firstMinutes",
    "lastMinutes",
    "firstAction",
    "lastAction",
    "enforcement",
    "revision",
    "appliedRevision",
    "appliedDate",
  ],
  bellPeriods: [
    "workspaceId",
    "scheduleId",
    "periodName",
    "scheduleName",
    "classSection",
    "start",
    "end",
    "weekdays",
  ],
  scheduleExceptions: ["workspaceId", "date", "scheduleName", "isNoSchool"],
};
const pick = (value: object, keys: string[]) =>
  Object.fromEntries(keys.map((key) => [key, (value as Record<string, unknown>)[key]]));
function invalid(): never {
  throw new HallzeeError(
    "INVALID_BACKUP",
    "This backup contains invalid, unsupported, or conflicting classroom data. Nothing was replaced.",
  );
}
export function validateBackup(text: string): Backup {
  if (new TextEncoder().encode(text).length > 50 * 1024 * 1024)
    throw new HallzeeError("BACKUP_TOO_LARGE", "Backups may be at most 50 MiB.");
  let b: Backup;
  try {
    b = JSON.parse(text);
  } catch {
    return invalid();
  }
  if (
    !b ||
    b.format !== "hallzee-web-data" ||
    b.formatVersion !== 1 ||
    b.schemaVersion !== 1 ||
    typeof b.appVersion !== "string" ||
    !validUtc(b.exportedAtUtc)
  )
    return invalid();
  if (
    Object.keys(b).some(
      (k) =>
        ![
          "format",
          "formatVersion",
          "schemaVersion",
          "appVersion",
          "exportedAtUtc",
          ...Object.keys(mapping),
        ].includes(k),
    )
  )
    return invalid();
  for (const key of Object.keys(mapping) as (keyof typeof mapping)[]) {
    if (!Array.isArray(b[key])) return invalid();
    for (const row of b[key])
      if (
        !row ||
        typeof row !== "object" ||
        Object.keys(row).some((k) => !fields[key].includes(k)) ||
        fields[key].some((k) => !(k in row))
      )
        return invalid();
  }
  if (
    b.workspaces.length !== 1 ||
    b.terminals.length > 100 ||
    b.trips.length > 100000 ||
    b.rosterStudents.length > 10000 ||
    b.bellPeriods.length > 1000 ||
    b.scheduleExceptions.length > 1000 ||
    b.rosterEnrollments.length > 100000 ||
    b.policyRules.length !== 1
  )
    return invalid();
  const textField = (s: unknown, max = 200) => typeof s === "string" && s.length <= max;
  const unique = (values: string[]) => new Set(values).size === values.length;
  const workspace = b.workspaces[0];
  if (
    !textField(workspace.workspaceId) ||
    !workspace.workspaceId ||
    !textField(workspace.name) ||
    !workspace.name.trim() ||
    ![workspace.teacher, workspace.school, workspace.room].every((s) => textField(s)) ||
    !validUtc(workspace.createdAtUtc) ||
    !validUtc(workspace.updatedAtUtc)
  )
    return invalid();
  try {
    if (typeof workspace.timeZone !== "string" || !workspace.timeZone) return invalid();
    new Intl.DateTimeFormat("en", { timeZone: workspace.timeZone });
  } catch {
    return invalid();
  }
  if (
    !unique(b.terminals.map((t) => t.terminalId)) ||
    !unique(b.trips.map((t) => JSON.stringify([t.terminalId, t.tripId]))) ||
    !unique(b.rosterStudents.map((s) => s.studentId)) ||
    !unique(b.rosterEnrollments.map((e) => JSON.stringify([e.studentId, e.classSection])))
  )
    return invalid();
  for (const t of b.terminals) {
    try {
      if (terminalId(t.terminalId) !== t.terminalId || terminalName(t.customName) !== t.customName)
        return invalid();
    } catch {
      return invalid();
    }
    if (
      t.protocolVersion !== 2 ||
      !Number.isInteger(t.maxIdLength) ||
      t.maxIdLength < 4 ||
      t.maxIdLength > 16
    )
      return invalid();
  }
  for (const t of b.trips) {
    if (
      !b.terminals.some((d) => d.terminalId === t.terminalId) ||
      t.receivedWorkspaceId !== workspace.workspaceId ||
      !validUtc(t.receivedAtUtc) ||
      !["resolved-on-receipt", "unknown"].includes(t.contextSource) ||
      ![t.scheduleName, t.classSection].every((s) => s === null || textField(s))
    )
      return invalid();
    try {
      const parsed = parseTrip(
        [
          t.tripId,
          t.studentId,
          t.tripDate,
          t.timeOut,
          t.timeIn ?? "",
          t.durationSeconds ?? "",
          t.status,
        ].join(","),
      );
      for (const k of Object.keys(parsed))
        if (
          (parsed as unknown as Record<string, unknown>)[k] !==
          (t as unknown as Record<string, unknown>)[k]
        )
          return invalid();
    } catch {
      return invalid();
    }
  }
  for (const s of b.rosterStudents)
    if (
      s.workspaceId !== workspace.workspaceId ||
      typeof s.studentId !== "string" ||
      !/^\d{1,16}$/.test(s.studentId) ||
      ![s.firstName, s.lastName].every((v) => textField(v)) ||
      !(s.firstName + s.lastName).trim() ||
      !(s.grade === null || textField(s.grade)) ||
      !validUtc(s.createdAtUtc) ||
      !validUtc(s.updatedAtUtc)
    )
      return invalid();
  for (const e of b.rosterEnrollments)
    if (
      e.workspaceId !== workspace.workspaceId ||
      !textField(e.classSection) ||
      !e.classSection ||
      !b.rosterStudents.some((s) => s.studentId === e.studentId)
    )
      return invalid();
  const p = b.policyRules[0];
  if (
    p.workspaceId !== workspace.workspaceId ||
    !Number.isInteger(p.revision) ||
    p.revision < 0 ||
    !(p.appliedRevision === null || Number.isInteger(p.appliedRevision)) ||
    !(p.appliedDate === null || validDate(p.appliedDate))
  )
    return invalid();
  for (const period of b.bellPeriods)
    if (
      !Array.isArray(period.weekdays) ||
      ![
        period.scheduleId,
        period.periodName,
        period.scheduleName,
        period.classSection,
        period.start,
        period.end,
      ].every((v) => textField(v))
    )
      return invalid();
  for (const e of b.scheduleExceptions)
    if (typeof e.isNoSchool !== "boolean" || !textField(e.scheduleName)) return invalid();
  try {
    validatePolicy(p, b.bellPeriods, b.scheduleExceptions);
  } catch {
    return invalid();
  }
  return b;
}
const validUtc = (s: unknown) =>
  typeof s === "string" &&
  /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/.test(s) &&
  !Number.isNaN(Date.parse(s)) &&
  new Date(s).toISOString() === s;
export class BackupService {
  constructor(private db: LocalDatabase) {}
  async export(): Promise<string> {
    const result: Record<string, unknown> = {
      format: "hallzee-web-data",
      formatVersion: 1,
      schemaVersion: 1,
      appVersion: "1.2.0-dev.1",
      exportedAtUtc: new Date().toISOString(),
    };
    await this.db.transaction(Object.values(mapping), "readonly", async (tx) => {
      for (const [key, store] of Object.entries(mapping)) {
        result[key] = (await request<object[]>(tx.objectStore(store).getAll())).map((row) =>
          pick(row, fields[key]),
        );
      }
    });
    return JSON.stringify(result, null, 2);
  }
  async restore(backup: Backup) {
    // Validate again at the mutation boundary; callers cannot bypass the file preview.
    const b = validateBackup(JSON.stringify(backup));
    await this.db.transaction(
      [...Object.values(mapping), "sync_state", "app_meta"],
      "readwrite",
      async (tx) => {
        const previous = await request<Terminal[]>(tx.objectStore("terminals").getAll());
        for (const store of Object.values(mapping)) await request(tx.objectStore(store).clear());
        await request(tx.objectStore("sync_state").clear());
        for (const [key, store] of Object.entries(mapping)) {
          for (const value of b[key as keyof typeof mapping]) {
            let row: object = value;
            if (key === "terminals") {
              const t = value as Terminal;
              const old = previous.find(
                (p) =>
                  p.terminalId === t.terminalId &&
                  b.workspaces.some((w) => w.workspaceId === p.assignedWorkspaceId),
              );
              row = {
                ...t,
                ...(old
                  ? { assignedWorkspaceId: old.assignedWorkspaceId, deviceIdHint: old.deviceIdHint }
                  : {}),
              };
              await request(
                tx
                  .objectStore("sync_state")
                  .put({ ...defaultSync(t.terminalId), recoveryRequired: true }),
              );
            }
            if (key === "policyRules") row = { ...value, appliedRevision: null, appliedDate: null };
            await request(tx.objectStore(store).put(row));
          }
        }
        await request(
          tx
            .objectStore("app_meta")
            .put({ key: "activeWorkspaceId", value: b.workspaces[0].workspaceId }),
        );
        await request(tx.objectStore("app_meta").put({ key: "autoConnect", value: false }));
      },
    );
  }
}
