export type View = "dashboard" | "trips" | "roster" | "policies" | "terminal" | "settings";
export type TerminalState = "disconnected" | "discovering" | "connecting" | "connected" | "syncing" | "recoverableError";
export type TripStatus = "OCCUPIED" | "COMPLETED";

export interface Trip {
  id: string;
  studentId: string;
  studentName: string;
  destination: string;
  departTime: string;
  returnTime: string;
  durationSeconds: number;
  status: TripStatus;
  date: string;
}

export interface ActiveTrip {
  studentId: string;
  studentName: string;
  destination: string;
  departTime: string;
  elapsedSeconds: number;
}

export interface TerminalDevice {
  id: string;
  name: string;
  rssi: number;
  signal: string;
  status: string;
}

export interface TerminalSettings {
  name: string;
  maxStudentIdLength: number;
}
