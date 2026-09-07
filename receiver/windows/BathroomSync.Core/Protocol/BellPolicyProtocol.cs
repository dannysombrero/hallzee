namespace BathroomSync.Core;

public static class BellPolicyProtocol {
  public const int CacheDays = 14;
  public const int MaximumWindows = 96;

  public static IReadOnlyList<string> BuildTransfer(
    DateTime firstDate,
    PolicyRule rule,
    IEnumerable<BellSchedulePeriod> periods,
    IEnumerable<ScheduleException> exceptions
  ) {
    var commands = new List<string> { $"POLICY_BEGIN,{(rule.TerminalEnforcementEnabled ? 1 : 0)}" };
    if (rule.TerminalEnforcementEnabled) {
      var resolver = new PolicyScheduleService();
      for (var offset = 0; offset < CacheDays && commands.Count - 1 < MaximumWindows; offset++) {
        var date = firstDate.Date.AddDays(offset);
        foreach (var resolved in resolver.ResolvePeriodsForDate(date, periods, exceptions)) {
          if (commands.Count - 1 >= MaximumWindows) break;
          var start = resolved.StartsAt.Hour * 60 + resolved.StartsAt.Minute;
          var end = resolved.EndsAt.Date > resolved.StartsAt.Date ? 1440 : resolved.EndsAt.Hour * 60 + resolved.EndsAt.Minute;
          var firstEnd = Math.Min(end, start + Math.Max(0, rule.LockoutStartMinutes));
          var lastStart = Math.Max(start, end - Math.Max(0, rule.LockoutEndMinutes));
          commands.Add($"POLICY_WINDOW,{date:yyyyMMdd},{start},{end},{firstEnd},{lastStart},{Encode(rule.FirstWindowAction)},{Encode(rule.LastWindowAction)}");
        }
      }
    }
    commands.Add($"POLICY_COMMIT,{commands.Count - 1}");
    return commands;
  }

  static int Encode(string? action) => action?.Trim().ToUpperInvariant() switch {
    "LOCK" => 2,
    "WARN" => 1,
    _ => 0
  };
}
