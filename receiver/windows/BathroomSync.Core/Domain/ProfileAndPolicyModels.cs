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
  int LockoutEndMinutes = 10
);

public record BellSchedulePeriod(
  string ScheduleId,
  string ProfileId,
  string PeriodName,
  string StartTime,
  string EndTime,
  string DaysOfWeek = "1,2,3,4,5"
);

public record TerminalDeviceConfig(
  string TerminalId,
  string CustomName,
  string? BleAddress = null,
  DateTime? LastSeenAt = null,
  int MaxIdLength = 10
);
