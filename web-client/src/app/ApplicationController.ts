import { Signal } from "./events";
import { capabilities } from "./capabilities";
import { RuntimeLock } from "./RuntimeLock";
import { HallzeeError, abortCheck, errorText } from "./errors";
import { LocalDatabase } from "../storage/LocalDatabase";
import { CredentialRepository } from "../storage/CredentialRepository";
import { WorkspaceRepository } from "../storage/WorkspaceRepository";
import { TripRepository } from "../storage/TripRepository";
import { BackupService, type Backup } from "../storage/BackupService";
import {
  defaultPolicy,
  type Workspace,
  type Terminal,
  type Student,
  type Enrollment,
  type Trip,
  type Policy,
  type BellPeriod,
  type ScheduleException,
} from "../storage/schema";
import { WebBluetoothTerminalConnection } from "../transport/WebBluetoothTerminalConnection";
import type { BluetoothPort, DeviceHandle } from "../transport/BluetoothPort";
import { WebTerminalSession, type SessionState } from "../protocol/WebTerminalSession";
import { TerminalOperationQueue } from "../protocol/TerminalOperationQueue";
import { terminalName } from "../protocol/WebTerminalCrypto";
import { SyncEngine } from "../sync/SyncEngine";
import { ActivePassStore } from "../sync/ActivePassStore";
import { localDate } from "../sync/TerminalClock";
import { resolvePeriod } from "../domain/PolicyScheduleService";
import { buildPolicyTransfer } from "../domain/BellPolicyProtocol";
import { RosterService } from "../domain/RosterService";
import { AutoReconnectCoordinator } from "../lifecycle/AutoReconnectCoordinator";
export interface AppSnapshot {
  ready: boolean;
  locked: boolean;
  error: string | null;
  busy: boolean;
  session: SessionState;
  syncing: boolean;
  workspace?: Workspace;
  terminal?: Terminal;
  students: Student[];
  enrollments: Enrollment[];
  trips: Trip[];
  policy: Policy;
  periods: BellPeriod[];
  exceptions: ScheduleException[];
  active: { passes: ActivePassStore["passes"]; fresh: boolean; singlePass: boolean };
  lastSync: string | null;
  persistent: boolean;
  usage: number;
  quota: number;
  retryUntil: number | null;
  clockNotice: boolean;
}
export const initialSnapshot: AppSnapshot = {
  ready: false,
  locked: false,
  error: null,
  busy: false,
  session: "Disconnected",
  syncing: false,
  students: [],
  enrollments: [],
  trips: [],
  policy: defaultPolicy(""),
  periods: [],
  exceptions: [],
  active: { passes: [], fresh: false, singlePass: false },
  lastSync: null,
  persistent: false,
  usage: 0,
  quota: 0,
  retryUntil: null,
  clockNotice: false,
};
export class ApplicationController {
  private changes = new Signal<void>();
  subscribe = this.changes.subscribe;
  private state: AppSnapshot = { ...initialSnapshot };
  getSnapshot = () => this.state;
  private lock = new RuntimeLock();
  private stopped = false;
  private started = false;
  private db?: LocalDatabase;
  private credentials?: CredentialRepository;
  private workspaceRepo?: WorkspaceRepository;
  private tripRepo?: TripRepository;
  private session?: WebTerminalSession;
  private sync?: SyncEngine;
  private active = new ActivePassStore();
  private queue = new TerminalOperationQueue();
  private connection = new AbortController();
  private lifetime = new AbortController();
  private device?: DeviceHandle;
  private autoConnect = false;
  private reconnect?: AutoReconnectCoordinator;
  private timer?: ReturnType<typeof setInterval>;
  private refreshTimer?: ReturnType<typeof setTimeout>;
  private refreshing = false;
  private snapshotPending = false;
  private syncPending = false;
  private operationCount = 0;
  private lastTick = Date.now();
  private lastOffset = new Date().getTimezoneOffset();
  private lastReconcile = 0;
  constructor(private port: BluetoothPort = new WebBluetoothTerminalConnection()) {}
  private publish(patch: Partial<AppSnapshot> = {}) {
    if (this.stopped) return;
    this.state = {
      ...this.state,
      ...patch,
      active: {
        passes: [...this.active.passes],
        fresh: this.active.fresh,
        singlePass: this.active.singlePass,
      },
    };
    this.changes.emit();
  }
  async start() {
    if (this.started) return;
    this.started = true;
    try {
      const caps = capabilities();
      if (!caps.secure || !caps.storage || !caps.crypto || !caps.locks)
        throw new HallzeeError(
          "CAPABILITY_REQUIRED",
          "Hallzee needs a secure page, persistent browser storage, Web Crypto, and Web Locks. Open this build in Chrome.",
        );
      if (!(await this.lock.acquire())) {
        this.publish({ locked: true });
        return;
      }
      if (this.stopped) return;
      this.db = await LocalDatabase.open("hallzee-web", () => {
        this.disconnect(false);
        this.publish({
          ready: false,
          error: "A newer Hallzee version needs this window closed. Reopen Hallzee.",
        });
      });
      if (this.stopped) {
        this.db.close();
        return;
      }
      await this.db.probe();
      this.credentials = new CredentialRepository(this.db);
      await this.credentials.installationId();
      abortCheck(this.lifetime.signal);
      this.workspaceRepo = new WorkspaceRepository(this.db);
      this.tripRepo = new TripRepository(this.db);
      const workspace = await this.workspaceRepo.ensure();
      abortCheck(this.lifetime.signal);
      this.publish({ workspace });
      this.session = new WebTerminalSession(this.port, this.credentials);
      this.session.changed.subscribe((session) => this.publish({ session }));
      this.session.application.subscribe((line) => this.onApplication(line));
      this.session.dropped.subscribe(() => {
        this.connection.abort();
        this.sync?.dispose();
        this.active.unknown();
        this.publish();
        if (this.autoConnect && this.state.terminal) this.reconnect?.start();
      });
      this.reconnect = new AutoReconnectCoordinator(
        async (signal) => {
          let handle = this.device;
          const assigned = this.state.terminal;
          if (!assigned) throw new HallzeeError("NO_ASSIGNMENT", "Choose a terminal to connect.");
          if (!handle || handle.id !== assigned.deviceIdHint) {
            const devices = await this.port.getRememberedDevices();
            handle = devices.find((d) => d.id === assigned.deviceIdHint);
          }
          if (!handle)
            throw new HallzeeError(
              "CHOOSE_TERMINAL",
              "Choose your saved terminal to reconnect. Chrome has no remembered device handle for it.",
            );
          await this.queue.run(
            "reconnect",
            () => this.establish(handle, undefined, signal),
            signal,
          );
          void this.syncNow();
        },
        (retryUntil) => this.publish({ retryUntil }),
        (error) => this.publish({ error: errorText(error) }),
      );
      await this.refresh();
      this.autoConnect = await this.db.meta("autoConnect", false);
      abortCheck(this.lifetime.signal);
      this.publish({ ready: true });
      for (const name of ["focus", "pageshow"])
        window.addEventListener(name, this.wake, { signal: this.lifetime.signal });
      document.addEventListener(
        "visibilitychange",
        () => {
          if (document.visibilityState === "visible") this.wake();
        },
        { signal: this.lifetime.signal },
      );
      document.addEventListener("resume", this.resume, { signal: this.lifetime.signal });
      document.addEventListener("freeze", this.suspend, { signal: this.lifetime.signal });
      window.addEventListener("pagehide", this.suspend, { signal: this.lifetime.signal });
      this.timer = setInterval(() => {
        const now = Date.now();
        const offset = new Date().getTimezoneOffset();
        if (now - this.lastTick > 10000 || now < this.lastTick || offset !== this.lastOffset) {
          this.active.unknown();
          this.publish({ clockNotice: offset !== this.lastOffset || now < this.lastTick });
          this.wake();
        }
        this.lastTick = now;
        this.lastOffset = offset;
        if (this.session?.state === "Authenticated" && now - this.lastReconcile >= 300000)
          void this.syncNow();
      }, 1000);
      if (this.autoConnect && this.state.terminal) this.reconnect.start();
    } catch (error) {
      this.publish({ error: errorText(error) });
    }
  }
  private suspended = false;
  private suspend = () => {
    this.suspended = true;
    this.reconnect?.stop();
    this.connection.abort();
    this.sync?.dispose();
    this.session?.disconnect();
    this.active.unknown();
    this.db?.close();
    this.lock.close();
    this.publish({ ready: false });
  };
  private resume = () => {
    if (this.suspended) {
      window.location.reload();
      return;
    }
    this.wake();
  };
  private wake = () => {
    if (this.stopped) return;
    if (this.suspended) {
      this.resume();
      return;
    }
    if (!this.state.ready) return;
    this.active.unknown();
    this.publish();
    if (this.session?.state === "Authenticated") void this.syncNow();
    else if (this.autoConnect && this.state.terminal) this.reconnect?.start();
  };
  async refresh() {
    if (!this.db || this.stopped) return;
    if (this.refreshing) {
      this.scheduleRefresh();
      return;
    }
    this.refreshing = true;
    try {
      const [
        workspaces,
        terminals,
        students,
        enrollments,
        trips,
        policies,
        periods,
        exceptions,
        states,
      ] = await Promise.all([
        this.db.all("workspaces"),
        this.db.all("terminals"),
        this.db.all("roster_students"),
        this.db.all("roster_enrollments"),
        this.db.all("trips"),
        this.db.all("policy_rules"),
        this.db.all("bell_periods"),
        this.db.all("schedule_exceptions"),
        this.db.all("sync_state"),
      ]);
      const workspace = workspaces[0],
        terminal = terminals.find((t) => t.assignedWorkspaceId === workspace?.workspaceId);
      const estimate: StorageEstimate =
        (await navigator.storage?.estimate?.().catch(() => ({}))) ?? {};
      this.publish({
        workspace,
        terminal,
        students,
        enrollments,
        trips,
        policy:
          policies.find((p) => p.workspaceId === workspace?.workspaceId) ??
          defaultPolicy(workspace?.workspaceId ?? ""),
        periods,
        exceptions,
        lastSync:
          states.find((s) => s.terminalId === terminal?.terminalId)?.lastSuccessfulSyncUtc ?? null,
        persistent: (await navigator.storage?.persisted?.().catch(() => false)) ?? false,
        usage: estimate.usage ?? 0,
        quota: estimate.quota ?? 0,
      });
    } finally {
      this.refreshing = false;
    }
  }
  private scheduleRefresh = () => {
    if (this.refreshTimer || this.stopped) return;
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = undefined;
      void this.refresh().catch((error) => this.publish({ error: errorText(error) }));
    }, 100);
  };
  clearError() {
    this.publish({ error: null });
  }
  async action(kind: string, task: () => Promise<void>): Promise<boolean> {
    this.operationCount++;
    this.publish({ busy: true, error: null });
    try {
      await this.queue.run(kind, task, this.lifetime.signal);
      await this.refresh();
      return true;
    } catch (error) {
      if (
        ["connect", "sync", "settings", "check-in", "release", "recovery", "policy"].includes(kind)
      )
        this.disconnect(false);
      this.publish({ error: errorText(error) });
      return false;
    } finally {
      this.operationCount--;
      this.publish({ busy: this.operationCount > 0 });
    }
  }
  private async establish(handle: DeviceHandle, code?: string, outerSignal?: AbortSignal) {
    if (!this.session || !this.state.workspace) throw new HallzeeError("NOT_READY");
    this.connection.abort();
    this.connection = new AbortController();
    const control = this.connection;
    const abort = () => control.abort();
    outerSignal?.addEventListener("abort", abort, { once: true });
    if (outerSignal?.aborted) abort();
    this.sync?.dispose();
    this.active.unknown();
    try {
      const terminal = await this.session.open(
        handle,
        this.state.workspace.workspaceId,
        this.state.terminal?.terminalId,
        code,
        control.signal,
      );
      abortCheck(control.signal);
      this.device = handle;
      this.publish({ terminal });
      const workspace = this.state.workspace;
      this.sync = new SyncEngine(
        this.session,
        this.tripRepo!,
        (trip) => {
          const date =
            trip.tripDate && trip.timeOut
              ? new Date(`${trip.tripDate}T${trip.timeOut}`)
              : undefined;
          const period = date
            ? resolvePeriod(date, this.state.periods, this.state.exceptions)
            : undefined;
          return {
            terminalId: terminal.terminalId,
            receivedWorkspaceId: workspace.workspaceId,
            scheduleName: period?.scheduleName ?? null,
            classSection: period?.classSection ?? null,
            contextSource: period ? "resolved-on-receipt" : "unknown",
          };
        },
        this.scheduleRefresh,
        (error) => {
          this.connection.abort();
          this.session?.disconnect();
          this.active.unknown();
          this.publish({ error: errorText(error) });
        },
      );
    } finally {
      outerSignal?.removeEventListener("abort", abort);
      code = undefined;
    }
  }
  async chooseTerminal(code?: string) {
    // Keep requestDevice in the user activation, before any async storage work.
    if (!capabilities().bluetooth) {
      this.publish({
        error: "Web Bluetooth is unavailable. Use Chrome and check school browser policy.",
      });
      return false;
    }
    const selection = this.port.requestDevice();
    void selection.catch(() => {});
    this.reconnect?.stop();
    return this.action("connect", async () => {
      const device = await selection;
      this.autoConnect = true;
      await this.db!.setMeta("autoConnect", true);
      await this.requestPersistence();
      await this.establish(device, code);
      await this.initialize();
    });
  }
  retry() {
    this.clearError();
    this.autoConnect = true;
    void this.db
      ?.setMeta("autoConnect", true)
      .catch((error) => this.publish({ error: errorText(error) }));
    this.reconnect?.start();
  }
  disconnect(user = true) {
    this.reconnect?.stop();
    this.connection.abort();
    this.sync?.dispose();
    this.session?.disconnect();
    this.active.unknown();
    if (user) {
      this.autoConnect = false;
      void this.db
        ?.setMeta("autoConnect", false)
        .catch((error) => this.publish({ error: errorText(error) }));
    }
    this.publish({ syncing: false });
  }
  private async queryActive() {
    const session = this.session!,
      signal = this.connection.signal;
    try {
      const line = await session.request(
        "GET_ACTIVE_PASSES",
        (l) => l === "ACTIVE_PASSES" || l.startsWith("ACTIVE_PASSES,"),
        signal,
      );
      this.active.singlePass = false;
      this.active.snapshot(line);
    } catch (error) {
      if (!(error instanceof HallzeeError) || error.code !== "UNKNOWN_COMMAND") throw error;
      const line = await session.request(
        "GET_ACTIVE_PASS",
        (l) => l.startsWith("ACTIVE_PASS,"),
        signal,
      );
      this.active.singlePass = true;
      this.active.snapshot(line);
    }
    this.publish();
  }
  private async initialize() {
    const session = this.session!,
      signal = this.connection.signal;
    this.publish({ syncing: true });
    try {
      const line = await session.request(
        "GET_SETTINGS",
        (l) => /^SETTINGS,MAX_ID_LENGTH,(?:[4-9]|1[0-6])$/.test(l),
        signal,
      );
      const terminal = {
        ...this.state.terminal!,
        maxIdLength: Number(line.split(",")[2]),
        settingsReadUtc: new Date().toISOString(),
      };
      await this.db!.put("terminals", terminal);
      this.publish({ terminal });
      await this.queryActive();
      if (Intl.DateTimeFormat().resolvedOptions().timeZone !== this.state.workspace!.timeZone)
        throw new HallzeeError(
          "TIMEZONE_MISMATCH",
          "Set this computer’s time zone to the classroom time zone before syncing the terminal clock.",
        );
      await this.sync!.synchronize(signal);
      await this.applyPolicy();
      await this.queryActive();
      this.lastReconcile = Date.now();
    } catch (error) {
      this.connection.abort();
      this.sync?.dispose();
      this.session?.disconnect();
      this.active.unknown();
      throw error;
    } finally {
      this.publish({ syncing: false });
    }
  }
  async syncNow() {
    if (this.syncPending || this.session?.state !== "Authenticated") return false;
    this.syncPending = true;
    try {
      return await this.action("sync", () => this.initialize());
    } finally {
      this.syncPending = false;
    }
  }
  private onApplication(line: string) {
    try {
      if (line.startsWith("EVENT,")) {
        this.active.event(line);
        this.publish();
        if (!this.snapshotPending) {
          this.snapshotPending = true;
          void this.queue
            .run("active-snapshot", () => this.queryActive(), this.connection.signal)
            .catch((error) => this.publish({ error: errorText(error) }))
            .finally(() => {
              this.snapshotPending = false;
            });
        }
      } else if (line.startsWith("ACTIVE_PASS")) {
        this.active.snapshot(line);
        this.publish();
      }
      this.sync?.receive(line);
    } catch (error) {
      this.disconnect(false);
      this.publish({ error: errorText(error) });
    }
  }
  private async applyPolicy() {
    const policy = this.state.policy;
    if (
      policy.revision === 0 ||
      (policy.appliedRevision === policy.revision && policy.appliedDate === localDate())
    )
      return;
    const commands = buildPolicyTransfer(
      new Date(),
      policy,
      this.state.periods,
      this.state.exceptions,
    );
    const signal = this.connection.signal;
    await this.session!.request(
      `SET,MAX_ACTIVE_PASSES,${policy.capacity}`,
      (l) => l === `SETTINGS_ACK,MAX_ACTIVE_PASSES,${policy.capacity}`,
      signal,
    );
    for (const command of commands) {
      const expected = command.startsWith("POLICY_BEGIN")
        ? "POLICY_ACK,BEGIN"
        : command.startsWith("POLICY_WINDOW")
          ? "POLICY_ACK,WINDOW"
          : `POLICY_ACK,COMMIT,${commands.length - 2}`;
      await this.session!.request(command, (l) => l === expected, signal);
    }
    const applied = { ...policy, appliedRevision: policy.revision, appliedDate: localDate() };
    await this.db!.put("policy_rules", applied);
    this.publish({ policy: applied });
  }
  saveWorkspace(workspace: Workspace) {
    return this.action("workspace", async () => {
      await this.workspaceRepo!.save(workspace);
      await this.requestPersistence();
    });
  }
  savePolicy(policy: Policy, periods: BellPeriod[], exceptions: ScheduleException[]) {
    return this.action("policy", async () => {
      buildPolicyTransfer(new Date(), policy, periods, exceptions);
      await this.workspaceRepo!.savePolicy(policy, periods, exceptions);
      await this.refresh();
      if (this.session?.state === "Authenticated") await this.applyPolicy();
    });
  }
  saveRoster(students: Student[], enrollments: Enrollment[], replace = false) {
    return this.action("roster", async () => {
      await new RosterService(this.db!).save(
        this.state.workspace!.workspaceId,
        students,
        enrollments,
        replace,
      );
      await this.requestPersistence();
    });
  }
  removeStudent(id: string) {
    return this.action("roster", () =>
      new RosterService(this.db!).remove(this.state.workspace!.workspaceId, id),
    );
  }
  settings(name: string, max: number) {
    return this.action("settings", async () => {
      const newName = terminalName(name);
      if (!Number.isInteger(max) || max < 4 || max > 16) throw new HallzeeError("INVALID_MAX_ID");
      const session = this.session!,
        signal = this.connection.signal;
      await session.request(
        `SET,TERMINAL_NAME,${newName}`,
        (l) => l === `SETTINGS_ACK,TERMINAL_NAME,${newName}`,
        signal,
      );
      const renamed = { ...this.state.terminal!, customName: newName };
      await this.db!.put("terminals", renamed);
      this.publish({ terminal: renamed });
      await session.request(
        `SET,MAX_ID_LENGTH,${max}`,
        (l) => l === `SETTINGS_ACK,MAX_ID_LENGTH,${max}`,
        signal,
      );
      await this.db!.put("terminals", {
        ...renamed,
        maxIdLength: max,
        settingsReadUtc: new Date().toISOString(),
      });
    });
  }
  checkIn(studentId: string) {
    return this.action("check-in", async () => {
      if (!this.active.fresh || !this.active.passes.some((p) => p.studentId === studentId))
        throw new HallzeeError("STALE_PASS", "Refresh pass status before checking in.");
      const line = await this.session!.request(
        `MANUAL_CHECKIN,${studentId}`,
        (l) => l.startsWith(`EVENT,CHECKIN,${studentId},`),
        this.connection.signal,
      );
      this.active.event(line);
      await this.queryActive();
      await this.sync!.drain();
    });
  }
  unpair() {
    return this.action("release", async () => {
      this.reconnect?.stop();
      await this.initialize();
      if (!this.active.fresh || this.active.passes.length)
        throw new HallzeeError("ACTIVE_PASS", "Check in every active pass before unpairing.");
      const id = this.state.terminal!.terminalId;
      await this.session!.request(
        "RELEASE_OWNER",
        (l) => l === "OWNER_RELEASED",
        this.connection.signal,
      );
      await this.credentials!.release(id);
      this.device = undefined;
      this.disconnect();
    });
  }
  recover() {
    return this.action("recovery", async () => {
      await this.tripRepo!.recover(this.state.terminal!.terminalId);
      if (this.session?.state === "Authenticated") await this.initialize();
    });
  }
  resetHistory() {
    return this.action("reset-history", async () => {
      if (this.session?.state === "Authenticated") throw new HallzeeError("DISCONNECT_FIRST");
      await this.tripRepo!.resetHistory(this.state.terminal!.terminalId);
    });
  }
  async prepareUpdate() {
    // Preserve reconnect preference; wait for durable writes before activation.
    this.disconnect(false);
    return this.action("update", async () => {
      await this.sync?.drain();
    });
  }
  async backup() {
    this.disconnect();
    let text = "";
    const ok = await this.action("backup", async () => {
      await this.sync?.drain();
      text = await new BackupService(this.db!).export();
      await this.db!.setMeta("lastBackupUtc", new Date().toISOString());
    });
    return ok ? text : null;
  }
  restore(backup: Backup) {
    this.disconnect();
    return this.action("restore", async () => {
      await this.sync?.drain();
      await new BackupService(this.db!).restore(backup);
    });
  }
  async requestPersistence() {
    if (!this.db || (await this.db.meta("persistenceRequested", false))) return;
    await this.db.setMeta("persistenceRequested", true);
    await navigator.storage.persist?.().catch(() => false);
  }
  stop() {
    this.stopped = true;
    this.lifetime.abort();
    this.reconnect?.stop();
    this.connection.abort();
    this.sync?.dispose();
    this.session?.dispose();
    clearInterval(this.timer);
    clearTimeout(this.refreshTimer);
    this.db?.close();
    this.lock.close();
    this.changes.clear();
  }
}
