"use client";

import { useMemo, useState } from "react";
import { ArrowUpDown, Download, History, Search } from "./Icons";
import ModalDialog from "./ModalDialog";
import { useHallzee } from "../HallzeeProvider";
import type { Trip } from "../types";

export function TripsContent() {
  const { trips, exportCsv } = useHallzee();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("ALL");
  const [sort, setSort] = useState<keyof Trip>("id");
  const [direction, setDirection] = useState<"asc" | "desc">("desc");

  const results = useMemo(
    () =>
      trips
        .filter((trip) => {
          const query = search.toLowerCase();
          const matches =
            trip.studentName.toLowerCase().includes(query) ||
            trip.studentId.includes(search) ||
            trip.id.toLowerCase().includes(query);
          return matches && (status === "ALL" || trip.status === status);
        })
        .sort(
          (a, b) =>
            String(a[sort] ?? "").localeCompare(String(b[sort] ?? ""), undefined, {
              numeric: true,
            }) * (direction === "asc" ? 1 : -1),
        ),
    [trips, search, status, sort, direction],
  );

  const changeSort = (field: keyof Trip) => {
    if (sort === field) {
      setDirection((value) => (value === "asc" ? "desc" : "asc"));
    } else {
      setSort(field);
      setDirection("asc");
    }
  };

  return (
    <div className="space-y-4">
      {/* Action and Filter Controls */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <label className="relative flex-1 min-w-[240px] max-w-md">
          <span className="sr-only">Search trips</span>
          <Search className="w-4 h-4 absolute left-3.5 top-3 text-slate-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Filter student name, ID, or trip..."
            className="w-full border border-sky-200 bg-sky-50/70 focus:bg-white focus:border-sky-400 rounded-2xl pl-10 pr-4 py-2 text-sm text-slate-800 placeholder-slate-400 outline-none transition"
          />
        </label>

        <div className="flex items-center gap-3">
          <div className="flex bg-sky-100/80 p-1 rounded-2xl border border-sky-200/60">
            {[
              ["ALL", "All"],
              ["OCCUPIED", "Active"],
              ["COMPLETED", "Completed"],
            ].map(([value, label]) => (
              <button
                key={value}
                type="button"
                onClick={() => setStatus(value)}
                className={`px-3.5 py-1.5 rounded-xl font-bold text-xs transition cursor-pointer ${
                  status === value
                    ? "bg-white text-sky-900 shadow-sm"
                    : "text-slate-600 hover:text-sky-900"
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          <button
            type="button"
            onClick={exportCsv}
            className="hallzee-gradient-btn rounded-2xl px-4 py-2 text-sm font-bold flex items-center gap-2 cursor-pointer"
          >
            <Download className="w-4 h-4" />
            Export CSV
          </button>
        </div>
      </div>

      {/* Trips Table */}
      <div className="overflow-x-auto rounded-2xl border border-sky-200/80 shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="bg-gradient-to-r from-sky-50 to-cyan-50/40 text-sky-950 uppercase text-xs border-b border-sky-100">
            <tr>
              {[
                ["id", "Trip ID"],
                ["studentId", "Student ID"],
                ["studentName", "Student Name"],
                ["departTime", "Departed"],
                ["returnTime", "Returned"],
                ["durationSeconds", "Duration"],
                ["status", "Status"],
              ].map(([field, label]) => (
                <th key={field} className="px-4 py-3 font-extrabold">
                  <button
                    type="button"
                    onClick={() => changeSort(field as keyof Trip)}
                    className="flex items-center gap-1.5 font-extrabold hover:text-sky-600 transition cursor-pointer"
                  >
                    {label}
                    <ArrowUpDown className="w-3 h-3 text-slate-400" />
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-sky-100 bg-white">
            {results.map((trip) => (
              <tr key={trip.id} className="hover:bg-sky-50/60 transition-colors">
                <td className="px-4 py-3 font-mono-hardware text-xs text-slate-500">{trip.id}</td>
                <td className="px-4 py-3 font-bold text-slate-800">#{trip.studentId}</td>
                <td className="px-4 py-3 font-bold text-slate-900">
                  {trip.studentName || (
                    <span className="italic font-normal text-slate-400">
                      Student ID #{trip.studentId} (Unmapped)
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-slate-600">{trip.departTime}</td>
                <td className="px-4 py-3 text-slate-600">{trip.returnTime}</td>
                <td className="px-4 py-3 font-bold text-slate-800 font-numeric-data">
                  {Math.floor(trip.durationSeconds / 60)}m {trip.durationSeconds % 60}s
                </td>
                <td className="px-4 py-3">
                  <span
                    className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold ${
                      trip.status === "OCCUPIED"
                        ? "bg-amber-100 text-amber-800 border border-amber-200"
                        : "bg-emerald-100 text-emerald-800 border border-emerald-200"
                    }`}
                  >
                    {trip.status === "OCCUPIED" ? "Active" : "Completed"}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {results.length === 0 && (
          <div className="p-10 text-center text-slate-500 bg-slate-50/50">
            No trips match the current filter or search criteria.
          </div>
        )}
      </div>
    </div>
  );
}

export default function TripsModal({ onClose }: { onClose: () => void }) {
  return (
    <ModalDialog
      title="Complete Hall Pass History Log"
      subtitle="All recorded kiosk transactions with roster matches and durations"
      icon={History}
      maxWidthClass="max-w-4xl"
      onClose={onClose}
    >
      <TripsContent />
    </ModalDialog>
  );
}
