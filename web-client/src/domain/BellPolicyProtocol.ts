import type { Policy, BellPeriod, ScheduleException } from "../storage/schema";
import { periodsForDate, minutes } from "./PolicyScheduleService";
import { localDate } from "../sync/TerminalClock";
import { HallzeeError } from "../app/errors";
export function buildPolicyTransfer(
  first: Date,
  policy: Policy,
  periods: BellPeriod[],
  exceptions: ScheduleException[],
) {
  const commands = [`POLICY_BEGIN,${policy.enforcement ? 1 : 0}`];
  if (policy.enforcement)
    for (let day = 0; day < 14; day++) {
      const date = new Date(first.getFullYear(), first.getMonth(), first.getDate() + day);
      for (const p of periodsForDate(date, periods, exceptions)) {
        const start = minutes(p.start),
          end = minutes(p.end) <= start ? 1440 : minutes(p.end);
        const action = (v: string) => (v === "Lock" ? 2 : v === "Warn" ? 1 : 0);
        commands.push(
          `POLICY_WINDOW,${localDate(date).replaceAll("-", "")},${start},${end},${Math.min(end, start + policy.firstMinutes)},${Math.max(start, end - policy.lastMinutes)},${action(policy.firstAction)},${action(policy.lastAction)}`,
        );
      }
    }
  if (commands.length - 1 > 96)
    throw new HallzeeError(
      "TOO_MANY_WINDOWS",
      `This schedule creates ${commands.length - 1} windows; the terminal accepts 96. Simplify it before applying.`,
    );
  commands.push(`POLICY_COMMIT,${commands.length - 1}`);
  return commands;
}
