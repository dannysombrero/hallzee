namespace BathroomSync.Core;

public enum TripStatus {
  Completed,
  ManualReset,
  Unknown
}

public static class TripStatusExtensions {
  public static TripStatus ParseStatus(string? status) {
    if (string.Equals(status, "COMPLETE", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)) {
      return TripStatus.Completed;
    }
    if (string.Equals(status, "MANUAL_RESET", StringComparison.OrdinalIgnoreCase)) {
      return TripStatus.ManualReset;
    }
    return TripStatus.Unknown;
  }

  public static string ToStorageString(this TripStatus status) => status switch {
    TripStatus.Completed => "COMPLETED",
    TripStatus.ManualReset => "MANUAL_RESET",
    _ => "UNKNOWN"
  };
}

public record TripRecord(
  long TripId,
  string StudentId,
  string TripDate,
  string TimeOut,
  string TimeIn,
  int DurationSeconds,
  string Status,
  DateTime SyncedAt,
  string TerminalId
);

public record EnrichedTripRecord(
  long TripId,
  string StudentId,
  string TripDate,
  string TimeOut,
  string TimeIn,
  int DurationSeconds,
  string Status,
  DateTime SyncedAt,
  string TerminalId,
  string? FirstName,
  string? LastName,
  string? Grade,
  string? ClassPeriod
) {
  public string? FullName =>
    !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName)
      ? $"{FirstName} {LastName}".Trim()
      : null;

  public string DisplayName => FullName ?? $"#{StudentId}";
}

public record TripQueryFilter(
  string? SearchText = null,
  string? Status = null,
  string? StartDate = null,
  string? EndDate = null,
  string? ProfileId = null,
  string? TerminalId = null,
  int Limit = 50,
  int Offset = 0,
  string OrderBy = "trip_id DESC"
);

public record TripSummary(
  int TotalTrips,
  int CompletedTrips,
  int ManualResetTrips,
  int UniqueStudents,
  double AverageDurationSeconds
);
