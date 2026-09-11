import { HallzeeError } from "../app/errors";
import type { Student, Enrollment } from "../storage/schema";
import { LocalDatabase, request } from "../storage/LocalDatabase";
export interface Mapping {
  studentId: number;
  firstName: number;
  lastName: number;
  fullName: number;
  grade: number;
  classSection: number;
}
export function parseCsv(text: string): string[][] {
  if (new TextEncoder().encode(text).length > 5 * 1024 * 1024)
    throw new HallzeeError("CSV_TOO_LARGE", "Roster files may be at most 5 MiB.");
  text = text.replace(/^\uFEFF/, "");
  const rows: string[][] = [];
  let row: string[] = [],
    cell = "",
    quoted = false,
    afterQuote = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i++;
        } else {
          quoted = false;
          afterQuote = true;
        }
      } else cell += c;
      continue;
    }
    if (c === '"') {
      if (cell || afterQuote)
        throw new HallzeeError("INVALID_CSV", "A CSV quote is not correctly escaped.");
      quoted = true;
      continue;
    }
    if (c === "," || c === "\r" || c === "\n") {
      row.push(cell);
      cell = "";
      afterQuote = false;
      if (c !== ",") {
        if (row.some((s) => s.trim())) rows.push(row);
        row = [];
        if (c === "\r" && text[i + 1] === "\n") i++;
      }
    } else {
      if (afterQuote && c.trim())
        throw new HallzeeError("INVALID_CSV", "Unexpected text after a quoted CSV field.");
      if (!afterQuote) cell += c;
    }
    if (rows.length > 10001)
      throw new HallzeeError("CSV_TOO_LARGE", "A roster may contain at most 10,000 students.");
  }
  if (quoted) throw new HallzeeError("INVALID_CSV", "A CSV quoted field was not closed.");
  row.push(cell);
  if (row.some((s) => s.trim())) rows.push(row);
  if (rows.length > 10001) throw new HallzeeError("CSV_TOO_LARGE");
  return rows;
}
export function suggestMapping(headers: string[]): Mapping {
  const normalized = headers.map((h) => h.toLowerCase().replace(/[^a-z0-9]/g, ""));
  const find = (names: string[]) => normalized.findIndex((h) => names.includes(h));
  return {
    studentId: find(["studentid", "id", "studentnumber", "studentnum", "sid"]),
    firstName: find(["firstname", "first", "givenname"]),
    lastName: find(["lastname", "last", "surname", "familyname"]),
    fullName: find(["name", "studentname", "fullname"]),
    grade: find(["grade", "gradelevel"]),
    classSection: find(["classperiod", "period", "class", "section", "classsection"]),
  };
}
export function mapRoster(rows: string[][], mapping: Mapping, workspaceId: string) {
  if (
    mapping.studentId < 0 ||
    (mapping.fullName < 0 && (mapping.firstName < 0 || mapping.lastName < 0))
  )
    throw new HallzeeError(
      "MAPPING_REQUIRED",
      "Map Student ID and either Full Name or First/Last Name.",
    );
  const students = new Map<string, Student>(),
    enrollments = new Map<string, Enrollment>(),
    errors: string[] = [];
  const now = new Date().toISOString();
  const value = (row: string[], index: number) => (row[index] ?? "").trim();
  for (let i = 1; i < rows.length; i++) {
    const row = rows[i],
      id = value(row, mapping.studentId);
    let first = value(row, mapping.firstName),
      last = value(row, mapping.lastName);
    if (mapping.firstName < 0 || mapping.lastName < 0) {
      const full = value(row, mapping.fullName);
      if (full.includes(",")) {
        const parts = full.split(",");
        last = parts[0].trim();
        first = parts.slice(1).join(",").trim();
      } else {
        const parts = full.split(/\s+/);
        first = parts.shift() ?? "";
        last = parts.join(" ");
      }
    }
    if (!/^\d{1,16}$/.test(id) || (!first && !last) || [first, last].some((v) => v.length > 200)) {
      errors.push(`Row ${i + 1}: use a numeric ID (1–16 digits) and a student name.`);
      continue;
    }
    const student: Student = {
      workspaceId,
      studentId: id,
      firstName: first,
      lastName: last,
      grade: value(row, mapping.grade) || null,
      createdAtUtc: now,
      updatedAtUtc: now,
    };
    const old = students.get(id);
    if (old && (old.firstName !== first || old.lastName !== last || old.grade !== student.grade)) {
      errors.push(`Row ${i + 1}: conflicting details for duplicate ID ${id}.`);
      continue;
    }
    students.set(id, student);
    const section = value(row, mapping.classSection);
    if (section)
      enrollments.set(JSON.stringify([id, section]), {
        workspaceId,
        studentId: id,
        classSection: section,
      });
  }
  return { students: [...students.values()], enrollments: [...enrollments.values()], errors };
}
export class RosterService {
  constructor(private db: LocalDatabase) {}
  async save(workspaceId: string, students: Student[], enrollments: Enrollment[], replace = false) {
    if (
      students.some(
        (s) =>
          s.workspaceId !== workspaceId ||
          !/^\d{1,16}$/.test(s.studentId) ||
          !(s.firstName + s.lastName).trim(),
      ) ||
      enrollments.some(
        (e) => e.workspaceId !== workspaceId || !students.some((s) => s.studentId === e.studentId),
      )
    )
      throw new HallzeeError("INVALID_ROSTER");
    await this.db.transaction(
      ["roster_students", "roster_enrollments"],
      "readwrite",
      async (tx) => {
        for (const name of ["roster_students", "roster_enrollments"]) {
          const store = tx.objectStore(name);
          const old = await request(store.getAll());
          for (const row of old)
            if (
              row.workspaceId === workspaceId &&
              (replace ||
                (name === "roster_enrollments" &&
                  students.some((s) => s.studentId === row.studentId)))
            )
              await request(
                store.delete(
                  name === "roster_students"
                    ? [workspaceId, row.studentId]
                    : [workspaceId, row.studentId, row.classSection],
                ),
              );
        }
        for (const s of students) await request(tx.objectStore("roster_students").put(s));
        for (const e of enrollments) await request(tx.objectStore("roster_enrollments").put(e));
      },
    );
  }
  async remove(workspaceId: string, studentId: string) {
    await this.db.transaction(
      ["roster_students", "roster_enrollments"],
      "readwrite",
      async (tx) => {
        await request(tx.objectStore("roster_students").delete([workspaceId, studentId]));
        const store = tx.objectStore("roster_enrollments");
        for (const e of await request<Enrollment[]>(store.getAll()))
          if (e.workspaceId === workspaceId && e.studentId === studentId)
            await request(store.delete([workspaceId, studentId, e.classSection]));
      },
    );
  }
}
