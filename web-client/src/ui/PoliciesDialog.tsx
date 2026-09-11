import { useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { useHallzee } from "../app/HallzeeProvider";
import { Dialog } from "./Dialog";
import { weekdays } from "../domain/PolicyScheduleService";
import type { BellPeriod, Decision } from "../storage/schema";
export function PoliciesDialog({ onClose }: { onClose: () => void }) {
  const { controller, state } = useHallzee();
  const [policy, setPolicy] = useState(state.policy);
  const [periods, setPeriods] = useState(state.periods);
  const [exceptions, setExceptions] = useState(state.exceptions);
  const update = (index: number, patch: Partial<BellPeriod>) =>
    setPeriods(periods.map((p, i) => (i === index ? { ...p, ...patch } : p)));
  return (
    <Dialog title="Policies & bell times" onClose={onClose}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void controller?.savePolicy(policy, periods, exceptions).then((ok) => ok && onClose());
        }}
      >
        <p role="status">
          {state.policy.appliedRevision === state.policy.revision && state.policy.appliedDate
            ? `Applied to terminal on ${state.policy.appliedDate}`
            : state.policy.revision === 0
              ? "Terminal policy has not been configured."
              : "Saved locally · Pending terminal acknowledgment"}
        </p>
        <div className="form-grid">
          {(
            [
              { key: "capacity", label: "Pass capacity", min: 1, max: 8 },
              { key: "warningSeconds", label: "Overdue after (seconds)", min: 1, max: 86400 },
              { key: "dailyGuideline", label: "Daily guideline (0 disables)", min: 0, max: 1000 },
              { key: "firstMinutes", label: "First window (minutes)", min: 0, max: 1440 },
              { key: "lastMinutes", label: "Last window (minutes)", min: 0, max: 1440 },
            ] as const
          ).map(({ key, label, min, max }) => (
            <label key={key}>
              {label}
              <input
                type="number"
                required
                min={min}
                max={max}
                value={policy[key]}
                onChange={(e) => setPolicy({ ...policy, [key]: Number(e.target.value) })}
              />
            </label>
          ))}
          {(["firstAction", "lastAction"] as const).map((key) => (
            <label key={key}>
              {key === "firstAction" ? "First window action" : "Last window action"}
              <select
                value={policy[key]}
                onChange={(e) => setPolicy({ ...policy, [key]: e.target.value as Decision })}
              >
                {["Allow", "Warn", "Lock"].map((v) => (
                  <option key={v}>{v}</option>
                ))}
              </select>
            </label>
          ))}
        </div>
        <label className="check">
          <input
            type="checkbox"
            checked={policy.enforcement}
            onChange={(e) => setPolicy({ ...policy, enforcement: e.target.checked })}
          />
          Enforce bell windows on the terminal while this browser is disconnected
        </label>
        <p className="muted">
          Missing cached dates allow passes. Students can always check back in. Daily and overdue
          guidelines are advisory.
        </p>
        <div className="toolbar">
          <h3>Bell periods</h3>
          <button
            type="button"
            className="secondary"
            onClick={() =>
              setPeriods([
                ...periods,
                {
                  workspaceId: policy.workspaceId,
                  scheduleId: crypto.randomUUID(),
                  periodName: "New period",
                  scheduleName: "Regular",
                  classSection: "",
                  start: "09:00",
                  end: "10:00",
                  weekdays: ["Mon", "Tue", "Wed", "Thu", "Fri"],
                },
              ])
            }
          >
            <Plus size={16} />
            Add period
          </button>
        </div>
        {periods.map((p, i) => (
          <fieldset key={p.scheduleId}>
            <legend>{p.periodName}</legend>
            <div className="form-grid">
              <label>
                Period name
                <input
                  required
                  value={p.periodName}
                  onChange={(e) => update(i, { periodName: e.target.value })}
                />
              </label>
              <label>
                Schedule name
                <input
                  required
                  value={p.scheduleName}
                  onChange={(e) => update(i, { scheduleName: e.target.value })}
                />
              </label>
              <label>
                Class section
                <input
                  value={p.classSection}
                  onChange={(e) => update(i, { classSection: e.target.value })}
                />
              </label>
              <label>
                Start
                <input
                  type="time"
                  required
                  value={p.start}
                  onChange={(e) => update(i, { start: e.target.value })}
                />
              </label>
              <label>
                End
                <input
                  type="time"
                  required
                  value={p.end}
                  onChange={(e) => update(i, { end: e.target.value })}
                />
              </label>
            </div>
            <div className="button-row wrap">
              {weekdays.map((day) => (
                <label className="check" key={day}>
                  <input
                    type="checkbox"
                    checked={p.weekdays.includes(day)}
                    onChange={(e) =>
                      update(i, {
                        weekdays: e.target.checked
                          ? [...p.weekdays, day]
                          : p.weekdays.filter((d) => d !== day),
                      })
                    }
                  />
                  {day}
                </label>
              ))}
              <button
                type="button"
                className="icon-button"
                aria-label={`Remove ${p.periodName}`}
                onClick={() => setPeriods(periods.filter((_, j) => i !== j))}
              >
                <Trash2 size={17} />
              </button>
            </div>
          </fieldset>
        ))}
        <div className="toolbar">
          <h3>Date exceptions</h3>
          <button
            type="button"
            className="secondary"
            onClick={() =>
              setExceptions([
                ...exceptions,
                {
                  workspaceId: policy.workspaceId,
                  date: "",
                  scheduleName: "Regular",
                  isNoSchool: false,
                },
              ])
            }
          >
            <Plus size={16} />
            Add exception
          </button>
        </div>
        {exceptions.map((exception, i) => (
          <fieldset key={i}>
            <div className="form-grid">
              <label>
                Date
                <input
                  type="date"
                  required
                  value={exception.date}
                  onChange={(e) =>
                    setExceptions(
                      exceptions.map((v, j) => (j === i ? { ...v, date: e.target.value } : v)),
                    )
                  }
                />
              </label>
              <label>
                Schedule
                <input
                  disabled={exception.isNoSchool}
                  value={exception.scheduleName}
                  onChange={(e) =>
                    setExceptions(
                      exceptions.map((v, j) =>
                        j === i ? { ...v, scheduleName: e.target.value } : v,
                      ),
                    )
                  }
                />
              </label>
            </div>
            <label className="check">
              <input
                type="checkbox"
                checked={exception.isNoSchool}
                onChange={(e) =>
                  setExceptions(
                    exceptions.map((v, j) =>
                      j === i ? { ...v, isNoSchool: e.target.checked } : v,
                    ),
                  )
                }
              />
              No school
            </label>
            <button
              type="button"
              className="secondary"
              onClick={() => setExceptions(exceptions.filter((_, j) => i !== j))}
            >
              Remove exception
            </button>
          </fieldset>
        ))}
        {state.error && (
          <p role="alert" className="error">
            {state.error}
          </p>
        )}
        <button disabled={state.busy}>Save policies & schedules</button>
        <p className="muted">
          Changes are saved locally. Terminal settings are marked applied only after acknowledgment.
          Up to 96 dated windows can be transferred.
        </p>
      </form>
    </Dialog>
  );
}
