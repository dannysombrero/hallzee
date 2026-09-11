using System.Globalization;

namespace BathroomSync.Core;

public enum BellWindowDecision {
  Allow,
  Warn,
  Lock
}

public sealed class PolicyScheduleService {
  static readonly string[] DayTokens = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

  public ResolvedBellPeriod? ResolvePeriod(
    DateTime moment,
    IEnumerable<BellSchedulePeriod> periods,
    IEnumerable<ScheduleException>? exceptions = null
  ) {
    var exception = exceptions?.FirstOrDefault(item =>
      DateOnly.TryParseExact(item.ExceptionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
      date == DateOnly.FromDateTime(moment));
    if (exception?.IsNoSchool == true) return null;

    var selectedSchedule = exception?.ScheduleName;
    var candidates = periods.Where(period => exception != null
      ? string.Equals(NormalizeScheduleName(period.ScheduleName), selectedSchedule, StringComparison.OrdinalIgnoreCase)
      : IsActiveOnDay(period.DaysOfWeek, moment.DayOfWeek));

    foreach (var period in candidates) {
      if (!TryResolveRange(moment.Date, period, out var startsAt, out var endsAt)) continue;
      if (moment >= startsAt && moment < endsAt) {
        return new ResolvedBellPeriod(period, startsAt, endsAt, NormalizeScheduleName(period.ScheduleName), period.ClassSection);
      }
    }
    return null;
  }

  public IReadOnlyList<ResolvedBellPeriod> ResolvePeriodsForDate(
    DateTime date,
    IEnumerable<BellSchedulePeriod> periods,
    IEnumerable<ScheduleException>? exceptions = null
  ) {
    var exception = exceptions?.FirstOrDefault(item =>
      DateOnly.TryParseExact(item.ExceptionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) &&
      parsed == DateOnly.FromDateTime(date));
    if (exception?.IsNoSchool == true) return Array.Empty<ResolvedBellPeriod>();
    var selectedSchedule = exception?.ScheduleName;
    return periods
      .Where(period => exception != null
        ? string.Equals(NormalizeScheduleName(period.ScheduleName), selectedSchedule, StringComparison.OrdinalIgnoreCase)
        : IsActiveOnDay(period.DaysOfWeek, date.DayOfWeek))
      .Select(period => TryResolveRange(date.Date, period, out var start, out var end)
        ? new ResolvedBellPeriod(period, start, end, NormalizeScheduleName(period.ScheduleName), period.ClassSection)
        : null)
      .Where(item => item != null)
      .Cast<ResolvedBellPeriod>()
      .OrderBy(item => item.StartsAt)
      .ToList();
  }

  public ResolvedBellTransition? ResolveTransition(
    DateTime moment,
    IEnumerable<BellSchedulePeriod> periods,
    IEnumerable<ScheduleException>? exceptions = null
  ) {
    var dayPeriods = ResolvePeriodsForDate(moment.Date, periods, exceptions);
    if (dayPeriods.Count < 2) return null;

    for (var i = 0; i < dayPeriods.Count - 1; i++) {
      var prev = dayPeriods[i];
      var next = dayPeriods[i + 1];
      if (moment >= prev.EndsAt && moment < next.StartsAt) {
        return new ResolvedBellTransition(prev, next, prev.EndsAt, next.StartsAt);
      }
    }
    return null;
  }

  public BellWindowDecision Evaluate(DateTime moment, ResolvedBellPeriod? period, PolicyRule rule) {
    if (period == null) return BellWindowDecision.Allow;
    if (rule.BellTimeRulesDisabled || string.Equals(rule.ClassPassPolicyMode, "NoRules", StringComparison.OrdinalIgnoreCase)) {
      return BellWindowDecision.Allow;
    }
    if (string.Equals(rule.ClassPassPolicyMode, "NoPasses", StringComparison.OrdinalIgnoreCase)) {
      return BellWindowDecision.Lock;
    }

    var firstEnds = period.StartsAt.AddMinutes(Math.Max(0, rule.LockoutStartMinutes));
    var lastStarts = period.EndsAt.AddMinutes(-Math.Max(0, rule.LockoutEndMinutes));
    var inFirst = moment >= period.StartsAt && moment < firstEnds;
    var inLast = moment >= lastStarts && moment < period.EndsAt;

    if (inFirst && inLast) {
      return (BellWindowDecision)Math.Max((int)ParseDecision(rule.FirstWindowAction), (int)ParseDecision(rule.LastWindowAction));
    }
    if (inFirst) {
      return ParseDecision(rule.FirstWindowAction);
    }
    if (inLast) {
      return ParseDecision(rule.LastWindowAction);
    }
    return ParseDecision(rule.MiddleWindowAction);
  }

  public static bool IsActiveOnDay(string? days, DayOfWeek day) {
    if (string.IsNullOrWhiteSpace(days)) return false;
    var token = DayTokens[(int)day];
    var numeric = day == DayOfWeek.Sunday ? "7" : ((int)day).ToString(CultureInfo.InvariantCulture);
    return days.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
      .Any(value => value.Equals(token, StringComparison.OrdinalIgnoreCase) || value == numeric);
  }

  public static bool TryResolveRange(DateTime date, BellSchedulePeriod period, out DateTime startsAt, out DateTime endsAt) {
    startsAt = default;
    endsAt = default;
    if (!DateTime.TryParse(period.StartTime, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var start) ||
        !DateTime.TryParse(period.EndTime, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var end)) return false;
    startsAt = date.Add(start.TimeOfDay);
    endsAt = date.Add(end.TimeOfDay);
    if (endsAt <= startsAt) endsAt = endsAt.AddDays(1);
    return true;
  }

  static string NormalizeScheduleName(string? name) => string.IsNullOrWhiteSpace(name) ? "Regular" : name.Trim();

  static BellWindowDecision ParseDecision(string? action) => action?.Trim().ToUpperInvariant() switch {
    "LOCK" => BellWindowDecision.Lock,
    "WARN" => BellWindowDecision.Warn,
    _ => BellWindowDecision.Allow
  };
}
