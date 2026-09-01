"use client";

import { SettingsContent } from "../components/SettingsModal";
import { Settings } from "../components/Icons";

export default function SettingsPage() {
  return (
    <div className="space-y-6 pb-24">
      <section className="max-w-3xl bg-white/95 border border-sky-200/80 rounded-3xl p-6 shadow-lg">
        <div className="flex items-center gap-3 border-b border-sky-100 pb-4 mb-4">
          <div className="h-10 w-10 rounded-2xl bg-gradient-to-tr from-sky-500 to-cyan-400 text-white flex items-center justify-center shadow-md shadow-sky-500/20 shrink-0">
            <Settings className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-black text-slate-900">Application Settings</h1>
            <p className="text-xs text-slate-500 font-medium mt-0.5">
              Preferences and synchronization behavior for the active classroom profile
            </p>
          </div>
        </div>
        <SettingsContent />
      </section>
    </div>
  );
}
