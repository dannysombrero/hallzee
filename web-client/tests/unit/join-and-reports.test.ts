import { describe, expect, it } from "vitest";
import { appRoute, roomLinks, validRoomCode } from "../../src/domain/RoomLinks";
import { exceededStudents } from "../../src/domain/ExceededTrips";
import type { Student, Trip } from "../../src/storage/schema";

describe("student joining", () => {
  it("separates the public landing page from the teacher dashboard and accepts direct and legacy links", () => {
    expect(appRoute(new URL("https://pass.hallzee.com/"))).toEqual({ type: "join" });
    expect(appRoute(new URL("https://web.hallzee.com/"))).toEqual({ type: "dashboard" });
    for (const url of ["https://pass.hallzee.com/room-2107", "https://web.hallzee.com/pass/room-2107", "http://localhost:4187/?room=room-2107"])
      expect(appRoute(new URL(url))).toEqual({ type: "station", roomCode: "ROOM-2107" });
    expect(appRoute(new URL("https://pass.hallzee.com/room-2107/extra"))).toEqual({ type: "join", invalidCode: true });
    expect(validRoomCode("  room-2107  ")).toBe(true);
    expect(validRoomCode("JOIN")).toBe(false);
  });
  it("shares production links on the pass host and keeps local previews on their own origin", () => {
    expect(roomLinks("ROOM-2107", "https://web.hallzee.com")).toEqual({ join: "https://pass.hallzee.com/", terminal: "https://pass.hallzee.com/room-2107" });
    expect(roomLinks("ROOM-2107", "http://localhost:4187")).toEqual({ join: "http://localhost:4187/join", terminal: "http://localhost:4187/room-2107" });
  });
});
describe("Exceeded Time controls", () => {
  const now = new Date(2026, 8, 25, 10);
  const students = [
    { workspaceId: "demo", studentId: "101", firstName: "Zoe", lastName: "Example" },
    { workspaceId: "demo", studentId: "102", firstName: "Alex", lastName: "Example" },
  ] as Student[];
  const trip = (id: number, sid: string, date: string, section: string, duration = 600) => ({
    tripId: id, studentId: sid, tripDate: date, classSection: section, durationSeconds: duration,
    status: "COMPLETE", receivedWorkspaceId: "demo",
  } as Trip);
  const trips = [trip(1, "101", "2026-09-25", "Period 2"), trip(2, "101", "2026-09-15", "Period 2"), trip(3, "102", "2026-09-12", "Period 10"), trip(4, "102", "2026-09-11", "Period 10"), trip(5, "102", "2026-09-26", "Period 10")];
  const options = { now, days: 14, thresholdMinutes: 7, search: "", sort: "period" as const };
  it("uses an inclusive 14-day range, excludes future trips, and supports today and threshold changes", () => {
    expect(exceededStudents(trips, students, options).map(g => g.trips.length)).toEqual([2, 1]);
    expect(exceededStudents(trips, students, { ...options, days: 1 }).map(g => g.studentId)).toEqual(["101"]);
    expect(exceededStudents(trips, students, { ...options, thresholdMinutes: 10 })).toEqual([]);
  });
  it("searches recorded periods and actually sorts by period, name, or count", () => {
    expect(exceededStudents(trips, students, { ...options, search: "period 10" }).map(g => g.studentId)).toEqual(["102"]);
    expect(exceededStudents(trips, students, { ...options, sort: "name" }).map(g => g.studentId)).toEqual(["102", "101"]);
    expect(exceededStudents(trips, students, { ...options, sort: "count" }).map(g => g.studentId)).toEqual(["101", "102"]);
  });
});
