import { useState } from "react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { validateBackup, type Backup } from "../storage/BackupService";
import { download } from "../domain/TripReports";
import { errorText } from "../app/errors";
export function DataSettingsDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [workspace, setWorkspace] = useState(state.workspace!);
  const [backup, setBackup] = useState<Backup | null>(null);
  const [error, setError] = useState("");
  const saveBackup = async () => {
    if (
      !window.confirm(
        "Download an unencrypted backup containing student data? Choose a district-approved local destination. Bluetooth will disconnect for a consistent snapshot.",
      )
    )
      return;
    const text = await controller?.backup();
    if (text) download(text, "hallzee-classroom-backup.json");
  };
  return (
    <Dialog title="Classroom & local data" onClose={onClose}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void controller?.saveWorkspace(workspace);
        }}
      >
        <div className="form-grid">
          {(["name", "teacher", "school", "room", "timeZone"] as const).map((key) => (
            <label key={key}>
              {
                {
                  name: "Classroom name",
                  teacher: "Teacher",
                  school: "School",
                  room: "Room",
                  timeZone: "Classroom time zone",
                }[key]
              }
              <input
                required={key === "name" || key === "timeZone"}
                maxLength={200}
                value={workspace[key]}
                onChange={(e) => setWorkspace({ ...workspace, [key]: e.target.value })}
              />
            </label>
          ))}
        </div>
        <button disabled={state.busy}>Save classroom</button>
      </form>
      <hr />
      <h3>Stored on this computer</h3>
      <p>
        {state.persistent
          ? "Persistent storage granted"
          : "Persistent storage not granted — keep a current backup"}
      </p>
      <p className="muted">
        {(state.usage / 1048576).toFixed(1)} MiB used · {(state.quota / 1073741824).toFixed(1)} GiB
        estimated quota. Clearing browser data, removing this profile, or resetting the computer can
        erase your classroom and owner key.
      </p>
      <button className="secondary" disabled={state.busy} onClick={() => void saveBackup()}>
        Download classroom backup
      </button>
      <label className="file-label">
        Restore a web-client backup
        <input
          type="file"
          accept=".json,application/json"
          disabled={state.busy}
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (!file) return;
            setError("");
            if (file.size > 50 * 1024 * 1024) {
              setError("Backup exceeds 50 MiB.");
              return;
            }
            void file
              .text()
              .then((text) => {
                setBackup(validateBackup(text));
              })
              .catch((e) => setError(errorText(e)));
          }}
        />
      </label>
      {backup && (
        <div className="inset">
          <h3>Review replacement</h3>
          <p>
            {backup.workspaces[0].name} · {backup.trips.length} trips ·{" "}
            {backup.rosterStudents.length} students · exported{" "}
            {new Date(backup.exportedAtUtc).toLocaleString()}
          </p>
          <p>
            This replaces classroom data. Owner keys are not in backups. This browser’s existing
            keys are retained; another browser must claim the terminal separately.
          </p>
          <button
            className="danger"
            disabled={state.busy}
            onClick={() => {
              if (
                window.confirm(
                  "Replace local classroom data with this backup? Download a current backup first if you need it.",
                )
              )
                void controller?.restore(backup).then((ok) => {
                  if (ok) {
                    setBackup(null);
                    onClose();
                  }
                });
            }}
          >
            Replace local classroom data
          </button>
        </div>
      )}
      {state.terminal && (
        <>
          <hr />
          <h3>Terminal history recovery</h3>
          <p className="muted">
            Replay records still on this terminal. Identical records are deduplicated; conflicting
            history stops for review.
          </p>
          <button
            className="secondary"
            disabled={state.busy}
            onClick={() => {
              if (window.confirm(`Recover all history from ${state.terminal?.terminalId}?`))
                void controller?.recover();
            }}
          >
            Recover terminal history
          </button>
          <p className="muted">
            Only after a known factory reset or flash rollback: export your old history, disconnect,
            then begin a new generation. This removes this terminal’s local trip rows.
          </p>
          <button
            className="danger"
            disabled={state.busy || state.session === "Authenticated"}
            onClick={() => {
              if (
                window.confirm(
                  "Have you exported the old terminal history? Remove its local trip rows and restart the cursor at zero for a known reset?",
                )
              )
                void controller?.resetHistory();
            }}
          >
            Start a new terminal history
          </button>
        </>
      )}
      {(error || state.error) && (
        <p className="error" role="alert">
          {error || state.error}
        </p>
      )}
      <hr />
      <p className="muted">
        Hallzee 1.2.0-dev.1 · Browser hardware acceptance pending. No student records or owner
        credentials are uploaded. Downloads leave the browser sandbox. Guest and Incognito profiles
        are not supported.
      </p>
      <p>
        <a href="/LICENSE.txt" target="_blank" rel="noreferrer">
          License
        </a>{" "}
        ·{" "}
        <a href="/THIRD-PARTY-NOTICES.md" target="_blank" rel="noreferrer">
          Third-party notices
        </a>{" "}
        ·{" "}
        <a href="/source.txt" target="_blank" rel="noreferrer">
          Corresponding source
        </a>
      </p>
    </Dialog>
  );
}
