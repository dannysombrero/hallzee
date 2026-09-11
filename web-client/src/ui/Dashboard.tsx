import { useEffect, useMemo, useState } from "react";
import {
  Bluetooth,
  RefreshCw,
  Users,
  History,
  Settings,
  Clock3,
  ShieldCheck,
  Monitor,
  LogOut,
  Download,
  Search,
  ChevronRight,
  UserCheck,
  UserX,
  Radio,
  Plus,
  Check,
  Calendar,
  ChevronDown,
} from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { elapsed, durationLabel, localDate } from "../sync/TerminalClock";
import { fullName, filterTrips, exportTrips, download } from "../domain/TripReports";
import { resolvePeriod, evaluateWindow } from "../domain/PolicyScheduleService";
import { ConnectionDialog } from "./ConnectionDialog";
import { TripsDialog } from "./TripsDialog";
import { RosterDialog } from "./RosterDialog";
import { PoliciesDialog } from "./PoliciesDialog";
import { TerminalSettingsDialog } from "./TerminalSettingsDialog";
import { DataSettingsDialog } from "./DataSettingsDialog";
import { ProjectionView, type ProjectionProps } from "./ProjectionView";
import { DocumentPictureInPictureButton } from "./DocumentPictureInPictureButton";
import { UpdateCoordinator, type OfflineStatus } from "../pwa/updateCoordinator";
import { StatusPill } from "./StatusPill";

type Modal = "connect" | "trips" | "roster" | "policies" | "terminal" | "data" | null;

export function Dashboard() {
  const { controller, state } = useHallzee();
  const [modal, setModal] = useState<Modal>(null);
  const [now, setNow] = useState(new Date());
  const [projecting, setProjecting] = useState(false);
  const [offline, setOffline] = useState<OfflineStatus>({
    ready: false,
    update: false,
    error: null,
  });
  const [updater] = useState(() => new UpdateCoordinator());

  // Exceeded Time threshold and search state
  const defaultWarningMins = Math.round(state.policy.warningSeconds / 60) || 7;
  const [thresholdMinutes, setThresholdMinutes] = useState(defaultWarningMins);
  const [searchExceeded, setSearchExceeded] = useState("");

  useEffect(() => {
    const tick = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(tick);
  }, []);

  useEffect(() => {
    const dispose = updater.changed.subscribe(setOffline);
    void updater.start();
    return () => {
      dispose();
      updater.dispose();
    };
  }, [updater]);

  const connected = state.session === "Authenticated";
  const period = resolvePeriod(now, state.periods, state.exceptions);
  const windowDecision = evaluateWindow(now, period, state.policy);
  const projection: ProjectionProps = {
    fresh: state.active.fresh,
    occupiedCount: state.active.passes.length,
    capacity: state.policy.capacity,
    elapsedSeconds: state.active.passes.map((p) => elapsed(p.epoch, now)),
    periodLabel: period?.periodName ?? "Between periods",
    windowDecision,
  };

  const recent = useMemo(
    () =>
      filterTrips(state.trips, state.students, {
        search: "",
        from: "",
        to: "",
        status: "",
        section: "",
      }).slice(0, 6),
    [state.trips, state.students],
  );

  const todayTrips = useMemo(
    () =>
      state.trips.filter(
        (t) => t.tripDate === localDate(now) && t.status !== "MANUAL_RESET",
      ),
    [state.trips, now],
  );

  const dailyCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const t of todayTrips) counts.set(t.studentId, (counts.get(t.studentId) ?? 0) + 1);
    return counts;
  }, [todayTrips]);

  const dailyExceeded = useMemo(
    () =>
      state.policy.dailyGuideline > 0
        ? [...dailyCounts].filter(([, n]) => n > state.policy.dailyGuideline).length
        : 0,
    [dailyCounts, state.policy.dailyGuideline],
  );

  // Exceeded time filtering and grouping
  const thresholdSeconds = thresholdMinutes * 60;
  const exceededTrips = useMemo(
    () =>
      todayTrips.filter(
        (t) => t.durationSeconds !== null && t.durationSeconds > thresholdSeconds,
      ),
    [todayTrips, thresholdSeconds],
  );

  interface ExceededGroup {
    studentId: string;
    displayName: string;
    trips: typeof exceededTrips;
    maxDuration: number;
  }

  const exceededStudents = useMemo(() => {
    const map = new Map<string, ExceededGroup>();
    for (const trip of exceededTrips) {
      let entry = map.get(trip.studentId);
      if (!entry) {
        const student = state.students.find((s) => s.studentId === trip.studentId);
        entry = {
          studentId: trip.studentId,
          displayName: fullName(student) || trip.studentId,
          trips: [],
          maxDuration: 0,
        };
        map.set(trip.studentId, entry);
      }
      entry.trips.push(trip);
      if ((trip.durationSeconds ?? 0) > entry.maxDuration) {
        entry.maxDuration = trip.durationSeconds ?? 0;
      }
    }
    let list = Array.from(map.values());
    if (searchExceeded.trim()) {
      const q = searchExceeded.toLowerCase();
      list = list.filter(
        (item) =>
          item.displayName.toLowerCase().includes(q) ||
          item.studentId.toLowerCase().includes(q),
      );
    }
    return list.sort((a, b) => b.trips.length - a.trips.length);
  }, [exceededTrips, state.students, searchExceeded]);

  const onExportRecent = () => {
    const exportData = exportTrips(todayTrips.length ? todayTrips : state.trips, state.students);
    download(exportData, `hallzee-trips-${localDate(now)}.csv`, "text/csv;charset=utf-8");
  };

  const close = () => setModal(null);

  if (projecting) {
    return (
      <main className="projection-page">
        <ProjectionView {...projection} />
        <div className="button-row">
          <button
            className="secondary"
            onClick={() => void document.documentElement.requestFullscreen?.().catch(() => {})}
          >
            Full screen
          </button>
          <button
            onClick={() => {
              setProjecting(false);
              if (document.fullscreenElement) void document.exitFullscreen();
            }}
          >
            Exit projection
          </button>
        </div>
      </main>
    );
  }

  return (
    <div className="app-shell">
      {/* 1. TOP WINDOW HEADER BAR */}
      <header className="top-window-bar">
        <div className="top-window-left">
          <a className="top-window-brand" href="/" aria-label="Hallzee home">
            <img src="/icons/hallzee-logo.png" alt="" />
            <span>Hallzee Desktop Client</span>
          </a>
          <span className="top-window-divider">|</span>
          <span className="top-window-location">
            {state.workspace?.room
              ? `Room ${state.workspace.room}${state.workspace.teacher ? ` – ${state.workspace.teacher}` : ""}${state.workspace.name ? ` – ${state.workspace.name}` : ""}`
              : state.workspace?.name || "Your classroom"}
          </span>
        </div>

        <div className="top-window-right">
          <DocumentPictureInPictureButton
            projection={projection}
            fallback={() => setProjecting(true)}
          />
          <span className="top-window-workspace-label">Teacher workspace</span>
          <div className="top-window-workspace-pill">
            <span>{state.workspace?.name || "Test"}</span>
            <ChevronDown size={11} />
          </div>
          <span className="top-window-version">v1.0.0</span>
          <span className="offline-label">
            {offline.ready
              ? "Ready offline"
              : import.meta.env.DEV
                ? "Development build"
                : "Preparing offline use"}
          </span>
          <span className="top-window-avatar" title={state.workspace?.teacher || "Teacher"}>
            {state.workspace?.teacher.slice(0, 1).toUpperCase() || "H"}
          </span>
        </div>
      </header>

      {/* 2. APP BODY (SIDEBAR + MAIN CONTENT) */}
      <div className="app-body">
        {/* LEFT SIDEBAR */}
        <aside className="sidebar">
          {/* Logo & Brand Box */}
          <div className="sidebar-brand-group">
            <div className="sidebar-logo-box">
              <img src="/icons/hallzee-logo.png" alt="Hallzee" />
            </div>
            <div className="sidebar-brand-text">
              <span className="sidebar-brand-title">Hallzee</span>
              <span className="sidebar-brand-subtitle">Classroom hall passes</span>
            </div>
          </div>

          {/* Classroom Profile Inset Box */}
          <div className="classroom-profile-box">
            <span className="classroom-profile-eyebrow">CLASSROOM PROFILE</span>
            <h2 className="classroom-profile-name">
              {state.workspace?.name || "Test"}
            </h2>
            <span className="classroom-profile-detail">
              {period
                ? `${period.periodName}${period.start && period.end ? ` (${period.start}–${period.end})` : ""}${state.workspace?.name ? ` • ${state.workspace.name}` : ""}`
                : state.workspace?.room
                  ? `Room ${state.workspace.room}`
                  : "No room configured"}
            </span>
            {state.workspace?.teacher && (
              <span className="classroom-profile-teacher">Teacher: {state.workspace.teacher}</span>
            )}
          </div>

          {/* Navigation Menu */}
          <nav aria-label="Classroom navigation">
            <button className="sidebar-nav-btn nav-active">
              <Monitor size={19} />
              <span>Dashboard</span>
            </button>
            <button
              className="sidebar-nav-btn"
              onClick={() => setModal("trips")}
              disabled={!state.ready}
            >
              <History size={19} />
              <span>Trip History Log</span>
            </button>
            <button
              className="sidebar-nav-btn"
              onClick={() => setModal("roster")}
              disabled={!state.ready}
            >
              <Users size={19} />
              <span>Student Roster</span>
              {state.students.length > 0 && (
                <span className="sidebar-nav-badge">{state.students.length}</span>
              )}
            </button>
            <button
              className="sidebar-nav-btn"
              onClick={() => setModal("policies")}
              disabled={!state.ready}
            >
              <Calendar size={19} />
              <span>Policies & Bell Times</span>
            </button>
            <button
              className="sidebar-nav-btn"
              disabled={!state.ready}
              onClick={() => setModal("data")}
              aria-label="Classroom & data"
            >
              <Settings size={19} />
              <span>Settings</span>
            </button>
          </nav>

          {/* Sidebar Quick Utility Buttons */}
          <div className="sidebar-quick-actions">
            <button
              className="sidebar-quick-btn"
              onClick={() => window.open("https://github.com/dannysombrero/hallzee/wiki", "_blank")}
            >
              Help
            </button>
            <button className="sidebar-quick-btn" onClick={() => void updater.check()}>
              Updates
            </button>
          </div>

          {/* Bottom Terminal Node Box */}
          <div className="terminal-node-box">
            <div className="terminal-node-header">
              <span className="terminal-node-eyebrow">TERMINAL NODE</span>
              <StatusPill variant={connected ? "ready" : "neutral"}>
                {connected ? "ONLINE" : "OFFLINE"}
              </StatusPill>
            </div>
            <span className="terminal-node-caption">Terminal clock is set when you sync</span>
            <div className="terminal-node-device-card">
              <div className="terminal-node-device-title">
                <Radio size={13} color="#0284c7" />
                <span>
                  {state.terminal?.customName
                    ? `Last paired: ${state.terminal.customName}`
                    : "No terminal paired"}
                </span>
              </div>
              <span className="terminal-node-device-sub">
                {connected
                  ? "Standby • Connected live"
                  : state.terminal
                    ? "Standby • Ready to reconnect"
                    : "Pair once to sync"}
              </span>
            </div>
            <div className="terminal-node-actions">
              {connected ? (
                <>
                  <button
                    className="secondary compact"
                    disabled={!state.ready || state.busy}
                    onClick={() => setModal("terminal")}
                  >
                    <Settings size={13} />
                    Terminal settings
                  </button>
                  <button
                    className="secondary compact"
                    disabled={!state.ready || state.busy}
                    onClick={() => void controller?.syncNow()}
                  >
                    <RefreshCw size={13} className={state.syncing ? "spin" : ""} />
                    Sync now
                  </button>
                </>
              ) : (
                <button
                  className="secondary compact"
                  disabled={!state.ready || state.busy}
                  onClick={() => setModal("connect")}
                >
                  <Bluetooth size={13} />
                  Pair terminal
                </button>
              )}
            </div>
          </div>
        </aside>

        {/* MAIN COLUMN */}
        <div className="main-column">
          <main className="workspace">
            {/* Banners */}
            {state.locked && (
              <div className="banner warning" role="alert">
                <strong>Hallzee is already open in another window.</strong>
                <p>Close the other tab or installed app, then retry here.</p>
                <button onClick={() => location.reload()}>Retry</button>
              </div>
            )}
            {state.error && (
              <div className="banner warning" role="alert">
                <p>{state.error}</p>
                <button className="secondary" onClick={() => controller?.clearError()}>
                  Dismiss
                </button>
              </div>
            )}
            {!state.ready && !state.locked && !state.error && (
              <div className="banner">Opening this browser’s classroom…</div>
            )}
            {state.clockNotice && (
              <div className="banner warning">
                The computer clock or time zone changed. Verify classroom time before relying on timers.
              </div>
            )}
            {offline.update && (
              <div className="banner">
                <strong>Update ready.</strong>
                <span>Apply when your work is saved and other Hallzee windows are closed.</span>
                <button
                  disabled={state.busy}
                  onClick={() => {
                    if (
                      window.confirm(
                        "Disconnect and apply the downloaded update? Saved classroom data will remain.",
                      )
                    ) {
                      void controller?.prepareUpdate().then((ok) => {
                        if (ok) updater.apply();
                      });
                    }
                  }}
                >
                  Apply update
                </button>
              </div>
            )}
            {offline.error && <p className="error">{offline.error}</p>}

            {/* HERO ACTIVE PASS CARD */}
            {!state.active.fresh ? (
              /* State C: Status Unknown / Waiting for Terminal */
              <section className="hero-pass-card unknown">
                <div className="hero-pass-left">
                  <div className="hero-avatar unknown">
                    <UserCheck size={28} />
                  </div>
                  <div className="hero-pass-details">
                    <StatusPill variant="neutral">STATUS UNKNOWN</StatusPill>
                    <h3>Pass Status Unknown</h3>
                    <p className="hero-pass-subtitle">
                      Connect to the Hallzee terminal to confirm whether a student is out.
                    </p>
                  </div>
                </div>
                <div className="hero-floating-card">
                  <div className="hero-floating-info">
                    <span className="hero-floating-label muted">TERMINAL STATUS</span>
                    <span className="hero-floating-val">
                      {connected ? "AUTHENTICATED" : "OFFLINE"}
                    </span>
                    <span className="hero-floating-sub">
                      {connected ? "Receiving live activity" : "Sync to check pass status"}
                    </span>
                  </div>
                  {connected ? (
                    <button
                      className="hallzee-pill-btn"
                      disabled={!state.ready || state.busy}
                      onClick={() => void controller?.syncNow()}
                    >
                      <RefreshCw size={14} className={state.syncing ? "spin" : ""} />
                      Sync now
                    </button>
                  ) : (
                    <button
                      className="hallzee-pill-btn"
                      aria-label="Connect terminal"
                      disabled={!state.ready || state.busy}
                      onClick={() => setModal("connect")}
                    >
                      <Search size={14} />
                      Connect
                    </button>
                  )}
                </div>
              </section>
            ) : !state.active.passes.length ? (
              /* State A: Pass Available */
              <section className="hero-pass-card available">
                <div className="hero-pass-left">
                  <div className="hero-avatar available">
                    <ShieldCheck size={28} />
                  </div>
                  <div className="hero-pass-details">
                    <StatusPill variant="ready">PASS READY</StatusPill>
                    <h3>Pass available</h3>
                    <p className="hero-pass-subtitle">
                      {state.policy.capacity} {state.policy.capacity === 1 ? "pass" : "passes"}{" "}
                      configured for this classroom · Ready for next student
                    </p>
                  </div>
                </div>
                <div className="hero-floating-card">
                  <div className="hero-floating-info">
                    <span className="hero-floating-label green">PASS READY</span>
                    <span className="hero-floating-val">Available</span>
                    <span className="hero-floating-sub">Ready for next student</span>
                  </div>
                  <button className="hallzee-pill-btn" onClick={() => setModal("roster")}>
                    <Plus size={14} />
                    Start Pass
                  </button>
                </div>
              </section>
            ) : state.active.passes.length === 1 ? (
              /* State B: Single Pass Occupied */
              (() => {
                const pass = state.active.passes[0];
                const student = state.students.find((s) => s.studentId === pass.studentId);
                const studentName = fullName(student);
                const passElapsed = elapsed(pass.epoch, now);
                const isOverdue = passElapsed > state.policy.warningSeconds;
                return (
                  <section className="hero-pass-card occupied active-student">
                    <div className="hero-pass-left">
                      <div className="hero-avatar occupied">
                        <UserX size={28} />
                      </div>
                      <div className="hero-pass-details">
                        <StatusPill variant={isOverdue ? "danger" : "warning"}>
                          {isOverdue ? "OVERDUE" : "PASS OCCUPIED"}
                        </StatusPill>
                        <h3>1 student out</h3>
                        <p className="hero-pass-subtitle">
                          <strong>{studentName || pass.studentId}</strong>
                          {studentName ? ` (ID: #${pass.studentId})` : ""} ·{" "}
                          {durationLabel(passElapsed)} elapsed
                        </p>
                      </div>
                    </div>
                    <div className="hero-floating-card">
                      <div className="hero-floating-info">
                        <span className="hero-floating-label amber">TRIP ELAPSED</span>
                        <span className={`hero-floating-val mono ${isOverdue ? "danger" : ""}`}>
                          {durationLabel(passElapsed)}
                        </span>
                        <span className="hero-floating-sub">Student ID: #{pass.studentId}</span>
                      </div>
                      <button
                        className="dark-action"
                        disabled={state.busy}
                        onClick={() => {
                          if (window.confirm("Check in this selected terminal pass now?")) {
                            void controller?.checkIn(pass.studentId);
                          }
                        }}
                      >
                        Check in
                      </button>
                    </div>
                  </section>
                );
              })()
            ) : (
              /* State B: Multiple Passes Occupied */
              <>
                <section className="hero-pass-card occupied">
                  <div className="hero-pass-left">
                    <div className="hero-avatar occupied">
                      <UserX size={28} />
                    </div>
                    <div className="hero-pass-details">
                      <StatusPill variant="warning">MULTIPLE PASSES</StatusPill>
                      <h3>{state.active.passes.length} students out</h3>
                      <p className="hero-pass-subtitle">
                        {state.active.passes.length} active passes in progress · Capacity:{" "}
                        {state.policy.capacity}
                      </p>
                    </div>
                  </div>
                  <div className="hero-floating-card">
                    <div className="hero-floating-info">
                      <span className="hero-floating-label amber">CAPACITY UTILIZATION</span>
                      <span className="hero-floating-val mono">
                        {state.active.passes.length} / {state.policy.capacity}
                      </span>
                      <span className="hero-floating-sub">Simultaneous students out</span>
                    </div>
                  </div>
                </section>
                <div className="additional-passes-list">
                  {state.active.passes.map((pass) => {
                    const student = state.students.find((s) => s.studentId === pass.studentId);
                    const studentName = fullName(student);
                    const passElapsed = elapsed(pass.epoch, now);
                    const isOverdue = passElapsed > state.policy.warningSeconds;
                    return (
                      <div className="active-student-card active-student" key={pass.studentId}>
                        <div className="hero-pass-left">
                          <div className="hero-avatar occupied">
                            <UserX size={24} />
                          </div>
                          <div className="hero-pass-details">
                            <StatusPill variant={isOverdue ? "danger" : "warning"}>
                              {isOverdue ? "OVERDUE" : "PASS OCCUPIED"}
                            </StatusPill>
                            <strong>{studentName || pass.studentId}</strong>
                            <span className="hero-pass-subtitle">
                              Student ID: #{pass.studentId} · {durationLabel(passElapsed)} elapsed
                            </span>
                          </div>
                        </div>
                        <div className="button-row">
                          <span className={`hero-floating-val mono ${isOverdue ? "danger" : ""}`}>
                            {durationLabel(passElapsed)}
                          </span>
                          <button
                            className="dark-action"
                            disabled={state.busy}
                            onClick={() => {
                              if (window.confirm("Check in this selected terminal pass now?")) {
                                void controller?.checkIn(pass.studentId);
                              }
                            }}
                          >
                            Check in
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </>
            )}

            {/* MAIN DASHBOARD 2-COLUMN GRID (1.8fr 1.2fr) */}
            <div className="dashboard-grid-2col">
              {/* LEFT COLUMN: RECENT ACTIVITY */}
              <section className="dashboard-card">
                <div className="dashboard-card-header">
                  <div>
                    <h2>Recent Activity</h2>
                    <p>Today&apos;s completed student trips</p>
                  </div>
                  <div className="button-row">
                    <button
                      className="pill-outline"
                      onClick={onExportRecent}
                      title="Export completed trips to CSV"
                    >
                      <Download size={14} />
                      Export
                    </button>
                    <button
                      className="pill-outline"
                      onClick={() => setModal("trips")}
                      title="View full trip history"
                    >
                      View All
                      <ChevronRight size={13} />
                    </button>
                  </div>
                </div>

                <div className="recent-trips-list">
                  {recent.map((t) => (
                    <div className="recent-trip-row" key={`${t.terminalId}:${t.tripId}`}>
                      <div className="trip-avatar-icon">
                        <UserCheck size={18} />
                      </div>
                      <div className="trip-student-info">
                        <span className="trip-student-name">
                          {fullName(state.students.find((s) => s.studentId === t.studentId)) ||
                            t.studentId}
                        </span>
                        <span className="trip-student-id">ID: #{t.studentId}</span>
                      </div>
                      <div className="trip-times-col">
                        <span>Out: {t.timeOut || "—"}</span>
                        <span>In: {t.timeIn || "—"}</span>
                      </div>
                      <div className="trip-duration-col">
                        <span className="trip-duration-text">
                          {t.durationSeconds === null ? "—" : durationLabel(t.durationSeconds)}
                        </span>
                        <StatusPill variant="neutral">
                          {t.status === "COMPLETE"
                            ? "Completed"
                            : t.status === "MANUAL"
                              ? "Teacher check-in"
                              : "Reset"}
                        </StatusPill>
                      </div>
                    </div>
                  ))}

                  {!recent.length && (
                    <div className="recent-activity-empty-state">
                      <button
                        className="btn-show-more-pill"
                        onClick={() => setModal("trips")}
                      >
                        Show more in history
                        <ChevronRight size={14} />
                      </button>
                    </div>
                  )}
                </div>

                {recent.length > 0 && (
                  <button
                    className="btn-show-more-pill"
                    onClick={() => setModal("trips")}
                  >
                    Show more in history
                    <ChevronRight size={14} />
                  </button>
                )}
              </section>

              {/* RIGHT COLUMN: ROSTER & POLICY + EXCEEDED TIME */}
              <div className="dashboard-right-column">
                {/* CARD 1: ROSTER & PASS POLICY */}
                <section className="dashboard-card">
                  <div className="dashboard-card-header">
                    <div className="dashboard-card-title-group">
                      <div className="dashboard-card-icon-badge sky">
                        <ShieldCheck size={18} />
                      </div>
                      <div>
                        <h2>Roster & Pass Policy</h2>
                      </div>
                    </div>
                  </div>

                  <div className="roster-policy-inset">
                    <div className="roster-policy-row">
                      <span className="label">Pass Capacity:</span>
                      <span className="value">{state.policy.capacity} student(s)</span>
                    </div>
                    <div className="roster-policy-row">
                      <span className="label">Overdue Warning:</span>
                      <span className="value amber">
                        {Math.round(state.policy.warningSeconds / 60)} minutes
                      </span>
                    </div>
                    <div className="roster-policy-row">
                      <span className="label">Daily guideline:</span>
                      <span className="value">
                        {state.policy.dailyGuideline
                          ? `${state.policy.dailyGuideline} trips`
                          : "2 trips"}
                      </span>
                    </div>

                    {dailyExceeded > 0 && (
                      <div className="guideline-alert-box">
                        {dailyExceeded} student(s) above local daily guideline today
                      </div>
                    )}
                  </div>

                  <button
                    className="hallzee-pill-btn btn-block"
                    disabled={!state.ready}
                    onClick={() => setModal("policies")}
                  >
                    <Users size={14} />
                    Manage Pass Rules
                  </button>
                </section>

                {/* CARD 2: EXCEEDED TIME */}
                <section className="dashboard-card">
                  <div className="dashboard-card-header">
                    <div className="dashboard-card-title-group">
                      <div className="dashboard-card-icon-badge amber">
                        <Clock3 size={18} />
                      </div>
                      <div>
                        <h2>Exceeded Time</h2>
                      </div>
                    </div>
                    <button
                      className="status-pill warning exceeded-threshold-pill"
                      onClick={() => {
                        const presets = [5, 7, 10, 15];
                        const nextIdx = (presets.indexOf(thresholdMinutes) + 1) % presets.length;
                        setThresholdMinutes(presets[nextIdx]);
                      }}
                      title="Toggle warning threshold preset"
                    >
                      &gt; {thresholdMinutes}m
                      <ChevronDown size={11} />
                    </button>
                  </div>
                  <p className="exceeded-card-subtitle">
                    Students whose hall pass trips exceeded {thresholdMinutes}m within the last 2 weeks.
                  </p>

                  {/* Filter controls toolbar */}
                  <div className="exceeded-filter-bar">
                    <div className="card-search-wrapper">
                      <Search size={14} />
                      <input
                        className="card-search-input"
                        placeholder="Search period, name, or ID…"
                        value={searchExceeded}
                        onChange={(e) => setSearchExceeded(e.target.value)}
                        aria-label="Filter exceeded students"
                      />
                    </div>
                    <div className="exceeded-filter-row">
                      <div className="exceeded-timeframe-group">
                        <span className="exceeded-filter-label">Timeframe:</span>
                        <div className="exceeded-timeframe-pill">
                          <span>Last 2 Weeks</span>
                          <ChevronDown size={11} />
                        </div>
                      </div>
                    </div>
                    <div className="exceeded-sort-row">
                      <span className="exceeded-filter-label">Sort:</span>
                      <div className="exceeded-sort-pills">
                        <button className="pill-filter active" type="button">Period</button>
                        <button className="pill-filter" type="button">Name</button>
                        <button className="pill-filter" type="button">Count</button>
                      </div>
                    </div>
                  </div>

                  {/* Exceeded list */}
                  <div className="exceeded-list">
                    {exceededStudents.map((item) => (
                      <div className="exceeded-item-row" key={item.studentId}>
                        <div className="exceeded-item-details">
                          <span className="exceeded-item-name">#{item.studentId}</span>
                          <span className="exceeded-item-sub">ID: #{item.studentId} • {item.displayName}</span>
                        </div>
                        <div className="exceeded-item-stats">
                          <span className="exceeded-badge">{item.trips.length} {item.trips.length === 1 ? "time" : "times"}</span>
                          <span className="exceeded-max-duration">
                            Max: {durationLabel(item.maxDuration)}
                          </span>
                        </div>
                      </div>
                    ))}

                    {!exceededStudents.length && (
                      <div className="exceeded-empty">
                        <Check size={24} />
                        <strong>
                          No students over threshold
                        </strong>
                        <span>
                          Students exceeding {thresholdMinutes}m will appear here.
                        </span>
                      </div>
                    )}
                  </div>
                </section>
              </div>
            </div>

            {/* WORKSPACE FOOTER */}
            <footer className="workspace-footer">
              <span>
                <ShieldCheck size={14} />
                Classroom data stays in this browser and the terminal.
              </span>
              <div className="button-row">
                <button className="text-button" onClick={() => void updater.check()}>
                  Check updates
                </button>
                <button className="text-button" onClick={() => setProjecting(true)}>
                  Projection view
                </button>
                {connected && (
                  <button className="text-button" onClick={() => controller?.disconnect()}>
                    <LogOut size={14} />
                    Disconnect
                  </button>
                )}
              </div>
            </footer>
          </main>
        </div>
      </div>

      {/* MODALS */}
      {modal === "connect" && <ConnectionDialog onClose={close} />}
      {modal === "trips" && <TripsDialog onClose={close} />}
      {modal === "roster" && <RosterDialog onClose={close} />}
      {modal === "policies" && <PoliciesDialog onClose={close} />}
      {modal === "terminal" && <TerminalSettingsDialog onClose={close} />}
      {modal === "data" && state.workspace && <DataSettingsDialog onClose={close} />}
    </div>
  );
}
