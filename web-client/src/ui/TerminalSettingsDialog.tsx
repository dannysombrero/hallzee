import { useState } from "react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
export function TerminalSettingsDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [name, setName] = useState(state.terminal?.customName ?? "");
  const [max, setMax] = useState(state.terminal?.maxIdLength ?? 10);
  const connected = state.session === "Authenticated";
  return (
    <Dialog title="Terminal settings" onClose={onClose}>
      {!state.terminal ? (
        <p>Connect a terminal first.</p>
      ) : (
        <>
          <p>
            <strong>{state.terminal.terminalId}</strong>
          </p>
          <p className="muted">
            {connected ? "Connected and authenticated" : "Cached settings — reconnect to verify"}
          </p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void controller?.settings(name, max);
            }}
          >
            <label>
              Terminal name
              <input
                value={name}
                maxLength={24}
                required
                onChange={(e) => setName(e.target.value)}
              />
            </label>
            <label>
              Maximum student ID length
              <input
                type="number"
                min={4}
                max={16}
                value={max}
                onChange={(e) => setMax(Number(e.target.value))}
              />
            </label>
            <button disabled={!connected || state.busy}>Save terminal settings</button>
          </form>
          <hr />
          <div className="button-row wrap">
            <button
              className="secondary"
              disabled={state.busy || !connected}
              onClick={() => controller?.disconnect()}
            >
              Disconnect
            </button>
            <button
              className="danger"
              disabled={state.busy || !connected}
              onClick={() => {
                if (
                  window.confirm(
                    "Sync this classroom, release ownership, and disconnect? History and roster stay here. No passes may be active. This does not sanitize the terminal for another teacher.",
                  )
                )
                  void controller?.unpair();
              }}
            >
              Disconnect & Unpair
            </button>
          </div>
          <p className="muted">
            Ordinary disconnect preserves ownership. Unpair only to move this same classroom to
            another client.
          </p>
        </>
      )}
      {state.error && (
        <p className="error" role="alert">
          {state.error}
        </p>
      )}
    </Dialog>
  );
}
