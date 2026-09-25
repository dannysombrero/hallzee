import { useEffect, useRef, useState } from "react";
import QRCode from "qrcode";
import { Copy, ExternalLink, Maximize2, Download, Globe } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { errorText } from "../app/errors";
import { roomLinks, validRoomCode } from "../domain/RoomLinks";
import { Dialog } from "./Dialog";

function randomCode() { return `ROOM-${crypto.getRandomValues(new Uint32Array(1))[0] % 900000 + 100000}`; }
function JoinQr({ url, large }: { url: string; large: boolean }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const [error, setError] = useState(false);
  useEffect(() => {
    setError(false);
    if (canvas.current) void QRCode.toCanvas(canvas.current, url, {
      width: large ? 320 : 200, margin: 4, errorCorrectionLevel: "M",
    }).catch(() => setError(true));
  }, [url, large]);
  return <div className="join-qr">
    {error ? <p role="alert">QR code unavailable. Use the join link below.</p> : <canvas ref={canvas} role="img" aria-label="QR code to join this terminal" />}
    {!large && <button type="button" className="text-button" disabled={error} onClick={() => {
      canvas.current?.toBlob(blob => {
        if (!blob) return;
        const objectUrl = URL.createObjectURL(blob), a = document.createElement("a");
        a.href = objectUrl; a.download = "hallzee-join-qr.png"; a.click();
        setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
      });
    }}><Download size={14} /> Download QR</button>}
  </div>;
}
export function VirtualTerminalDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const room = state.virtualTerminal;
  const [code, setCode] = useState(() => room?.roomCode || randomCode());
  const [pin, setPin] = useState("");
  const [working, setWorking] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState("");
  const [large, setLarge] = useState(false);
  const [now, setNow] = useState(Date.now());
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []);
  const links = roomLinks(room?.roomCode || code);
  const open = room?.active && room.expiresAtEpoch * 1000 > now;
  const ended = room && ["expired", "closed"].includes(room.status);
  const copy = async (value: string, label: string) => {
    try { await navigator.clipboard.writeText(value); setCopied(label); }
    catch { setError("Copy was unavailable. Select and copy the address or code below."); }
  };
  const start = async () => {
    if (!controller) return;
    setWorking(true); setError("");
    try { await controller.startVirtualTerminal(code, pin); setPin(""); }
    catch (err) { setError(errorText(err)); }
    finally { setWorking(false); }
  };
  const end = async () => {
    if (!controller || !window.confirm("End this virtual session? Students will see that the terminal is closed.")) return;
    setWorking(true); setError("");
    try { await controller.stopVirtualTerminal(); onClose(); }
    catch (err) { setError(errorText(err)); }
    finally { setWorking(false); }
  };
  return <Dialog title={large ? "Join this terminal" : room ? "Virtual Terminal" : "Start Virtual Terminal"}
    subtitle={large ? "Scan the QR code or enter the terminal code at the join address." : "A classroom pass station for student devices and door tablets."}
    size={large ? "wide" : "compact"} onClose={onClose} className={large ? "join-display-dialog" : ""}>
    {room ? <>
      <div className={`virtual-room-status ${open ? "is-open" : "is-waiting"}`} role="status">
        <span className="room-status-dot" />
        {open ? "Virtual terminal open" : ended ? "Session ended" : room.status === "starting" ? "Starting terminal…" : room.status === "error" ? "Terminal unavailable" : "Reconnecting…"}
      </div>
      <div className={`room-share ${large ? "room-share-large" : ""}`}>
        <JoinQr url={links.terminal} large={large} />
        <div className="room-share-details">
          <span className="eyebrow">TERMINAL CODE</span>
          <strong className="room-code">{room.roomCode}</strong>
          {!large && <button type="button" className="text-button" onClick={() => void copy(room.roomCode, "Code copied")}><Copy size={14} /> Copy code</button>}
          <label>Join address<a href={links.join} target="_blank" rel="noreferrer">{links.join.replace(/^https?:\/\//, "").replace(/\/$/, "")}</a></label>
          <label>Direct link<a className="room-direct-link" href={links.terminal} target="_blank" rel="noreferrer">{links.terminal}</a></label>
        </div>
      </div>
      {!large && <div className="room-share-actions">
        <button type="button" className="hallzee-pill-btn" onClick={() => void copy(links.terminal, "Link copied")}><Copy size={15} /> Copy Link</button>
        <a className="hallzee-pill-btn-sky" href={links.terminal} target="_blank" rel="noreferrer"><ExternalLink size={15} /> Open Terminal</a>
        <button type="button" className="hallzee-pill-btn-sky" onClick={() => setLarge(true)}><Maximize2 size={15} /> Display Join Code</button>
      </div>}
      {copied && !large && <p role="status">{copied}.</p>}
      {room.expiresAtEpoch > 0 && <p className="room-session-note">{ended ? "Session ended" : "Session ends"} at {new Date(room.expiresAtEpoch * 1000).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}.</p>}
      {!large && <p className="room-session-note">Keep this teacher window open and connected to the internet.</p>}
      {room.error && <p role="alert" className="error">{room.error}</p>}
      {large ? <button type="button" className="hallzee-pill-btn-sky" onClick={() => setLarge(false)}>Back to sharing</button> : <div className="modal-actions-row">
        {ended || (room.status === "error" && !room.expiresAtEpoch) ? <button type="button" className="hallzee-pill-btn" disabled={working} onClick={() => void start()}>Restart Terminal</button> : null}
        {room.status === "error" && !room.expiresAtEpoch && <button type="button" className="hallzee-pill-btn-sky" disabled={working} onClick={() => {
          void controller?.stopVirtualTerminal().then(() => { setCode(randomCode()); setError(""); }).catch(err => setError(errorText(err)));
        }}>Change Code</button>}
        <button type="button" className="text-button danger-text" disabled={working || state.busy || state.active.passes.length > 0} onClick={() => void end()}>End Session</button>
        <button type="button" className="hallzee-pill-btn-sky" onClick={onClose}>Done</button>
      </div>}
      {!large && state.active.passes.length > 0 && <p className="room-session-note">Check in or void active passes before ending or switching the session.</p>}
    </> : <form onSubmit={event => { event.preventDefault(); void start(); }}>
      {state.session === "Authenticated" && <p className="banner">Starting a virtual terminal disconnects Bluetooth. Check in active passes first.</p>}
      <label className="virtual-form-label">Terminal code
        <div className="virtual-code-row"><input className="kiosk-pill-input" aria-label="Terminal code" value={code}
          maxLength={16} autoCapitalize="characters" autoComplete="off" spellCheck={false} disabled={working}
          onChange={event => setCode(event.target.value.toUpperCase().replace(/[^A-Z0-9-]/g, ""))} />
          <button type="button" className="hallzee-pill-btn-sky" disabled={working} onClick={() => setCode(randomCode())}>Random</button></div>
      </label>
      <p className="room-session-note">Choose a recognizable code with 3–16 letters, numbers, or hyphens. Codes are not case-sensitive.</p>
      <details className="help-disclosure"><summary>Optional recovery PIN</summary>
        <label className="virtual-form-label">Recovery PIN<input className="kiosk-pill-input" aria-label="Recovery PIN" type="password" inputMode="numeric" maxLength={6}
          autoComplete="new-password" value={pin} onChange={event => setPin(event.target.value.replace(/\D/g, ""))} /></label>
        <p>Use 4–6 digits to reclaim this room on another computer. Your saved roster and history stay in this browser.</p>
      </details>
      <div className="modal-actions-row"><button type="button" className="hallzee-pill-btn-sky" onClick={onClose}>Cancel</button>
        <button type="submit" className="hallzee-pill-btn" disabled={working || state.busy || !validRoomCode(code) || (!!pin && pin.length < 4) || state.active.passes.length > 0}>
          <Globe size={15} /> {working ? "Starting…" : "Start Virtual Terminal"}</button></div>
    </form>}
    {error && <p role="alert" className="error">{error}</p>}
  </Dialog>;
}
