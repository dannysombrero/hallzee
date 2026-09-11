import type { Policy, BellPeriod, ScheduleException, Decision } from "../storage/schema";
import { HallzeeError } from "../app/errors";
import { localDate } from "../sync/TerminalClock";
import { validDate } from "../protocol/messages";
export const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
export const minutes = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
export const validBellTime = (time: string) => /^([01]\d|2[0-3]):[0-5]\d$/.test(time);
export function periodsForDate(date: Date, periods: BellPeriod[], exceptions: ScheduleException[]) {
  const exception = exceptions.find((e) => e.date === localDate(date));
  if (exception?.isNoSchool) return [];
  return periods
    .filter((p) =>
      exception
        ? p.scheduleName.trim().toLowerCase() === exception.scheduleName.trim().toLowerCase()
        : p.weekdays.includes(weekdays[date.getDay()]),
    )
    .sort((a, b) => a.start.localeCompare(b.start));
}
export function resolvePeriod(date: Date, periods: BellPeriod[], exceptions: ScheduleException[]) {
  const minute = date.getHours() * 60 + date.getMinutes() + date.getSeconds() / 60;
  return periodsForDate(date, periods, exceptions).find(
    (p) =>
      minute >= minutes(p.start) &&
      minute < (minutes(p.end) <= minutes(p.start) ? minutes(p.end) + 1440 : minutes(p.end)),
  );
}
export function evaluateWindow(
  date: Date,
  period: BellPeriod | undefined,
  policy: Policy,
): Decision {
  if (!period) return "Allow";
  const at = date.getHours() * 60 + date.getMinutes() + date.getSeconds() / 60;
  const start = minutes(period.start),
    end = minutes(period.end) <= start ? minutes(period.end) + 1440 : minutes(period.end);
  const weights: Decision[] = ["Allow", "Warn", "Lock"];
  const first =
    at >= start && at < start + policy.firstMinutes ? weights.indexOf(policy.firstAction) : 0;
  const last = at >= end - policy.lastMinutes && at < end ? weights.indexOf(policy.lastAction) : 0;
  return weights[Math.max(first, last)];
}
export function validatePolicy(
  policy: Policy,
  periods: BellPeriod[],
  exceptions: ScheduleException[],
) {
  const integer = (n: number, min: number, max: number) =>
    Number.isInteger(n) && n >= min && n <= max;
  if (
    !integer(policy.capacity, 1, 8) ||
    !integer(policy.warningSeconds, 1, 86400) ||
    !integer(policy.dailyGuideline, 0, 1000) ||
    !integer(policy.firstMinutes, 0, 1440) ||
    !integer(policy.lastMinutes, 0, 1440) ||
    !["Allow", "Warn", "Lock"].includes(policy.firstAction) ||
    !["Allow", "Warn", "Lock"].includes(policy.lastAction) ||
    typeof policy.enforcement !== "boolean"
  )
    throw new HallzeeError("INVALID_POLICY", "Check the policy limits and time windows.");
  for (const p of periods)
    if (
      !p.periodName.trim() ||
      !p.scheduleName.trim() ||
      !validBellTime(p.start) ||
      !validBellTime(p.end) ||
      p.start >= p.end ||
      p.weekdays.some((d) => !weekdays.includes(d)) ||
      p.workspaceId !== policy.workspaceId
    )
      throw new HallzeeError(
        "INVALID_PERIOD",
        "Use named, non-overnight periods with valid days and start/end times.",
      );
  for (const e of exceptions)
    if (
      !validDate(e.date) ||
      e.workspaceId !== policy.workspaceId ||
      (!e.isNoSchool && !periods.some((p) => p.scheduleName === e.scheduleName))
    )
      throw new HallzeeError(
        "INVALID_EXCEPTION",
        "An exception must use a valid date and an existing schedule.",
      );
  if (
    new Set(exceptions.map((e) => e.date)).size !== exceptions.length ||
    new Set(periods.map((p) => p.scheduleId)).size !== periods.length
  )
    throw new HallzeeError("DUPLICATE_SCHEDULE");
  const overlap = (list: BellPeriod[]) => list.some((p, i) => i > 0 && p.start < list[i - 1].end);
  for (const day of weekdays)
    if (
      overlap(
        periods
          .filter((p) => p.weekdays.includes(day))
          .sort((a, b) => a.start.localeCompare(b.start)),
      )
    )
      throw new HallzeeError("OVERLAPPING_PERIODS", "Periods overlap on a weekday.");
  for (const name of new Set(exceptions.filter((e) => !e.isNoSchool).map((e) => e.scheduleName)))
    if (
      overlap(
        periods
          .filter((p) => p.scheduleName === name)
          .sort((a, b) => a.start.localeCompare(b.start)),
      )
    )
      throw new HallzeeError("OVERLAPPING_PERIODS", "Periods overlap in an exception schedule.");
}
