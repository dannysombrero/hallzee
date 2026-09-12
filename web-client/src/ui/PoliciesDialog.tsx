import { useState, useRef, useMemo, type FormEvent } from "react";
import {
  ShieldCheck,
  Calendar,
  Plus,
  Trash2,
  Pencil,
  Volume2,
  Check,
  X,
  Tag,
  Download,
  RotateCw,
  Clock,
  UserX,
} from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { weekdays } from "../domain/PolicyScheduleService";
import type { BellPeriod, Decision, Policy, ScheduleException } from "../storage/schema";

const ALERT_SOUNDS = [
  "Chime",
  "Bell",
  "Soft alert",
  "Marimba",
  "Subtle Ping",
  "Digital Watch",
  "Gentle Knock",
  "Harp Ascend",
];

type PolicyMode = "Windows" | "NoPasses" | "NoRules";

function formatTime12h(timeStr: string) {
  if (!timeStr) return "—";
  const [hStr, mStr] = timeStr.split(":");
  let h = parseInt(hStr, 10);
  const m = mStr || "00";
  if (isNaN(h)) return timeStr;
  const ampm = h >= 12 ? "PM" : "AM";
  h = h % 12;
  if (h === 0) h = 12;
  return `${String(h).padStart(2, "0")}:${m} ${ampm}`;
}

function playAlertSound(sound: string, volume: number) {
  try {
    const AudioCtx =
      window.AudioContext ||
      (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!AudioCtx) return;
    const ctx = new AudioCtx();
    const gain = ctx.createGain();
    const vol = Math.max(0.01, Math.min(1, volume / 100));
    gain.gain.value = vol;
    gain.connect(ctx.destination);

    const now = ctx.currentTime;
    switch (sound) {
      case "Bell": {
        const osc = ctx.createOscillator();
        osc.type = "sine";
        osc.frequency.setValueAtTime(784, now);
        gain.gain.setValueAtTime(vol, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.8);
        osc.connect(gain);
        osc.start(now);
        osc.stop(now + 0.85);
        break;
      }
      case "Soft alert": {
        const osc1 = ctx.createOscillator();
        osc1.frequency.setValueAtTime(523.25, now);
        osc1.connect(gain);
        osc1.start(now);
        osc1.stop(now + 0.15);

        const osc2 = ctx.createOscillator();
        osc2.frequency.setValueAtTime(659.25, now + 0.15);
        osc2.connect(gain);
        osc2.start(now + 0.15);
        osc2.stop(now + 0.4);
        break;
      }
      case "Subtle Ping": {
        const osc = ctx.createOscillator();
        osc.frequency.setValueAtTime(1046.5, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.3);
        osc.connect(gain);
        osc.start(now);
        osc.stop(now + 0.35);
        break;
      }
      case "Digital Watch": {
        for (let i = 0; i < 2; i++) {
          const t = now + i * 0.1;
          const osc = ctx.createOscillator();
          osc.type = "square";
          osc.frequency.setValueAtTime(2093, t);
          const bGain = ctx.createGain();
          bGain.gain.value = vol * 0.3;
          osc.connect(bGain);
          bGain.connect(ctx.destination);
          osc.start(t);
          osc.stop(t + 0.05);
        }
        break;
      }
      case "Marimba": {
        const osc = ctx.createOscillator();
        osc.type = "triangle";
        osc.frequency.setValueAtTime(440, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.25);
        osc.connect(gain);
        osc.start(now);
        osc.stop(now + 0.28);
        break;
      }
      case "Harp Ascend": {
        const notes = [523.25, 659.25, 783.99, 1046.5];
        notes.forEach((freq, idx) => {
          const t = now + idx * 0.08;
          const osc = ctx.createOscillator();
          osc.frequency.setValueAtTime(freq, t);
          gain.gain.setValueAtTime(vol * 0.8, t);
          gain.gain.exponentialRampToValueAtTime(0.001, t + 0.3);
          osc.connect(gain);
          osc.start(t);
          osc.stop(t + 0.35);
        });
        break;
      }
      case "Gentle Knock": {
        const osc = ctx.createOscillator();
        osc.type = "sine";
        osc.frequency.setValueAtTime(180, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.1);
        osc.connect(gain);
        osc.start(now);
        osc.stop(now + 0.12);
        break;
      }
      case "Chime":
      default: {
        const freqs = [587.33, 880];
        freqs.forEach((f) => {
          const osc = ctx.createOscillator();
          osc.frequency.setValueAtTime(f, now);
          gain.gain.exponentialRampToValueAtTime(0.001, now + 0.6);
          osc.connect(gain);
          osc.start(now);
          osc.stop(now + 0.65);
        });
        break;
      }
    }
  } catch {
    // AudioContext blocked or unsupported in environment
  }
}

export function PoliciesDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Policy form state
  const [policy, setPolicy] = useState<Policy>(state.policy);
  const [periods, setPeriods] = useState<BellPeriod[]>(state.periods);
  const [exceptions, setExceptions] = useState<ScheduleException[]>(state.exceptions);

  // Pass policy mode: Windows, NoPasses, NoRules
  const [policyMode, setPolicyMode] = useState<PolicyMode>(() => {
    if (policy.firstAction === "Lock" && policy.lastAction === "Lock" && policy.firstMinutes >= 30) {
      return "NoPasses";
    }
    if (policy.firstAction === "Allow" && policy.lastAction === "Allow" && policy.firstMinutes === 0 && policy.lastMinutes === 0) {
      return "NoRules";
    }
    return "Windows";
  });

  const [middleAction, setMiddleAction] = useState<Decision>("Allow");

  // Secondary audio & transition settings
  const [bellTransitionEnabled, setBellTransitionEnabled] = useState(true);
  const [warningSoundEnabled, setWarningSoundEnabled] = useState(true);
  const [selectedSound, setSelectedSound] = useState(ALERT_SOUNDS[0]);
  const [soundVolume, setSoundVolume] = useState(80);

  // Workspace actions state
  const [isRenaming, setIsRenaming] = useState(false);
  const [renameValue, setRenameValue] = useState("");
  const [isCreating, setIsCreating] = useState(false);
  const [newWorkspaceName, setNewWorkspaceName] = useState("");
  const [statusMessage, setStatusMessage] = useState("");

  // Sort periods: earliest vs latest
  const [earliestFirst, setEarliestFirst] = useState(true);

  // Editing period state
  const [editingScheduleId, setEditingScheduleId] = useState<string | null>(null);
  const [editPeriodName, setEditPeriodName] = useState("");
  const [editStartTime, setEditStartTime] = useState("");
  const [editEndTime, setEditEndTime] = useState("");
  const [editClassSection, setEditClassSection] = useState("");
  const [editScheduleName, setEditScheduleName] = useState("Regular");
  const [editDays, setEditDays] = useState<string[]>(["Mon", "Tue", "Wed", "Thu", "Fri"]);

  // Date exception input state
  const [newExceptionDate, setNewExceptionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [newExceptionSchedule, setNewExceptionSchedule] = useState("Regular");
  const [newExceptionNoSchool, setNewExceptionNoSchool] = useState(false);

  // Sorting periods
  const sortedPeriods = useMemo(() => {
    const list = [...periods];
    list.sort((a, b) => {
      const cmp = a.start.localeCompare(b.start);
      return earliestFirst ? cmp : -cmp;
    });
    return list;
  }, [periods, earliestFirst]);

  // Mode changes
  const handleModeChange = (mode: PolicyMode) => {
    setPolicyMode(mode);
    if (mode === "NoPasses") {
      setPolicy((prev) => ({
        ...prev,
        firstMinutes: 30,
        lastMinutes: 30,
        firstAction: "Lock",
        lastAction: "Lock",
      }));
      setMiddleAction("Lock");
    } else if (mode === "NoRules") {
      setPolicy((prev) => ({
        ...prev,
        firstMinutes: 0,
        lastMinutes: 0,
        firstAction: "Allow",
        lastAction: "Allow",
      }));
      setMiddleAction("Allow");
    } else {
      setPolicy((prev) => ({
        ...prev,
        firstMinutes: 10,
        lastMinutes: 10,
        firstAction: "Warn",
        lastAction: "Warn",
      }));
      setMiddleAction("Allow");
    }
  };

  // Presets
  const applyPreset = (preset: "10/10" | "StartEnd" | "Warnings") => {
    setPolicyMode("Windows");
    if (preset === "10/10") {
      setPolicy((prev) => ({
        ...prev,
        firstMinutes: 10,
        lastMinutes: 10,
        firstAction: "Lock",
        lastAction: "Lock",
      }));
      setMiddleAction("Allow");
    } else if (preset === "StartEnd") {
      setPolicy((prev) => ({
        ...prev,
        firstMinutes: 10,
        lastMinutes: 10,
        firstAction: "Allow",
        lastAction: "Allow",
      }));
      setMiddleAction("Lock");
    } else if (preset === "Warnings") {
      setPolicy((prev) => ({
        ...prev,
        firstMinutes: 10,
        lastMinutes: 10,
        firstAction: "Warn",
        lastAction: "Warn",
      }));
      setMiddleAction("Allow");
    }
  };

  // Stepper handlers
  const updateCapacity = (delta: number) => {
    setPolicy((prev) => ({
      ...prev,
      capacity: Math.max(1, Math.min(8, prev.capacity + delta)),
    }));
  };

  const updateWarningMins = (delta: number) => {
    setPolicy((prev) => {
      const currentMins = Math.round(prev.warningSeconds / 60);
      const nextMins = Math.max(1, Math.min(60, currentMins + delta));
      return { ...prev, warningSeconds: nextMins * 60 };
    });
  };

  const updateDailyGuideline = (delta: number) => {
    setPolicy((prev) => ({
      ...prev,
      dailyGuideline: Math.max(0, Math.min(20, prev.dailyGuideline + delta)),
    }));
  };

  const updateFirstMinutes = (delta: number) => {
    setPolicy((prev) => ({
      ...prev,
      firstMinutes: Math.max(0, Math.min(30, prev.firstMinutes + delta)),
    }));
  };

  const updateLastMinutes = (delta: number) => {
    setPolicy((prev) => ({
      ...prev,
      lastMinutes: Math.max(0, Math.min(30, prev.lastMinutes + delta)),
    }));
  };

  // Workspace actions
  const handleSaveRename = async () => {
    if (!renameValue.trim() || !state.workspace) return;
    try {
      await controller?.saveWorkspace({
        ...state.workspace,
        name: renameValue.trim(),
      });
      setIsRenaming(false);
      setStatusMessage(`Workspace renamed to "${renameValue.trim()}".`);
    } catch {
      setStatusMessage("Failed to rename workspace.");
    }
  };

  const handleExportWorkspace = () => {
    const data = JSON.stringify(
      {
        workspace: state.workspace,
        policy,
        periods,
        exceptions,
      },
      null,
      2,
    );
    const blob = new Blob([data], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "workspace.hallzee.json";
    a.click();
    URL.revokeObjectURL(url);
    setStatusMessage("Workspace exported with rules, schedules, and date exceptions.");
  };

  const handleImportFile = async (file?: File) => {
    if (!file) return;
    try {
      const text = await file.text();
      const parsed = JSON.parse(text);
      if (parsed.policy) setPolicy(parsed.policy);
      if (Array.isArray(parsed.periods)) setPeriods(parsed.periods);
      if (Array.isArray(parsed.exceptions)) setExceptions(parsed.exceptions);
      setStatusMessage("Workspace imported. Rules and bell schedules are ready.");
    } catch {
      setStatusMessage("Failed to import workspace JSON.");
    }
  };

  // Period management
  const startEditPeriod = (p: BellPeriod) => {
    setEditingScheduleId(p.scheduleId);
    setEditPeriodName(p.periodName);
    setEditStartTime(p.start);
    setEditEndTime(p.end);
    setEditClassSection(p.classSection);
    setEditScheduleName(p.scheduleName || "Regular");
    setEditDays(p.weekdays.length ? p.weekdays : ["Mon", "Tue", "Wed", "Thu", "Fri"]);
  };

  const saveEditingPeriod = () => {
    if (!editingScheduleId) return;
    setPeriods(
      periods.map((p) =>
        p.scheduleId === editingScheduleId
          ? {
              ...p,
              periodName: editPeriodName.trim() || "Period",
              start: editStartTime.trim() || "09:00",
              end: editEndTime.trim() || "10:00",
              classSection: editClassSection.trim(),
              scheduleName: editScheduleName.trim() || "Regular",
              weekdays: editDays,
            }
          : p,
      ),
    );
    setEditingScheduleId(null);
  };

  const addPeriod = () => {
    const newId = crypto.randomUUID();
    const newP: BellPeriod = {
      workspaceId: policy.workspaceId,
      scheduleId: newId,
      periodName: `Period ${periods.length + 1}`,
      scheduleName: "Regular",
      classSection: "",
      start: "09:00",
      end: "10:00",
      weekdays: ["Mon", "Tue", "Wed", "Thu", "Fri"],
    };
    setPeriods([...periods, newP]);
    startEditPeriod(newP);
  };

  const removePeriod = (scheduleId: string) => {
    setPeriods(periods.filter((p) => p.scheduleId !== scheduleId));
  };

  // Date exception management
  const addException = () => {
    if (!newExceptionDate.trim()) return;
    setExceptions([
      ...exceptions,
      {
        workspaceId: policy.workspaceId,
        date: newExceptionDate.trim(),
        scheduleName: newExceptionNoSchool ? "" : newExceptionSchedule.trim(),
        isNoSchool: newExceptionNoSchool,
      },
    ]);
  };

  const removeException = (index: number) => {
    setExceptions(exceptions.filter((_, i) => i !== index));
  };

  // Form submit: Save Workspace Settings
  const handleSaveAll = (e: FormEvent) => {
    e.preventDefault();
    void controller?.savePolicy(policy, periods, exceptions).then((ok) => {
      if (ok) {
        setStatusMessage("Settings saved successfully.");
        onClose();
      }
    });
  };

  return (
    <Dialog
      title="Hall Pass Policies & Schedule"
      subtitle="Configure classroom pass limits, grace periods, and timetable."
      size="wide"
      onClose={onClose}
    >
      <form onSubmit={handleSaveAll} className="policies-form">
        {/* 1. WORKSPACE CONTROL TOOLBAR */}
        <div className="policies-toolbar">
          <div className="policies-toolbar-left">
            <Tag size={15} color="#0284c7" />
            <span className="font-bold text-13 text-slate-700">Workspace:</span>
            <select
              className="policies-workspace-select"
              value={state.workspace?.workspaceId ?? ""}
              aria-label="Workspace selection"
              disabled
            >
              <option value={state.workspace?.workspaceId ?? ""}>
                {state.workspace?.name ?? "Test"}
              </option>
            </select>

            <button
              type="button"
              className="preset-pill-btn"
              onClick={() => {
                setRenameValue(state.workspace?.name ?? "");
                setIsRenaming(!isRenaming);
                setIsCreating(false);
              }}
            >
              <Pencil size={11} className="inline mr-1" />
              Rename
            </button>

            <button
              type="button"
              className="preset-pill-btn"
              onClick={() => {
                setNewWorkspaceName("");
                setIsCreating(!isCreating);
                setIsRenaming(false);
              }}
            >
              <Plus size={11} className="inline mr-1" />
              New
            </button>

            <div className="policies-toolbar-divider" />

            <button
              type="button"
              className="preset-pill-btn"
              onClick={handleExportWorkspace}
            >
              <Download size={11} className="inline mr-1" />
              Export
            </button>

            <button
              type="button"
              className="preset-pill-btn"
              onClick={() => fileInputRef.current?.click()}
            >
              <RotateCw size={11} className="inline mr-1" />
              Import
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept=".json,application/json"
              hidden
              onChange={(e) => void handleImportFile(e.target.files?.[0])}
            />
          </div>

          <div className="policies-toolbar-right">
            <button
              type="submit"
              className="hallzee-pill-btn"
              disabled={state.busy}
            >
              Save Workspace Settings
            </button>
          </div>
        </div>

        {/* 2. DYNAMIC INLINE ACTION PANELS */}
        {isRenaming && (
          <div className="policies-inline-bar">
            <Pencil size={14} color="#0284c7" />
            <span className="text-12 font-semibold text-slate-700">Rename workspace:</span>
            <input
              className="pill-input"
              value={renameValue}
              onChange={(e) => setRenameValue(e.target.value)}
              placeholder="Enter new workspace name"
            />
            <button
              type="button"
              className="hallzee-pill-btn"
              onClick={() => void handleSaveRename()}
            >
              Save Name
            </button>
            <button
              type="button"
              className="hallzee-outline-btn"
              onClick={() => setIsRenaming(false)}
            >
              Cancel
            </button>
          </div>
        )}

        {isCreating && (
          <div className="policies-inline-bar">
            <Plus size={14} color="#0284c7" />
            <span className="text-12 font-semibold text-slate-700">New workspace name:</span>
            <input
              className="pill-input"
              value={newWorkspaceName}
              onChange={(e) => setNewWorkspaceName(e.target.value)}
              placeholder="Teacher name (e.g. Ms. Rivera)"
            />
            <button
              type="button"
              className="hallzee-pill-btn"
              onClick={() => {
                if (!newWorkspaceName.trim() || !state.workspace) return;
                void controller
                  ?.saveWorkspace({
                    ...state.workspace,
                    workspaceId: crypto.randomUUID(),
                    name: newWorkspaceName.trim(),
                  })
                  .then(() => {
                    setIsCreating(false);
                    setStatusMessage(`Created workspace "${newWorkspaceName.trim()}".`);
                  });
              }}
            >
              Create Workspace
            </button>
            <button
              type="button"
              className="hallzee-outline-btn"
              onClick={() => setIsCreating(false)}
            >
              Cancel
            </button>
          </div>
        )}

        {statusMessage && (
          <div className="policies-status-badge">
            <Check size={13} color="#0284c7" />
            <span>{statusMessage}</span>
          </div>
        )}

        {(state.error) && (
          <p role="alert" className="error mb-3">
            {state.error}
          </p>
        )}

        {/* 3. TWO-COLUMN MAIN CONTENT */}
        <div className="policies-scroll-container">
          <div className="policies-2col-grid">
            {/* LEFT COLUMN: PASS POLICY RULES & BELL-TIME WINDOWS */}
            <div className="policies-column">
              {/* Section Header: Pass Policy Rules */}
              <div className="policies-section-header">
                <div className="policies-section-title">
                  <ShieldCheck size={18} color="#0284c7" />
                  <span>Pass Policy Rules</span>
                </div>
              </div>

              {/* Card 1: Pass Limits */}
              <div className="policies-card">
                <div className="policies-row">
                  <span>Students allowed out simultaneously:</span>
                  <div className="hallzee-stepper">
                    <input
                      type="number"
                      min={1}
                      max={8}
                      value={policy.capacity}
                      onChange={(e) =>
                        setPolicy({ ...policy, capacity: Math.max(1, Math.min(8, Number(e.target.value))) })
                      }
                    />
                    <div className="hallzee-stepper-controls">
                      <button
                        type="button"
                        className="hallzee-stepper-btn"
                        onClick={() => updateCapacity(1)}
                      >
                        ▲
                      </button>
                      <button
                        type="button"
                        className="hallzee-stepper-btn"
                        onClick={() => updateCapacity(-1)}
                      >
                        ▼
                      </button>
                    </div>
                  </div>
                </div>

                <div className="policies-divider" />

                <div className="policies-row">
                  <span>Overdue warning threshold:</span>
                  <div className="flex items-center gap-2">
                    <div className="hallzee-stepper">
                      <input
                        type="number"
                        min={1}
                        max={60}
                        value={Math.round(policy.warningSeconds / 60)}
                        onChange={(e) =>
                          setPolicy({
                            ...policy,
                            warningSeconds: Math.max(1, Math.min(60, Number(e.target.value))) * 60,
                          })
                        }
                      />
                      <div className="hallzee-stepper-controls">
                        <button
                          type="button"
                          className="hallzee-stepper-btn"
                          onClick={() => updateWarningMins(1)}
                        >
                          ▲
                        </button>
                        <button
                          type="button"
                          className="hallzee-stepper-btn"
                          onClick={() => updateWarningMins(-1)}
                        >
                          ▼
                        </button>
                      </div>
                    </div>
                    <span className="status-pill warning text-11">mins</span>
                  </div>
                </div>

                <div className="policies-divider" />

                <div className="policies-row">
                  <span>Daily pass guideline per student:</span>
                  <div className="flex items-center gap-2">
                    <div className="hallzee-stepper">
                      <input
                        type="number"
                        min={0}
                        max={20}
                        value={policy.dailyGuideline ?? 2}
                        onChange={(e) =>
                          setPolicy({
                            ...policy,
                            dailyGuideline: Math.max(0, Math.min(20, Number(e.target.value))),
                          })
                        }
                      />
                      <div className="hallzee-stepper-controls">
                        <button
                          type="button"
                          className="hallzee-stepper-btn"
                          onClick={() => updateDailyGuideline(1)}
                        >
                          ▲
                        </button>
                        <button
                          type="button"
                          className="hallzee-stepper-btn"
                          onClick={() => updateDailyGuideline(-1)}
                        >
                          ▼
                        </button>
                      </div>
                    </div>
                    <span className="status-pill dark text-11">trips</span>
                  </div>
                </div>
              </div>

              {/* Card 2: Bell-Time Windows & Class Pass Rules */}
              <div className="policies-card">
                <div className="policies-card-title">Class Pass Rules & Bell-Time Windows</div>

                {/* 3-Option Segmented Mode Selector */}
                <div className="segmented-mode-bar">
                  <button
                    type="button"
                    className={`segmented-mode-btn ${policyMode === "Windows" ? "active" : ""}`}
                    onClick={() => handleModeChange("Windows")}
                  >
                    Windows
                  </button>
                  <button
                    type="button"
                    className={`segmented-mode-btn ${policyMode === "NoPasses" ? "active" : ""}`}
                    onClick={() => handleModeChange("NoPasses")}
                  >
                    No Passes
                  </button>
                  <button
                    type="button"
                    className={`segmented-mode-btn ${policyMode === "NoRules" ? "active" : ""}`}
                    onClick={() => handleModeChange("NoRules")}
                  >
                    No Rules
                  </button>
                </div>

                {/* Mode: Windows */}
                {policyMode === "Windows" && (
                  <>
                    <div className="presets-row">
                      <span>Quick presets:</span>
                      <div className="presets-pills">
                        <button
                          type="button"
                          className="preset-pill-btn"
                          onClick={() => applyPreset("10/10")}
                        >
                          10/10 Lockout
                        </button>
                        <button
                          type="button"
                          className="preset-pill-btn"
                          onClick={() => applyPreset("StartEnd")}
                        >
                          Start &amp; End Only
                        </button>
                        <button
                          type="button"
                          className="preset-pill-btn"
                          onClick={() => applyPreset("Warnings")}
                        >
                          Warnings
                        </button>
                      </div>
                    </div>

                    <div className="policies-divider" />

                    {/* First Minutes */}
                    <div className="policies-row">
                      <span>First minutes of period:</span>
                      <div className="flex items-center gap-2">
                        <div className="hallzee-stepper">
                          <input
                            type="number"
                            min={0}
                            max={30}
                            value={policy.firstMinutes}
                            onChange={(e) =>
                              setPolicy({ ...policy, firstMinutes: Math.max(0, Math.min(30, Number(e.target.value))) })
                            }
                          />
                          <div className="hallzee-stepper-controls">
                            <button
                              type="button"
                              className="hallzee-stepper-btn"
                              onClick={() => updateFirstMinutes(1)}
                            >
                              ▲
                            </button>
                            <button
                              type="button"
                              className="hallzee-stepper-btn"
                              onClick={() => updateFirstMinutes(-1)}
                            >
                              ▼
                            </button>
                          </div>
                        </div>
                        <select
                          className="policies-action-select"
                          value={policy.firstAction}
                          onChange={(e) => setPolicy({ ...policy, firstAction: e.target.value as Decision })}
                        >
                          <option value="Allow">Allow</option>
                          <option value="Warn">Warn</option>
                          <option value="Lock">Lock</option>
                        </select>
                      </div>
                    </div>

                    {/* Middle of Class */}
                    <div className="policies-row">
                      <div>
                        <div>Middle of class (in between):</div>
                        <small className="text-11 text-slate-400">
                          Action during instruction between first and last windows
                        </small>
                      </div>
                      <select
                        className="policies-action-select"
                        value={middleAction}
                        onChange={(e) => setMiddleAction(e.target.value as Decision)}
                      >
                        <option value="Allow">Allow</option>
                        <option value="Warn">Warn</option>
                        <option value="Lock">Lock</option>
                      </select>
                    </div>

                    {/* Last Minutes */}
                    <div className="policies-row">
                      <span>Last minutes of period:</span>
                      <div className="flex items-center gap-2">
                        <div className="hallzee-stepper">
                          <input
                            type="number"
                            min={0}
                            max={30}
                            value={policy.lastMinutes}
                            onChange={(e) =>
                              setPolicy({ ...policy, lastMinutes: Math.max(0, Math.min(30, Number(e.target.value))) })
                            }
                          />
                          <div className="hallzee-stepper-controls">
                            <button
                              type="button"
                              className="hallzee-stepper-btn"
                              onClick={() => updateLastMinutes(1)}
                            >
                              ▲
                            </button>
                            <button
                              type="button"
                              className="hallzee-stepper-btn"
                              onClick={() => updateLastMinutes(-1)}
                            >
                              ▼
                            </button>
                          </div>
                        </div>
                        <select
                          className="policies-action-select"
                          value={policy.lastAction}
                          onChange={(e) => setPolicy({ ...policy, lastAction: e.target.value as Decision })}
                        >
                          <option value="Allow">Allow</option>
                          <option value="Warn">Warn</option>
                          <option value="Lock">Lock</option>
                        </select>
                      </div>
                    </div>
                  </>
                )}

              {/* Mode: No Passes */}
              {policyMode === "NoPasses" && (
                <div className="info-box-danger">
                  <UserX size={18} color="#e11d48" />
                  <div>
                    <strong className="block text-13 font-bold">No Passes During Class</strong>
                    <span className="text-11">
                      Pass checkouts are locked for the entire class period. The Mini Window indicates passes are locked.
                    </span>
                  </div>
                </div>
              )}

              {/* Mode: No Rules */}
              {policyMode === "NoRules" && (
                <div className="info-box-success">
                  <Check size={18} color="#059669" />
                  <div>
                    <strong className="block text-13 font-bold">Open Pass Access (No Rules)</strong>
                    <span className="text-11">
                      Students may take passes at any time during class without bell-window lockouts or warnings.
                    </span>
                  </div>
                </div>
              )}

              {/* Timeline Preview Strip */}
              <div className="timeline-preview-strip">
                <div className="timeline-preview-header">
                  <span>PERIOD TIMELINE PREVIEW</span>
                  <span>
                    {policyMode === "NoPasses"
                      ? "Passes Locked (100% of period)"
                      : policyMode === "NoRules"
                        ? "Open Pass Access (No rules)"
                        : `First ${policy.firstMinutes}m (${policy.firstAction}) · Middle (${middleAction}) · Last ${policy.lastMinutes}m (${policy.lastAction})`}
                  </span>
                </div>

                {policyMode === "Windows" && (
                  <div
                    className="timeline-bar"
                    style={{ gridTemplateColumns: "auto 1fr auto" }}
                  >
                    <div className={`timeline-segment ${policy.firstAction.toLowerCase()}`}>
                      First {policy.firstMinutes}m · {policy.firstAction}
                    </div>
                    <div className={`timeline-segment ${middleAction.toLowerCase()}`}>
                      In Between · {middleAction}
                    </div>
                    <div className={`timeline-segment ${policy.lastAction.toLowerCase()}`}>
                      Last {policy.lastMinutes}m · {policy.lastAction}
                    </div>
                  </div>
                )}

                {policyMode === "NoPasses" && (
                  <div className="timeline-segment lock text-center">
                    Entire Class Period · Passes Locked (No Passes Allowed)
                  </div>
                )}

                {policyMode === "NoRules" && (
                  <div className="timeline-segment allow text-center">
                    Entire Class Period · Open Pass Access (No Restrictions)
                  </div>
                )}
              </div>

              <div className="policies-divider" />

              {/* Bell Transition Time Option */}
              <label className="check text-13">
                <input
                  type="checkbox"
                  checked={bellTransitionEnabled}
                  onChange={(e) => setBellTransitionEnabled(e.target.checked)}
                />
                Enable bell transition time between periods
              </label>
              <p className="text-11 text-slate-500 pl-6 -mt-2">
                Marks passing periods between scheduled classes as 'Transition Time' in the Mini Window with countdown to next class.
              </p>

              <div className="policies-divider" />

              {/* Warning Sounds Configuration */}
              <div>
                <div className="policies-row mb-2">
                  <div className="flex items-center gap-2 font-bold text-slate-700 text-13">
                    <Volume2 size={15} color="#0284c7" />
                    <span>Warning Sound</span>
                  </div>
                  <label className="check text-12">
                    <input
                      type="checkbox"
                      checked={warningSoundEnabled}
                      onChange={(e) => setWarningSoundEnabled(e.target.checked)}
                    />
                    Play warning sound
                  </label>
                </div>

                <div className="flex items-center gap-3 flex-wrap">
                  <select
                    className="policies-sound-select flex-1"
                    value={selectedSound}
                    disabled={!warningSoundEnabled}
                    onChange={(e) => setSelectedSound(e.target.value)}
                  >
                    {ALERT_SOUNDS.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>

                  <div className="flex items-center gap-2">
                    <Volume2 size={13} color="#64748b" />
                    <input
                      type="range"
                      min={10}
                      max={100}
                      className="volume-slider"
                      value={soundVolume}
                      disabled={!warningSoundEnabled}
                      onChange={(e) => setSoundVolume(Number(e.target.value))}
                    />
                    <span className="text-11 font-bold text-slate-500 w-8">{soundVolume}%</span>
                  </div>

                  <button
                    type="button"
                    className="preset-pill-btn"
                    disabled={!warningSoundEnabled}
                    onClick={() => playAlertSound(selectedSound, soundVolume)}
                  >
                    Preview
                  </button>
                </div>
              </div>

              <div className="policies-divider" />

              {/* Terminal Enforcement Toggle */}
              <label className="check text-13">
                <input
                  type="checkbox"
                  checked={policy.enforcement}
                  onChange={(e) => setPolicy({ ...policy, enforcement: e.target.checked })}
                />
                Enforce bell-time lockouts on the terminal
              </label>
              <p className="text-11 text-slate-500 pl-6 -mt-2">
                Optional and off by default. When enabled, the next 14 days are copied to the kiosk for offline enforcement.
              </p>
            </div>
          </div>

          {/* RIGHT COLUMN: BELL SCHEDULE & DATE EXCEPTIONS */}
          <div className="policies-column">
            {/* Section Header: Bell Schedule */}
            <div className="policies-section-header">
              <div className="policies-section-title">
                <Calendar size={18} color="#0284c7" />
                <span>Bell Schedule</span>
              </div>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  className="preset-pill-btn"
                  onClick={() => setEarliestFirst(!earliestFirst)}
                >
                  {earliestFirst ? "Earliest First ▲" : "Latest First ▼"}
                </button>
                <button
                  type="button"
                  className="preset-pill-btn"
                  onClick={addPeriod}
                >
                  <Plus size={11} className="inline mr-1" />
                  Add Period
                </button>
              </div>
            </div>

            <p className="text-12 text-slate-500 -mt-2">
              Regular and alternate templates stay inside this teacher workspace. Each period can identify the class section active at that time.
            </p>

            {/* Periods List */}
            <div className="flex flex-col gap-2">
              {sortedPeriods.map((p) =>
                editingScheduleId === p.scheduleId ? (
                  /* Edit Mode Card */
                  <div key={p.scheduleId} className="period-item-edit-card">
                    <div className="grid grid-cols-4 gap-2">
                      <input
                        className="pill-input text-12 col-span-2"
                        value={editPeriodName}
                        onChange={(e) => setEditPeriodName(e.target.value)}
                        placeholder="Period name"
                      />
                      <input
                        type="time"
                        className="pill-input text-12"
                        value={editStartTime}
                        onChange={(e) => setEditStartTime(e.target.value)}
                      />
                      <input
                        type="time"
                        className="pill-input text-12"
                        value={editEndTime}
                        onChange={(e) => setEditEndTime(e.target.value)}
                      />
                    </div>
                    <div className="grid grid-cols-4 gap-2 items-center">
                      <input
                        className="pill-input text-12 col-span-2"
                        value={editClassSection}
                        onChange={(e) => setEditClassSection(e.target.value)}
                        placeholder="Class section (e.g. Chemistry 3)"
                      />
                      <input
                        className="pill-input text-12"
                        value={editScheduleName}
                        onChange={(e) => setEditScheduleName(e.target.value)}
                        placeholder="Template (Regular)"
                      />
                      <div className="flex items-center gap-2 justify-end">
                        <button
                          type="button"
                          className="hallzee-pill-btn p-1.5"
                          title="Save Changes"
                          onClick={saveEditingPeriod}
                        >
                          <Check size={14} />
                        </button>
                        <button
                          type="button"
                          className="hallzee-outline-btn p-1.5"
                          title="Cancel"
                          onClick={() => setEditingScheduleId(null)}
                        >
                          <X size={14} />
                        </button>
                      </div>
                    </div>
                    {/* Weekday Checkboxes */}
                    <div className="days-checkboxes pt-1">
                      <span className="font-bold text-11 text-emerald-800">Active Days:</span>
                      {weekdays.map((day) => (
                        <label key={day} className="check text-11">
                          <input
                            type="checkbox"
                            checked={editDays.includes(day)}
                            onChange={(e) =>
                              setEditDays(
                                e.target.checked
                                  ? [...editDays, day]
                                  : editDays.filter((d) => d !== day),
                              )
                            }
                          />
                          {day}
                        </label>
                      ))}
                    </div>
                  </div>
                ) : (
                  /* Read Mode Card */
                  <div key={p.scheduleId} className="period-item-card">
                    <div className="period-item-top">
                      <strong className="text-14 text-slate-800">{p.periodName}</strong>
                      <span className="status-pill info text-11">
                        {p.weekdays.length === 5 ? "Mon–Fri" : p.weekdays.join(", ") || "No days"}
                      </span>
                    </div>
                    <div className="period-item-bottom">
                      <div className="flex items-center gap-2">
                        <Clock size={12} color="#64748b" />
                        <span className="period-time-range">
                          {formatTime12h(p.start)} – {formatTime12h(p.end)}
                        </span>
                        {p.classSection && (
                          <span className="text-11 text-slate-400">· {p.classSection}</span>
                        )}
                      </div>
                      <div className="flex items-center gap-1">
                        <button
                          type="button"
                          className="btn-action-edit"
                          title="Edit period"
                          onClick={() => startEditPeriod(p)}
                        >
                          <Pencil size={13} />
                        </button>
                        <button
                          type="button"
                          className="btn-action-delete"
                          title="Delete period"
                          onClick={() => removePeriod(p.scheduleId)}
                        >
                          <Trash2 size={13} />
                        </button>
                      </div>
                    </div>
                  </div>
                ),
              )}

              {!periods.length && (
                <div className="hallzee-table-empty">
                  <Calendar size={32} color="#94a3b8" />
                  <strong>No bell periods defined</strong>
                  <span>Click "+ Add Period" above to create scheduled periods.</span>
                </div>
              )}
            </div>

            {/* DATE EXCEPTIONS CARD */}
            <div className="policies-card mt-2">
              <div className="policies-card-title">Date Exceptions</div>
              <p className="text-11 text-slate-500 -mt-1">
                Choose which named schedule template applies on a special date, or mark the day as no school.
              </p>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 items-center">
                <input
                  type="date"
                  className="pill-input text-12"
                  value={newExceptionDate}
                  onChange={(e) => setNewExceptionDate(e.target.value)}
                />
                <input
                  className="pill-input text-12"
                  placeholder="Schedule template"
                  value={newExceptionSchedule}
                  disabled={newExceptionNoSchool}
                  onChange={(e) => setNewExceptionSchedule(e.target.value)}
                />
                <button
                  type="button"
                  className="preset-pill-btn text-center"
                  onClick={addException}
                >
                  Add
                </button>
              </div>

              <label className="check text-12">
                <input
                  type="checkbox"
                  checked={newExceptionNoSchool}
                  onChange={(e) => setNewExceptionNoSchool(e.target.checked)}
                />
                No school on this date
              </label>

              {exceptions.length > 0 && (
                <div className="flex flex-col gap-1 pt-1">
                  {exceptions.map((exc, idx) => (
                    <div
                      key={idx}
                      className="flex items-center justify-between text-12 bg-slate-50 border border-slate-200 rounded-lg px-3 py-1.5"
                    >
                      <span>
                        <strong>{exc.date}</strong>: {exc.isNoSchool ? "No school" : exc.scheduleName || "Regular"}
                      </span>
                      <button
                        type="button"
                        className="text-rose-500 hover:text-rose-700 font-bold px-2 py-0.5"
                        title="Remove date exception"
                        onClick={() => removeException(idx)}
                      >
                        ✕
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
        </div>
      </form>
    </Dialog>
  );
}
