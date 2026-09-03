"use client";

import { useHallzee } from "../HallzeeProvider";

export type BackgroundPreset =
  | "waves-corner"
  | "waves-bottom"
  | "waves-dual"
  | "waves-horizon"
  | "default";

interface DemoControlsProps {
  bgPreset?: BackgroundPreset;
  setBgPreset?: (preset: BackgroundPreset) => void;
}

export default function DemoControls({ bgPreset = "waves-corner", setBgPreset }: DemoControlsProps) {
  const { terminalState, isOccupied, disconnect, findTerminals, toggleOccupancy, toggleStudentName } =
    useHallzee();

  const isConnected = terminalState === "connected" || terminalState === "syncing";

  return (
    <div className="fixed bottom-5 left-1/2 -translate-x-1/2 z-40 bg-slate-950/90 backdrop-blur-md text-white px-4 py-2 rounded-full shadow-[0_10px_25px_-5px_rgba(0,0,0,0.5)] border border-slate-800/80 flex items-center gap-2.5 text-xs font-semibold select-none">
      <span className="text-[11px] font-extrabold uppercase tracking-wider text-sky-400 pl-1">
        Sandbox:
      </span>
      <button
        onClick={isConnected ? disconnect : findTerminals}
        disabled={terminalState === "syncing" || terminalState === "connecting"}
        className="px-3 py-1 rounded-full bg-slate-800/90 hover:bg-slate-700 text-slate-200 transition cursor-pointer disabled:opacity-40 shadow-xs"
      >
        {isConnected ? "⚡ Disconnect" : "🔍 Find Terminal"}
      </button>
      <button
        onClick={toggleOccupancy}
        disabled={terminalState !== "connected"}
        className="px-3 py-1 rounded-full bg-slate-800/90 hover:bg-slate-700 text-slate-200 transition cursor-pointer disabled:opacity-40 shadow-xs"
      >
        {isOccupied ? "👥 Student Out" : "🟢 Pass Empty"}
      </button>
      <button
        onClick={toggleStudentName}
        className="px-3 py-1 rounded-full bg-sky-950/80 hover:bg-sky-900/80 text-sky-300 border border-sky-800/50 transition cursor-pointer shadow-xs"
      >
        🏷️ Toggle Name
      </button>

      <div className="h-4 w-px bg-slate-700 mx-1" />

      <div className="flex items-center gap-1.5">
        <span className="text-[11px] font-extrabold uppercase tracking-wider text-emerald-400">
          Background:
        </span>
        <select
          value={bgPreset}
          onChange={(e) => setBgPreset?.(e.target.value as BackgroundPreset)}
          className="bg-slate-900 border border-slate-700 text-sky-200 rounded-full px-2.5 py-0.5 text-xs font-semibold cursor-pointer focus:outline-none"
        >
          <option value="waves-corner">1. Top-Right Corner Sweep</option>
          <option value="waves-bottom">2. Lower-Third Crest Flow</option>
          <option value="waves-dual">3. Dual-Corner Whispers</option>
          <option value="waves-horizon">4. Single Horizon Stream</option>
          <option value="default">Plain Gradient (Original)</option>
        </select>
      </div>
    </div>
  );
}
