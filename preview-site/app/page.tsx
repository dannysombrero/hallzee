"use client";

import { useState } from "react";

type PreviewState = "ready" | "finding" | "found" | "connecting" | "syncing" | "complete";

const stateDetails: Record<PreviewState, { title: string; detail: string; tone: string }> = {
  ready: { title: "Ready to find a terminal", detail: "Choose Find terminal to start the simulated discovery flow.", tone: "green" },
  finding: { title: "Finding Bathroom-Terminal", detail: "Simulating nearby-device discovery…", tone: "blue" },
  found: { title: "Terminal ready", detail: "Bathroom-Terminal is ready to sync.", tone: "green" },
  connecting: { title: "Connecting", detail: "Simulating a secure Bluetooth connection…", tone: "blue" },
  syncing: { title: "Synchronizing", detail: "Securely retrieving trips from Bathroom-Terminal.", tone: "green" },
  complete: { title: "Sync complete", detail: "3 new trip(s) saved this session.", tone: "green" },
};

const wait = (milliseconds: number) => new Promise((resolve) => setTimeout(resolve, milliseconds));

export default function Home() {
  const [state, setState] = useState<PreviewState>("ready");
  const [savedTrips, setSavedTrips] = useState(0);
  const [log, setLog] = useState(["Preview ready. No Bluetooth hardware is used in this site."]);
  const [exported, setExported] = useState(false);
  const details = stateDetails[state];
  const found = state === "found" || state === "connecting" || state === "syncing" || state === "complete";

  const findTerminal = async () => {
    setState("finding");
    setSavedTrips(0);
    setExported(false);
    setLog((items) => [...items, "Scanning for Bathroom-Terminal…"]);
    await wait(900);
    setState("found");
    setLog((items) => [...items, "Discovery completed: 1 matching terminal."]);
  };

  const syncTerminal = async () => {
    if (!found) return;
    setState("connecting");
    setLog((items) => [...items, "Connected; time synchronization requested."]);
    await wait(800);
    setState("syncing");
    setLog((items) => [...items, "SYNC started", "Received 3 trip record(s)."]);
    await wait(1000);
    setSavedTrips(3);
    setState("complete");
    setLog((items) => [...items, "All trip records acknowledged.", "Sync completed successfully."]);
  };

  return (
    <main>
      <section className="app-shell" aria-label="Bathroom Terminal preview">
        <header className="app-header">
          <div>
            <p className="eyebrow">Preview site</p>
            <h1>Bathroom Terminal</h1>
            <p className="subtitle">Desktop sync client · Browser simulation</p>
          </div>
          <span className="simulation-badge">Simulation — no hardware connected</span>
        </header>

        <div className="content-grid">
          <div className="main-column">
            <section className="card connect-card">
              <div>
                <p className="section-label">Connection</p>
                <h2>Connect a terminal</h2>
                <p>Practice the same discovery and sync flow used by the desktop client.</p>
              </div>
              <label htmlFor="terminal">Nearby terminal</label>
              <select id="terminal" value={found ? "bathroom-terminal" : ""} disabled={!found} onChange={() => undefined}>
                <option value="">{state === "finding" ? "Searching…" : "Find a terminal first"}</option>
                <option value="bathroom-terminal">Bathroom-Terminal</option>
              </select>
              <div className="actions">
                <button className="button secondary" onClick={findTerminal} disabled={state === "finding" || state === "connecting" || state === "syncing"}>Find terminal</button>
                <button className="button primary" onClick={syncTerminal} disabled={!found || state === "connecting" || state === "syncing"}>Sync now</button>
              </div>
            </section>

            <section className="card status-card" aria-live="polite">
              <span className={`status-dot ${details.tone}`} aria-hidden="true" />
              <div>
                <h2>{details.title}</h2>
                <p>{details.detail}</p>
              </div>
            </section>

            <section className="trip-card">
              <div>
                <p>Trips saved this session</p>
                <strong>{savedTrips}</strong>
              </div>
              <span aria-hidden="true">✓</span>
            </section>
          </div>

          <section className="activity-card">
            <div className="activity-heading">
              <div>
                <p className="section-label">Simulation log</p>
                <h2>Sync activity</h2>
              </div>
              <span className="live-dot">Live</span>
            </div>
            <ol>
              {log.map((entry, index) => <li key={`${entry}-${index}`}>{entry}</li>)}
            </ol>
          </section>
        </div>

        <footer className="app-footer">
          <p>{exported ? "Sample CSV export prepared for download." : "Exports are simulated in this browser preview."}</p>
          <div className="footer-actions">
            <button className="text-button" onClick={() => setExported(true)}>Open export folder</button>
            <button className="text-button" onClick={() => setExported(true)}>Save CSV as…</button>
          </div>
        </footer>
      </section>

      <aside className="testing-note">
        <div className="warning-icon" aria-hidden="true">!</div>
        <div>
          <p className="section-label">Testing note</p>
          <h2>Windows PC required for Bluetooth tests</h2>
          <p>This site faithfully simulates the shared client flow. Finding, pairing, and syncing a physical ESP32 terminal use the Windows Bluetooth transport and must be tested with the Windows desktop app. UI, simulated sync, and export-flow checks can be done on your Mac here.</p>
        </div>
      </aside>
    </main>
  );
}
