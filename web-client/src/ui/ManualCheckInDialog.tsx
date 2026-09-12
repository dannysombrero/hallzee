import { useState, useMemo, type FormEvent } from "react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { UserCheck } from "lucide-react";
import { resolvePeriod } from "../domain/PolicyScheduleService";
import type { Student } from "../storage/schema";

export interface ManualPassDetails {
  studentId: string;
  studentName: string;
  period?: string;
  destination?: string;
  purpose?: string;
}

export function ManualCheckInDialog({
  onClose,
  onStartPass,
}: {
  onClose: () => void;
  onStartPass: (pass: ManualPassDetails) => void;
}) {
  const { state } = useHallzee();

  // Prepopulate current period if bell schedule resolves one
  const currentResolved = useMemo(() => {
    return resolvePeriod(new Date(), state.periods, state.exceptions);
  }, [state.periods, state.exceptions]);

  const [studentName, setStudentName] = useState("");
  const [studentId, setStudentId] = useState("");
  const [period, setPeriod] = useState(currentResolved?.classSection ?? "");
  const [destination, setDestination] = useState("");
  const [purpose, setPurpose] = useState("");
  const [statusMessage, setStatusMessage] = useState("");
  const [showSuggestions, setShowSuggestions] = useState(false);

  // Student suggestions based on name query
  const filteredSuggestions = useMemo(() => {
    const q = studentName.trim().toLowerCase();
    if (!q) return [];
    return state.students
      .filter((s) => `${s.firstName} ${s.lastName}`.toLowerCase().includes(q))
      .slice(0, 5);
  }, [studentName, state.students]);

  // Handle student name change
  const handleNameChange = (val: string) => {
    setStudentName(val);
    setShowSuggestions(true);
    setStatusMessage("");

    // If ID is empty, try exact name match
    if (!studentId.trim()) {
      const match = state.students.find(
        (s) => `${s.firstName} ${s.lastName}`.trim().toLowerCase() === val.trim().toLowerCase(),
      );
      if (match) {
        setStudentId(match.studentId);
      }
    }
  };

  // Handle student ID change
  const handleIdChange = (val: string) => {
    setStudentId(val);
    setStatusMessage("");

    // If name is empty, try exact ID match
    if (!studentName.trim()) {
      const match = state.students.find((s) => s.studentId === val.trim());
      if (match) {
        setStudentName(`${match.firstName} ${match.lastName}`.trim());
      }
    }
  };

  const handleSelectStudent = (student: Student) => {
    setStudentName(`${student.firstName} ${student.lastName}`.trim());
    setStudentId(student.studentId);
    setShowSuggestions(false);
  };

  const canSubmit = studentName.trim().length > 0 || studentId.trim().length > 0;

  const handleSubmit = (e?: FormEvent) => {
    if (e) e.preventDefault();
    if (!canSubmit) {
      setStatusMessage("Enter a student name or student ID to continue.");
      return;
    }

    // Resolve pass details matching desktop ResolvePassDetails()
    let resolvedId = studentId.trim();
    let resolvedName = studentName.trim();

    if (resolvedId && resolvedName) {
      // Both provided
    } else if (resolvedId) {
      const match = state.students.find((s) => s.studentId === resolvedId);
      resolvedName = match ? `${match.firstName} ${match.lastName}`.trim() : `#${resolvedId}`;
    } else if (resolvedName) {
      const match = state.students.find(
        (s) =>
          `${s.firstName} ${s.lastName}`.trim().toLowerCase() === resolvedName.toLowerCase(),
      );
      resolvedId = match ? match.studentId : `M-${Date.now().toString().slice(-4)}`;
    }

    onStartPass({
      studentId: resolvedId,
      studentName: resolvedName,
      period: period.trim() || undefined,
      destination: destination.trim() || undefined,
      purpose: purpose.trim() || undefined,
    });
    onClose();
  };

  return (
    <Dialog
      title="Teacher-Started Pass"
      subtitle="This pass is saved on this computer and resumes after a restart. It does not reserve a terminal slot."
      size="default"
      onClose={onClose}
    >
      <form onSubmit={handleSubmit} className="manual-pass-form">
        {/* STUDENT NAME */}
        <div className="manual-pass-field relative">
          <label htmlFor="manual-student-name">Student Name</label>
          <input
            id="manual-student-name"
            placeholder="e.g. Avery Chen (optional if ID is provided)"
            value={studentName}
            onChange={(e) => handleNameChange(e.target.value)}
            onFocus={() => setShowSuggestions(true)}
            autoComplete="off"
          />
          {showSuggestions && filteredSuggestions.length > 0 && (
            <div className="manual-pass-suggestions">
              {filteredSuggestions.map((s) => (
                <div
                  key={s.studentId}
                  className="manual-pass-suggestion-item"
                  onClick={() => handleSelectStudent(s)}
                >
                  <span className="font-semibold text-slate-800">
                    {s.firstName} {s.lastName}
                  </span>
                  <span className="text-11 font-mono text-slate-400">#{s.studentId}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* STUDENT ID */}
        <div className="manual-pass-field">
          <label htmlFor="manual-student-id">Student ID</label>
          <input
            id="manual-student-id"
            placeholder="e.g. 10482 (optional if Name is provided)"
            value={studentId}
            onChange={(e) => handleIdChange(e.target.value)}
            autoComplete="off"
          />
        </div>

        {/* PERIOD (OPTIONAL) */}
        <div className="manual-pass-field">
          <label htmlFor="manual-period">Period (optional)</label>
          <input
            id="manual-period"
            placeholder="e.g. Period 3"
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
          />
        </div>

        {/* DESTINATION (OPTIONAL) */}
        <div className="manual-pass-field">
          <label htmlFor="manual-destination">Destination (optional)</label>
          <input
            id="manual-destination"
            placeholder="e.g. Hallway Restroom, Clinic, Room 204"
            value={destination}
            onChange={(e) => setDestination(e.target.value)}
          />
        </div>

        {/* PURPOSE (OPTIONAL) */}
        <div className="manual-pass-field">
          <label htmlFor="manual-purpose">Purpose (optional)</label>
          <input
            id="manual-purpose"
            placeholder="e.g. restroom, nurse, office"
            value={purpose}
            onChange={(e) => setPurpose(e.target.value)}
          />
        </div>

        {/* HELPER HINT */}
        <div className="manual-pass-hint">
          Student Name or Student ID is required. Period, destination, and purpose are optional.
          Submitting starts the live timer.
        </div>

        {/* FOOTER ACTIONS */}
        <div className="settings-footer pt-2">
          <span className="text-12 font-semibold text-rose-600 flex-1">{statusMessage}</span>
          <button type="button" className="hallzee-outline-btn" onClick={onClose}>
            Cancel
          </button>
          <button
            type="submit"
            className="hallzee-pill-btn"
            disabled={!canSubmit || state.busy}
          >
            <UserCheck size={14} className="inline mr-1.5" />
            Start Pass
          </button>
        </div>
      </form>
    </Dialog>
  );
}
