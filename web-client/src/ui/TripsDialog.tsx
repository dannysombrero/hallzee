import { useMemo, useState } from "react";
import { Download, Search, ChevronDown, ArrowUpDown } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import {
  exportTrips,
  download,
  fullName,
} from "../domain/TripReports";
import { durationLabel, localDate } from "../sync/TerminalClock";
import { StatusPill } from "./StatusPill";

type TimeframeOption = "all" | "today" | "week" | "2weeks" | "month";
type DurationOption = "all" | "under5" | "5to10" | "over10" | "overdue";
type StatusOption = "all" | "COMPLETE" | "MANUAL" | "MANUAL_RESET";
type SortField = "tripId" | "studentId" | "studentName" | "date" | "timeOut" | "timeIn" | "duration" | "status";

export function TripsDialog({ onClose }: { onClose: () => void }) {
  const { state } = useHallzee();
  const [search, setSearch] = useState("");
  const [timeframe, setTimeframe] = useState<TimeframeOption>("all");
  const [durationFilter, setDurationFilter] = useState<DurationOption>("all");
  const [statusFilter, setStatusFilter] = useState<StatusOption>("all");
  const [sortField, setSortField] = useState<SortField>("date");
  const [sortAsc, setSortAsc] = useState(false);
  const [page, setPage] = useState(0);
  const pageSize = 50;

  const now = useMemo(() => new Date(), []);
  const todayStr = localDate(now);

  const filteredTrips = useMemo(() => {
    const query = search.trim().toLowerCase();
    const warningSecs = state.policy.warningSeconds || 420;

    return state.trips.filter((t) => {
      // 1. Search filter
      if (query) {
        const student = state.students.find(
          (s) => s.studentId === t.studentId && s.workspaceId === t.receivedWorkspaceId,
        );
        const studentName = fullName(student).toLowerCase();
        const tripCode = `trip-${String(t.tripId).padStart(4, "0")}`.toLowerCase();
        const matchesQuery =
          t.studentId.toLowerCase().includes(query) ||
          studentName.includes(query) ||
          tripCode.includes(query) ||
          String(t.tripId).includes(query);
        if (!matchesQuery) return false;
      }

      // 2. Timeframe filter
      if (timeframe !== "all" && t.tripDate) {
        const tripDate = new Date(t.tripDate);
        const diffDays = (now.getTime() - tripDate.getTime()) / (1000 * 60 * 60 * 24);
        if (timeframe === "today" && t.tripDate !== todayStr) return false;
        if (timeframe === "week" && diffDays > 7) return false;
        if (timeframe === "2weeks" && diffDays > 14) return false;
        if (timeframe === "month" && diffDays > 30) return false;
      }

      // 3. Duration filter
      if (durationFilter !== "all") {
        const dur = t.durationSeconds ?? 0;
        if (durationFilter === "under5" && dur >= 300) return false;
        if (durationFilter === "5to10" && (dur < 300 || dur > 600)) return false;
        if (durationFilter === "over10" && dur <= 600) return false;
        if (durationFilter === "overdue" && dur <= warningSecs) return false;
      }

      // 4. Status filter
      if (statusFilter !== "all" && t.status !== statusFilter) {
        return false;
      }

      return true;
    });
  }, [state.trips, state.students, search, timeframe, durationFilter, statusFilter, state.policy.warningSeconds, now, todayStr]);

  const sortedTrips = useMemo(() => {
    return [...filteredTrips].sort((a, b) => {
      let cmp = 0;
      switch (sortField) {
        case "tripId":
          cmp = a.tripId - b.tripId;
          break;
        case "studentId":
          cmp = a.studentId.localeCompare(b.studentId, undefined, { numeric: true });
          break;
        case "studentName": {
          const sA = fullName(state.students.find((s) => s.studentId === a.studentId));
          const sB = fullName(state.students.find((s) => s.studentId === b.studentId));
          cmp = sA.localeCompare(sB);
          break;
        }
        case "date":
          cmp = (a.tripDate || "").localeCompare(b.tripDate || "");
          if (cmp === 0) cmp = (a.timeOut || "").localeCompare(b.timeOut || "");
          break;
        case "timeOut":
          cmp = (a.timeOut || "").localeCompare(b.timeOut || "");
          break;
        case "timeIn":
          cmp = (a.timeIn || "").localeCompare(b.timeIn || "");
          break;
        case "duration":
          cmp = (a.durationSeconds ?? 0) - (b.durationSeconds ?? 0);
          break;
        case "status":
          cmp = a.status.localeCompare(b.status);
          break;
      }
      return sortAsc ? cmp : -cmp;
    });
  }, [filteredTrips, sortField, sortAsc, state.students]);

  const totalPages = Math.max(1, Math.ceil(sortedTrips.length / pageSize));
  const currentPage = Math.min(page, totalPages - 1);
  const pagedRows = sortedTrips.slice(currentPage * pageSize, currentPage * pageSize + pageSize);

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortAsc(!sortAsc);
    } else {
      setSortField(field);
      setSortAsc(false);
    }
  };

  const onExport = () => {
    const exportData = exportTrips(sortedTrips, state.students);
    download(exportData, `hallzee-trips-${todayStr}.csv`, "text/csv;charset=utf-8");
  };

  return (
    <Dialog
      title="Hall Pass Trip History"
      subtitle="Search, filter, and inspect detailed pass usage."
      size="wide"
      onClose={onClose}
    >
      {/* 1. TOP ROW: WORKSPACE & EXPORT */}
      <div className="modal-top-row">
        <div className="modal-top-left">
          <span className="modal-workspace-label">Teacher workspace:</span>
          <div className="modal-workspace-pill">
            <span>{state.workspace?.name || "Test"}</span>
            <ChevronDown size={11} />
          </div>
          <span className="modal-count-badge">{sortedTrips.length} Records</span>
        </div>
        <button className="pill-outline" type="button" onClick={onExport}>
          <Download size={13} />
          Export
        </button>
      </div>

      {/* 2. SEARCH & FILTER TOOLBAR */}
      <div className="modal-filter-toolbar">
        <label className="modal-search-field">
          <Search size={15} />
          <input
            aria-label="Search trips"
            placeholder="Search student name, ID, or trip…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(0);
            }}
          />
        </label>

        <div className="modal-filter-select-wrapper">
          <select
            className="modal-filter-select"
            value={timeframe}
            aria-label="Filter timeframe"
            onChange={(e) => {
              setTimeframe(e.target.value as TimeframeOption);
              setPage(0);
            }}
          >
            <option value="all">All Time</option>
            <option value="today">Today</option>
            <option value="week">This Week</option>
            <option value="2weeks">Last 2 Weeks</option>
            <option value="month">This Month</option>
          </select>
          <ChevronDown size={12} />
        </div>

        <div className="modal-filter-select-wrapper">
          <select
            className="modal-filter-select"
            value={durationFilter}
            aria-label="Filter duration"
            onChange={(e) => {
              setDurationFilter(e.target.value as DurationOption);
              setPage(0);
            }}
          >
            <option value="all">All Durations</option>
            <option value="under5">&lt; 5m</option>
            <option value="5to10">5m–10m</option>
            <option value="over10">&gt; 10m</option>
            <option value="overdue">Overdue (&gt; 7m)</option>
          </select>
          <ChevronDown size={12} />
        </div>

        <div className="modal-filter-select-wrapper">
          <select
            className="modal-filter-select"
            value={statusFilter}
            aria-label="Filter status"
            onChange={(e) => {
              setStatusFilter(e.target.value as StatusOption);
              setPage(0);
            }}
          >
            <option value="all">All Statuses</option>
            <option value="COMPLETE">Complete</option>
            <option value="MANUAL">Teacher check-in</option>
            <option value="MANUAL_RESET">Reset</option>
          </select>
          <ChevronDown size={12} />
        </div>
      </div>

      {/* 3. TRIPS TABLE WITH UNIFIED DESKTOP STYLES */}
      <div className="hallzee-table-container">
        <div className="hallzee-table-scroll">
          <table className="hallzee-table">
            <thead>
              <tr>
                <th className="sortable" onClick={() => handleSort("tripId")}>
                  Trip ID <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("studentId")}>
                  Student ID <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("studentName")}>
                  Student Name <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("date")}>
                  Date <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("timeOut")}>
                  Departed <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("timeIn")}>
                  Returned <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("duration")}>
                  Duration <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
                <th className="sortable" onClick={() => handleSort("status")}>
                  Status <ArrowUpDown size={11} className="table-sort-icon" />
                </th>
              </tr>
            </thead>
            <tbody>
              {pagedRows.map((t) => {
                const student = state.students.find(
                  (s) => s.studentId === t.studentId && s.workspaceId === t.receivedWorkspaceId,
                );
                const sName = fullName(student);
                const tripCode = `TRIP-${String(t.tripId).padStart(4, "0")}`;

                return (
                  <tr key={`${t.terminalId}:${t.tripId}`}>
                    <td>
                      <strong>{tripCode}</strong>
                    </td>
                    <td>
                      <strong>#{t.studentId}</strong>
                    </td>
                    <td>
                      {sName ? (
                        <span>{sName}</span>
                      ) : (
                        <span className="muted italic">
                          Student ID #{t.studentId} (Unmapped)
                        </span>
                      )}
                    </td>
                    <td>{t.tripDate || "—"}</td>
                    <td>{t.timeOut || "—"}</td>
                    <td>{t.timeIn || "—"}</td>
                    <td>
                      <strong>
                        {t.durationSeconds === null ? "—" : durationLabel(t.durationSeconds)}
                      </strong>
                    </td>
                    <td>
                      <StatusPill
                        variant={
                          t.status === "COMPLETE"
                            ? "ready"
                            : t.status === "MANUAL"
                              ? "info"
                              : "neutral"
                        }
                      >
                        {t.status === "COMPLETE"
                          ? "COMPLETE"
                          : t.status === "MANUAL"
                            ? "TEACHER CHECK-IN"
                            : "RESET"}
                      </StatusPill>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {!sortedTrips.length && (
            <div className="hallzee-table-empty">
              <Search size={28} />
              <strong>No matching trips found</strong>
              <span>Adjust your search keywords or clear filters to view trips.</span>
            </div>
          )}
        </div>
      </div>

      {/* 4. PAGINATION FOOTER */}
      <div className="hallzee-table-footer">
        <span>
          Showing {sortedTrips.length === 0 ? 0 : currentPage * pageSize + 1} to{" "}
          {Math.min((currentPage + 1) * pageSize, sortedTrips.length)} of {sortedTrips.length}{" "}
          records
        </span>
        <div className="button-row">
          <button
            type="button"
            className="pill-filter"
            disabled={currentPage === 0}
            onClick={() => setPage(currentPage - 1)}
          >
            Previous
          </button>
          <span className="modal-page-info">
            Page {currentPage + 1} of {totalPages}
          </span>
          <button
            type="button"
            className="pill-filter"
            disabled={(currentPage + 1) * pageSize >= sortedTrips.length}
            onClick={() => setPage(currentPage + 1)}
          >
            Next
          </button>
        </div>
      </div>
    </Dialog>
  );
}

