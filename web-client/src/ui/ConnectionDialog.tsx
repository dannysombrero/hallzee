import { useState, useEffect, useRef } from "react";
import { Radio, HelpCircle, Globe, Copy, ExternalLink } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { capabilities } from "../app/capabilities";
import { errorText } from "../app/errors";
import type { DiscoveredTerminal } from "../transport/TerminalDiscovery";

export function ConnectionDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [tab, setTab] = useState<"bluetooth" | "virtual">(() =>
    state.virtualTerminal?.active ? "virtual" : "bluetooth",
  );
  const [virtualCode, setVirtualCode] = useState(
    () => state.virtualTerminal?.roomCode || `ROOM-${Math.floor(100 + Math.random() * 900)}`,
  );
  const [virtualPin, setVirtualPin] = useState("");
  const [copied, setCopied] = useState(false);
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
    void controller
      ?.knownTerminals()
      .then((rows) => {
        if (active) setDevices(rows);
      })
      .catch((error) => {
        if (active) setLocalError(errorText(error));
      });
    return () => {
      active = false;
      mounted.current = false;
      attempt.current?.abort();
      endDiscovery?.();
    };
  }, [controller]);

  const remember = (terminal: DiscoveredTerminal) =>
    setDevices((rows) => [
      terminal,
      ...rows.filter(
        (row) =>
          row.device?.id !== terminal.device?.id &&
          (!terminal.terminalId || row.terminalId !== terminal.terminalId),
      ),
    ]);

  const select = async (row?: DiscoveredTerminal) => {
    if (!controller || working || state.busy) return;
    attempt.current?.abort();
    controller.cancelInspection();
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
      if (!selected) {
        setNotice("");
        return;
      }
      remember(selected);
      if (row?.terminalId && row.terminalId !== selected.terminalId) {
        controller.cancelInspection();
        setLocalError("This is not the selected terminal. Choose the matching terminal ID.");
      } else if (selected.pairingStatus === "Paired to other device") {
        setNotice(
          "Paired to other device. Release it from its owner’s Hallzee client before pairing here.",
        );
      } else if (
        selected.pairingStatus === "Currently Paired" ||
        selected.pairingStatus === "Last Paired"
      ) {
        setNotice("Reconnecting…");
        if (
          await controller.connectDevice(
            selected.device!,
            undefined,
            selected.terminalId,
            control.signal,
          )
        ) {
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
      const ok = await controller.connectDevice(
        candidate.device,
        value,
        candidate.terminalId,
        control.signal,
      );
      if (ok && mounted.current) onClose();
    } catch (error) {
      if (mounted.current) setLocalError(errorText(error));
    } finally {
      if (mounted.current) setWorking(false);
    }
  };

  const startVirtual = async () => {
    if (!controller || working || state.busy) return;
    setWorking(true);
    setLocalError("");
    try {
      await controller.startVirtualTerminal(virtualCode, virtualPin);
      if (mounted.current) onClose();
    } catch (err) {
      if (mounted.current) setLocalError(errorText(err));
    } finally {
      if (mounted.current) setWorking(false);
    }
  };

  const stopVirtual = () => {
    controller?.stopVirtualTerminal();
  };

  const stationUrl =
    typeof window !== "undefined"
      ? `${window.location.origin}/pass/${(state.virtualTerminal?.roomCode || virtualCode).toUpperCase()}`
      : `/pass/${virtualCode}`;

  const copyUrl = () => {
    void navigator.clipboard.writeText(stationUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const busy = working || state.busy;
  const close = () => {
    attempt.current?.abort();
    setCode("");
    onClose();
  };

  return (
    <Dialog
      title={candidate ? "Enter terminal pairing code" : "Connect Classroom Terminal"}
      subtitle={
        candidate
          ? candidate.name
          : tab === "bluetooth"
            ? "Discover nearby Hallzee Bluetooth LE kiosks."
            : "Launch a zero-hardware Virtual Web Terminal for tablets or Chromebooks."
      }
      size="compact"
      onClose={close}
    >
      <div style={{ display: "flex", gap: "8px", marginBottom: "16px" }}>
        <button
          type="button"
          className={tab === "bluetooth" ? "hallzee-pill-btn" : "hallzee-pill-btn-sky"}
          onClick={() => setTab("bluetooth")}
        >
          <Radio size={14} style={{ marginRight: "4px", verticalAlign: "middle" }} />
          Bluetooth Terminal
        </button>
        <button
          type="button"
          className={tab === "virtual" ? "hallzee-pill-btn" : "hallzee-pill-btn-sky"}
          onClick={() => setTab("virtual")}
        >
          <Globe size={14} style={{ marginRight: "4px", verticalAlign: "middle" }} />
          Virtual Web Terminal
        </button>
      </div>

      {tab === "virtual" ? (
        <div>
          {state.virtualTerminal?.active ? (
            <div>
              <div
                style={{
                  background: "rgba(16, 185, 129, 0.1)",
                  border: "1px solid rgba(16, 185, 129, 0.3)",
                  borderRadius: "12px",
                  padding: "16px",
                  marginBottom: "16px",
                  textAlign: "center",
                }}
              >
                <span style={{ color: "#059669", fontWeight: 700, fontSize: "13px" }}>
                  ● VIRTUAL TERMINAL ONLINE
                </span>
                <h2
                  style={{
                    fontSize: "26px",
                    fontWeight: 800,
                    margin: "8px 0",
                    letterSpacing: "2px",
                  }}
                >
                  {state.virtualTerminal.roomCode}
                </h2>
                <p style={{ fontSize: "13px", color: "#64748b", margin: 0 }}>
                  Door station and students can connect at this room URL:
                </p>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "8px",
                    background: "#f8fafc",
                    padding: "8px 12px",
                    borderRadius: "8px",
                    marginTop: "8px",
                    border: "1px solid #e2e8f0",
                  }}
                >
                  <code style={{ fontSize: "12px", flex: 1, wordBreak: "break-all" }}>
                    {stationUrl}
                  </code>
                  <button
                    type="button"
                    className="hallzee-pill-btn-sky"
                    style={{ padding: "4px 8px", fontSize: "12px" }}
                    onClick={copyUrl}
                  >
                    <Copy size={12} style={{ marginRight: "4px" }} />
                    {copied ? "Copied!" : "Copy"}
                  </button>
                  <a
                    href={stationUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="hallzee-pill-btn-sky"
                    style={{ padding: "4px 8px", fontSize: "12px", textDecoration: "none" }}
                  >
                    <ExternalLink size={12} />
                  </a>
                </div>
              </div>

              <div className="modal-actions-row">
                <button
                  type="button"
                  className="hallzee-pill-btn-sky"
                  style={{ color: "#dc2626" }}
                  onClick={stopVirtual}
                >
                  Stop Virtual Terminal
                </button>
                <button type="button" className="hallzee-pill-btn" onClick={onClose}>
                  Done
                </button>
              </div>
            </div>
          ) : (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void startVirtual();
              }}
            >
              <p className="dialog-status-text">
                Run a web-based pass station for tablets or 1:1 Chromebooks with zero physical hardware.
              </p>

              <label style={{ display: "block", marginBottom: "12px" }}>
                Classroom Room Code
                <div style={{ display: "flex", gap: "8px", marginTop: "4px" }}>
                  <input
                    aria-label="Room code"
                    className="kiosk-pill-input"
                    placeholder="e.g. RODRIG235"
                    autoCapitalize="characters"
                    autoComplete="off"
                    maxLength={16}
                    value={virtualCode}
                    disabled={busy}
                    onChange={(e) =>
                      setVirtualCode(e.target.value.toUpperCase().replace(/[^A-Z0-9-]/g, ""))
                    }
                  />
                  <button
                    type="button"
                    className="hallzee-pill-btn-sky"
                    style={{ whiteSpace: "nowrap" }}
                    onClick={() => setVirtualCode(`ROOM-${Math.floor(100 + Math.random() * 900)}`)}
                  >
                    Random
                  </button>
                </div>
              </label>

              <label style={{ display: "block", marginBottom: "16px" }}>
                Recovery PIN (Optional, 4-6 digits)
                <input
                  aria-label="Recovery PIN"
                  className="kiosk-pill-input"
                  placeholder="Optional PIN to claim room on another computer"
                  type="password"
                  inputMode="numeric"
                  maxLength={6}
                  value={virtualPin}
                  disabled={busy}
                  onChange={(e) => setVirtualPin(e.target.value.replace(/\D/g, ""))}
                />
              </label>

              <div className="modal-actions-row">
                <button type="button" className="hallzee-pill-btn-sky" onClick={onClose}>
                  Cancel
                </button>
                <button
                  type="submit"
                  className="hallzee-pill-btn"
                  disabled={busy || virtualCode.trim().length < 3}
                >
                  {busy ? "Starting…" : "Start Virtual Terminal"}
                </button>
              </div>
            </form>
          )}
        </div>
      ) : candidate ? (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void pair();
          }}
        >
          <p>
            Enter the six-digit code shown in pairing mode on this terminal. With no pass active,
            hold * and # for five seconds to open pairing mode.
          </p>
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
          <p>
            This pairs the terminal to this browser profile and syncs its records into this
            classroom.
          </p>
          <div className="modal-actions-row">
            <button
              type="button"
              className="hallzee-pill-btn-sky"
              disabled={busy}
              onClick={() => {
                attempt.current?.abort();
                controller?.cancelInspection();
                setCandidate(undefined);
                setCode("");
                controller?.clearError();
              }}
            >
              Back
            </button>
            <button
              type="submit"
              className="hallzee-pill-btn"
              disabled={busy || code.length !== 6}
            >
              {busy ? "Pairing…" : "Pair & Connect"}
            </button>
          </div>
        </form>
      ) : (
        <>
          <p className="dialog-status-text">
            Choose a terminal to pair or reconnect. Enter its code in Hallzee only when pairing for
            the first time.
          </p>
          {!capabilities().bluetooth && (
            <p className="banner warning">
              Web Bluetooth is unavailable in this browser. Use Chrome or Edge and check your
              school’s Bluetooth policy.
            </p>
          )}
          <div className="terminal-picker-list" aria-label="Known terminals">
            {devices.length === 0 ? (
              <p>No saved terminals. Click Find nearby terminals to choose one in your browser.</p>
            ) : (
              devices.map((device) => (
                <button
                  type="button"
                  key={device.device?.id ?? device.terminalId}
                  className="terminal-picker-item"
                  disabled={busy}
                  onClick={() => void select(device)}
                  aria-label={`${device.hasSavedCredential || device.pairingStatus === "Currently Paired" ? "Reconnect" : "Select"} ${device.name}`}
                >
                  <span className="terminal-picker-item-icon">
                    <Radio size={22} />
                  </span>
                  <div className="terminal-picker-item-info">
                    <strong className="terminal-picker-item-name">{device.name}</strong>
                    {device.terminalId && <div>{device.terminalId}</div>}
                  </div>
                  <span
                    className={`terminal-badge ${device.pairingStatus === "Currently Paired" || device.pairingStatus === "Last Paired" ? "paired" : device.pairingStatus === "Paired to other device" ? "busy" : device.pairingStatus === "Not Paired" ? "ready" : "unknown"}`}
                  >
                    {device.pairingStatus}
                  </span>
                </button>
              ))
            )}
          </div>
          <p>
            Last Paired means this browser has a saved Hallzee credential for the terminal; select it
            to reconnect and confirm its current status. Status unknown means Hallzee has not checked
            a terminal yet.
          </p>
          <div className="modal-actions-row">
            <button
              type="button"
              className="hallzee-pill-btn"
              disabled={busy}
              onClick={() => void select()}
            >
              {busy ? "Connecting…" : "Find nearby terminals"}
            </button>
          </div>
        </>
      )}
      {notice && <p role="status">{notice}</p>}
      {(localError || state.error) && (
        <p role="alert" className="error">
          {localError || state.error}
        </p>
      )}
      <details className="help-disclosure">
        <summary>
          <HelpCircle size={15} /> Pairing tips & Bluetooth troubleshooting
        </summary>
        <div className="help-disclosure-content">
          <p>
            <strong>Reconnect:</strong> Hallzee tries your saved terminal when this app reopens and
            after a dropped connection. If your browser needs you to choose it again, click the
            saved terminal or Find nearby terminals. No new pairing code or pairing mode is needed.
          </p>
          <p>
            <strong>Bluetooth prompts:</strong> Updated firmware uses a code only in Hallzee. The
            browser or operating system may still ask you to allow Bluetooth access. Firmware that
            asks for a separate Bluetooth passkey needs updating.
          </p>
          <p>
            <strong>Browser’s Paired badge:</strong> Chrome can show “Paired” when this site already
            has permission to access the terminal, even after a failed connection. Hallzee cannot
            hide that browser badge; it does not confirm ownership or a connection.
          </p>
          <p>
            <strong>Lost Bluetooth pairing (BT REPAIR):</strong> With updated firmware and no
            active pass, hold * alone for five seconds to repair a stale Bluetooth bond, then
            reconnect. Keep Hallzee site data and ownership.
          </p>
          <p>
            <strong>Ownership & Release:</strong> Another app or browser profile counts as another
            device. Clearing Bluetooth permission does not release ownership. Terminal names stay
            the same when paired.
          </p>
        </div>
      </details>
    </Dialog>
  );
}

