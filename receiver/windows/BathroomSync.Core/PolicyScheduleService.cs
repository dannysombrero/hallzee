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

  public BellWindowDecision Evaluate(DateTime moment, ResolvedBellPeriod? period, PolicyRule rule) {
    if (period == null) return BellWindowDecision.Allow;
    var firstEnds = period.StartsAt.AddMinutes(Math.Max(0, rule.LockoutStartMinutes));
    var lastStarts = period.EndsAt.AddMinutes(-Math.Max(0, rule.LockoutEndMinutes));
    var first = moment >= period.StartsAt && moment < firstEnds ? ParseDecision(rule.FirstWindowAction) : BellWindowDecision.Allow;
    var last = moment >= lastStarts && moment < period.EndsAt ? ParseDecision(rule.LastWindowAction) : BellWindowDecision.Allow;
    return (BellWindowDecision)Math.Max((int)first, (int)last);
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
