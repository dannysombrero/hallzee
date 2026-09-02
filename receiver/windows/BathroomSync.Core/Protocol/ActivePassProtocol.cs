namespace BathroomSync.Core;

public enum ActivePassStatus {
  Available,
  Occupied,
  Unknown
}

public record ActivePassInfo(
  ActivePassStatus Status,
  string? StudentId = null,
  long? CheckoutEpochSeconds = null,
  DateTime? CheckedOutAt = null
) {
  public static ActivePassInfo Available() => new(ActivePassStatus.Available);
  public static ActivePassInfo Occupied(string studentId, long epochSeconds) {
    // The ESP32 records classroom local wall time, but its clock is configured
    // without a timezone. Preserve those wall-clock components on the desktop
    // rather than applying the Mac's UTC offset a second time.
    var dt = DateTime.SpecifyKind(
      DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime,
      DateTimeKind.Local
    );
    return new(ActivePassStatus.Occupied, studentId, epochSeconds, dt);
  }
}

public enum LivePassEventType {
  Checkout,
  Checkin,
  Reset
}

public record LivePassEvent(
  LivePassEventType EventType,
  string StudentId,
  long Value
) {
  public DateTime? CheckoutTime =>
    EventType == LivePassEventType.Checkout && Value > 0
      ? DateTime.SpecifyKind(
        DateTimeOffset.FromUnixTimeSeconds(Value).UtcDateTime,
        DateTimeKind.Local
      )
      : null;

  public long DurationSeconds =>
    EventType != LivePassEventType.Checkout ? Value : 0;
}

public static class ActivePassProtocol {
  public const string GetActivePassCommand = "GET_ACTIVE_PASS\n";

  public static string BuildGetActivePassCommand() => GetActivePassCommand;

  public static bool TryParseActivePassResponse(string line, out ActivePassInfo? info) {
    info = null;
    if (string.IsNullOrWhiteSpace(line)) return false;

    var trimmed = line.Trim();
    if (!trimmed.StartsWith("ACTIVE_PASS,", StringComparison.Ordinal)) return false;

    var payload = trimmed[12..];
    if (string.Equals(payload, "NONE", StringComparison.OrdinalIgnoreCase)) {
      info = ActivePassInfo.Available();
      return true;
    }

    var fields = payload.Split(',');
    if (fields.Length == 2 &&
        !string.IsNullOrWhiteSpace(fields[0]) &&
        long.TryParse(fields[1], out var epoch) &&
        epoch > 0) {
      info = ActivePassInfo.Occupied(fields[0].Trim(), epoch);
      return true;
    }

    return false;
  }

  public static bool TryParseLiveEvent(string line, out LivePassEvent? liveEvent) {
    liveEvent = null;
    if (string.IsNullOrWhiteSpace(line)) return false;

    var trimmed = line.Trim();
    if (!trimmed.StartsWith("EVENT,", StringComparison.Ordinal)) return false;

    var fields = trimmed.Split(',');
    if (fields.Length != 4) return false;

    var eventTypeStr = fields[1].ToUpperInvariant();
    var studentId = fields[2].Trim();
    if (string.IsNullOrWhiteSpace(studentId) || !long.TryParse(fields[3], out var val) || val < 0) {
      return false;
    }

    LivePassEventType eventType;
    if (eventTypeStr == "CHECKOUT") {
      eventType = LivePassEventType.Checkout;
    } else if (eventTypeStr == "CHECKIN") {
      eventType = LivePassEventType.Checkin;
    } else if (eventTypeStr == "RESET") {
      eventType = LivePassEventType.Reset;
    } else {
      return false;
    }

    liveEvent = new LivePassEvent(eventType, studentId, val);
    return true;
  }
}
