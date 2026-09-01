"use client";

import { Bluetooth, Loader2, Radio, Signal, X } from "./Icons";
import { useHallzee } from "../HallzeeProvider";

export default function TerminalSearchDialog() {
  const {
    searchOpen,
    setSearchOpen,
    terminalState,
    discoveredTerminals,
    connectingTerminalId,
    connectTerminal,
    findTerminals,
  } = useHallzee();

  if (!searchOpen) return null;

  const scanning = terminalState === "discovering";

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 overflow-y-auto animate-in fade-in duration-200"
      role="dialog"
      aria-modal="true"
      aria-labelledby="terminal-search-title"
    >
      {/* Backdrop */}
      <button
        type="button"
        aria-label="Close search overlay"
        tabIndex={-1}
        onClick={() => setSearchOpen(false)}
        className="fixed inset-0 bg-sky-950/60 backdrop-blur-sm transition-opacity cursor-default"
      />

      {/* Dialog Box */}
      <div className="relative z-10 bg-white border border-sky-200/80 rounded-3xl max-w-xl w-full p-6 shadow-2xl space-y-4 my-auto animate-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-sky-100 pb-3.5">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-2xl bg-gradient-to-tr from-sky-500 to-cyan-400 text-white flex items-center justify-center shadow-md shadow-sky-500/20 shrink-0">
              <Bluetooth className="w-5 h-5" />
            </div>
            <div>
              <h2 id="terminal-search-title" className="font-extrabold text-slate-900 text-base">
                Find Nearby Hallzee Terminal
              </h2>
              <p className="text-xs text-slate-500 font-medium">Choose the kiosk assigned to your classroom.</p>
            </div>
          </div>
          <button
            type="button"
            aria-label="Close terminal search"
            onClick={() => setSearchOpen(false)}
            className="h-8 w-8 rounded-full bg-slate-100 hover:bg-slate-200 text-slate-500 hover:text-slate-800 flex items-center justify-center transition cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Device List or Scanning Spinner */}
        {scanning ? (
          <div className="py-12 text-center">
            <Loader2 className="w-9 h-9 animate-spin text-sky-500 mx-auto mb-3" />
            <h3 className="font-extrabold text-slate-900 text-sm">Searching for Terminals...</h3>
            <p className="text-xs text-slate-500 mt-1">Scanning nearby Bluetooth Low Energy devices.</p>
          </div>
        ) : (
          <div className="space-y-2.5">
            {discoveredTerminals.map((terminal) => (
              <div
                key={terminal.id}
                className="flex items-center justify-between p-3.5 rounded-2xl border border-sky-200/80 bg-gradient-to-r from-sky-50/50 to-white hover:border-sky-300 transition"
              >
                <div className="flex items-center gap-3">
                  <div className="h-9 w-9 rounded-xl bg-sky-100 text-sky-600 flex items-center justify-center shrink-0">
                    <Radio className="w-4 h-4" />
                  </div>
                  <div>
                    <h3 className="font-bold text-sm text-slate-900 leading-tight">{terminal.name}</h3>
                    <p className="text-xs text-slate-500 font-mono-hardware mt-0.5">
                      {terminal.id} • {terminal.rssi} dBm
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <span className="text-xs font-semibold text-slate-600 flex items-center gap-1">
                    <Signal className="w-3.5 h-3.5 text-emerald-600" />
                    {terminal.signal}
                  </span>
                  <button
                    type="button"
                    onClick={() => connectTerminal(terminal)}
                    disabled={connectingTerminalId !== null}
                    className="hallzee-gradient-btn rounded-xl px-4 py-2 text-xs font-bold cursor-pointer disabled:opacity-50"
                  >
                    {connectingTerminalId === terminal.id ? "Connecting..." : "Connect"}
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Footer */}
        <div className="flex items-center justify-between pt-3 border-t border-sky-100">
          <button
            type="button"
            onClick={() => setSearchOpen(false)}
            className="px-4 py-2 text-xs font-bold text-slate-600 hover:text-slate-800 transition cursor-pointer"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={findTerminals}
            disabled={scanning}
            className="px-4 py-2 rounded-xl bg-sky-100 hover:bg-sky-200 text-sky-900 text-xs font-bold transition disabled:opacity-50 cursor-pointer shadow-xs"
          >
            Scan Again
          </button>
        </div>
      </div>
    </div>
  );
}
