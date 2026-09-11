import { useState } from "react";
import { Bluetooth, ShieldCheck } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { capabilities } from "../app/capabilities";
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
        <p>Keep your Hallzee terminal nearby. Your browser will open its own device chooser.</p>
      </div>
      {!capabilities().bluetooth && (
        <div className="banner warning" role="note">
          <p>
            <strong>Web Bluetooth is unavailable in this browser.</strong> Direct terminal Bluetooth
            connection requires Google Chrome or Microsoft Edge. Local classroom records, rosters,
            bell policies, and trip management remain fully functional.
          </p>
        </div>
      )}
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
          <p className="muted">
            If the operating system forgot its Bluetooth pairing, updated terminal firmware can
            repair it: with no pass active, hold <strong>* alone for five seconds</strong> until
            <strong> BT REPAIR</strong> appears. Then choose the saved terminal above and enter the
            displayed code only in the operating system’s prompt. This preserves ownership and
            records. If BT REPAIR does not appear, update the terminal firmware first.
          </p>
          <hr />
          <p>
            The code field below is only for claiming an unclaimed terminal after an intentional
            owner reset. It does not repair an existing owner’s Bluetooth bond.
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
        Entering this code does not complete operating-system pairing. If macOS or Windows asks for
        a Bluetooth code, enter the current code shown on the terminal there too. Hallzee waits up
        to one minute for secure pairing and does not save the code.
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
        A terminal already owned by a desktop app or another browser must be released there first.
        Clearing browser Bluetooth permissions does not release ownership.
      </p>
      {state.error && (
        <p role="alert" className="error">
          {state.error}
        </p>
      )}
    </Dialog>
  );
}
