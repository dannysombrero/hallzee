import { useState } from "react";
import { Upload, Plus, Trash2, Pencil } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { mapRoster, parseCsv, suggestMapping, type Mapping } from "../domain/RosterService";
import { fullName } from "../domain/TripReports";
import { errorText } from "../app/errors";
import type { Student } from "../storage/schema";
export function RosterDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [rows, setRows] = useState<string[][] | null>(null);
  const [mapping, setMapping] = useState<Mapping | null>(null);
  const [replace, setReplace] = useState(false);
  const [error, setError] = useState("");
  const [edit, setEdit] = useState<Student | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [sections, setSections] = useState("");
  async function file(file?: File) {
    if (!file) return;
    setError("");
    try {
      if (file.size > 5 * 1024 * 1024) throw new Error();
      const parsed = parseCsv(await file.text());
      if (!parsed.length) {
        setError("This CSV is empty.");
        return;
      }
      setRows(parsed);
      setMapping(suggestMapping(parsed[0]));
    } catch (e) {
      setError(errorText(e));
    }
  }
  let preview: ReturnType<typeof mapRoster> | null = null;
  try {
    if (rows && mapping) preview = mapRoster(rows, mapping, state.workspace!.workspaceId);
  } catch {
    /* display mapping instructions */
  }
  function editStudent(student?: Student) {
    setEditingId(student?.studentId ?? null);
    const now = new Date().toISOString();
    setEdit(
      student ?? {
        workspaceId: state.workspace!.workspaceId,
        studentId: "",
        firstName: "",
        lastName: "",
        grade: null,
        createdAtUtc: now,
        updatedAtUtc: now,
      },
    );
    setSections(
      student
        ? state.enrollments
            .filter((e) => e.studentId === student.studentId)
            .map((e) => e.classSection)
            .join(", ")
        : "",
    );
  }
  return (
    <Dialog title="Student roster" onClose={onClose}>
      <div className="toolbar">
        <p>{state.students.length} students · Names stay in this browser.</p>
        <button className="secondary" onClick={() => editStudent()}>
          <Plus size={17} />
          Add student
        </button>
      </div>
      <label
        className="drop-zone"
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          void file(e.dataTransfer.files[0]);
        }}
      >
        <Upload size={28} />
        <strong>Import a roster CSV</strong>
        <span>Drop a file or choose one · up to 5 MiB</span>
        <input
          type="file"
          accept=".csv,text/csv"
          onChange={(e) => void file(e.target.files?.[0])}
        />
      </label>
      {rows && mapping && (
        <section className="inset">
          <h3>Map your columns</h3>
          <p>{rows.length - 1} rows. Choose Student ID and either Full Name or First/Last Name.</p>
          <div className="form-grid">
            {(Object.keys(mapping) as (keyof Mapping)[]).map((key) => (
              <label key={key}>
                {
                  {
                    studentId: "Student ID",
                    firstName: "First name",
                    lastName: "Last name",
                    fullName: "Full name",
                    grade: "Grade",
                    classSection: "Class section",
                  }[key]
                }
                <select
                  value={mapping[key]}
                  onChange={(e) => setMapping({ ...mapping, [key]: Number(e.target.value) })}
                >
                  <option value={-1}>Not mapped</option>
                  {rows[0].map((h, i) => (
                    <option value={i} key={i}>
                      {h}
                    </option>
                  ))}
                </select>
              </label>
            ))}
          </div>
          <p>{preview?.students.length ?? 0} valid students</p>
          {preview?.errors.map((e, i) => (
            <p key={i} className="error">
              {e}
            </p>
          ))}
          <ul>
            {preview?.students.slice(0, 3).map((s) => (
              <li key={s.studentId}>
                {s.studentId} · {fullName(s)}
              </li>
            ))}
          </ul>
          <label className="check">
            <input
              type="checkbox"
              checked={replace}
              onChange={(e) => setReplace(e.target.checked)}
            />
            Replace roster, removing{" "}
            {
              state.students.filter(
                (s) => !preview?.students.some((p) => p.studentId === s.studentId),
              ).length
            }{" "}
            omitted students. Trip history remains.
          </label>
          <button
            disabled={state.busy || !preview?.students.length || !!preview.errors.length}
            onClick={() => {
              if (!preview) return;
              if (
                !window.confirm(
                  `${replace ? "Replace" : "Merge"} the local roster with ${preview.students.length} students?`,
                )
              )
                return;
              void controller
                ?.saveRoster(preview.students, preview.enrollments, replace)
                .then((ok) => {
                  if (ok) {
                    setRows(null);
                    setMapping(null);
                  }
                });
            }}
          >
            Confirm {replace ? "replacement" : "import"}
          </button>
        </section>
      )}
      {edit && (
        <form
          className="inset"
          onSubmit={(e) => {
            e.preventDefault();
            const enrollments = sections
              .split(",")
              .map((s) => s.trim())
              .filter(Boolean)
              .map((classSection) => ({
                workspaceId: edit.workspaceId,
                studentId: edit.studentId,
                classSection,
              }));
            void controller
              ?.saveRoster([{ ...edit, updatedAtUtc: new Date().toISOString() }], enrollments)
              .then((ok) => ok && setEdit(null));
          }}
        >
          <h3>Student details</h3>
          <div className="form-grid">
            <label>
              Student ID
              <input
                required
                inputMode="numeric"
                pattern="[0-9]{1,16}"
                value={edit.studentId}
                disabled={editingId !== null}
                onChange={(e) => setEdit({ ...edit, studentId: e.target.value })}
              />
            </label>
            <label>
              First name
              <input
                value={edit.firstName}
                onChange={(e) => setEdit({ ...edit, firstName: e.target.value })}
              />
            </label>
            <label>
              Last name
              <input
                value={edit.lastName}
                onChange={(e) => setEdit({ ...edit, lastName: e.target.value })}
              />
            </label>
            <label>
              Grade
              <input
                value={edit.grade ?? ""}
                onChange={(e) => setEdit({ ...edit, grade: e.target.value || null })}
              />
            </label>
            <label>
              Sections (comma separated)
              <input value={sections} onChange={(e) => setSections(e.target.value)} />
            </label>
          </div>
          <div className="button-row">
            <button disabled={state.busy}>Save student</button>
            <button type="button" className="secondary" onClick={() => setEdit(null)}>
              Cancel
            </button>
          </div>
        </form>
      )}
      {(error || state.error) && (
        <p role="alert" className="error">
          {error || state.error}
        </p>
      )}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Student</th>
              <th>ID</th>
              <th>Grade</th>
              <th>Sections</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {state.students.map((s) => (
              <tr key={s.studentId}>
                <td>{fullName(s)}</td>
                <td>{s.studentId}</td>
                <td>{s.grade ?? "—"}</td>
                <td>
                  {state.enrollments
                    .filter((e) => e.studentId === s.studentId)
                    .map((e) => e.classSection)
                    .join(", ") || "—"}
                </td>
                <td>
                  <div className="button-row">
                    <button
                      className="icon-button"
                      aria-label={`Edit ${fullName(s)}`}
                      onClick={() => editStudent(s)}
                    >
                      <Pencil size={16} />
                    </button>
                    <button
                      className="icon-button"
                      aria-label={`Delete ${fullName(s)}`}
                      disabled={state.busy}
                      onClick={() => {
                        if (
                          window.confirm(
                            `Remove ${fullName(s)} from the local roster? Trip history remains.`,
                          )
                        )
                          void controller?.removeStudent(s.studentId);
                      }}
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {!state.students.length && (
          <div className="empty">Import a roster or add your first student.</div>
        )}
      </div>
    </Dialog>
  );
}
