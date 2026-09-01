"use client";

import { useState } from "react";
import { Bluetooth, Radio, Save, Search } from "./Icons";
import ModalDialog from "./ModalDialog";
import { useHallzee } from "../HallzeeProvider";
import type { TerminalSettings } from "../types";

function TerminalSettingsForm({
  initialSettings,
  onSave,
}: {
  initialSettings: TerminalSettings;
  onSave: (settings: TerminalSettings) => void;
}) {
  const [name, setName] = useState(initialSettings.name);
  const [length, setLength] = useState(initialSettings.maxStudentIdLength);

  return (
    <div className="space-y-3.5">
      <label className="block text-xs font-bold text-slate-700">
        Terminal Assigned Name
        <input
          value={name}
          onChange={(event) => setName(event.target.value)}
          className="mt-1 w-full rounded-2xl border border-sky-200 bg-sky-50/70 focus:bg-white focus:border-sky-400 px-3.5 py-2 text-sm font-mono-hardware outline-none transition"
          placeholder="e.g. ESP32-HALLZEE-204"
        />
      </label>

      <label className="block text-xs font-bold text-slate-700">
        Maximum Student ID Length
        <select
          value={length}
          onChange={(event) => setLength(Number(event.target.value))}
          className="mt-1 w-full rounded-2xl border border-sky-200 bg-sky-50/70 focus:bg-white focus:border-sky-400 px-3.5 py-2 text-sm outline-none transition cursor-pointer"
        >
          {Array.from({ length: 13 }, (_, index) => index + 4).map((value) => (
            <option key={value} value={value}>
              {value} digits {value === 8 ? "(Default)" : ""}
            </option>
          ))}
        </select>
      </label>

      <button
        type="button"
        onClick={() => onSave({ name, maxStudentIdLength: length })}
        className="w-full hallzee-gradient-btn rounded-2xl py-3 font-bold flex items-center justify-center gap-2 cursor-pointer text-sm"
      >
        <Save className="w-4 h-4" />
        Save Configuration
      </button>
    </div>
  );
}

export function TerminalContent() {
  const {
    terminalState,
    connectedTerminalName,
    terminalId,
    terminalSettings,
    findTerminals,
    applyTerminalSettings,
  } = useHallzee();

  return (
    <div className="grid md:grid-cols-[1fr_1.1fr] gap-6">
      {/* Terminal Connection Overview */}
      <section className="space-y-4">
        <div className="flex items-center gap-2 text-slate-900 font-extrabold text-sm border-b border-sky-100 pb-2">
          <Bluetooth className="w-4 h-4 text-sky-600" />
          <span>Active Connection</span>
        </div>

        <div className="bg-gradient-to-b from-sky-50/90 to-sky-100/60 border border-sky-200/80 rounded-2xl p-4 shadow-[inset_0_2px_4px_rgba(0,0,0,0.06),inset_0_1px_2px_rgba(0,0,0,0.04)] space-y-3">
          <div className="flex justify-between items-center">
            <span className="uppercase text-[11px] font-bold text-slate-500 tracking-wider">
              Connection State
            </span>
            <span
              className={`text-xs rounded-full px-2.5 py-0.5 font-bold ${
                terminalState === "connected"
                  ? "bg-emerald-500 text-white shadow-xs"
                  : "bg-slate-200 text-slate-700"
              }`}
            >
              {terminalState.toUpperCase()}
            </span>
          </div>
          <div>
            <h3 className="text-base font-extrabold text-slate-900 leading-tight">
              {connectedTerminalName}
            </h3>
            <p className="font-mono-hardware text-xs text-sky-700 font-bold mt-1">{terminalId}</p>
          </div>
        </div>

        <button
          type="button"
          onClick={findTerminals}
          className="w-full hallzee-gradient-btn rounded-2xl py-3 px-4 font-bold flex items-center justify-center gap-2 cursor-pointer text-sm"
        >
          <Search className="w-4 h-4" />
          Find / Reconnect Terminal
        </button>

        <div className="p-3.5 border border-amber-200 bg-amber-50/80 rounded-2xl text-xs text-amber-900 leading-relaxed">
          <strong className="block font-bold">Prototype Note:</strong>
          Bluetooth discovery and operations are simulated in this preview. Physical ESP32 BLE validation runs in the Windows client.
        </div>
      </section>

      {/* Terminal Hardware Parameters */}
      <section className="space-y-4">
        <div className="flex items-center gap-2 text-slate-900 font-extrabold text-sm border-b border-sky-100 pb-2">
          <Radio className="w-4 h-4 text-sky-600" />
          <span>Device Settings</span>
        </div>

        <TerminalSettingsForm
          key={`${terminalSettings.name}-${terminalSettings.maxStudentIdLength}`}
          initialSettings={terminalSettings}
          onSave={applyTerminalSettings}
        />
      </section>
    </div>
  );
}

export default function TerminalSettingsModal({ onClose }: { onClose: () => void }) {
  return (
    <ModalDialog
      title="Terminal Settings"
      subtitle="Configure kiosk parameters, maximum student ID length, and connection status"
      icon={Radio}
      maxWidthClass="max-w-3xl"
      onClose={onClose}
    >
      <TerminalContent />
    </ModalDialog>
  );
}
