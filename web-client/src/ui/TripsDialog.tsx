import { useMemo, useState } from "react";
import { Download, Search } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import {
  filterTrips,
  exportTrips,
  download,
  fullName,
  type TripFilter,
} from "../domain/TripReports";
import { durationLabel } from "../sync/TerminalClock";
export function TripsDialog({ onClose }: { onClose: () => void }) {
  const { state } = useHallzee();
  const [filter, setFilter] = useState<TripFilter>({
    search: "",
    from: "",
    to: "",
    status: "",
    section: "",
  });
  const [page, setPage] = useState(0);
  const rows = useMemo(
    () => filterTrips(state.trips, state.students, filter),
    [state.trips, state.students, filter],
  );
  const change = (field: keyof TripFilter, value: string) => {
    setFilter({ ...filter, [field]: value });
    setPage(0);
  };
  const current = Math.min(page, Math.max(0, Math.ceil(rows.length / 50) - 1));
  return (
    <Dialog title="Trip history" onClose={onClose}>
      <div className="toolbar">
        <label className="search-field">
          <Search size={17} />
          <input
            aria-label="Search trips"
            placeholder="Search name or student ID"
            value={filter.search}
            onChange={(e) => change("search", e.target.value)}
          />
        </label>
        <button
          className="secondary"
          onClick={() =>
            download(
              exportTrips(rows, state.students),
              "hallzee-trips.csv",
              "text/csv;charset=utf-8",
            )
          }
        >
          <Download size={17} />
          Export {rows.length} trips
        </button>
      </div>
      <div className="form-grid">
        <label>
          From date
          <input type="date" value={filter.from} onChange={(e) => change("from", e.target.value)} />
        </label>
        <label>
          To date
          <input type="date" value={filter.to} onChange={(e) => change("to", e.target.value)} />
        </label>
        <label>
          Status
          <select value={filter.status} onChange={(e) => change("status", e.target.value)}>
            <option value="">All statuses</option>
            <option value="COMPLETE">Complete</option>
            <option value="MANUAL">Teacher check-in</option>
            <option value="MANUAL_RESET">Reset</option>
          </select>
        </label>
        <label>
          Class section
          <select value={filter.section} onChange={(e) => change("section", e.target.value)}>
            <option value="">All sections</option>
            {[...new Set(state.trips.flatMap((t) => (t.classSection ? [t.classSection] : [])))]
              .sort()
              .map((s) => (
                <option key={s}>{s}</option>
              ))}
          </select>
        </label>
      </div>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Student</th>
              <th>Date</th>
              <th>Out / in</th>
              <th>Duration</th>
              <th>Status</th>
              <th>Section</th>
            </tr>
          </thead>
          <tbody>
            {rows.slice(current * 50, current * 50 + 50).map((t) => (
              <tr key={`${t.terminalId}:${t.tripId}`}>
                <td>
                  <strong>
                    {fullName(
                      state.students.find(
                        (s) =>
                          s.studentId === t.studentId && s.workspaceId === t.receivedWorkspaceId,
                      ),
                    ) || t.studentId}
                  </strong>
                  <small>
                    {t.studentId} · Trip {t.tripId}
                  </small>
                </td>
                <td>{t.tripDate || "Time unavailable"}</td>
                <td>
                  {t.timeOut || "—"} / {t.timeIn || "—"}
                </td>
                <td>{t.durationSeconds === null ? "—" : durationLabel(t.durationSeconds)}</td>
                <td>{t.status.replaceAll("_", " ")}</td>
                <td>{t.classSection || "Unassigned"}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {!rows.length && <div className="empty">No trips match these filters.</div>}
      </div>
      <div className="toolbar">
        <p className="muted">
          {rows.length} trips · Page {current + 1} of {Math.max(1, Math.ceil(rows.length / 50))}
        </p>
        <div className="button-row">
          <button
            className="secondary"
            disabled={current === 0}
            onClick={() => setPage(current - 1)}
          >
            Previous
          </button>
          <button
            className="secondary"
            disabled={(current + 1) * 50 >= rows.length}
            onClick={() => setPage(current + 1)}
          >
            Next
          </button>
        </div>
      </div>
      <p className="muted">
        Class sections are resolved when received. CSV is a report, not a backup; import ID columns
        as text in spreadsheets to preserve leading zeros.
      </p>
    </Dialog>
  );
}
