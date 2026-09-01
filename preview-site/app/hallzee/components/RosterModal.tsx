"use client";

import { useRef } from "react";
import { FileSpreadsheet, Upload, Users } from "./Icons";
import ModalDialog from "./ModalDialog";
import { useHallzee } from "../HallzeeProvider";

export function RosterContent() {
  const { rosterFileName, importRoster } = useHallzee();
  const input = useRef<HTMLInputElement>(null);

  return (
    <div className="grid lg:grid-cols-[1fr_320px] gap-6">
      {/* Upload Zone */}
      <section className="space-y-4">
        <button
          onClick={() => input.current?.click()}
          className="w-full border-2 border-dashed border-sky-300 hover:border-sky-500 bg-sky-50/50 hover:bg-sky-50/80 p-10 rounded-3xl text-center transition group cursor-pointer"
        >
          <div className="h-14 w-14 rounded-2xl bg-gradient-to-tr from-sky-500 to-cyan-400 text-white flex items-center justify-center mx-auto shadow-lg shadow-sky-500/25 group-hover:scale-105 transition-transform">
            <Upload className="w-7 h-7" />
          </div>
          <strong className="block mt-4 text-base text-slate-900 font-bold">
            Click to choose a student CSV roster
          </strong>
          <span className="text-xs text-slate-500 mt-1 block">
            Required columns: <code className="bg-sky-100/70 px-1.5 py-0.5 rounded text-sky-800 font-mono-hardware text-xs">student_id</code>,{" "}
            <code className="bg-sky-100/70 px-1.5 py-0.5 rounded text-sky-800 font-mono-hardware text-xs">student_name</code>,{" "}
            <code className="bg-sky-100/70 px-1.5 py-0.5 rounded text-sky-800 font-mono-hardware text-xs">class_name</code>,{" "}
            <code className="bg-sky-100/70 px-1.5 py-0.5 rounded text-sky-800 font-mono-hardware text-xs">period</code>
          </span>
        </button>
        <input
          ref={input}
          type="file"
          accept=".csv,text/csv"
          className="hidden"
          onChange={(event) =>
            event.target.files?.[0] && importRoster(event.target.files[0].name)
          }
        />
      </section>

      {/* Active Roster Info Box */}
      <aside className="space-y-4">
        <div className="bg-gradient-to-b from-sky-50/90 to-sky-100/60 border border-sky-200/80 rounded-2xl p-4 shadow-[inset_0_2px_4px_rgba(0,0,0,0.06),inset_0_1px_2px_rgba(0,0,0,0.04)] space-y-2">
          <div className="flex items-center gap-2 text-sky-900 font-bold text-sm">
            <FileSpreadsheet className="w-4 h-4 text-sky-600" />
            <span>Active Roster</span>
          </div>
          <p className="font-mono-hardware text-xs font-bold text-slate-800 bg-white/90 border border-sky-200/60 rounded-xl p-2.5 break-all shadow-xs">
            {rosterFileName}
          </p>
          <div className="text-xs text-slate-500 space-y-1 pt-1">
            <div className="flex justify-between">
              <span>Status:</span>
              <strong className="text-emerald-700 font-bold">Loaded & Mapped</strong>
            </div>
            <div className="flex justify-between">
              <span>Matched Students:</span>
              <strong>32 records</strong>
            </div>
          </div>
        </div>

        <p className="text-xs text-slate-500 leading-relaxed px-1">
          This prototype keeps roster data in memory and demonstrates the student ID enrichment workflow. Permanent SQLite storage is planned.
        </p>
      </aside>
    </div>
  );
}

export default function RosterModal({ onClose }: { onClose: () => void }) {
  return (
    <ModalDialog
      title="Student Roster Import"
      subtitle="Associate student IDs with names, classes, and periods"
      icon={Users}
      maxWidthClass="max-w-3xl"
      onClose={onClose}
    >
      <RosterContent />
    </ModalDialog>
  );
}
