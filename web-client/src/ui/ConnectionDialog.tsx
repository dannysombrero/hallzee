import { useState, useEffect } from "react";
import { Radio, HelpCircle } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { InfoTooltip } from "./Tooltip";
import { capabilities } from "../app/capabilities";

interface TerminalItem {
  id: string;
  name: string;
  isPaired: boolean;
  isInUse: boolean;
  rssi: number | null;
  nativeDevice?: any;
}

function getSignalInfo(rssi: number | null | undefined) {
  if (rssi == null) {
    return { quality: "Unknown", text: "Signal unavailable", count: 0 };
  }
  const count = rssi >= -60 ? 4 : rssi >= -70 ? 3 : rssi >= -80 ? 2 : 1;
  const quality = count === 4 ? "Excellent" : count === 3 ? "Good" : count === 2 ? "Fair" : "Weak";
  return { quality, text: `${rssi} dBm`, count };
}

export function ConnectionDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [code, setCode] = useState("");
  const [sameClass, setSameClass] = useState(false);
  const [selectedId, setSelectedId] = useState<string>("");
  const [isScanning, setIsScanning] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [devices, setDevices] = useState<TerminalItem[]>([]);
  const [statusText, setStatusText] = useState(
    "Claimed terminals require their owner to reconnect. Busy terminals cannot be newly paired. To pair a ready terminal, enter pairing mode on the kiosk and use its displayed passkey."
  );

  useEffect(() => {
    let active = true;

    async function loadDevices() {
      const list: TerminalItem[] = [];
      let remembered: any[] = [];

      if (typeof navigator !== "undefined" && navigator.bluetooth?.getDevices) {
        try {
          remembered = await navigator.bluetooth.getDevices();
        } catch {
          // ignore
        }
      }

      // 1. Saved terminal in database
      if (state.terminal) {
        const matchingNative = remembered.find(
          (d) =>
            d.id === state.terminal?.deviceIdHint ||
            (state.terminal?.customName && d.name === state.terminal.customName) ||
            (state.terminal?.terminalId && d.name?.endsWith(state.terminal.terminalId.slice(-4))) ||
            remembered.length === 1
        );
        list.push({
          id: state.terminal.terminalId,
          name: state.terminal.customName || `Hallzee-${state.terminal.terminalId.slice(-4)}`,
          isPaired: true,
          isInUse: false,
          rssi: -52,
          nativeDevice: matchingNative,
        });
      }

      // 2. Previously permitted devices from Web Bluetooth API
      for (const d of remembered) {
        const id = d.id || "simulated-gatt";
        if (!list.some((item) => item.id === id || (item.nativeDevice && item.nativeDevice.id === id))) {
          list.push({
            id,
            name: d.name || "Hallzee Terminal",
            isPaired: true,
            isInUse: false,
            rssi: -52,
            nativeDevice: d,
          });
        }
      }

      if (!active) return;
      setDevices(list);
      setSelectedId(list[0]?.id || "");
      if (list.length > 0 && list[0].isPaired) {
        setStatusText("Your saved terminal is ready to reconnect. Click Reconnect to resume sync.");
      }
    }

    void loadDevices();
    return () => {
      active = false;
    };
  }, [state.terminal]);

  const handleScan = () => {
    setIsScanning(true);
    setStatusText("Searching for devices...");
    void (async () => {
      let remembered: any[] = [];
      if (typeof navigator !== "undefined" && navigator.bluetooth?.getDevices) {
        try {
          remembered = await navigator.bluetooth.getDevices();
        } catch {
          // ignore
        }
      }
      const list: TerminalItem[] = [];
      if (state.terminal) {
        const matchingNative = remembered.find(
          (d) =>
            d.id === state.terminal?.deviceIdHint ||
            (state.terminal?.customName && d.name === state.terminal.customName) ||
            (state.terminal?.terminalId && d.name?.endsWith(state.terminal.terminalId.slice(-4))) ||
            remembered.length === 1
        );
        list.push({
          id: state.terminal.terminalId,
          name: state.terminal.customName || `Hallzee-${state.terminal.terminalId.slice(-4)}`,
          isPaired: true,
          isInUse: false,
          rssi: -52,
          nativeDevice: matchingNative,
        });
      }
      for (const d of remembered) {
        const id = d.id || "simulated-gatt";
        if (!list.some((item) => item.id === id || (item.nativeDevice && item.nativeDevice.id === id))) {
          list.push({
            id,
            name: d.name || "Hallzee Terminal",
            isPaired: true,
            isInUse: false,
            rssi: -52,
            nativeDevice: d,
          });
        }
      }
      setDevices(list);
      setSelectedId(list[0]?.id || "");
      setStatusText(
        list.length > 0 && list[0].isPaired
          ? "Your saved terminal is ready to reconnect. Click Reconnect to resume sync."
          : "Claimed terminals require their owner to reconnect. To pair a ready terminal, enter pairing mode on the kiosk and use its displayed passkey."
      );
      setIsScanning(false);
    })();
  };

  const handleConnect = () => {
    setIsConnecting(true);
    setStatusText("Connecting to terminal…");
    const selected = devices.find((d) => d.id === selectedId);
    const value = !selected?.isPaired && code.length === 6 ? code : undefined;
    setCode("");

    const connectPromise =
      selected?.nativeDevice && !value
        ? controller?.connectDevice(selected.nativeDevice)
        : controller?.chooseTerminal(value);

    void connectPromise
      ?.then((ok) => {
        setIsConnecting(false);
        if (ok) {
          onClose();
        } else {
          setStatusText(
            "Connection failed. Ensure kiosk is in range, awake, and not claimed by another device."
          );
        }
      })
      .catch((err) => {
        setIsConnecting(false);
        setStatusText(`Connection failed: ${err instanceof Error ? err.message : String(err)}`);
      });
  };

  const selectedDevice = devices.find((d) => d.id === selectedId);
  const isSavedTerminal = selectedDevice?.isPaired;

  const isBusy = state.busy || isScanning || isConnecting;
  const canConnect =
    Boolean(selectedId) &&
    !isBusy &&
    (isSavedTerminal ? true : code.length === 6 && sameClass);

  return (
    <Dialog
      title="Find Nearby Terminals"
      subtitle="Discover nearby Hallzee Bluetooth LE kiosks."
      size="compact"
      onClose={onClose}
    >
      <p className="dialog-status-text">{statusText}</p>

      {!capabilities().bluetooth && (
        <div className="banner warning" role="note">
          <p>
            <strong>Web Bluetooth is unavailable in this browser.</strong> Direct terminal Bluetooth
            connection requires Google Chrome or Microsoft Edge. Local classroom records, rosters,
            bell policies, and trip management remain fully functional.
          </p>
        </div>
      )}

      {/* Discovered / Nearby Devices Box */}
      <div className="hallzee-device-box" role="listbox" aria-label="Nearby terminals">
        {devices.length === 0 ? (
          <div className="terminal-picker-empty">
            {isScanning
              ? "Searching for devices…"
              : "No saved or nearby terminals found. To pair an unclaimed kiosk, enter pairing mode on the kiosk (* and # for five seconds), enter its 6-digit passkey below, and click Connect & Sync."}
          </div>
        ) : (
          devices.map((device) => {
            const signal = getSignalInfo(device.rssi);
            const isSelected = selectedId === device.id;
            return (
              <div
                key={device.id}
                role="option"
                aria-selected={isSelected}
                tabIndex={0}
                className={`terminal-picker-item ${isSelected ? "selected" : ""}`}
                onClick={() => setSelectedId(device.id)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    setSelectedId(device.id);
                  }
                }}
              >
                <div className="terminal-picker-item-icon">
                  <Radio size={18} strokeWidth={2} />
                </div>
                <div className="terminal-picker-item-info">
                  <div className="terminal-picker-item-name">{device.name}</div>
                  <div className="terminal-picker-signal-row">
                    <span className="terminal-picker-signal-label">Signal Strength:</span>
                    <div className="signal-bars" title={signal.quality}>
                      <div className={`signal-bar bar-1 ${signal.count >= 1 ? "filled" : ""}`} />
                      <div className={`signal-bar bar-2 ${signal.count >= 2 ? "filled" : ""}`} />
                      <div className={`signal-bar bar-3 ${signal.count >= 3 ? "filled" : ""}`} />
                      <div className={`signal-bar bar-4 ${signal.count >= 4 ? "filled" : ""}`} />
                    </div>
                    <span className="signal-quality-text">
                      {signal.quality} · {signal.text}
                    </span>
                  </div>
                </div>
                <div className="terminal-picker-item-badge">
                  {device.isInUse ? (
                    <span className="terminal-badge busy">CLAIMED OR BUSY</span>
                  ) : device.isPaired ? (
                    <span className="terminal-badge paired">CURRENTLY PAIRED</span>
                  ) : (
                    <span className="terminal-badge ready">READY TO PAIR</span>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {isSavedTerminal ? (
        <div className="banner info" role="note">
          <p>
            <strong>This terminal is already paired to this classroom.</strong> Click{" "}
            <strong>Reconnect</strong> to connect and resume sync. No pairing code or keypad gesture needed.
          </p>
        </div>
      ) : (
        <>
          <label>
            <span className="label-with-tooltip">
              Bluetooth passkey (unclaimed terminals only)
              <InfoTooltip text="Only for claiming an unclaimed terminal after an intentional owner reset. With no pass active, hold * and # for five seconds on the kiosk to show the 6-digit code. Note: Chrome/OS will also prompt for this same code to establish Bluetooth link encryption." />
            </span>
            <input
              aria-label="Physical pairing code"
              className="kiosk-pill-input"
              placeholder="6 digits shown on the kiosk"
              inputMode="numeric"
              autoComplete="off"
              type="password"
              maxLength={6}
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
            />
          </label>

          <label className="check">
            <input
              type="checkbox"
              checked={sameClass}
              onChange={(e) => setSameClass(e.target.checked)}
            />
            <span>This terminal belongs to this classroom. I understand it may contain existing trip history.</span>
          </label>
        </>
      )}

      {/* Action Row: Indeterminate Progress Bar + Scan Again + Connect & Sync */}
      <div className="modal-actions-row">
        <div className="modal-actions-left">
          {isBusy && (
            <div className="scan-progress-bar" role="progressbar" aria-label="Searching for devices">
              <div className="scan-progress-fill" />
            </div>
          )}
        </div>
        <div className="modal-actions-right">
          <button
            type="button"
            className="hallzee-pill-btn-sky"
            disabled={isBusy}
            onClick={handleScan}
          >
            Scan Again
          </button>
          <button
            type="button"
            className="hallzee-pill-btn"
            disabled={!canConnect}
            onClick={handleConnect}
            aria-label={
              code ? "Choose terminal and pair" : state.terminal ? "Choose saved terminal" : "Connect & Sync"
            }
          >
            {isConnecting || state.busy ? "Connecting…" : isSavedTerminal ? "Reconnect" : "Connect & Sync"}
          </button>
        </div>
      </div>

      {state.error && (
        <p role="alert" className="error">
          {state.error}
        </p>
      )}

      <details className="help-disclosure">
        <summary>
          <HelpCircle size={15} />
          Pairing tips & Bluetooth troubleshooting
        </summary>
        <div className="help-disclosure-content">
          <p>
            <strong>Lost Bluetooth pairing (BT REPAIR):</strong> If your computer forgot its
            Bluetooth pairing, updated terminal firmware can repair it: with no pass active, hold{" "}
            <strong>* alone for five seconds</strong> until <strong>BT REPAIR</strong> appears.
            Then choose the saved terminal above and enter the displayed code only in the operating
            system’s prompt. This preserves ownership and records.
          </p>
          <p>
            <strong>Operating system pairing prompts:</strong> Entering the code in Hallzee does
            not complete OS-level pairing because browser security prevents web apps from reading
            system Bluetooth prompts. When Chrome, Chromebook, Windows, or macOS asks for a Bluetooth
            pairing code, enter the same 6-digit code shown on the terminal there too.
          </p>
          <p>
            <strong>Ownership & Release:</strong> A terminal already owned by a desktop app or
            another browser must be released there first. Clearing browser Bluetooth permissions
            does not release ownership.
          </p>
        </div>
      </details>
    </Dialog>
  );
}
