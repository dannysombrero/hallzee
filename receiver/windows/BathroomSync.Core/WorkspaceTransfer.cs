using System.Globalization;
using System.Text.Json;

namespace BathroomSync.Core;

public sealed record WorkspacePackage(
  string Format,
  int Version,
  string Name,
  PolicyRule Rule,
  IReadOnlyList<BellSchedulePeriod> Periods,
  IReadOnlyList<ScheduleException> Exceptions
);

public static class WorkspaceTransfer {
  static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

  public static string Export(ClassroomProfile profile, IPolicyRepository repository) {
    var package = new WorkspacePackage("Hallzee Workspace", 1, profile.Name,
      repository.GetPolicyRule(profile.ProfileId), repository.GetBellSchedule(profile.ProfileId),
      repository.GetScheduleExceptions(profile.ProfileId));
    return JsonSerializer.Serialize(package, Options);
  }

  public static WorkspacePackage Parse(string json) {
    var package = JsonSerializer.Deserialize<WorkspacePackage>(json, Options)
      ?? throw new ArgumentException("The workspace file is empty.");
    Validate(package);
    return package;
  }

  public static void Validate(WorkspacePackage package) {
    if (package.Format != "Hallzee Workspace" || package.Version != 1)
      throw new ArgumentException("This is not a supported Hallzee workspace file.");
    if (string.IsNullOrWhiteSpace(package.Name) || package.Rule == null ||
        package.Periods == null || package.Exceptions == null)
      throw new ArgumentException("The workspace file is missing its name, rules, or schedules.");
    var rule = package.Rule;
    if (rule.MaxSimultaneousPasses is < 1 or > 8 || rule.DurationWarningSeconds is < 1 or > 3600 ||
        rule.MaxDailyPassesPerStudent is < 1 or > 20 || rule.LockoutStartMinutes is < 0 or > 60 ||
        rule.LockoutEndMinutes is < 0 or > 60 ||
        !new[] { "Allow", "Warn", "Lock" }.Contains(rule.FirstWindowAction) ||
        !new[] { "Allow", "Warn", "Lock" }.Contains(rule.LastWindowAction) || string.IsNullOrWhiteSpace(rule.AlertSound))
      throw new ArgumentException("The workspace contains invalid policy rules.");
    var validDays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    foreach (var p in package.Periods) {
      if (p == null || string.IsNullOrWhiteSpace(p.PeriodName) || string.IsNullOrWhiteSpace(p.ScheduleName) ||
          p.ClassSection == null || p.DaysOfWeek == null ||
          !TimeOnly.TryParse(p.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
          !TimeOnly.TryParse(p.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
          p.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(d => !validDays.Contains(d)))
        throw new ArgumentException("The workspace contains an invalid bell period.");
    }
    var dates = new HashSet<string>();
    foreach (var e in package.Exceptions) {
      if (e == null || !DateOnly.TryParseExact(e.ExceptionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
          !dates.Add(e.ExceptionDate) || (!e.IsNoSchool && !package.Periods.Any(p => p.ScheduleName == e.ScheduleName)))
        throw new ArgumentException("The workspace contains an invalid date exception.");
    }
  }
}
