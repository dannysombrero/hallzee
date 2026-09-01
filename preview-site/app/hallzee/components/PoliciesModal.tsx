"use client";

import { Calendar, PauseCircle, ShieldCheck } from "./Icons";
import ModalDialog from "./ModalDialog";

export function PoliciesContent() {
  return (
    <div className="grid md:grid-cols-2 gap-6">
      {/* Classroom Pass Policies */}
      <section className="space-y-4">
        <div className="flex items-center gap-2 text-slate-900 font-extrabold text-sm border-b border-sky-100 pb-2">
          <ShieldCheck className="w-4 h-4 text-sky-600" />
          <span>Pass Policy Rules</span>
        </div>

        <div className="bg-gradient-to-b from-sky-50/90 to-sky-100/60 border border-sky-200/80 rounded-2xl p-4 shadow-[inset_0_2px_4px_rgba(0,0,0,0.06),inset_0_1px_2px_rgba(0,0,0,0.04)] space-y-3 text-sm">
          <div className="flex justify-between items-center py-1 border-b border-sky-200/40">
            <span className="text-slate-600">Students allowed out simultaneously:</span>
            <strong className="font-extrabold text-slate-900">1 Student</strong>
          </div>
          <div className="flex justify-between items-center py-1 border-b border-sky-200/40">
            <span className="text-slate-600">Overdue warning threshold:</span>
            <strong className="font-extrabold text-amber-600">8 minutes</strong>
          </div>
          <div className="flex justify-between items-center py-1">
            <span className="text-slate-600">Daily pass guideline per student:</span>
            <strong className="font-extrabold text-slate-900">3 trips</strong>
          </div>
        </div>

        <div className="p-3.5 rounded-2xl border border-amber-200 bg-amber-50/80 flex items-start gap-3 text-xs text-amber-900">
          <PauseCircle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
          <div>
            <strong className="font-bold block">Pause rules is planned</strong>
            <p className="text-amber-700/90 mt-0.5">
              Trips will continue recording even when classroom rules are temporarily paused.
            </p>
          </div>
        </div>
      </section>

      {/* Bell Schedule */}
      <section className="space-y-4">
        <div className="flex items-center gap-2 text-slate-900 font-extrabold text-sm border-b border-sky-100 pb-2">
          <Calendar className="w-4 h-4 text-sky-600" />
          <span>Bell Schedule</span>
        </div>

        <p className="text-xs text-slate-500 leading-relaxed">
          Editable bell schedules and start/end-of-period alerts are planned profile features.
        </p>

        <div className="bg-gradient-to-b from-sky-50/90 to-sky-100/60 border border-sky-200/80 rounded-2xl p-4 shadow-[inset_0_2px_4px_rgba(0,0,0,0.06),inset_0_1px_2px_rgba(0,0,0,0.04)] space-y-2">
          <div className="flex justify-between items-center">
            <span className="font-extrabold text-slate-900 text-sm">Period 3 (Chemistry AP)</span>
            <span className="rounded-full bg-emerald-100 text-emerald-800 text-[10px] font-bold px-2 py-0.5 border border-emerald-200">
              Current Period
            </span>
          </div>
          <div className="text-xs text-slate-600 font-mono-hardware">9:15 AM – 10:05 AM (50 mins)</div>
        </div>

        <div className="space-y-1.5 text-xs text-slate-500">
          <div className="flex justify-between py-1 border-b border-sky-100">
            <span>Period 1:</span>
            <span className="font-mono-hardware">7:30 AM – 8:20 AM</span>
          </div>
          <div className="flex justify-between py-1 border-b border-sky-100">
            <span>Period 2:</span>
            <span className="font-mono-hardware">8:25 AM – 9:10 AM</span>
          </div>
          <div className="flex justify-between py-1">
            <span>Period 4:</span>
            <span className="font-mono-hardware">10:10 AM – 11:00 AM</span>
          </div>
        </div>
      </section>
    </div>
  );
}

export default function PoliciesModal({ onClose }: { onClose: () => void }) {
  return (
    <ModalDialog
      title="Policies & Bell Times"
      subtitle="Classroom pass limits, overdue warnings, and period schedules"
      icon={Calendar}
      maxWidthClass="max-w-3xl"
      onClose={onClose}
    >
      <PoliciesContent />
    </ModalDialog>
  );
}
