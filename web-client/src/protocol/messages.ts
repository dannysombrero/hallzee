import { HallzeeError } from "../app/errors";
import { nonce, terminalId } from "./WebTerminalCrypto";
import type { WireTrip } from "../storage/schema";
export interface Identity {
  terminalId: string;
  claimed: boolean;
  inUse: boolean;
  nonce: string;
}
export function parseIdentity(line: string): Identity {
  const f = line.split(",");
  if (
    f.length !== 7 ||
    f[0] !== "IDENTITY" ||
    f[1] !== "2" ||
    !["CLAIMED", "UNCLAIMED"].includes(f[4]) ||
    !["AVAILABLE", "IN_USE"].includes(f[5])
  )
    throw new HallzeeError("INVALID_IDENTITY");
  const id = terminalId(f[2]);
  if (f[3].toUpperCase() !== id.slice(-4)) throw new HallzeeError("INVALID_IDENTITY");
  return {
    terminalId: id,
    claimed: f[4] === "CLAIMED",
    inUse: f[5] === "IN_USE",
    nonce: nonce(f[6]),
  };
}
export function validDate(value: string) {
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(value) &&
    !Number.isNaN(Date.parse(value)) &&
    new Date(value).toISOString().slice(0, 10) === value
  );
}
export const validTime = (value: string) => /^([01]\d|2[0-3]):[0-5]\d:[0-5]\d$/.test(value);
export function parseTrip(payload: string): WireTrip {
  const f = payload.split(",");
  if (
    (f.length !== 7 && f.length !== 8) ||
    (f.length === 8 && !["0", "1"].includes(f[7])) ||
    !/^[0-9]+$/.test(f[0]) ||
    !Number.isSafeInteger(Number(f[0])) ||
    Number(f[0]) < 1 ||
    Number(f[0]) > 4294967295 ||
    !/^[0-9]{1,16}$/.test(f[1]) ||
    (f[2] !== "" && !validDate(f[2])) ||
    [f[3], f[4]].some((t) => t !== "" && !validTime(t)) ||
    (f[5] !== "" && (!/^\d+$/.test(f[5]) || !Number.isSafeInteger(Number(f[5])))) ||
    !["COMPLETE", "MANUAL", "MANUAL_RESET"].includes(f[6])
  )
    throw new HallzeeError(
      "INVALID_TRIP",
      "A terminal record is malformed. History recovery stopped without acknowledging it.",
    );
  return {
    tripId: Number(f[0]),
    studentId: f[1],
    tripDate: f[2],
    timeOut: f[3],
    timeIn: f[4] || null,
    durationSeconds: f[5] === "" ? null : Number(f[5]),
    status: f[6] as WireTrip["status"],
  };
}
export const immutableTrip = (trip: WireTrip) =>
  JSON.stringify([
    trip.tripId,
    trip.studentId,
    trip.tripDate,
    trip.timeOut,
    trip.timeIn,
    trip.durationSeconds,
    trip.status,
  ]);
export interface ActivePass {
  studentId: string;
  epoch: number;
}
export function parsePasses(line: string): ActivePass[] {
  if (line === "ACTIVE_PASSES" || line === "ACTIVE_PASS,NONE") return [];
  const f = line.split(",");
  if (
    !["ACTIVE_PASS", "ACTIVE_PASSES"].includes(f[0]) ||
    f.length % 2 !== 1 ||
    f.length > 17 ||
    (f[0] === "ACTIVE_PASS" && f.length !== 3)
  )
    throw new HallzeeError("INVALID_SNAPSHOT");
  const passes: ActivePass[] = [];
  for (let i = 1; i < f.length; i += 2) {
    if (
      !/^\d{1,16}$/.test(f[i]) ||
      !/^\d+$/.test(f[i + 1]) ||
      !Number.isSafeInteger(Number(f[i + 1])) ||
      Number(f[i + 1]) <= 0 ||
      passes.some((p) => p.studentId === f[i])
    )
      throw new HallzeeError("INVALID_SNAPSHOT");
    passes.push({ studentId: f[i], epoch: Number(f[i + 1]) });
  }
  return passes.sort((a, b) => a.epoch - b.epoch);
}
export function protocolError(line: string) {
  const code = line
    .split(",")
    .slice(1)
    .join("_")
    .replace(/[^A-Z0-9_]/g, "")
    .slice(0, 80);
  const help: Record<string, string> = {
    PAIRING_MODE_REQUIRED: "Open pairing mode on the terminal, then enter the code currently shown there in Hallzee.",
    AUTH_FAILED_CLAIM: "That pairing code was not accepted. Check the code currently shown on the terminal and try again.",
    ALREADY_CLAIMED: "This terminal is already paired. Reconnect from its paired client, or release it there before pairing here.",
    TERMINAL_IN_USE: "Check in every active pass before pairing this terminal.",
    AUTH_FAILED_AUTH: "The saved pairing was not accepted. Check that you selected your paired terminal. Keep Hallzee site data and records.",
    AUTH_TIMEOUT: "The connection timed out. Reconnect to start a fresh authentication.",
  };
  return new HallzeeError(
    code || "PROTOCOL_ERROR",
    help[code] ?? `The terminal rejected the operation (${code || "PROTOCOL_ERROR"}). Reconnect or check its settings.`,
  );
}
