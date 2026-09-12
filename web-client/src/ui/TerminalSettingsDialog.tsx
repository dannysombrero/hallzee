import { useState, type FormEvent } from "react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { ConnectionDialog } from "./ConnectionDialog";
import { validateBackup, type Backup } from "../storage/BackupService";
import { download } from "../domain/TripReports";
import { errorText } from "../app/errors";
import {
  Users,
  Radio,
  HardDrive,
  ShieldCheck,
  Pencil,
  Check,
  X,
  RefreshCw,
  AlertTriangle,
  Download,
} from "lucide-react";

type SettingsTab = "profile" | "device" | "data";

export function TerminalSettingsDialog({
  onClose,
  initialTab = "device",
}: {
  onClose: () => void;
  initialTab?: SettingsTab;
}) {
  const { controller, state } = useHallzee();
  const [activeTab, setActiveTab] = useState<SettingsTab>(initialTab);
  const [pairTerminal, setPairTerminal] = useState(false);

  // Classroom Profile State
  const [workspace, setWorkspace] = useState(
    state.workspace ?? {
      workspaceId: "default",
      name: "My classroom",
      teacher: "",
      school: "",
      room: "",
      timeZone: "America/New_York",
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    },
  );
  const [profileStatus, setProfileStatus] = useState("");

  // Terminal / Device State
  const [isEditingName, setIsEditingName] = useState(false);
  const [customName, setCustomName] = useState(state.terminal?.customName ?? "");
  const [maxIdLength, setMaxIdLength] = useState(state.terminal?.maxIdLength ?? 10);
  const [deviceStatus, setDeviceStatus] = useState("");

  // Storage / Backup State
  const [backup, setBackup] = useState<Backup | null>(null);
  const [backupError, setBackupError] = useState("");

  const connected = state.session === "Authenticated";
  const hasStudentsOut = state.active.passes.length > 0;

  // Save Classroom Profile
  const handleSaveProfile = async (e?: FormEvent) => {
    if (e) e.preventDefault();
    if (!controller) return;
    try {
      await controller.saveWorkspace(workspace);
      setProfileStatus("Classroom information saved.");
      setTimeout(() => setProfileStatus(""), 3000);
    } catch (err) {
      setProfileStatus(errorText(err));
    }
  };

  // Save Terminal Settings (Name & ID Limit)
  const handleSaveTerminalSettings = async () => {
    if (!controller) return;
    try {
      await controller.settings(customName, maxIdLength);
      setIsEditingName(false);
      setDeviceStatus("Terminal settings applied.");
      setTimeout(() => setDeviceStatus(""), 3000);
    } catch (err) {
      setDeviceStatus(errorText(err));
    }
  };

  // Download Classroom Backup
  const handleSaveBackup = async () => {
    if (
      !window.confirm(
        "Download an unencrypted backup containing student data? Choose a district-approved local destination. Bluetooth will disconnect for a consistent snapshot.",
      )
    )
      return;
    const text = await controller?.backup();
    if (text) download(text, "hallzee-classroom-backup.json");
  };

  if (pairTerminal) return <ConnectionDialog onClose={onClose} />;

  return (
    <Dialog
      title="Settings"
      subtitle="Configure this teacher workspace and its assigned kiosk."
      size="default"
      onClose={onClose}
    >
      {/* 1. SUBMENU TAB SELECTOR */}
      <div className="settings-subnav-bar">
        <button
          type="button"
          className={`settings-tab-btn ${activeTab === "profile" ? "active" : ""}`}
          onClick={() => setActiveTab("profile")}
        >
          <Users size={14} />
          <span>Teacher</span>
        </button>
        <button
          type="button"
          className={`settings-tab-btn ${activeTab === "device" ? "active" : ""}`}
          onClick={() => setActiveTab("device")}
        >
          <Radio size={14} />
          <span>Device</span>
        </button>
        <button
          type="button"
          className={`settings-tab-btn ${activeTab === "data" ? "active" : ""}`}
          onClick={() => setActiveTab("data")}
        >
          <HardDrive size={14} />
          <span>Storage & Backup</span>
        </button>
      </div>

      {/* 2. TAB 1: TEACHER / CLASSROOM INFORMATION */}
      {activeTab === "profile" && (
        <form onSubmit={handleSaveProfile} className="flex flex-col gap-4">
          <div className="settings-card">
            <div className="settings-card-title">Classroom & Teacher Information</div>
            <p className="text-12 text-slate-500 -mt-1">
              Set your classroom details to display in the application header.
            </p>

            <div className="flex flex-col gap-3 pt-1">
              <div className="settings-row">
                <label htmlFor="settings-name">Classroom name</label>
                <input
                  id="settings-name"
                  className="pill-input text-13"
                  placeholder="e.g. My classroom"
                  required
                  maxLength={200}
                  value={workspace.name}
                  onChange={(e) => setWorkspace({ ...workspace, name: e.target.value })}
                />
              </div>

              <div className="policies-divider" />

              <div className="settings-row">
                <label htmlFor="settings-teacher">Teacher Name:</label>
                <input
                  id="settings-teacher"
                  className="pill-input text-13"
                  placeholder="e.g. Herrero"
                  value={workspace.teacher}
                  onChange={(e) => setWorkspace({ ...workspace, teacher: e.target.value })}
                />
              </div>

              <div className="policies-divider" />

              <div className="settings-row">
                <label htmlFor="settings-school">School:</label>
                <input
                  id="settings-school"
                  className="pill-input text-13"
                  placeholder="e.g. GPMS"
                  value={workspace.school}
                  onChange={(e) => setWorkspace({ ...workspace, school: e.target.value })}
                />
              </div>

              <div className="policies-divider" />

              <div className="settings-row">
                <label htmlFor="settings-room">Room:</label>
                <input
                  id="settings-room"
                  className="pill-input text-13"
                  placeholder="e.g. Room 204"
                  value={workspace.room}
                  onChange={(e) => setWorkspace({ ...workspace, room: e.target.value })}
                />
              </div>

              <div className="policies-divider" />

              <div className="settings-row">
                <label htmlFor="settings-tz">Time Zone:</label>
                <input
                  id="settings-tz"
                  className="pill-input text-13"
                  placeholder="e.g. America/New_York"
                  value={workspace.timeZone}
                  onChange={(e) => setWorkspace({ ...workspace, timeZone: e.target.value })}
                />
              </div>
            </div>
          </div>

          {/* Header Preview Card */}
          <div className="settings-preview-card">
            <ShieldCheck size={20} color="#0284c7" />
            <div className="flex flex-col gap-0.5">
              <span className="text-11 font-bold text-sky-700">Header Bar Display:</span>
              <span className="text-13 font-semibold text-slate-800">
                Hallzee Web Client | {workspace.room || "Room 204"} – {workspace.teacher || "Rodriguez"} – {workspace.school || "GPMS"}
              </span>
            </div>
          </div>

          {/* Footer */}
          <div className="settings-footer">
            <span className="text-12 font-semibold text-sky-700">{profileStatus}</span>
            <button
              type="submit"
              className="hallzee-pill-btn"
              disabled={state.busy}
            >
              Save classroom
            </button>
          </div>

          {/* Local Storage Card */}
          <div className="settings-card">
            <div className="settings-card-title">Stored On This Computer</div>
            <div className="flex items-center justify-between">
              <div>
                <div className="text-13 font-semibold text-slate-800">
                  {state.persistent
                    ? "Persistent storage granted"
                    : "Persistent storage not granted — keep a current backup"}
                </div>
                <p className="text-12 text-slate-500 mt-0.5">
                  {(state.usage / 1048576).toFixed(1)} MiB used · {(state.quota / 1073741824).toFixed(1)} GiB estimated quota.
                </p>
              </div>
              <button
                type="button"
                className="hallzee-pill-btn"
                disabled={state.busy}
                onClick={() => void handleSaveBackup()}
              >
                <Download size={13} className="inline mr-1" />
                Download classroom backup
              </button>
            </div>
          </div>

          {/* Restore Backup Card */}
          <div className="settings-card">
            <div className="settings-card-title">Restore Classroom Backup</div>
            <p className="text-12 text-slate-500 -mt-1">
              Restore a previously saved Hallzee JSON backup to replace or migrate classroom data.
            </p>

            <label className="flex flex-col gap-1 text-12 font-semibold text-slate-700 cursor-pointer">
              <span>Restore a web-client backup</span>
              <input
                type="file"
                accept=".json,application/json"
                className="pill-input text-12"
                disabled={state.busy}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  setBackupError("");
                  if (file.size > 50 * 1024 * 1024) {
                    setBackupError("Backup exceeds 50 MiB.");
                    return;
                  }
                  void file
                    .text()
                    .then((text) => {
                      setBackup(validateBackup(text));
                    })
                    .catch((err) => setBackupError(errorText(err)));
                }}
              />
            </label>

            {backupError && (
              <p role="alert" className="error text-12">
                {backupError}
              </p>
            )}

            {/* Review Replacement Drawer */}
            {backup && (
              <div className="info-box-danger flex-col items-start gap-2">
                <div className="font-bold text-13">Review replacement</div>
                <div className="text-12">
                  {backup.workspaces[0].name} · {backup.trips.length} trips · {backup.rosterStudents.length} students · exported {new Date(backup.exportedAtUtc).toLocaleString()}
                </div>
                <p className="text-11 opacity-90">
                  This replaces classroom data. Owner keys are not in backups. This browser’s existing keys are retained; another browser must claim the terminal separately.
                </p>
                <button
                  type="button"
                  className="btn-danger-red text-12"
                  disabled={state.busy}
                  onClick={() => {
                    if (
                      window.confirm(
                        "Replace local classroom data with this backup? Download a current backup first if you need it.",
                      )
                    ) {
                      void controller?.restore(backup).then((ok) => {
                        if (ok) {
                          setBackup(null);
                          onClose();
                        }
                      });
                    }
                  }}
                >
                  Replace local classroom data
                </button>
              </div>
            )}
          </div>
        </form>
      )}

      {/* 3. TAB 2: DEVICE / KIOSK SETTINGS */}
      {activeTab === "device" && (
        <div className="flex flex-col gap-4">
          {!state.terminal ? (
            <div className="hallzee-table-empty">
              <Radio size={36} color="#94a3b8" />
              <strong>No terminal paired</strong>
              <span>Connect a terminal to configure friendly name, keypad limits, and device management.</span>
              <button
                type="button"
                className="hallzee-pill-btn mt-2"
                onClick={() => setPairTerminal(true)}
              >
                Pair Terminal
              </button>
            </div>
          ) : (
            <>
              {/* Firmware & Updates */}
              <div className="settings-card">
                <div className="settings-card-title">Firmware & Updates</div>
                <div className="text-13 font-semibold text-slate-700">
                  Web client: 1.2.0-dev.1 · Terminal: {state.terminal.protocolVersion ? `Protocol v${state.terminal.protocolVersion}` : "v1.2.0"}
                </div>
                <p className="text-12 text-slate-500 -mt-1">
                  Updates install over the air via Bluetooth. Your classroom data stays on this Chromebook.
                </p>
                <div className="flex items-center gap-3">
                  <a
                    href="https://github.com/dannysombrero/hallzee/releases"
                    target="_blank"
                    rel="noreferrer"
                    className="preset-pill-btn"
                  >
                    Firmware downloads
                  </a>
                  <button
                    type="button"
                    className="preset-pill-btn"
                    disabled={!connected || state.busy}
                    onClick={() => controller?.syncNow()}
                  >
                    <RefreshCw size={11} className="inline mr-1" />
                    Refresh status
                  </button>
                </div>
              </div>

              {/* Student ID Keypad Limit */}
              <div className="settings-card">
                <div className="settings-card-title">Student ID Keypad Limit</div>
                <p className="text-12 text-slate-500 -mt-1">
                  Set the maximum number of digits students can type at the kiosk (4–16 digits).
                </p>
                <div className="flex items-center justify-between pt-1">
                  <span className="text-13 font-semibold text-slate-700">Max Student ID Length:</span>
                  <div className="flex items-center gap-3">
                    <div className="hallzee-stepper">
                      <input
                        type="number"
                        min={4}
                        max={16}
                        value={maxIdLength}
                        onChange={(e) =>
                          setMaxIdLength(Math.max(4, Math.min(16, Number(e.target.value))))
                        }
                      />
                      <div className="hallzee-stepper-controls">
                        <button
                          type="button"
                          className="hallzee-stepper-btn"
                          onClick={() => setMaxIdLength(Math.min(16, maxIdLength + 1))}
                        >
                          ▲
                        </button>
                        <button
                          type="button"
                          className="hallzee-stepper-btn"
                          onClick={() => setMaxIdLength(Math.max(4, maxIdLength - 1))}
                        >
                          ▼
                        </button>
                      </div>
                    </div>
                    <button
                      type="button"
                      className="hallzee-pill-btn"
                      disabled={!connected || state.busy}
                      onClick={handleSaveTerminalSettings}
                    >
                      Apply ID Limit
                    </button>
                  </div>
                </div>
              </div>

              {/* Device Information */}
              <div className="settings-card">
                <div className="settings-card-title">Device Information</div>
                <div className="flex flex-col gap-3 pt-1">
                  {/* Terminal Name */}
                  <div className="settings-row">
                    <span className="text-12 text-slate-500 font-medium">Terminal Name:</span>
                    {isEditingName ? (
                      <div className="flex items-center gap-2">
                        <input
                          className="pill-input text-12 flex-1"
                          maxLength={24}
                          value={customName}
                          onChange={(e) => setCustomName(e.target.value)}
                          placeholder="e.g. Room #204"
                        />
                        <button
                          type="button"
                          className="hallzee-pill-btn p-1.5"
                          onClick={handleSaveTerminalSettings}
                          title="Save Name"
                        >
                          <Check size={13} />
                        </button>
                        <button
                          type="button"
                          className="hallzee-outline-btn p-1.5"
                          onClick={() => {
                            setCustomName(state.terminal?.customName ?? "");
                            setIsEditingName(false);
                          }}
                          title="Cancel"
                        >
                          <X size={13} />
                        </button>
                      </div>
                    ) : (
                      <div className="flex items-center justify-between">
                        <span className="text-13 font-semibold text-slate-800">
                          {state.terminal.customName || state.terminal.terminalId}
                        </span>
                        <button
                          type="button"
                          className="hallzee-outline-btn p-1.5"
                          onClick={() => setIsEditingName(true)}
                          title="Edit friendly name"
                          aria-label="Edit friendly name"
                        >
                          <Pencil size={13} />
                        </button>
                      </div>
                    )}
                  </div>

                  <div className="policies-divider" />

                  {/* Unique ID */}
                  <div className="settings-row">
                    <span className="text-12 text-slate-500 font-medium">Unique Device ID:</span>
                    <span className="text-12 font-mono font-bold text-slate-700 select-all">
                      {state.terminal.terminalId}
                    </span>
                  </div>

                  <div className="policies-divider" />

                  {/* Connection Type */}
                  <div className="settings-row">
                    <span className="text-12 text-slate-500 font-medium">Connection type:</span>
                    <span className="text-12 font-semibold text-slate-700">
                      Bluetooth Low Energy (Web Bluetooth API)
                    </span>
                  </div>

                  <div className="policies-divider" />

                  {/* Connection Status */}
                  <div className="settings-row">
                    <span className="text-12 text-slate-500 font-medium">Connection:</span>
                    <span
                      className={`status-pill ${
                        connected ? "active" : "offline"
                      } text-11`}
                    >
                      {connected ? "CONNECTED" : "DISCONNECTED"}
                    </span>
                  </div>
                </div>
              </div>

              {/* Unpair / Disconnect Card */}
              <div className="settings-card">
                <div className="flex items-start gap-3">
                  <AlertTriangle size={18} color="#d97706" className="shrink-0 mt-0.5" />
                  <div className="flex flex-col gap-1">
                    <span className="text-12 font-semibold text-slate-700">
                      Disconnecting will unpair this computer from the terminal. You will need pairing mode and a new passkey to connect again.
                    </span>
                    {hasStudentsOut && (
                      <span className="text-12 font-bold text-amber-700">
                        Check in all active passes before unpairing.
                      </span>
                    )}
                  </div>
                </div>

                <div className="flex items-center gap-3 pt-1">
                  <button
                    type="button"
                    className="hallzee-outline-btn"
                    disabled={state.busy || !connected}
                    onClick={() => controller?.disconnect()}
                  >
                    Disconnect
                  </button>
                  <button
                    type="button"
                    className="btn-danger-red"
                    disabled={state.busy || !connected}
                    onClick={() => {
                      if (
                        window.confirm(
                          "Sync this classroom, release ownership, and disconnect? History and roster stay here. No passes may be active. This does not sanitize the terminal for another teacher.",
                        )
                      ) {
                        void controller?.unpair();
                        onClose();
                      }
                    }}
                  >
                    Disconnect & Unpair
                  </button>
                </div>
              </div>
            </>
          )}

          {/* Footer */}
          <div className="settings-footer">
            <span className="text-12 font-semibold text-sky-700">{deviceStatus}</span>
          </div>
        </div>
      )}

      {/* 4. TAB 3: STORAGE & BACKUP */}
      {activeTab === "data" && (
        <div className="flex flex-col gap-4">
          {/* Local Storage Card */}
          <div className="settings-card">
            <div className="settings-card-title">Stored On This Computer</div>
            <div className="flex items-center justify-between">
              <div>
                <div className="text-13 font-semibold text-slate-800">
                  {state.persistent
                    ? "Persistent storage granted"
                    : "Persistent storage not granted — keep a current backup"}
                </div>
                <p className="text-12 text-slate-500 mt-0.5">
                  {(state.usage / 1048576).toFixed(1)} MiB used · {(state.quota / 1073741824).toFixed(1)} GiB estimated quota.
                </p>
              </div>
              <button
                type="button"
                className="hallzee-pill-btn"
                disabled={state.busy}
                onClick={() => void handleSaveBackup()}
              >
                <Download size={13} className="inline mr-1" />
                Download classroom backup
              </button>
            </div>
          </div>

          {/* Restore Backup Card */}
          <div className="settings-card">
            <div className="settings-card-title">Restore Classroom Backup</div>
            <p className="text-12 text-slate-500 -mt-1">
              Restore a previously saved Hallzee JSON backup to replace or migrate classroom data.
            </p>

            <label className="flex flex-col gap-1 text-12 font-semibold text-slate-700 cursor-pointer">
              <span>Restore a web-client backup</span>
              <input
                type="file"
                accept=".json,application/json"
                className="pill-input text-12"
                disabled={state.busy}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  setBackupError("");
                  if (file.size > 50 * 1024 * 1024) {
                    setBackupError("Backup exceeds 50 MiB.");
                    return;
                  }
                  void file
                    .text()
                    .then((text) => {
                      setBackup(validateBackup(text));
                    })
                    .catch((err) => setBackupError(errorText(err)));
                }}
              />
            </label>

            {backupError && (
              <p role="alert" className="error text-12">
                {backupError}
              </p>
            )}

            {/* Review Replacement Drawer */}
            {backup && (
              <div className="info-box-danger flex-col items-start gap-2">
                <div className="font-bold text-13">Review replacement</div>
                <div className="text-12">
                  {backup.workspaces[0].name} · {backup.trips.length} trips · {backup.rosterStudents.length} students · exported {new Date(backup.exportedAtUtc).toLocaleString()}
                </div>
                <p className="text-11 opacity-90">
                  This replaces classroom data. Owner keys are not in backups. This browser’s existing keys are retained; another browser must claim the terminal separately.
                </p>
                <button
                  type="button"
                  className="btn-danger-red text-12"
                  disabled={state.busy}
                  onClick={() => {
                    if (
                      window.confirm(
                        "Replace local classroom data with this backup? Download a current backup first if you need it.",
                      )
                    ) {
                      void controller?.restore(backup).then((ok) => {
                        if (ok) {
                          setBackup(null);
                          onClose();
                        }
                      });
                    }
                  }}
                >
                  Replace local classroom data
                </button>
              </div>
            )}
          </div>

          {/* Terminal History Recovery (when terminal present) */}
          {state.terminal && (
            <div className="settings-card">
              <div className="settings-card-title">Terminal History Recovery</div>
              <p className="text-12 text-slate-500 -mt-1">
                Replay records still on this terminal. Identical records are deduplicated; conflicting history stops for review.
              </p>
              <div className="flex items-center gap-3">
                <button
                  type="button"
                  className="hallzee-outline-btn"
                  disabled={state.busy}
                  onClick={() => {
                    if (window.confirm(`Recover all history from ${state.terminal?.terminalId}?`))
                      void controller?.recover();
                  }}
                >
                  Recover terminal history
                </button>
                <button
                  type="button"
                  className="btn-danger-red"
                  disabled={state.busy || state.session === "Authenticated"}
                  onClick={() => {
                    if (
                      window.confirm(
                        "Have you exported the old terminal history? Remove its local trip rows and restart the cursor at zero for a known reset?",
                      )
                    )
                      void controller?.resetHistory();
                  }}
                >
                  Start a new terminal history
                </button>
              </div>
            </div>
          )}

          {/* Legal / Notices */}
          <div className="pt-2 text-11 text-slate-400 border-t border-slate-100 flex flex-col gap-1">
            <span>
              Hallzee 1.2.0-dev.1 · Browser hardware acceptance pending. No student records or owner credentials are uploaded.
            </span>
            <div className="flex items-center gap-2">
              <a href="/LICENSE.txt" target="_blank" rel="noreferrer" className="text-sky-600 underline">
                License
              </a>
              <span>·</span>
              <a href="/THIRD-PARTY-NOTICES.md" target="_blank" rel="noreferrer" className="text-sky-600 underline">
                Third-party notices
              </a>
            </div>
          </div>
        </div>
      )}
    </Dialog>
  );
}
