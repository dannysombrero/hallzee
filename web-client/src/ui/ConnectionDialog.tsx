import { useState } from "react";
import { Bluetooth, ShieldCheck } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
export function ConnectionDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [code, setCode] = useState("");
  const [sameClass, setSameClass] = useState(false);
  return (
    <Dialog
      title={state.terminal ? "Connect your terminal" : "Pair your terminal"}
      onClose={onClose}
    >
      <div className="dialog-intro">
        <Bluetooth />
        <p>Keep your Hallzee terminal nearby. Chrome will open its own device chooser.</p>
      </div>
      {state.terminal ? (
        <>
          <p>
            Choose <strong>{state.terminal.customName}</strong> · {state.terminal.terminalId}. Your
            browser’s saved owner key will authenticate it.
          </p>
          <button
            disabled={state.busy}
            onClick={() => void controller?.chooseTerminal().then((ok) => ok && onClose())}
          >
            Choose saved terminal
          </button>
          <hr />
          <p>
            If a physical owner reset made the terminal unclaimed, use the fresh pairing code below.
          </p>
        </>
      ) : (
        <p>
          With no pass active, hold <strong>* and # for five seconds</strong>, then release. Enter
          the six-digit code shown on the terminal before opening the chooser.
        </p>
      )}
      <label>
        Physical pairing code
        <input
          inputMode="numeric"
          autoComplete="off"
          type="password"
          maxLength={6}
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
        />
      </label>
      <p className="muted">
        Chrome or the operating system may ask for this same code separately. The code is not saved.
      </p>
      <label className="check">
        <input
          type="checkbox"
          checked={sameClass}
          onChange={(e) => setSameClass(e.target.checked)}
        />
        This terminal belongs to this classroom. I understand it may contain existing trip history.
      </label>
      <button
        disabled={state.busy || code.length !== 6 || !sameClass}
        onClick={() => {
          const value = code;
          setCode("");
          void controller?.chooseTerminal(value).then((ok) => ok && onClose());
        }}
      >
        <ShieldCheck size={18} />
        Choose terminal and pair
      </button>
      <p className="muted">
        A terminal already owned by a desktop app must be released there first. Clearing Chrome’s
        Bluetooth permission does not release ownership.
      </p>
      {state.error && (
        <p role="alert" className="error">
          {state.error}
        </p>
      )}
    </Dialog>
  );
}
