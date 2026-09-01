"use client";
import { ArrowUpDown, Download, Search } from "../components/Icons";
import { useMemo, useState } from "react";
import { useHallzee } from "../HallzeeProvider";
import type { Trip } from "../types";

export default function TripsPage() {
  const { trips, exportCsv } = useHallzee();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("ALL");
  const [sort, setSort] = useState<keyof Trip>("id");
  const [direction, setDirection] = useState<"asc"|"desc">("desc");
  const results = useMemo(() => trips.filter((trip) => {
    const query = search.toLowerCase();
    const matches = trip.studentName.toLowerCase().includes(query) || trip.studentId.includes(search) || trip.id.toLowerCase().includes(query);
    return matches && (status === "ALL" || trip.status === status);
  }).sort((a,b) => String(a[sort] ?? "").localeCompare(String(b[sort] ?? ""), undefined, { numeric: true }) * (direction === "asc" ? 1 : -1)), [trips, search, status, sort, direction]);
  const changeSort = (field: keyof Trip) => { if (sort === field) setDirection((value) => value === "asc" ? "desc" : "asc"); else { setSort(field); setDirection("asc"); } };
  return <section className="bg-white/95 border border-sky-200 rounded-3xl p-6 shadow-lg pb-24"><div className="flex flex-wrap justify-between gap-4 border-b border-sky-100 pb-4"><div><h1 className="text-xl font-black">Complete Hall Pass History Log</h1><p className="text-sm text-slate-500 mt-1">All recorded kiosk transactions with roster matches and durations</p></div><button onClick={exportCsv} className="rounded-2xl bg-sky-600 text-white px-4 py-2 font-bold flex gap-2"><Download className="w-4"/>Export CSV</button></div><div className="flex flex-wrap justify-between gap-3 my-4"><label className="relative flex-1 max-w-sm"><span className="sr-only">Search trips</span><Search className="w-4 absolute left-3 top-3 text-slate-400"/><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Filter student name, ID, or trip..." className="w-full border border-sky-200 bg-sky-50 rounded-2xl pl-9 pr-3 py-2"/></label><div className="flex bg-sky-100 p-1 rounded-2xl">{[["ALL","All"],["OCCUPIED","Active"],["COMPLETED","Completed"]].map(([value,label]) => <button key={value} onClick={() => setStatus(value)} className={`px-3 py-1.5 rounded-xl font-bold text-sm ${status === value ? "bg-white shadow text-sky-900" : "text-slate-600"}`}>{label}</button>)}</div></div><div className="overflow-auto rounded-2xl border border-sky-200"><table className="w-full text-left text-sm"><thead className="bg-sky-50 text-sky-950 uppercase text-xs"><tr>{[["id","Trip ID"],["studentId","Student ID"],["studentName","Student Name"],["departTime","Departed"],["returnTime","Returned"],["durationSeconds","Duration"],["status","Status"]].map(([field,label]) => <th key={field} className="px-4 py-3"><button onClick={() => changeSort(field as keyof Trip)} className="flex gap-1 font-extrabold">{label}<ArrowUpDown className="w-3"/></button></th>)}</tr></thead><tbody className="divide-y divide-sky-100">{results.map((trip) => <tr key={trip.id} className="hover:bg-sky-50"><td className="px-4 py-3 text-slate-500">{trip.id}</td><td className="px-4 py-3 font-bold">#{trip.studentId}</td><td className="px-4 py-3 font-bold">{trip.studentName || <span className="italic text-slate-500">Student ID #{trip.studentId} (Unmapped)</span>}</td><td className="px-4 py-3">{trip.departTime}</td><td className="px-4 py-3">{trip.returnTime}</td><td className="px-4 py-3 font-bold">{Math.floor(trip.durationSeconds/60)}m {trip.durationSeconds%60}s</td><td className="px-4 py-3"><span className={`rounded-full px-2 py-1 text-xs font-bold ${trip.status === "OCCUPIED" ? "bg-amber-100 text-amber-800" : "bg-emerald-100 text-emerald-800"}`}>{trip.status}</span></td></tr>)}</tbody></table>{results.length === 0 && <div className="p-10 text-center text-slate-500">No trips match these filters.</div>}</div></section>;
}
