"use client";

import { useState, type CSSProperties } from "react";
import Image from "next/image";
import {
  Calendar,
  History,
  Laptop,
  Minus,
  Radio,
  Search,
  Settings,
  Square,
  Users,
} from "./Icons";
import { useHallzee } from "../HallzeeProvider";
import type { ModalView } from "../types";
import DashboardPage from "../pages/DashboardPage";
import TripsModal from "./TripsModal";
import RosterModal from "./RosterModal";
import PoliciesModal from "./PoliciesModal";
import TerminalSettingsModal from "./TerminalSettingsModal";
import SettingsModal from "./SettingsModal";
import TerminalSearchDialog from "./TerminalSearchDialog";
import DemoControls, { type BackgroundPreset } from "./DemoControls";

interface NavItem {
  id: "dashboard" | ModalView;
  label: string;
  icon: typeof Laptop;
}

const navigation: NavItem[] = [
  { id: "dashboard", label: "Dashboard Overview", icon: Laptop },
  { id: "trips", label: "Trip History Log", icon: History },
  { id: "roster", label: "Student Roster Import", icon: Users },
  { id: "policies", label: "Policies & Bell Times", icon: Calendar },
  { id: "terminal", label: "Terminal Settings", icon: Radio },
  { id: "settings", label: "App Settings", icon: Settings },
];

const bgStyles: Record<BackgroundPreset, CSSProperties> = {
  "vector-original": {
    backgroundImage: "url('/backgrounds/waves-sidebar.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "vector-emerald": {
    backgroundImage: "url('/backgrounds/vector-emerald.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "vector-azure": {
    backgroundImage: "url('/backgrounds/vector-azure.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "vector-sunlit": {
    backgroundImage: "url('/backgrounds/vector-sunlit.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "streamlines": {
    backgroundImage: "url('/backgrounds/waves-streamlines.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "glass-crest": {
    backgroundImage: "url('/backgrounds/waves-glass-crest.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "aurora-ribbon": {
    backgroundImage: "url('/backgrounds/waves-aurora-ribbon.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "dual-sweep": {
    backgroundImage: "url('/backgrounds/waves-dual-sweep.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "waves-corner": {
    backgroundImage: "url('/backgrounds/waves-corner.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "center",
    backgroundRepeat: "no-repeat",
  },
  "waves-bottom": {
    backgroundImage: "url('/backgrounds/waves-bottom.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "bottom center",
    backgroundRepeat: "no-repeat",
  },
  "waves-dual": {
    backgroundImage: "url('/backgrounds/waves-dual.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "center",
    backgroundRepeat: "no-repeat",
  },
  "waves-horizon": {
    backgroundImage: "url('/backgrounds/waves-horizon.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "top center",
    backgroundRepeat: "no-repeat",
  },
  "waves-prism": {
    backgroundImage: "url('/backgrounds/waves-prism.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "center",
    backgroundRepeat: "no-repeat",
  },
  "waves-sidebar": {
    backgroundImage: "url('/backgrounds/waves-sidebar.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "left center",
    backgroundRepeat: "no-repeat",
  },
  "waves-halos": {
    backgroundImage: "url('/backgrounds/waves-halos.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "top right",
    backgroundRepeat: "no-repeat",
  },
  "waves-tide": {
    backgroundImage: "url('/backgrounds/waves-tide.jpg')",
    backgroundSize: "cover",
    backgroundPosition: "bottom right",
    backgroundRepeat: "no-repeat",
  },
  default: {},
};

export default function AppShell() {
  const [bgPreset, setBgPreset] = useState<BackgroundPreset>("vector-emerald");
  const {
    view,
    setView,
    activeModal,
    openModal,
    closeModal,
    terminalState,
    terminalId,
    trips,
    toast,
    findTerminals,
  } = useHallzee();

  const connected = terminalState === "connected" || terminalState === "syncing";

  return (
    <div
      style={bgStyles[bgPreset]}
      className="min-h-screen bg-gradient-to-br from-[#DDF2FD] via-[#EDF9FE] to-[#D5F3F2] flex flex-col text-slate-900 select-none transition-all duration-300"
    >
      {/* 1. Desktop Window Top Bar */}
      <div className="bg-gradient-to-r from-[#0284C7] via-[#0369A1] to-[#075985] text-white border-b border-sky-400/40 px-4 py-1.5 flex items-center justify-between text-xs select-none shadow-xs">
        <div className="flex items-center gap-2">
          <Image
            src="/hallzee-logo.png"
            alt="Hallzee"
            width={16}
            height={16}
            className="h-4 w-4 object-contain rounded-xs drop-shadow-xs"
          />
          <span className="font-bold">Hallzee Desktop Client</span>
          <span className="text-white/40">|</span>
          <span className="text-white/90">Room 204 • Period 3 (Chemistry AP)</span>
        </div>
        <div className="flex items-center gap-3 text-white/80">
          <span className="font-mono-hardware text-[11px]">v1.5.2-win64</span>
          <Minus className="w-3.5 h-3.5 opacity-80 hover:opacity-100 transition cursor-pointer" />
          <Square className="w-3 h-3 opacity-80 hover:opacity-100 transition cursor-pointer" />
        </div>
      </div>

      {/* 2. Toast Notification Region */}
      {toast && (
        <div
          role="status"
          className="fixed top-10 right-5 z-[70] rounded-2xl bg-slate-950 text-white px-5 py-3 shadow-2xl text-sm font-semibold border border-slate-800 animate-in slide-in-from-top-2 duration-200"
        >
          {toast}
        </div>
      )}

      {/* 3. Main Body: Sidebar + Dashboard Workspace */}
      <div className="flex-1 flex min-h-0">
        {/* Left Navigation Sidebar */}
        <aside className="w-68 bg-white/85 backdrop-blur-md border-r border-sky-200 shadow-md flex flex-col justify-between p-4 shrink-0">
          <div className="space-y-5">
            {/* Logo and Brand */}
            <div>
              <div className="flex items-center gap-3">
                <div className="h-11 w-11 rounded-2xl bg-white border border-sky-200/80 p-1 flex items-center justify-center shadow-md shadow-sky-500/10 shrink-0">
                  <Image
                    src="/hallzee-logo.png"
                    alt="Hallzee Logo"
                    width={40}
                    height={40}
                    className="w-full h-full object-contain"
                  />
                </div>
                <div>
                  <h1 className="font-black text-lg leading-none text-slate-900 tracking-tight">Hallzee</h1>
                  <p className="text-xs text-sky-700 font-bold mt-1">Terminal Gateway</p>
                </div>
              </div>

              {/* Classroom Profile Box */}
              <div className="mt-4 p-3 rounded-2xl bg-sky-50/80 border border-sky-200/80 text-xs space-y-1 shadow-xs">
                <div className="flex justify-between items-center">
                  <span className="uppercase font-extrabold tracking-wider text-slate-500 text-[10px]">
                    Classroom Profile
                  </span>
                  <span className="rounded-full bg-emerald-100 text-emerald-800 border border-emerald-200 px-2 font-extrabold text-[10px]">
                    Active
                  </span>
                </div>
                <div className="font-bold text-sm text-slate-900">Room 204 • Chemistry AP</div>
                <div className="text-slate-600 font-medium">Period 3 (9:15–10:05)</div>
                <div className="text-slate-500">Teacher: Dr. Aris Thorne</div>
              </div>
            </div>

            {/* Nav Menu */}
            <nav className="space-y-2" aria-label="Primary navigation">
              {navigation.map(({ id: target, label, icon: Icon }) => {
                const isActive = view === target;
                return (
                  <button
                    key={target}
                    onClick={() => {
                      if (target === "dashboard") {
                        closeModal();
                        setView("dashboard");
                      } else {
                        openModal(target as ModalView);
                      }
                    }}
                    aria-current={isActive ? "page" : undefined}
                    className={`w-full flex items-center justify-between gap-3 px-4 py-3 rounded-full text-sm font-semibold transition cursor-pointer select-none ${
                      isActive
                        ? "hallzee-pill-btn-active font-bold"
                        : "text-sky-950 hover:bg-sky-100/80"
                    }`}
                  >
                    <span className="flex items-center gap-3">
                      <Icon className="w-5 h-5" />
                      <span className="font-bold">{label}</span>
                    </span>
                    {target === "trips" && (
                      <span
                        className={`rounded-full px-2 py-0.5 text-xs font-bold ${
                          isActive ? "bg-white text-sky-700" : "bg-sky-100 text-slate-700"
                        }`}
                      >
                        {trips.length}
                      </span>
                    )}
                  </button>
                );
              })}
            </nav>
          </div>

          {/* Bottom Terminal Node Panel */}
          <div className="p-3.5 rounded-2xl bg-sky-50/80 border border-sky-200/80 text-xs space-y-2 shadow-xs">
            <div className="flex justify-between items-center">
              <span className="uppercase font-extrabold tracking-wider text-slate-500 text-[10px]">
                Terminal Node
              </span>
              <span
                className={`rounded-full px-2 py-0.5 font-extrabold text-[10px] ${
                  connected ? "bg-emerald-500 text-white shadow-xs" : "bg-slate-200 text-slate-700"
                }`}
              >
                {terminalState.toUpperCase()}
              </span>
            </div>
            <div className="font-mono-hardware font-bold text-slate-800 truncate">
              {connected ? terminalId : "-- (No Terminal Linked)"}
            </div>
            <p className="text-[11px] text-slate-500 leading-tight">
              Auto-clock synced with Windows RTC
            </p>

            {/* Quick Action Button for Terminal Node */}
            {connected ? (
              <button
                type="button"
                onClick={() => openModal("terminal")}
                className="w-full mt-1 bg-white hover:bg-sky-50 text-sky-900 border border-sky-200/80 rounded-xl px-3 py-1.5 text-xs font-bold shadow-xs flex items-center justify-center gap-1.5 transition cursor-pointer"
              >
                <Radio className="w-3.5 h-3.5 text-sky-600" />
                Configure Node
              </button>
            ) : (
              <button
                type="button"
                onClick={findTerminals}
                className="w-full mt-1 hallzee-gradient-btn rounded-xl px-3 py-1.5 text-xs font-bold flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <Search className="w-3.5 h-3.5" />
                Find Terminal
              </button>
            )}
          </div>
        </aside>

        {/* Main Central Dashboard Content */}
        <main className="flex-1 overflow-y-auto p-6">
          <DashboardPage />
        </main>
      </div>

      {/* 4. Active Pop-up Modals */}
      {activeModal === "trips" && <TripsModal onClose={closeModal} />}
      {activeModal === "roster" && <RosterModal onClose={closeModal} />}
      {activeModal === "policies" && <PoliciesModal onClose={closeModal} />}
      {activeModal === "terminal" && <TerminalSettingsModal onClose={closeModal} />}
      {activeModal === "settings" && <SettingsModal onClose={closeModal} />}
      <TerminalSearchDialog />

      {/* 5. Demo Controls Bar */}
      <DemoControls bgPreset={bgPreset} setBgPreset={setBgPreset} />
    </div>
  );
}
