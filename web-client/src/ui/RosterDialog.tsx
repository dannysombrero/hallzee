import { useState, useRef, useMemo, type FormEvent } from "react";
import { Upload, Plus, Trash2, Pencil, Users, ArrowUpDown } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { mapRoster, parseCsv, suggestMapping, type Mapping } from "../domain/RosterService";
import { fullName } from "../domain/TripReports";
import { errorText } from "../app/errors";
import type { Student } from "../storage/schema";

type SortField = "studentId" | "name" | "grade" | "period";

export function RosterDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const idInputRef = useRef<HTMLInputElement>(null);

  // Sorting state
  const [sortField, setSortField] = useState<SortField>("name");
  const [sortAsc, setSortAsc] = useState(true);

  // Quick-add & Edit state
  const [editingId, setEditingId] = useState<string | null>(null);
  const [newStudentId, setNewStudentId] = useState("");
  const [newFirstName, setNewFirstName] = useState("");
  const [newLastName, setNewLastName] = useState("");
  const [newGrade, setNewGrade] = useState("");
  const [newPeriod, setNewPeriod] = useState("");
  const [statusMessage, setStatusMessage] = useState("Ready");

  // CSV Import state
  const [rows, setRows] = useState<string[][] | null>(null);
  const [mapping, setMapping] = useState<Mapping | null>(null);
  const [replace, setReplace] = useState(false);
  const [error, setError] = useState("");

  const getStudentPeriod = (studentId: string) => {
    return state.enrollments
      .filter((e) => e.studentId === studentId)
      .map((e) => e.classSection)
      .join(", ");
  };

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortAsc(!sortAsc);
    } else {
      setSortField(field);
      setSortAsc(true);
    }
  };

  const sortedStudents = useMemo(() => {
    const list = [...state.students];
    list.sort((a, b) => {
      let cmp = 0;
      switch (sortField) {
        case "studentId":
          cmp = a.studentId.localeCompare(b.studentId, undefined, { numeric: true });
          break;
        case "name":
          cmp = fullName(a).localeCompare(fullName(b));
          break;
        case "grade":
          cmp = (a.grade || "").localeCompare(b.grade || "", undefined, { numeric: true });
          break;
        case "period":
          cmp = getStudentPeriod(a.studentId).localeCompare(getStudentPeriod(b.studentId));
          break;
      }
      return sortAsc ? cmp : -cmp;
    });
    return list;
  }, [state.students, state.enrollments, sortField, sortAsc]);

  const handleAddClick = () => {
    setEditingId(null);
    setNewStudentId("");
    setNewFirstName("");
    setNewLastName("");
    setNewGrade("");
    setNewPeriod("");
    setError("");
    idInputRef.current?.focus();
  };

  const startEdit = (student: Student) => {
    setEditingId(student.studentId);
    setNewStudentId(student.studentId);
    setNewFirstName(student.firstName);
    setNewLastName(student.lastName);
    setNewGrade(student.grade ?? "");
    setNewPeriod(getStudentPeriod(student.studentId));
    setError("");
  };

  const cancelEdit = () => {
    setEditingId(null);
    setNewStudentId("");
    setNewFirstName("");
    setNewLastName("");
    setNewGrade("");
    setNewPeriod("");
  };

  const handleSaveStudent = (e: FormEvent) => {
    e.preventDefault();
    if (!newStudentId.trim()) return;

    const now = new Date().toISOString();
    const existing = editingId ? state.students.find((s) => s.studentId === editingId) : null;
    const student: Student = {
      workspaceId: state.workspace!.workspaceId,
      studentId: newStudentId.trim(),
      firstName: newFirstName.trim(),
      lastName: newLastName.trim(),
      grade: newGrade.trim() || null,
      createdAtUtc: existing?.createdAtUtc ?? now,
      updatedAtUtc: now,
    };

    const enrollments = newPeriod
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean)
      .map((classSection) => ({
        workspaceId: state.workspace!.workspaceId,
        studentId: student.studentId,
        classSection,
      }));

    void controller?.saveRoster([student], enrollments).then((ok) => {
      if (ok) {
        cancelEdit();
        setStatusMessage(editingId ? "Student updated" : "Student added");
      }
    });
  };

  const confirmDelete = (student: Student) => {
    if (
      window.confirm(
        `Remove ${fullName(student)} from the local roster? Trip history remains.`,
      )
    ) {
      void controller?.removeStudent(student.studentId).then(() => {
        setStatusMessage("Student removed");
      });
    }
  };

  // CSV Import handlers
  async function handleFileSelect(file?: File) {
    if (!file) return;
    setError("");
    try {
      if (file.size > 5 * 1024 * 1024) throw new Error("File exceeds 5 MiB.");
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
    /* mapping incomplete or error handled below */
  }

  const handleConfirmImport = () => {
    if (!preview) return;
    if (
      !window.confirm(
        `${replace ? "Replace" : "Merge"} the local roster with ${preview.students.length} students?`,
      )
    ) {
      return;
    }
    void controller
      ?.saveRoster(preview.students, preview.enrollments, replace)
      .then((ok) => {
        if (ok) {
          setRows(null);
          setMapping(null);
          setStatusMessage(`Successfully imported ${preview?.students.length} students`);
        }
      });
  };

  return (
    <Dialog
      title="Classroom Roster & Students"
      subtitle="Saved privately on this computer and kept when Hallzee is updated. Workspace exports do not include students."
      size="wide"
      onClose={onClose}
    >
      {/* 1. TOP BAR: WORKSPACE DROPDOWN, TOTAL BADGE, AND CSV IMPORT BUTTON */}
      <div className="modal-top-row">
        <div className="modal-top-left">
          <span className="modal-top-label">Teacher workspace:</span>
          <div className="modal-filter-select-wrapper">
            <select
              className="modal-filter-select"
              value={state.workspace?.workspaceId ?? ""}
              aria-label="Teacher workspace"
              disabled
            >
              <option value={state.workspace?.workspaceId ?? ""}>
                {state.workspace?.name ?? "Default"}
              </option>
            </select>
          </div>
          <div className="enrolled-badge">{state.students.length} Enrolled</div>
        </div>

        <div className="modal-top-right">
          <button
            type="button"
            className="hallzee-outline-btn"
            onClick={handleAddClick}
          >
            <Plus size={15} />
            Add student
          </button>
          <button
            type="button"
            className="hallzee-pill-btn"
            onClick={() => fileInputRef.current?.click()}
            disabled={state.busy}
          >
            <Upload size={14} />
            Import Roster (CSV)
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".csv,text/csv"
            hidden
            onChange={(e) => void handleFileSelect(e.target.files?.[0])}
          />
        </div>
      </div>

      {/* 2. VIEW SWITCHER: NORMAL LIST VS 2-PHASE CSV IMPORT PREVIEW */}
      {!rows || !mapping ? (
        <>
          {/* QUICK-ADD STUDENT INLINE SECTION */}
          <form className="quick-add-student-section" onSubmit={handleSaveStudent}>
            <div className="quick-add-student-title">
              {editingId ? "Edit student · ID cannot be changed" : "Add a student · ID and name required"}
            </div>
            <div className="quick-add-student-grid">
              <input
                ref={idInputRef}
                aria-label="Student ID"
                placeholder="Student ID"
                value={newStudentId}
                onChange={(e) => setNewStudentId(e.target.value)}
                required
                inputMode="numeric"
                pattern="[0-9]{1,16}"
                disabled={editingId !== null}
                className="pill-input"
              />
              <input
                aria-label="First name"
                placeholder="First name"
                value={newFirstName}
                onChange={(e) => setNewFirstName(e.target.value)}
                className="pill-input"
              />
              <input
                aria-label="Last name"
                placeholder="Last name"
                value={newLastName}
                onChange={(e) => setNewLastName(e.target.value)}
                className="pill-input"
              />
              <input
                aria-label="Grade"
                placeholder="Grade"
                value={newGrade}
                onChange={(e) => setNewGrade(e.target.value)}
                className="pill-input"
              />
              <input
                aria-label="Class / period"
                placeholder="Class / period"
                value={newPeriod}
                onChange={(e) => setNewPeriod(e.target.value)}
                className="pill-input"
              />
              <button
                type="submit"
                className="hallzee-pill-btn"
                aria-label="Save student"
                disabled={state.busy}
              >
                {editingId ? "Save student" : "Add Student"}
              </button>
              {editingId && (
                <button
                  type="button"
                  className="hallzee-outline-btn"
                  onClick={cancelEdit}
                >
                  Cancel
                </button>
              )}
            </div>
          </form>

          {(error || state.error) && (
            <p role="alert" className="error">
              {error || state.error}
            </p>
          )}

          {/* STUDENTS TABLE */}
          <div className="hallzee-table-container">
            <div className="hallzee-table-scroll">
              <table className="hallzee-table">
                <thead>
                  <tr>
                    <th className="sortable" onClick={() => handleSort("studentId")}>
                      Student ID <ArrowUpDown size={11} className="table-sort-icon" />
                    </th>
                    <th className="sortable" onClick={() => handleSort("name")}>
                      Student Name <ArrowUpDown size={11} className="table-sort-icon" />
                    </th>
                    <th className="sortable" onClick={() => handleSort("grade")}>
                      Grade <ArrowUpDown size={11} className="table-sort-icon" />
                    </th>
                    <th className="sortable" onClick={() => handleSort("period")}>
                      Period <ArrowUpDown size={11} className="table-sort-icon" />
                    </th>
                    <th className="table-th-actions">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {sortedStudents.map((s) => (
                    <tr key={s.studentId}>
                      <td>
                        <strong>#{s.studentId}</strong>
                      </td>
                      <td>{fullName(s)}</td>
                      <td>{s.grade ?? "—"}</td>
                      <td>{getStudentPeriod(s.studentId) || "—"}</td>
                      <td>
                        <div className="table-actions-cell">
                          <button
                            type="button"
                            className="btn-action-edit"
                            aria-label={`Edit ${fullName(s)}`}
                            title={`Edit ${fullName(s)}`}
                            onClick={() => startEdit(s)}
                          >
                            <Pencil size={15} />
                          </button>
                          <button
                            type="button"
                            className="btn-action-delete"
                            aria-label={`Delete ${fullName(s)}`}
                            title={`Remove ${fullName(s)} from roster`}
                            disabled={state.busy}
                            onClick={() => confirmDelete(s)}
                          >
                            <Trash2 size={15} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {!state.students.length && (
                <div className="hallzee-table-empty">
                  <Users size={36} strokeWidth={1.8} color="#cbd5e1" />
                  <strong>No students enrolled in this class yet</strong>
                  <span>Add a student above or import your roster from a CSV file.</span>
                </div>
              )}
            </div>

            <div className="hallzee-table-footer">
              <span>{statusMessage}</span>
            </div>
          </div>
        </>
      ) : (
        /* 2-PHASE CSV IMPORT & COLUMN MAPPING VERIFICATION VIEW */
        <div>
          <div className="csv-step2-banner">
            Step 2: Confirm Column Mappings below based on sample rows from your CSV.
          </div>

          <div className="csv-mapping-grid">
            <label className="csv-mapping-field">
              <span>Student ID Column *</span>
              <select
                className="modal-filter-select"
                value={mapping.studentId}
                onChange={(e) => setMapping({ ...mapping, studentId: Number(e.target.value) })}
              >
                <option value={-1}>Not mapped</option>
                {rows[0].map((h, i) => (
                  <option value={i} key={i}>
                    {h}
                  </option>
                ))}
              </select>
            </label>

            <label className="csv-mapping-field">
              <span>First Name Column</span>
              <select
                className="modal-filter-select"
                value={mapping.firstName}
                onChange={(e) => setMapping({ ...mapping, firstName: Number(e.target.value) })}
              >
                <option value={-1}>Not mapped</option>
                {rows[0].map((h, i) => (
                  <option value={i} key={i}>
                    {h}
                  </option>
                ))}
              </select>
            </label>

            <label className="csv-mapping-field">
              <span>Last Name Column</span>
              <select
                className="modal-filter-select"
                value={mapping.lastName}
                onChange={(e) => setMapping({ ...mapping, lastName: Number(e.target.value) })}
              >
                <option value={-1}>Not mapped</option>
                {rows[0].map((h, i) => (
                  <option value={i} key={i}>
                    {h}
                  </option>
                ))}
              </select>
            </label>

            <label className="csv-mapping-field">
              <span>Combined Full Name</span>
              <select
                className="modal-filter-select"
                value={mapping.fullName}
                onChange={(e) => setMapping({ ...mapping, fullName: Number(e.target.value) })}
              >
                <option value={-1}>Not mapped</option>
                {rows[0].map((h, i) => (
                  <option value={i} key={i}>
                    {h}
                  </option>
                ))}
              </select>
            </label>

            <label className="csv-mapping-field">
              <span>Grade Column</span>
              <select
                className="modal-filter-select"
                value={mapping.grade}
                onChange={(e) => setMapping({ ...mapping, grade: Number(e.target.value) })}
              >
                <option value={-1}>Not mapped</option>
                {rows[0].map((h, i) => (
                  <option value={i} key={i}>
                    {h}
                  </option>
                ))}
              </select>
            </label>

            <label className="csv-mapping-field">
              <span>Period / Section</span>
              <select
                className="modal-filter-select"
                value={mapping.classSection}
                onChange={(e) => setMapping({ ...mapping, classSection: Number(e.target.value) })}
              >
                <option value={-1}>Not mapped</option>
                {rows[0].map((h, i) => (
                  <option value={i} key={i}>
                    {h}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="csv-sample-container">
            <div className="csv-sample-title">CSV File Sample Rows:</div>
            {rows.slice(1, 5).map((row, idx) => (
              <div key={idx} className="csv-sample-row">
                {row.map((cell, cIdx) => (
                  <span key={cIdx}>{cell || "—"}</span>
                ))}
              </div>
            ))}
          </div>

          {preview?.errors.map((err, i) => (
            <p key={i} className="error">
              {err}
            </p>
          ))}

          <label className="csv-checkbox-label">
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

          <div className="csv-actions-row">
            <span className="status-text">{preview?.students.length ?? 0} valid students</span>
            <div className="csv-actions-buttons">
              <button
                type="button"
                className="hallzee-outline-btn"
                onClick={() => {
                  setRows(null);
                  setMapping(null);
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                className="hallzee-pill-btn"
                disabled={state.busy || !preview?.students.length || !!preview.errors.length}
                onClick={handleConfirmImport}
              >
                Confirm {replace ? "replacement" : "import"}
              </button>
            </div>
          </div>
        </div>
      )}
    </Dialog>
  );
}
