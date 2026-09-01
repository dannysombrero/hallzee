"use client";

import {
  ChevronRight,
  Clock,
  Download,
  Radio,
  RefreshCw,
  Search,
  ShieldCheck,
  UserCheck,
  UserX,
  Users,
  WifiOff,
} from "../components/Icons";
import { useHallzee } from "../HallzeeProvider";

const duration = (seconds: number) =>
  `${Math.floor(seconds / 60)}m ${String(seconds % 60).padStart(2, "0")}s`;

const studentName = (name: string, id: string) => name.trim() || `Student ID #${id}`;

export default function DashboardPage() {
  const {
    terminalState,
    connectedTerminalName,
    isOccupied,
    activeTrip,
    lastSyncTime,
    trips,
    rosterFileName,
    findTerminals,
    syncNow,
    toggleOccupancy,
    exportCsv,
    openModal,
  } = useHallzee();

  const disconnected = terminalState === "disconnected" || terminalState === "recoverableError";
  const syncing = terminalState === "syncing";

  return (
    <div className="space-y-6 pb-24 max-w-6xl mx-auto">
      {/* 1. Top Terminal Status Strip */}
      <section className="p-4 sm:p-5 rounded-3xl bg-white/90 border border-sky-200/80 shadow-lg shadow-sky-500/5 flex flex-wrap items-center justify-between gap-4 transition">
        <div className="flex items-center gap-3.5">
          <div
            className={`h-12 w-12 rounded-2xl flex items-center justify-center text-white shadow-md shrink-0 ${
              disconnected
                ? "bg-slate-400 shadow-slate-400/30"
                : "bg-gradient-to-tr from-emerald-500 to-teal-400 shadow-emerald-500/30"
            }`}
          >
            {disconnected ? <WifiOff className="w-6 h-6" /> : <Radio className="w-6 h-6" />}
          </div>
          <div>
            <div className="flex items-center gap-2.5">
              <h2 className="font-extrabold text-base text-slate-900">
                {disconnected ? "No Terminal Connected" : connectedTerminalName}
              </h2>
              <span
                className={`text-[11px] rounded-full px-2.5 py-0.5 font-extrabold tracking-wider ${
                  disconnected
                    ? "bg-slate-200 text-slate-700"
                    : syncing
                      ? "bg-sky-100 text-sky-800 animate-pulse border border-sky-300"
                      : "bg-emerald-100 text-emerald-800 border border-emerald-200"
                }`}
              >
                {disconnected ? "OFFLINE" : syncing ? "SYNCING" : "BLE CONNECTED"}
              </span>
            </div>
            <p className="text-xs text-slate-600 mt-1 font-medium">
              {disconnected ? (
                "Search for a nearby Hallzee kiosk to sync bathroom pass logs."
              ) : (
                <>
                  Last successful sync: <strong className="text-slate-900">{lastSyncTime}</strong>
                </>
              )}
            </p>
          </div>
        </div>

        {/* Action Buttons for Top Strip */}
        {disconnected ? (
          <button
            type="button"
            onClick={findTerminals}
            className="hallzee-gradient-btn rounded-full px-6 py-3 font-bold text-sm flex items-center gap-2.5 cursor-pointer"
          >
            <Search className="w-4 h-4" />
            Find Terminal
          </button>
        ) : (
          <div className="flex items-center gap-3">
            {/* Sync Now Button matching Image 2 */}
            <button
              type="button"
              onClick={syncNow}
              disabled={syncing}
              className="hallzee-gradient-btn rounded-full px-6 py-3 font-bold text-sm flex items-center gap-3.5 cursor-pointer disabled:opacity-60"
            >
              <RefreshCw className={`w-5 h-5 shrink-0 ${syncing ? "animate-spin" : ""}`} />
              <div className="text-left font-bold text-sm leading-tight">
                <div>Sync</div>
                <div>Now</div>
              </div>
            </button>
            <button
              type="button"
              onClick={() => openModal("terminal")}
              className="rounded-2xl bg-white hover:bg-sky-50 text-sky-900 border border-sky-200/80 px-4 py-3 font-bold text-sm shadow-sm hover:shadow-md hover:shadow-sky-500/10 active:scale-[0.98] transition cursor-pointer"
            >
              Configure Node
            </button>
          </div>
        )}
      </section>

      {/* 2. Pass Status Hero Card */}
      <section
        className={`rounded-3xl p-5 sm:p-6 min-h-40 flex flex-wrap items-center justify-between gap-5 border-2 shadow-lg transition-all ${
          disconnected
            ? "bg-white border-slate-200 shadow-slate-200/50"
            : isOccupied
              ? "bg-gradient-to-br from-amber-50/90 via-orange-50/50 to-amber-100/30 border-amber-300 shadow-amber-500/10"
              : "bg-gradient-to-br from-emerald-50/90 via-teal-50/50 to-emerald-100/30 border-emerald-300 shadow-emerald-500/10"
        }`}
      >
        <div className="flex items-center gap-4 min-w-0">
          <div
            className={`h-16 w-16 rounded-2xl flex items-center justify-center text-white shrink-0 shadow-md ${
              disconnected
                ? "bg-slate-400 shadow-slate-400/30"
                : isOccupied
                  ? "bg-gradient-to-tr from-amber-500 to-orange-400 shadow-amber-500/30"
                  : "bg-gradient-to-tr from-emerald-500 to-teal-400 shadow-emerald-500/30"
            }`}
          >
            {disconnected ? (
              <WifiOff className="w-8 h-8" />
            ) : isOccupied ? (
              <UserX className="w-8 h-8" />
            ) : (
              <UserCheck className="w-8 h-8" />
            )}
          </div>
          <div>
            <span
              className={`inline-block rounded-full px-3 py-0.5 text-xs font-extrabold tracking-wider ${
                disconnected
                  ? "bg-slate-200 text-slate-700"
                  : isOccupied
                    ? "bg-amber-500 text-white shadow-xs"
                    : "bg-emerald-500 text-white shadow-xs"
              }`}
            >
              {disconnected
                ? "TERMINAL OFFLINE"
                : isOccupied
                  ? "PASS OCCUPIED (DEMO STATE)"
                  : "PASS AVAILABLE (DEMO STATE)"}
            </span>
            <h2 className="text-2xl font-black text-slate-900 mt-1.5 leading-tight">
              {disconnected
                ? "Terminal Disconnected"
                : isOccupied
                  ? `${studentName(activeTrip.studentName, activeTrip.studentId)} is Out of Class`
                  : "Restroom is Currently Empty"}
            </h2>
            <p className="text-xs sm:text-sm text-slate-600 mt-1 font-medium">
              {disconnected
                ? "Find and connect to the classroom kiosk to view active pass occupancy."
                : isOccupied
                  ? `Departed at ${activeTrip.departTime} for ${activeTrip.destination}.`
                  : "The physical Hallzee terminal is ready for the next student."}
            </p>
          </div>
        </div>

        {!disconnected && (
          <div className="w-full sm:w-80 bg-white/95 border border-sky-200/80 rounded-2xl p-4 flex items-center justify-between shadow-md shadow-sky-500/5">
            <div>
              <span className="text-[11px] uppercase font-bold text-slate-400 tracking-wider">
                {isOccupied ? "Trip Elapsed" : "Pass State"}
              </span>
              <div
                className={`text-2xl font-black font-timer-display leading-tight ${
                  isOccupied ? "text-amber-600" : "text-emerald-700"
                }`}
              >
                {isOccupied ? duration(activeTrip.elapsedSeconds) : "Ready"}
              </div>
              <span className="text-xs text-slate-500 font-numeric-data font-medium">
                {isOccupied ? `Student ID: #${activeTrip.studentId}` : "Door kiosk live"}
              </span>
            </div>
            <button
              type="button"
              onClick={toggleOccupancy}
              className="rounded-2xl bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 hover:from-slate-800 hover:to-slate-700 text-white px-4 py-2.5 font-bold text-sm shadow-md shadow-slate-950/20 hover:shadow-lg hover:shadow-slate-950/30 hover:brightness-110 active:scale-[0.98] transition cursor-pointer"
            >
              {isOccupied ? "Check In" : "Simulate Tap"}
            </button>
          </div>
        )}
      </section>

      {/* 3. Grid: Recent Activity (2 cols) & Roster/Policy (1 col) */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        {/* Left: Recent Hall Pass Activity */}
        <section className="xl:col-span-2 bg-white/90 border border-sky-200/80 rounded-3xl p-5 shadow-lg shadow-sky-500/5 flex flex-col justify-between">
          <div>
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-sky-100 pb-3.5 mb-3.5">
              <div>
                <h2 className="font-extrabold text-base text-slate-900">Recent Hall Pass Activity</h2>
                <p className="text-xs text-slate-500 font-medium">Latest completed student trips</p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={exportCsv}
                  className="rounded-xl border border-sky-200/80 bg-white hover:bg-sky-50 text-sky-900 px-3 py-1.5 text-xs font-bold flex items-center gap-1.5 shadow-xs transition cursor-pointer"
                >
                  <Download className="w-3.5 h-3.5" />
                  Export CSV
                </button>
                <button
                  type="button"
                  onClick={() => openModal("trips")}
                  className="rounded-xl bg-sky-100 hover:bg-sky-200 text-sky-900 px-3 py-1.5 text-xs font-bold flex items-center gap-1 shadow-xs transition cursor-pointer"
                >
                  View All History
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            {/* List of Recent Trips with Student Icons */}
            <div className="space-y-2.5">
              {trips.slice(0, 4).map((trip) => (
                <div
                  key={trip.id}
                  className="flex items-center justify-between p-3 rounded-2xl border border-sky-100/90 bg-gradient-to-r from-sky-50/40 via-white to-sky-50/20 hover:border-sky-200 transition"
                >
                  {/* Left: Avatar / Status Icon + Student Details */}
                  <div className="flex items-center gap-3 min-w-0">
                    <div
                      className={`h-10 w-10 rounded-2xl flex items-center justify-center shrink-0 shadow-xs ${
                        trip.status === "OCCUPIED"
                          ? "bg-amber-100/90 text-amber-700 border border-amber-200"
                          : "bg-emerald-100/90 text-emerald-700 border border-emerald-200"
                      }`}
                    >
                      {trip.status === "OCCUPIED" ? (
                        <Clock className="w-5 h-5" />
                      ) : (
                        <UserCheck className="w-5 h-5" />
                      )}
                    </div>
                    <div className="min-w-0">
                      <div className="font-bold text-sm text-slate-900 truncate">
                        {studentName(trip.studentName, trip.studentId)}
                      </div>
                      <div className="text-xs text-slate-500 font-medium">
                        #{trip.studentId} • {trip.departTime} → {trip.returnTime}
                      </div>
                    </div>
                  </div>

                  {/* Right: Duration + Status Badge */}
                  <div className="text-right shrink-0 pl-2">
                    <div className="font-bold text-xs text-slate-800 font-numeric-data">
                      {duration(trip.durationSeconds)}
                    </div>
                    <span
                      className={`inline-block text-[10px] rounded-full px-2 py-0.5 font-extrabold mt-0.5 ${
                        trip.status === "OCCUPIED"
                          ? "bg-amber-100 text-amber-800 border border-amber-200"
                          : "bg-emerald-100 text-emerald-800 border border-emerald-200"
                      }`}
                    >
                      {trip.status === "OCCUPIED" ? "Out" : "Returned"}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Right: Roster & Pass Policy Card */}
        <section className="bg-white/90 border border-sky-200/80 rounded-3xl p-5 shadow-lg shadow-sky-500/5 flex flex-col justify-between gap-4">
          <div>
            <div className="flex items-center gap-2.5 border-b border-sky-100 pb-3.5 mb-3.5">
              <div className="h-8 w-8 rounded-xl bg-sky-100 text-sky-600 flex items-center justify-center shadow-xs shrink-0">
                <ShieldCheck className="w-4 h-4" />
              </div>
              <h2 className="font-extrabold text-base text-slate-900">Roster & Pass Policy</h2>
            </div>

            {/* Inner-shadow Container 1: Pass Policy Summary */}
            <div className="bg-gradient-to-b from-sky-50/90 to-sky-100/60 border border-sky-200/80 rounded-2xl p-4 shadow-[inset_0_2px_4px_rgba(0,0,0,0.06),inset_0_1px_2px_rgba(0,0,0,0.04)] text-xs space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-slate-600 font-medium">Pass Capacity:</span>
                <strong className="font-extrabold text-slate-900">1 Student</strong>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-slate-600 font-medium">Overdue Warning:</span>
                <strong className="font-extrabold text-amber-600">8 minutes</strong>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-slate-600 font-medium">Daily Max:</span>
                <strong className="font-extrabold text-slate-900">3 trips</strong>
              </div>
            </div>

            {/* Inner-shadow Container 2: Loaded Roster CSV */}
            <div className="mt-3 bg-gradient-to-b from-sky-50/90 to-sky-100/60 border border-sky-200/80 rounded-2xl p-4 shadow-[inset_0_2px_4px_rgba(0,0,0,0.06),inset_0_1px_2px_rgba(0,0,0,0.04)] space-y-1">
              <span className="text-[10px] uppercase font-bold text-slate-500 tracking-wider">
                Loaded Roster CSV
              </span>
              <div className="font-mono-hardware text-xs font-bold text-slate-900 truncate">
                {rosterFileName}
              </div>
            </div>
          </div>

          {/* Manage Roster Action Button */}
          <button
            type="button"
            onClick={() => openModal("roster")}
            className="w-full hallzee-gradient-btn rounded-2xl py-3 font-bold text-xs flex items-center justify-center gap-2 cursor-pointer"
          >
            <Users className="w-4 h-4" />
            Manage Student Roster CSV
          </button>
        </section>
      </div>
    </div>
  );
}
