"use client";
import { useHallzee } from "../HallzeeProvider";

export default function DemoControls() {
  const { terminalState, isOccupied, disconnect, findTerminals, toggleOccupancy, toggleStudentName } = useHallzee();
  return <div className="fixed bottom-5 left-1/2 -translate-x-1/2 z-40 bg-slate-950/95 text-white px-4 py-2 rounded-full shadow-2xl flex items-center gap-3">
    <span className="text-xs font-bold uppercase tracking-wider text-sky-400">Sandbox:</span>
    <button onClick={terminalState === "connected" ? disconnect : findTerminals} className="px-3 py-1 rounded-full bg-slate-800 text-sm">{terminalState === "connected" ? "⚡ Disconnect" : "🔍 Find Terminal"}</button>
    <button onClick={toggleOccupancy} disabled={terminalState !== "connected"} className="px-3 py-1 rounded-full bg-slate-800 text-sm disabled:opacity-40">{isOccupied ? "👥 Student Out" : "🟢 Pass Empty"}</button>
    <button onClick={toggleStudentName} className="px-3 py-1 rounded-full bg-sky-950 text-sky-300 text-sm">🏷️ Toggle Name</button>
  </div>;
}
