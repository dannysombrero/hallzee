"use client";
import { Bluetooth, Radio, Save, Search } from "../components/Icons";
import { useEffect, useState } from "react";
import { useHallzee } from "../HallzeeProvider";

export default function TerminalPage() {
  const { terminalState, connectedTerminalName, terminalId, terminalSettings, findTerminals, applyTerminalSettings } = useHallzee();
  const [name, setName] = useState(terminalSettings.name);
  const [length, setLength] = useState(terminalSettings.maxStudentIdLength);

  // Keep the form in sync when the context settings change (e.g., after connecting).
  useEffect(() => {
    setName(terminalSettings.name);
    setLength(terminalSettings.maxStudentIdLength);
  }, [terminalSettings]);

  return <div className="grid lg:grid-cols-[1fr_420px] gap-6 pb-24">
    <section className="bg-white rounded-3xl border border-sky-200 p-6 shadow-lg">
      <div className="flex justify-between gap-4 border-b border-sky-100 pb-4">
        <div className="flex gap-3">
          <div className="p-3 rounded-2xl bg-sky-500 text-white"><Bluetooth/></div>
          <div>
            <h1 className="text-xl font-black">Terminal Connection</h1>
            <p className="text-sm text-slate-500">Find and identify the correct Hallzee kiosk.</p>
          </div>
        </div>
        </div>
      <button onClick={findTerminals} className="rounded-2xl bg-sky-600 text-white px-4 py-2 font-bold flex gap-2"><Search className="w-4"/>Find Terminal</button>
      <div className="mt-5 rounded-2xl bg-sky-50 p-5">
        <div className="flex justify-between">
          <span className="uppercase text-xs font-bold text-slate-500">Connection state</span>
          <strong>{terminalState.toUpperCase()}</strong>
        </div>
        <h2 className="text-lg font-black mt-4">{connectedTerminalName}</h2>
        <p className="font-mono-hardware text-sm text-slate-500 mt-1">{terminalId}</p>
      </div>
      <div className="mt-5 p-4 border border-amber-200 bg-amber-50 rounded-2xl text-sm">
        <strong>Prototype note:</strong> discovery and connections are simulated here. Physical BLE validation remains in the Windows client.
      </div>
    </section>
    <section className="bg-white rounded-3xl border border-sky-200 p-6 shadow-lg">
      <Radio className="text-sky-600"/>
      <h2 className="text-xl font-black mt-3">Terminal Device Settings</h2>
      <div className="space-y-4 mt-5">
        <label className="block text-sm font-bold">Terminal assigned name
          <input value={name} onChange={(event) => setName(event.target.value)} className="mt-1 w-full rounded-2xl border border-sky-200 bg-sky-50 px-3 py-2 font-mono-hardware"/>
        </label>
        <label className="block text-sm font-bold">Maximum student ID length
          <select value={length} onChange={(event) => setLength(Number(event.target.value))} className="mt-1 w-full rounded-2xl border border-sky-200 bg-sky-50 px-3 py-2">
            {Array.from({length:13},(_,index)=>index+4).map((value)=> <option key={value} value={value}>{value} digits</option>)}
          </select>
        </label>
        <button onClick={() => applyTerminalSettings({ name, maxStudentIdLength: length })} className="w-full rounded-2xl bg-sky-600 text-white py-2.5 font-bold flex justify-center gap-2"><Save className="w-4"/>Save Configuration</button>
      </div>
    </section>
  </div>;
}
