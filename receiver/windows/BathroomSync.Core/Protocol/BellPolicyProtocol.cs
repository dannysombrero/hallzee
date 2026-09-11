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
    var commands = new List<string> { $"POLICY_BEGIN,{(rule.TerminalEnforcementEnabled && !rule.BellTimeRulesDisabled && !string.Equals(rule.ClassPassPolicyMode, "NoRules", StringComparison.OrdinalIgnoreCase) ? 1 : 0)}" };
    if (rule.TerminalEnforcementEnabled && !rule.BellTimeRulesDisabled && !string.Equals(rule.ClassPassPolicyMode, "NoRules", StringComparison.OrdinalIgnoreCase)) {
      var resolver = new PolicyScheduleService();
      for (var offset = 0; offset < CacheDays && commands.Count - 1 < MaximumWindows; offset++) {
        var date = firstDate.Date.AddDays(offset);
        foreach (var resolved in resolver.ResolvePeriodsForDate(date, periods, exceptions)) {
          if (commands.Count - 1 >= MaximumWindows) break;
          var start = resolved.StartsAt.Hour * 60 + resolved.StartsAt.Minute;
          var end = resolved.EndsAt.Date > resolved.StartsAt.Date ? 1440 : resolved.EndsAt.Hour * 60 + resolved.EndsAt.Minute;

          if (string.Equals(rule.ClassPassPolicyMode, "NoPasses", StringComparison.OrdinalIgnoreCase)) {
            commands.Add($"POLICY_WINDOW,{date:yyyyMMdd},{start},{end},{end},{start},2,2");
          } else if (string.Equals(rule.FirstWindowAction, "Allow", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(rule.MiddleWindowAction, "Lock", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(rule.LastWindowAction, "Allow", StringComparison.OrdinalIgnoreCase)) {
            var midStart = Math.Min(end, start + Math.Max(0, rule.LockoutStartMinutes));
            var midEnd = Math.Max(start, end - Math.Max(0, rule.LockoutEndMinutes));
            if (midEnd > midStart) {
              commands.Add($"POLICY_WINDOW,{date:yyyyMMdd},{midStart},{midEnd},{midEnd},{midStart},2,2");
            }
          } else {
            var firstEnd = Math.Min(end, start + Math.Max(0, rule.LockoutStartMinutes));
            var lastStart = Math.Max(start, end - Math.Max(0, rule.LockoutEndMinutes));
            commands.Add($"POLICY_WINDOW,{date:yyyyMMdd},{start},{end},{firstEnd},{lastStart},{Encode(rule.FirstWindowAction)},{Encode(rule.LastWindowAction)}");
          }
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
