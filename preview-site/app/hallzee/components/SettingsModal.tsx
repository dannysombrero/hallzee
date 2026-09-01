"use client";

import { useState } from "react";
import Image from "next/image";
import { Clock, RefreshCw, Settings } from "./Icons";
import ModalDialog from "./ModalDialog";

function Toggle({
  label,
  description,
  value,
  onChange,
  planned = false,
}: {
  label: string;
  description: string;
  value: boolean;
  onChange: (value: boolean) => void;
  planned?: boolean;
}) {
  return (
    <div className="flex items-center justify-between gap-4 p-4 rounded-2xl bg-sky-50/70 border border-sky-200/60 hover:bg-sky-50 transition">
      <div>
        <div className="font-bold text-sm text-slate-900 flex items-center gap-2">
          {label}
          {planned && (
            <span className="text-[10px] uppercase tracking-wider rounded-full bg-sky-100 text-sky-800 px-2 py-0.5 font-extrabold border border-sky-200">
              Planned
            </span>
          )}
        </div>
        <p className="text-xs text-slate-500 mt-0.5 leading-normal">{description}</p>
      </div>
      <button
        role="switch"
        aria-checked={value}
        onClick={() => onChange(!value)}
        className={`w-12 h-7 p-1 rounded-full transition-colors cursor-pointer shrink-0 ${
          value ? "bg-gradient-to-r from-sky-500 to-cyan-500 shadow-md shadow-sky-500/25" : "bg-slate-300"
        }`}
      >
        <span
          className={`block h-5 w-5 bg-white rounded-full transition-transform shadow-xs ${
            value ? "translate-x-5" : ""
          }`}
        />
      </button>
    </div>
  );
}

export function SettingsContent() {
  const [onConnection, setOnConnection] = useState(false);
  const [live, setLive] = useState(false);

  return (
    <div className="space-y-4">
      <p className="text-xs text-slate-500 leading-relaxed">
        Application preferences will eventually be stored alongside the active classroom profile in SQLite.
      </p>

      <div className="space-y-3">
        <Toggle
          label="Sync on connection"
          description="Run one incremental trip check immediately after connecting."
          value={onConnection}
          onChange={setOnConnection}
          planned
        />
        <Toggle
          label="Sync new transactions while connected"
          description="Transfer newly completed trips automatically while Bluetooth remains active."
          value={live}
          onChange={setLive}
          planned
        />

        <div className="flex items-start gap-3 p-4 rounded-2xl border border-emerald-200 bg-emerald-50/70">
          <Clock className="w-5 h-5 text-emerald-700 shrink-0 mt-0.5" />
          <div className="text-xs">
            <strong className="text-emerald-900 font-bold block">
              Date and time sync is automatic
            </strong>
            <p className="text-emerald-800/80 mt-0.5">
              The client aligns the terminal clock with Windows RTC during the established connection workflow.
            </p>
          </div>
        </div>

        <div className="flex items-start gap-3 p-4 rounded-2xl border border-sky-200 bg-sky-50/50">
          <RefreshCw className="w-5 h-5 text-sky-700 shrink-0 mt-0.5" />
          <div className="text-xs">
            <strong className="text-sky-950 font-bold block">Trip sync remains manual</strong>
            <p className="text-slate-600 mt-0.5">
              Use <strong>Sync Now</strong> to fetch new records until continuous background sync is approved and enabled.
            </p>
          </div>
        </div>

        {/* About Hallzee Brand Box */}
        <div className="p-4 rounded-2xl border border-sky-200/80 bg-gradient-to-r from-sky-50/70 via-white to-sky-50/40 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="h-12 w-12 rounded-2xl bg-white border border-sky-200/80 p-1.5 flex items-center justify-center shadow-md shadow-sky-500/10 shrink-0">
              <Image
                src="/hallzee-logo.png"
                alt="Hallzee Logo"
                width={44}
                height={44}
                className="w-full h-full object-contain"
              />
            </div>
            <div>
              <div className="font-extrabold text-sm text-slate-900 flex items-center gap-2">
                Hallzee Desktop Gateway
                <span className="text-[10px] uppercase font-bold tracking-wider rounded-full bg-emerald-100 text-emerald-800 border border-emerald-200 px-2 py-0.5">
                  v1.5.2
                </span>
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                ESP32 BLE Bathroom Sign-In & Classroom Management System
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function SettingsModal({ onClose }: { onClose: () => void }) {
  return (
    <ModalDialog
      title="Application Settings"
      subtitle="Preferences and synchronization behavior for the active classroom profile"
      icon={Settings}
      maxWidthClass="max-w-2xl"
      onClose={onClose}
    >
      <SettingsContent />
    </ModalDialog>
  );
}
