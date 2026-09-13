import { useState, useEffect, useRef } from "react";
import { Radio, HelpCircle } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { capabilities } from "../app/capabilities";
import { errorText } from "../app/errors";
import type { DiscoveredTerminal } from "../transport/TerminalDiscovery";

export function ConnectionDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [code, setCode] = useState("");
  const [devices, setDevices] = useState<DiscoveredTerminal[]>([]);
  const [candidate, setCandidate] = useState<DiscoveredTerminal>();
  const [working, setWorking] = useState(false);
  const [notice, setNotice] = useState("");
  const [localError, setLocalError] = useState("");
  const attempt = useRef<AbortController | undefined>(undefined);
  const mounted = useRef(false);

  useEffect(() => {
    let active = true;
    mounted.current = true;
    const endDiscovery = controller?.beginDiscovery();
    void controller?.knownTerminals().then((rows) => {
      if (active) setDevices(rows);
    }).catch((error) => {
      if (active) setLocalError(errorText(error));
    });
    return () => {
      active = false;
      mounted.current = false;
      attempt.current?.abort();
      endDiscovery?.();
    };
  }, [controller]);

  const remember = (terminal: DiscoveredTerminal) => setDevices((rows) => [
    terminal,
    ...rows.filter((row) => row.device?.id !== terminal.device?.id &&
      (!terminal.terminalId || row.terminalId !== terminal.terminalId)),
  ]);

  const select = async (row?: DiscoveredTerminal) => {
    if (!controller || working || state.busy) return;
    const control = new AbortController();
    attempt.current = control;
    setWorking(true);
    setCode("");
    setCandidate(undefined);
    setLocalError("");
    setNotice("Checking terminal pairing status…");
    try {
      const selected = await controller.inspectTerminal(row?.device, control.signal);
      if (!mounted.current || control.signal.aborted) return;
      if (!selected) { setNotice(""); return; }
      remember(selected);
      if (row?.terminalId && row.terminalId !== selected.terminalId) {
        setLocalError("This is not the selected terminal. Choose the matching terminal ID.");
      } else if (selected.pairingStatus === "Paired to other device") {
        setNotice("Paired to other device. Release it from its owner’s Hallzee client before pairing here.");
      } else if (selected.pairingStatus === "Currently Paired") {
        setNotice("Reconnecting…");
        if (await controller.connectDevice(selected.device!, undefined, selected.terminalId, control.signal)) {
          if (mounted.current) onClose();
        } else setNotice("");
      } else if (selected.inUse) {
        setNotice("Not Paired. Check in every active pass before pairing this terminal.");
      } else {
        setCandidate(selected);
        setNotice("");
      }
    } catch (error) {
      if (mounted.current) setLocalError(errorText(error));
    } finally {
      if (mounted.current) setWorking(false);
    }
  };

  const pair = async () => {
    if (!controller || !candidate?.device || code.length !== 6 || working) return;
    const value = code;
    setCode("");
    setWorking(true);
    setLocalError("");
    const control = new AbortController();
    attempt.current = control;
    try {
      const ok = await controller.connectDevice(candidate.device, value, candidate.terminalId, control.signal);
      if (ok && mounted.current) onClose();
    } catch (error) {
      if (mounted.current) setLocalError(errorText(error));
    } finally {
      if (mounted.current) setWorking(false);
    }
  };
  const busy = working || state.busy;
  const close = () => { attempt.current?.abort(); setCode(""); onClose(); };
  return (
    <Dialog
      title={candidate ? "Enter terminal pairing code" : "Find Nearby Terminals"}
      subtitle={candidate ? candidate.name : "Discover nearby Hallzee Bluetooth LE kiosks."}
      size="compact"
      onClose={close}
    >
      {candidate ? (
        <form onSubmit={(event) => { event.preventDefault(); void pair(); }}>
          <p>Enter the six-digit code shown in pairing mode on this terminal. With no pass active, hold * and # for five seconds to open pairing mode.</p>
          <label>
            Terminal pairing code
            <input
              aria-label="Physical pairing code"
              className="kiosk-pill-input"
              placeholder="6 digits shown on the terminal"
              inputMode="numeric"
              autoComplete="off"
              type="password"
              maxLength={6}
              autoFocus
              value={code}
              disabled={busy}
              onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))}
            />
          </label>
          <p>This pairs the terminal to this browser profile and syncs its records into this classroom.</p>
          <div className="modal-actions-row">
            <button type="button" className="hallzee-pill-btn-sky" disabled={busy} onClick={() => {
              setCandidate(undefined); setCode(""); controller?.clearError();
            }}>Back</button>
            <button type="submit" className="hallzee-pill-btn" disabled={busy || code.length !== 6}>
              {busy ? "Pairing…" : "Pair & Connect"}
            </button>
          </div>
        </form>
      ) : (
        <>
          <p className="dialog-status-text">Choose a terminal to pair or reconnect. Enter its code in Hallzee only when pairing for the first time.</p>
          {!capabilities().bluetooth && <p className="banner warning">Web Bluetooth is unavailable in this browser. Use Chrome or Edge and check your school’s Bluetooth policy.</p>}
          <div className="terminal-picker-list" aria-label="Known terminals">
            {devices.length === 0 ? <p>No saved terminals. Click Find nearby terminals to choose one in your browser.</p> : devices.map((device) => (
              <button
                type="button"
                key={device.device?.id ?? device.terminalId}
                className="terminal-picker-item"
                disabled={busy}
                onClick={() => void select(device)}
                aria-label={`${device.hasSavedCredential || device.pairingStatus === "Currently Paired" ? "Reconnect" : "Select"} ${device.name}`}
              >
                <span className="terminal-picker-item-icon"><Radio size={22} /></span>
                <div className="terminal-picker-item-info">
                  <strong className="terminal-picker-item-name">{device.name}</strong>
                  {device.terminalId && <div>{device.terminalId}</div>}
                </div>
                <span className={`terminal-badge ${device.pairingStatus === "Currently Paired" ? "paired" : device.pairingStatus === "Paired to other device" ? "busy" : device.pairingStatus === "Not Paired" ? "ready" : "unknown"}`}>
                  {device.pairingStatus}
                </span>
              </button>
            ))}
          </div>
          <p>Browsers list only terminals you have allowed. Status unknown means Hallzee has not checked the terminal yet, even if this browser has a saved pairing. Select it to check and reconnect.</p>
          <div className="modal-actions-row">
            <button type="button" className="hallzee-pill-btn" disabled={busy} onClick={() => void select()}>
              {busy ? "Connecting…" : "Find nearby terminals"}
            </button>
          </div>
        </>
      )}
      {notice && <p role="status">{notice}</p>}
      {(localError || state.error) && <p role="alert" className="error">{localError || state.error}</p>}
      <details className="help-disclosure">
        <summary><HelpCircle size={15} /> Pairing tips & Bluetooth troubleshooting</summary>
        <div className="help-disclosure-content">
          <p><strong>Reconnect:</strong> Hallzee tries your saved terminal when this app reopens and after a dropped connection. If your browser needs you to choose it again, click the saved terminal or Find nearby terminals. No new pairing code or pairing mode is needed.</p>
          <p><strong>Bluetooth prompts:</strong> Updated firmware uses a code only in Hallzee. The browser or operating system may still ask you to allow Bluetooth access. Firmware that asks for a separate Bluetooth passkey needs updating.</p>
          <p><strong>Browser’s Paired badge:</strong> Chrome can show “Paired” when this site already has permission to access the terminal, even after a failed connection. Hallzee cannot hide that browser badge; it does not confirm ownership or a connection.</p>
          <p><strong>Lost Bluetooth pairing (BT REPAIR):</strong> With updated firmware and no active pass, hold * alone for five seconds to repair a stale Bluetooth bond, then reconnect. Keep Hallzee site data and ownership.</p>
          <p><strong>Ownership & Release:</strong> Another app or browser profile counts as another device. Clearing Bluetooth permission does not release ownership. Terminal names stay the same when paired.</p>
        </div>
      </details>
    </Dialog>
  );
}
