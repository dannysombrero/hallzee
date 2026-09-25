import type { Trip, Student } from "../storage/schema";
import { fullName } from "./TripReports";
import { localDate } from "../sync/TerminalClock";

export type ExceededSort = "period" | "name" | "count";
export function exceededStudents(trips: Trip[], students: Student[], options: {
  now: Date; days: number; thresholdMinutes: number; search: string; sort: ExceededSort;
}) {
  const start = new Date(options.now);
  start.setDate(start.getDate() - (options.days - 1));
  const from = localDate(start), to = localDate(options.now);
  const names = new Map(students.map(s => [`${s.workspaceId}:${s.studentId}`, fullName(s)]));
  const groups = new Map<string, {
    studentId: string; displayName: string; periods: string[]; trips: Trip[]; maxDuration: number;
  }>();
  for (const trip of trips) {
    if (trip.tripDate < from || trip.tripDate > to || trip.status === "MANUAL_RESET" ||
      trip.durationSeconds === null || trip.durationSeconds <= options.thresholdMinutes * 60) continue;
    const name = names.get(`${trip.receivedWorkspaceId}:${trip.studentId}`) || trip.studentId;
    const group = groups.get(trip.studentId) ?? {
      studentId: trip.studentId, displayName: name, periods: [], trips: [], maxDuration: 0,
    };
    group.trips.push(trip);
    group.maxDuration = Math.max(group.maxDuration, trip.durationSeconds);
    if (trip.classSection && !group.periods.includes(trip.classSection)) group.periods.push(trip.classSection);
    groups.set(trip.studentId, group);
  }
  const query = options.search.trim().toLowerCase();
  const compare = (a: string, b: string) => a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });
  return [...groups.values()].filter(g =>
    `${g.studentId} ${g.displayName} ${g.periods.join(" ")}`.toLowerCase().includes(query),
  ).map(g => ({ ...g, periods: g.periods.sort(compare) })).sort((a, b) => {
    if (options.sort === "count") return b.trips.length - a.trips.length || compare(a.displayName, b.displayName);
    if (options.sort === "period") return compare(a.periods[0] || "\uffff", b.periods[0] || "\uffff") || compare(a.displayName, b.displayName);
    return compare(a.displayName, b.displayName);
  });
}
