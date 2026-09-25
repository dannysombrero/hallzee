import { useState } from "react";
import { ArrowRight } from "lucide-react";
import { normalizeRoomCode, validRoomCode } from "../../domain/RoomLinks";

export function JoinPage({ invalidCode = false }: { invalidCode?: boolean }) {
  const [code, setCode] = useState("");
  const [error, setError] = useState(invalidCode ? "Check the terminal code or ask your teacher for a new link." : "");
  return <main className="join-page">
    <section className="join-card">
      <div className="join-brand"><img src="/icons/hallzee-logo.png" alt="" /><span>Hallzee</span></div>
      <p className="eyebrow">CLASSROOM TERMINAL</p>
      <h1>Where are you checking in?</h1>
      <p>Enter the terminal code shared by your teacher.</p>
      <form onSubmit={event => {
        event.preventDefault();
        if (!validRoomCode(code)) { setError("Use 3–16 letters, numbers, or hyphens, starting with a letter or number."); return; }
        window.location.assign(`/${encodeURIComponent(normalizeRoomCode(code).toLowerCase())}`);
      }}>
        <label htmlFor="terminal-code">Terminal code</label>
        <input id="terminal-code" className="kiosk-pill-input join-code-input" placeholder="e.g. ROOM-2107"
          value={code} maxLength={16} autoComplete="off" autoCapitalize="characters" spellCheck={false}
          onChange={event => { setCode(event.target.value); setError(""); }} aria-describedby={error ? "join-error" : undefined} />
        {error && <p id="join-error" role="alert" className="error">{error}</p>}
        <button className="hallzee-pill-btn" type="submit" disabled={!code.trim()}>Join Terminal <ArrowRight size={17} /></button>
      </form>
      <p className="join-note">Your teacher needs to keep the virtual terminal open.</p>
    </section>
  </main>;
}
