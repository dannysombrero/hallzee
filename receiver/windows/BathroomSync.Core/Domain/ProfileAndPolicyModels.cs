namespace BathroomSync.Core;

public record ClassroomProfile(
  string ProfileId,
  string Name,
  bool IsActive = false,
  DateTime? CreatedAt = null,
  DateTime? UpdatedAt = null
);

public record PolicyRule(
  string RuleId,
  string ProfileId,
  int MaxSimultaneousPasses = 1,
  int DurationWarningSeconds = 420,
  int MaxDailyPassesPerStudent = 2,
  int LockoutStartMinutes = 10,
  int LockoutEndMinutes = 10,
  string FirstWindowAction = "Warn",
  string LastWindowAction = "Warn",
  string AlertSound = "Chime",
  bool TerminalEnforcementEnabled = false,
  string ClassPassPolicyMode = "Windows",
  string MiddleWindowAction = "Allow",
  bool BellTimeRulesDisabled = false,
  bool BellTransitionEnabled = true,
  bool WarningSoundEnabled = true,
  int WarningSoundVolume = 80
);

public sealed class BellSchedulePeriod {
  public BellSchedulePeriod(
    string ScheduleId,
    string ProfileId,
    string PeriodName,
    string StartTime,
    string EndTime,
    string DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
    string ScheduleName = "Regular",
    string ClassSection = ""
  ) {
    this.ScheduleId = ScheduleId;
    this.ProfileId = ProfileId;
    this.PeriodName = PeriodName;
    this.StartTime = StartTime;
    this.EndTime = EndTime;
    this.DaysOfWeek = DaysOfWeek;
    this.ScheduleName = ScheduleName;
    this.ClassSection = ClassSection;
  }

  public string ScheduleId { get; set; }
  public string ProfileId { get; set; }
  public string PeriodName { get; set; }
  public string StartTime { get; set; }
  public string EndTime { get; set; }
  public string DaysOfWeek { get; set; }
  public string ScheduleName { get; set; }
  public string ClassSection { get; set; }
}

public record ScheduleException(
  string ExceptionId,
  string ProfileId,
  string ExceptionDate,
  string ScheduleName,
  bool IsNoSchool = false
);

public record ResolvedBellPeriod(
  BellSchedulePeriod Period,
  DateTime StartsAt,
  DateTime EndsAt,
  string ScheduleName,
  string ClassSection
);

public record ResolvedBellTransition(
  ResolvedBellPeriod PreviousPeriod,
  ResolvedBellPeriod NextPeriod,
  DateTime StartsAt,
  DateTime EndsAt
);

public record TerminalDeviceConfig(
  string TerminalId,
  string CustomName,
  string? BleAddress = null,
  DateTime? LastSeenAt = null,
  int MaxIdLength = 10,
  int ProtocolVersion = 2,
  string ClaimStatus = "UNKNOWN",
  string? TransportId = null
);
