"use client";

import { TripsContent } from "../components/TripsModal";
import { History } from "../components/Icons";

export default function TripsPage() {
  return (
    <div className="space-y-6 pb-24">
      <section className="bg-white/95 border border-sky-200/80 rounded-3xl p-6 shadow-lg">
        <div className="flex items-center gap-3 border-b border-sky-100 pb-4 mb-4">
          <div className="h-10 w-10 rounded-2xl bg-gradient-to-tr from-sky-500 to-cyan-400 text-white flex items-center justify-center shadow-md shadow-sky-500/20 shrink-0">
            <History className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-black text-slate-900">Complete Hall Pass History Log</h1>
            <p className="text-xs text-slate-500 font-medium mt-0.5">
              All recorded kiosk transactions with roster matches and durations
            </p>
          </div>
        </div>
        <TripsContent />
      </section>
    </div>
  );
}
