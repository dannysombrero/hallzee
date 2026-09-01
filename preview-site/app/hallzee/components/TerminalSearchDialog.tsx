"use client";
import { Bluetooth, Loader2, Radio, Signal, X } from "./Icons";
import { useHallzee } from "../HallzeeProvider";

export default function TerminalSearchDialog() {
  const { searchOpen, setSearchOpen, terminalState, discoveredTerminals, connectingTerminalId, connectTerminal, findTerminals } = useHallzee();
  if (!searchOpen) return null;
  const scanning = terminalState === "discovering";
  return <div className="fixed inset-0 z-50 bg-sky-950/60 backdrop-blur-sm flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="terminal-search-title">
    <div className="bg-white border-2 border-sky-200 rounded-3xl max-w-2xl w-full p-6 shadow-2xl space-y-4">
      <div className="flex justify-between border-b border-sky-100 pb-3"><div className="flex gap-3"><div className="p-2.5 rounded-2xl bg-sky-500 text-white"><Bluetooth className="w-5 h-5"/></div><div><h2 id="terminal-search-title" className="font-extrabold">Find Nearby Hallzee Terminal</h2><p className="text-sm text-slate-500">Choose the kiosk assigned to your classroom.</p></div></div><button aria-label="Close terminal search" onClick={() => setSearchOpen(false)}><X/></button></div>
      {scanning ? <div className="py-12 text-center"><Loader2 className="w-9 h-9 animate-spin text-sky-500 mx-auto mb-3"/><h3 className="font-bold">Searching for Terminals...</h3><p className="text-sm text-slate-500">Scanning nearby Bluetooth devices.</p></div> : <div className="space-y-2">{discoveredTerminals.map((terminal) => <div key={terminal.id} className="flex items-center justify-between p-4 rounded-2xl border border-sky-200 bg-sky-50/50"><div className="flex gap-3"><Radio className="text-sky-600"/><div><h3 className="font-bold">{terminal.name}</h3><p className="text-sm text-slate-500 font-mono-hardware">{terminal.id} • {terminal.rssi} dBm</p></div></div><div className="flex items-center gap-3"><span className="text-sm flex items-center gap-1"><Signal className="w-4"/>{terminal.signal}</span><button onClick={() => connectTerminal(terminal)} className="rounded-xl bg-sky-600 text-white px-4 py-2 font-bold">{connectingTerminalId === terminal.id ? "Connecting..." : "Connect"}</button></div></div>)}</div>}
      <div className="flex justify-between pt-2 border-t border-sky-100"><button onClick={() => setSearchOpen(false)} className="px-4 py-2 text-slate-600">Cancel</button><button onClick={findTerminals} disabled={scanning} className="px-4 py-2 rounded-xl bg-sky-100 text-sky-900 font-bold disabled:opacity-50">Scan Again</button></div>
    </div>
  </div>;
}
