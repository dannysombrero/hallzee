export interface Workspace {
  workspaceId: string;
  name: string;
  teacher: string;
  school: string;
  room: string;
  timeZone: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface Terminal {
  terminalId: string;
  customName: string;
  protocolVersion: 2;
  deviceIdHint?: string;
  assignedWorkspaceId?: string;
  maxIdLength: number;
  settingsReadUtc?: string;
}
export interface Credential {
  terminalId: string;
  clientId: string;
  key: CryptoKey;
  state: "pendingCommit" | "confirmed";
  createdAtUtc: string;
}
export interface WireTrip {
  tripId: number;
  studentId: string;
  tripDate: string;
  timeOut: string;
  timeIn: string | null;
  durationSeconds: number | null;
  status: "COMPLETE" | "MANUAL" | "MANUAL_RESET";
}
export interface Trip extends WireTrip {
  terminalId: string;
  receivedAtUtc: string;
  receivedWorkspaceId: string;
  scheduleName: string | null;
  classSection: string | null;
  contextSource: "resolved-on-receipt" | "unknown";
}
export interface SyncState {
  terminalId: string;
  completedCursor: number;
  lastSuccessfulSyncUtc: string | null;
  recoveryRequired: boolean;
  historyGeneration: number;
}
export interface Student {
  workspaceId: string;
  studentId: string;
  firstName: string;
  lastName: string;
  grade: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface Enrollment {
  workspaceId: string;
  studentId: string;
  classSection: string;
}
export type Decision = "Allow" | "Warn" | "Lock";
export interface Policy {
  workspaceId: string;
  capacity: number;
  warningSeconds: number;
  dailyGuideline: number;
  firstMinutes: number;
  lastMinutes: number;
  firstAction: Decision;
  lastAction: Decision;
  enforcement: boolean;
  revision: number;
  appliedRevision: number | null;
  appliedDate: string | null;
}
export interface BellPeriod {
  workspaceId: string;
  scheduleId: string;
  periodName: string;
  scheduleName: string;
  classSection: string;
  start: string;
  end: string;
  weekdays: string[];
}
export interface ScheduleException {
  workspaceId: string;
  date: string;
  scheduleName: string;
  isNoSchool: boolean;
}
export interface Stores {
  app_meta: { key: string; value: unknown };
  workspaces: Workspace;
  terminals: Terminal;
  credentials: Credential;
  trips: Trip;
  sync_state: SyncState;
  roster_students: Student;
  roster_enrollments: Enrollment;
  policy_rules: Policy;
  bell_periods: BellPeriod;
  schedule_exceptions: ScheduleException;
}
export type StoreName = keyof Stores;
export const storeNames: StoreName[] = [
  "app_meta",
  "workspaces",
  "terminals",
  "credentials",
  "trips",
  "sync_state",
  "roster_students",
  "roster_enrollments",
  "policy_rules",
  "bell_periods",
  "schedule_exceptions",
];
export const defaultPolicy = (workspaceId: string): Policy => ({
  workspaceId,
  capacity: 1,
  warningSeconds: 420,
  dailyGuideline: 2,
  firstMinutes: 10,
  lastMinutes: 10,
  firstAction: "Warn",
  lastAction: "Warn",
  enforcement: false,
  revision: 0,
  appliedRevision: null,
  appliedDate: null,
});
export const defaultSync = (terminalId: string): SyncState => ({
  terminalId,
  completedCursor: 0,
  lastSuccessfulSyncUtc: null,
  recoveryRequired: false,
  historyGeneration: 1,
});
