import type { Trip, Student } from "../storage/schema";
export const CSV_HEADER =
  "trip_id,student_id,student_name,class_section,schedule_name,grade,trip_date,time_out,time_in,duration_seconds,status,terminal_id,synced_at";
export const fullName = (student?: Student) =>
  student ? `${student.firstName} ${student.lastName}`.trim() : "";
export interface TripFilter {
  search: string;
  from: string;
  to: string;
  status: string;
  section: string;
}
export function filterTrips(trips: Trip[], students: Student[], filter: TripFilter) {
  const names = new Map(
    students.map((s) => [`${s.workspaceId}:${s.studentId}`, fullName(s).toLowerCase()]),
  );
  const query = filter.search.trim().toLowerCase();
  return trips
    .filter(
      (t) =>
        (!query ||
          t.studentId.includes(query) ||
          (names.get(`${t.receivedWorkspaceId}:${t.studentId}`) ?? "").includes(query)) &&
        (!filter.from || t.tripDate >= filter.from) &&
        (!filter.to || t.tripDate <= filter.to) &&
        (!filter.status || t.status === filter.status) &&
        (!filter.section || t.classSection === filter.section),
    )
    .sort(
      (a, b) =>
        b.tripDate.localeCompare(a.tripDate) ||
        b.timeOut.localeCompare(a.timeOut) ||
        b.tripId - a.tripId,
    );
}
export function csvCell(value: unknown) {
  let text = value == null ? "" : String(value);
  if (/^[=+\-@\t\r]/.test(text)) text = "'" + text;
  return /[,"\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}
export function exportTrips(trips: Trip[], students: Student[]) {
  const roster = new Map(students.map((s) => [`${s.workspaceId}:${s.studentId}`, s]));
  return (
    "\uFEFF" +
    CSV_HEADER +
    "\r\n" +
    trips
      .map((t) => {
        const s = roster.get(`${t.receivedWorkspaceId}:${t.studentId}`);
        return [
          t.tripId,
          t.studentId,
          fullName(s),
          t.classSection,
          t.scheduleName,
          s?.grade,
          t.tripDate,
          t.timeOut,
          t.timeIn,
          t.durationSeconds,
          t.status,
          t.terminalId,
          t.receivedAtUtc,
        ]
          .map(csvCell)
          .join(",");
      })
      .join("\r\n") +
    "\r\n"
  );
}
export function download(text: string, name: string, type = "application/json") {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
