import { useEffect, useMemo, useState } from "react";
import {
  Bluetooth,
  RefreshCw,
  Users,
  History,
  SlidersHorizontal,
  Settings,
  ArrowUpRight,
  Clock3,
  ShieldCheck,
  Monitor,
  LogOut,
  Download,
} from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { elapsed, durationLabel, localDate } from "../sync/TerminalClock";
import { fullName, filterTrips } from "../domain/TripReports";
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
      }).slice(0, 5),
    [state.trips, state.students],
  );
  const today = state.trips.filter(
    (t) => t.tripDate === localDate(now) && t.status !== "MANUAL_RESET",
  );
  const overdue = today.filter(
    (t) => t.durationSeconds !== null && t.durationSeconds > state.policy.warningSeconds,
  );
  const dailyCounts = new Map<string, number>();
  for (const t of today) dailyCounts.set(t.studentId, (dailyCounts.get(t.studentId) ?? 0) + 1);
  const dailyExceeded =
    state.policy.dailyGuideline > 0
      ? [...dailyCounts].filter(([, n]) => n > state.policy.dailyGuideline).length
      : 0;
  const close = () => setModal(null);
  if (projecting)
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
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href="/" aria-label="Hallzee home">
          <img src="/icons/hallzee.svg" alt="" />
          Hallzee<span>WEB</span>
        </a>
        <div className="workspace-label">YOUR CLASSROOM</div>
        <nav aria-label="Classroom navigation">
          <button className="nav-active">
            <Monitor size={19} />
            Overview
          </button>
          <button onClick={() => setModal("trips")} disabled={!state.ready}>
            <History size={19} />
            Trip history
          </button>
          <button onClick={() => setModal("roster")} disabled={!state.ready}>
            <Users size={19} />
            Student roster<span>{state.students.length}</span>
          </button>
          <button onClick={() => setModal("policies")} disabled={!state.ready}>
            <SlidersHorizontal size={19} />
            Policies & bell times
          </button>
        </nav>
        <div className="sidebar-bottom">
          <div className="local-note">
            <ShieldCheck size={20} />
            <div>
              <strong>Local classroom data</strong>
              <small>No student data uploads</small>
            </div>
          </div>
          <button className="nav-setting" disabled={!state.ready} onClick={() => setModal("data")}>
            <Settings size={18} />
            Classroom & data
          </button>
          <small className="build-label">1.2.0 development · Hardware acceptance pending</small>
        </div>
      </aside>
      <div className="main-column">
        <header className="topbar">
          <div>
            <span className="breadcrumb">Classroom</span>
            <span className="slash">/</span>
            <strong>Overview</strong>
          </div>
          <div className="topbar-right">
            <span className="offline-label">
              {offline.ready
                ? "Ready offline"
                : import.meta.env.DEV
                  ? "Development build"
                  : "Preparing offline use"}
            </span>
            <span className="avatar">
              {state.workspace?.teacher.slice(0, 1).toUpperCase() || "H"}
            </span>
          </div>
        </header>
        <main className="workspace">
          <div className="page-heading">
            <div>
              <p className="eyebrow">
                {now.toLocaleDateString(undefined, {
                  weekday: "long",
                  month: "long",
                  day: "numeric",
                })}
              </p>
              <h1>{state.workspace?.name || "Your classroom"}</h1>
              <p className="muted">Pass status and classroom activity, in one place.</p>
            </div>
            <DocumentPictureInPictureButton
              projection={projection}
              fallback={() => setProjecting(true)}
            />
          </div>
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
              The computer clock or time zone changed. Verify classroom time before relying on
              timers.
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
          <section className="connection-strip">
            <div className={`connection-icon ${connected ? "is-connected" : ""}`}>
              <Bluetooth size={23} />
            </div>
            <div className="connection-details">
              <strong>{state.terminal?.customName || "Connect your Hallzee terminal"}</strong>
              <span>
                {state.syncing
                  ? "Synchronizing saved trips…"
                  : state.retryUntil
                    ? `Reconnecting · ${Math.max(0, Math.ceil((state.retryUntil - now.getTime()) / 1000))}s remaining`
                    : connected
                      ? "Authenticated · receiving live activity"
                      : state.terminal
                        ? "Disconnected · pass status unknown"
                        : "Pair once to see live pass status and saved trips"}
              </span>
            </div>
            <div className="button-row">
              <button
                className="secondary"
                disabled={!state.ready || state.busy || !connected}
                onClick={() => void controller?.syncNow()}
              >
                <RefreshCw size={16} className={state.syncing ? "spin" : ""} />
                Sync now
              </button>
              <button
                disabled={!state.ready || state.busy}
                onClick={() => setModal(connected ? "terminal" : "connect")}
              >
                {connected ? (
                  "Terminal settings"
                ) : (
                  <>
                    <Bluetooth size={16} />
                    Connect terminal
                  </>
                )}
              </button>
            </div>
          </section>
          <div className="overview-grid">
            <section className="card pass-card">
              <div className="card-heading">
                <h2>Hall pass status</h2>
                <span className={`pill ${state.active.fresh ? "green" : "gray"}`}>
                  {state.active.fresh ? "Live" : "Unknown"}
                </span>
              </div>
              <div
                className={`pass-status ${!state.active.fresh ? "status-unknown" : state.active.passes.length ? "status-out" : "status-available"}`}
              >
                <span className="status-symbol">
                  {!state.active.fresh ? (
                    "—"
                  ) : state.active.passes.length ? (
                    <ArrowUpRight size={31} />
                  ) : (
                    <ShieldCheck size={31} />
                  )}
                </span>
                <h3>
                  {!state.active.fresh
                    ? "Waiting for terminal"
                    : state.active.passes.length
                      ? `${state.active.passes.length} ${state.active.passes.length === 1 ? "student" : "students"} out`
                      : "Pass available"}
                </h3>
                <p>
                  {!state.active.fresh
                    ? "Connect to check current pass availability."
                    : `${state.policy.capacity} ${state.policy.capacity === 1 ? "pass" : "passes"} configured for this classroom`}
                </p>
              </div>
              {state.active.fresh &&
                state.active.passes.map((pass) => {
                  const seconds = elapsed(pass.epoch, now);
                  return (
                    <div className="active-student" key={pass.studentId}>
                      <span className="student-avatar">
                        {fullName(state.students.find((s) => s.studentId === pass.studentId)).slice(
                          0,
                          1,
                        ) || "#"}
                      </span>
                      <div>
                        <strong>
                          {fullName(state.students.find((s) => s.studentId === pass.studentId)) ||
                            pass.studentId}
                        </strong>
                        <small>
                          {seconds > state.policy.warningSeconds ? "Overdue · " : ""}
                          {durationLabel(seconds)} elapsed
                        </small>
                      </div>
                      <button
                        className="secondary compact"
                        disabled={state.busy}
                        onClick={() => {
                          if (window.confirm("Check in this selected terminal pass now?"))
                            void controller?.checkIn(pass.studentId);
                        }}
                      >
                        Check in
                      </button>
                    </div>
                  );
                })}
              <div className="card-footer">
                <Clock3 size={15} />
                <span>
                  {state.lastSync
                    ? `Last synced ${new Date(state.lastSync).toLocaleTimeString()}`
                    : "No completed sync yet"}
                </span>
              </div>
            </section>
            <section className="card">
              <div className="card-heading">
                <h2>Today at a glance</h2>
                <span className="muted">{state.workspace?.room || "Your room"}</span>
              </div>
              <div className="metrics">
                <div>
                  <span>Completed trips</span>
                  <strong>{today.length}</strong>
                  <small>From terminal history</small>
                </div>
                <div>
                  <span>Overdue trips</span>
                  <strong className={overdue.length ? "amber-text" : ""}>{overdue.length}</strong>
                  <small>Over {Math.round(state.policy.warningSeconds / 60)} minutes</small>
                </div>
                <div>
                  <span>Above daily guideline</span>
                  <strong>{dailyExceeded}</strong>
                  <small>
                    {state.policy.dailyGuideline
                      ? "Students above local guideline"
                      : "Guideline disabled"}
                  </small>
                </div>
              </div>
              <div className="period-panel">
                <div>
                  <span className="eyebrow">CURRENT PERIOD</span>
                  <h3>{period?.periodName || "Between periods"}</h3>
                  <p>
                    {period
                      ? `${period.start} – ${period.end}${period.classSection ? " · " + period.classSection : ""}`
                      : "Add your bell schedule to see class periods."}
                  </p>
                </div>
                <span className={`pill ${windowDecision === "Allow" ? "green" : "amber"}`}>
                  {windowDecision}
                </span>
              </div>
              <button
                className="text-button"
                disabled={!state.ready}
                onClick={() => setModal("policies")}
              >
                Manage policies & bell times <ArrowUpRight size={15} />
              </button>
            </section>
          </div>
          <section className="card activity-card">
            <div className="card-heading">
              <div>
                <h2>Recent activity</h2>
                <p className="muted">Completed passes from your terminal</p>
              </div>
              <button
                className="text-button"
                disabled={!state.ready}
                onClick={() => setModal("trips")}
              >
                View all trips <ArrowUpRight size={16} />
              </button>
            </div>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Student</th>
                    <th>Checked out</th>
                    <th>Checked in</th>
                    <th>Duration</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {recent.map((t) => (
                    <tr key={`${t.terminalId}:${t.tripId}`}>
                      <td>
                        <strong>
                          {fullName(state.students.find((s) => s.studentId === t.studentId)) ||
                            t.studentId}
                        </strong>
                        <small>{t.tripDate || "Time unavailable"}</small>
                      </td>
                      <td>{t.timeOut || "—"}</td>
                      <td>{t.timeIn || "—"}</td>
                      <td>{t.durationSeconds === null ? "—" : durationLabel(t.durationSeconds)}</td>
                      <td>
                        <span className="pill gray">
                          {t.status === "COMPLETE"
                            ? "Completed"
                            : t.status === "MANUAL"
                              ? "Teacher check-in"
                              : "Reset"}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {!recent.length && (
                <div className="empty">
                  <History size={29} />
                  <h3>Your trip history will appear here</h3>
                  <p>Connect a terminal and sync. Existing records stay local.</p>
                </div>
              )}
            </div>
          </section>
          <div className="bottom-grid">
            <button
              className="quick-card"
              disabled={!state.ready}
              onClick={() => setModal("roster")}
            >
              <Users size={24} />
              <span>
                <strong>Student roster</strong>
                <small>
                  {state.students.length
                    ? `${state.students.length} students saved locally`
                    : "Import names and student IDs"}
                </small>
              </span>
              <ArrowUpRight size={19} />
            </button>
            <button className="quick-card" disabled={!state.ready} onClick={() => setModal("data")}>
              <Download size={24} />
              <span>
                <strong>Keep a classroom backup</strong>
                <small>
                  {state.persistent
                    ? "Persistent storage granted"
                    : "Back up before clearing browser data"}
                </small>
              </span>
              <ArrowUpRight size={19} />
            </button>
          </div>
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
      {modal === "connect" && <ConnectionDialog onClose={close} />}{" "}
      {modal === "trips" && <TripsDialog onClose={close} />}{" "}
      {modal === "roster" && <RosterDialog onClose={close} />}{" "}
      {modal === "policies" && <PoliciesDialog onClose={close} />}{" "}
      {modal === "terminal" && <TerminalSettingsDialog onClose={close} />}{" "}
      {modal === "data" && state.workspace && <DataSettingsDialog onClose={close} />}
    </div>
  );
}
