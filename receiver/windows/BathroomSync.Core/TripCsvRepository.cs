namespace BathroomSync.Core;

public enum TripStoreResult {
  Saved,
  Duplicate,
  Invalid
}

public interface ITripRepository {
  TripStoreResult Store(string payload);
}

public sealed class TripCsvRepository : ITripRepository {
  public const string Header = "trip_id,student_id,date,time_out,time_in,duration_seconds,status";

  private readonly HashSet<long> tripIds = new();
  private readonly string csvPath;

  public TripCsvRepository(string csvPath) {
    this.csvPath = csvPath;
    var directory = Path.GetDirectoryName(csvPath);
    if (!string.IsNullOrEmpty(directory)) {
      Directory.CreateDirectory(directory);
    }

    if (!File.Exists(csvPath)) {
      File.WriteAllText(csvPath, Header + Environment.NewLine);
    }

    foreach (var line in File.ReadLines(csvPath).Skip(1)) {
      var separator = line.IndexOf(',');
      var idText = separator >= 0 ? line[..separator] : line;
      if (long.TryParse(idText, out var tripId) && tripId > 0) {
        tripIds.Add(tripId);
      }
    }
  }

  public TripStoreResult Store(string payload) {
    var fields = payload.Split(',');
    if (fields.Length != 8 || !long.TryParse(fields[0], out var tripId) || tripId <= 0) {
      return TripStoreResult.Invalid;
    }

    if (!tripIds.Add(tripId)) {
      return TripStoreResult.Duplicate;
    }

    File.AppendAllText(csvPath, string.Join(',', fields.Take(7)) + Environment.NewLine);
    return TripStoreResult.Saved;
  }
}
