"use client";

import { Calendar, History, Laptop, Minus, Radio, Settings, Square, Users, Droplets } from "lucide-react";
import { useHallzee } from "../HallzeeProvider";
import type { View } from "../types";
import DashboardPage from "../pages/DashboardPage";
import TripsPage from "../pages/TripsPage";
import RosterPage from "../pages/RosterPage";
import PoliciesPage from "../pages/PoliciesPage";
import TerminalPage from "../pages/TerminalPage";
import SettingsPage from "../pages/SettingsPage";
import TerminalSearchDialog from "./TerminalSearchDialog";
import DemoControls from "./DemoControls";

const navigation: Array<{ view: View; label: string; icon: typeof Laptop }> = [
  { view: "dashboard", label: "Dashboard Overview", icon: Laptop },
  { view: "trips", label: "Trip History Log", icon: History },
  { view: "roster", label: "Student Roster Import", icon: Users },
  { view: "policies", label: "Policies & Bell Times", icon: Calendar },
  { view: "terminal", label: "Terminal Settings", icon: Radio },
  { view: "settings", label: "App Settings", icon: Settings },
];

const pages: Record<View, React.ReactNode> = {
  dashboard: <DashboardPage />,
  trips: <TripsPage />,
  roster: <RosterPage />,
  policies: <PoliciesPage />,
  terminal: <TerminalPage />,
  settings: <SettingsPage />,
};

export default function AppShell() {
  const { view, setView, terminalState, terminalId, trips, toast } = useHallzee();
  const connected = terminalState === "connected" || terminalState === "syncing";

  return (
    <div className="min-h-screen bg-gradient-to-br from-[#DDF2FD] via-[#EDF9FE] to-[#D5F3F2] flex flex-col text-slate-900">
      <div className="bg-gradient-to-r from-[#0284C7] via-[#0369A1] to-[#075985] text-white border-b border-sky-400/40 px-4 py-1.5 flex items-center justify-between text-xs select-none">
        <div className="flex items-center gap-2">
          <div className="h-4 w-4 rounded bg-white text-sky-800 font-black flex items-center justify-center text-[10px]">H</div>
          <span className="font-bold">Hallzee Desktop Client</span><span className="text-white/40">|</span>
          <span className="text-white/90">Room 204 • Period 3 (Chemistry AP)</span>
        </div>
        <div className="flex items-center gap-3 text-white/80"><span>v1.5.2-win64</span><Minus className="w-3.5"/><Square className="w-3"/></div>
      </div>

      {toast && <div role="status" className="fixed top-10 right-5 z-[70] rounded-2xl bg-slate-950 text-white px-5 py-3 shadow-2xl text-sm">{toast}</div>}

      <div className="flex-1 flex min-h-0">
        <aside className="w-64 bg-white/85 border-r border-sky-200 shadow-md flex flex-col justify-between p-4">
          <div className="space-y-6">
            <div>
              <div className="flex items-center gap-2.5">
                <div className="h-10 w-10 rounded-2xl bg-gradient-to-tr from-sky-400 to-cyan-300 text-white flex items-center justify-center shadow-md"><Droplets className="w-5 h-5"/></div>
                <div><h1 className="font-black text-base leading-none">Hallzee</h1><p className="text-xs text-sky-700 font-semibold mt-1">Terminal Gateway</p></div>
              </div>
              <div className="mt-4 p-3 rounded-2xl bg-sky-50 border border-sky-200 text-xs space-y-1">
                <div className="flex justify-between"><span className="uppercase font-bold tracking-wider text-slate-500">Classroom Profile</span><span className="rounded-full bg-emerald-100 text-emerald-800 px-2 font-bold">Active</span></div>
                <div className="font-bold text-sm">Room 204 • Chemistry AP</div><div className="text-slate-600">Period 3 (9:15–10:05)</div><div className="text-slate-500">Teacher: Dr. Aris Thorne</div>
              </div>
            </div>
            <nav className="space-y-1.5" aria-label="Primary navigation">
              {navigation.map(({ view: target, label, icon: Icon }) => (
                <button key={target} onClick={() => setView(target)} aria-current={view === target ? "page" : undefined}
                  className={`w-full flex items-center justify-between gap-3 px-3.5 py-2.5 rounded-2xl text-sm font-semibold transition ${view === target ? "bg-gradient-to-r from-sky-500 to-cyan-500 text-white shadow-md" : "text-sky-950 hover:bg-sky-100"}`}>
                  <span className="flex items-center gap-3"><Icon className="w-4 h-4"/>{label}</span>{target === "trips" && <span className="rounded-full bg-white/80 text-slate-700 px-2 text-xs">{trips.length}</span>}
                </button>
              ))}
            </nav>
          </div>
          <div className="p-3.5 rounded-2xl bg-sky-50 border border-sky-200 text-xs space-y-2">
            <div className="flex justify-between"><span className="uppercase font-bold tracking-wider text-slate-500">Terminal Node</span><span className={`rounded-full px-2 font-bold ${connected ? "bg-emerald-500 text-white" : "bg-slate-200"}`}>{terminalState.toUpperCase()}</span></div>
            <div className="font-mono font-bold truncate">{connected ? terminalId : "-- (No Terminal Linked)"}</div><p className="text-slate-500">Auto-clock synced with Windows RTC</p>
          </div>
        </aside>
        <main className="flex-1 overflow-y-auto p-6">{pages[view]}</main>
      </div>
      <TerminalSearchDialog />
      <DemoControls />
    </div>
  );
}
