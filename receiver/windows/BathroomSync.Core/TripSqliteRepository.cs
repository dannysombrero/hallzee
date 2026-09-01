using Microsoft.Data.Sqlite;

namespace BathroomSync.Core;

public enum TripStoreResult {
  Saved,
  Duplicate,
  Invalid,
  Unavailable
}

public interface ITripRepository {
  TripStoreResult Store(string payload);
}

public sealed class TripSqliteRepository : ITripRepository {
  public const string CsvHeader = "trip_id,student_id,date,time_out,time_in,duration_seconds,status";

  readonly string connectionString;

  public TripSqliteRepository(string databasePath, string? legacyCsvPath = null) {
    var directory = Path.GetDirectoryName(databasePath);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    connectionString = new SqliteConnectionStringBuilder {
      DataSource = databasePath,
      Mode = SqliteOpenMode.ReadWriteCreate,
      Cache = SqliteCacheMode.Shared,
      Pooling = false
    }.ToString();

    using var connection = OpenConnection();
    CreateSchema(connection);
    ImportLegacyCsv(connection, legacyCsvPath);
  }

  public TripStoreResult Store(string payload) {
    var fields = payload.Split(',');
    if (fields.Length != 8 || !long.TryParse(fields[0], out var tripId) || tripId <= 0) {
      return TripStoreResult.Invalid;
    }

    try {
      using var connection = OpenConnection();
      using var command = connection.CreateCommand();
      command.CommandText = """
        INSERT OR IGNORE INTO trips
          (trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status)
        VALUES ($id, $studentId, $date, $timeOut, $timeIn, $duration, $status);
        """;
      command.Parameters.AddWithValue("$id", tripId);
      command.Parameters.AddWithValue("$studentId", fields[1]);
      command.Parameters.AddWithValue("$date", fields[2]);
      command.Parameters.AddWithValue("$timeOut", fields[3]);
      command.Parameters.AddWithValue("$timeIn", fields[4]);
      command.Parameters.AddWithValue("$duration", fields[5]);
      command.Parameters.AddWithValue("$status", fields[6]);
      return command.ExecuteNonQuery() == 1 ? TripStoreResult.Saved : TripStoreResult.Duplicate;
    } catch (SqliteException) {
      return TripStoreResult.Unavailable;
    } catch (IOException) {
      return TripStoreResult.Unavailable;
    } catch (UnauthorizedAccessException) {
      return TripStoreResult.Unavailable;
    }
  }

  public long GetLatestTripId() {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT COALESCE(MAX(trip_id), 0) FROM trips;";
    return (long)(command.ExecuteScalar() ?? 0L);
  }

  public void ExportCsv(string destinationPath) {
    var directory = Path.GetDirectoryName(destinationPath);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    var temporaryPath = destinationPath + ".tmp";
    try {
      {
        using var writer = new StreamWriter(temporaryPath, false);
        writer.WriteLine(CsvHeader);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
          SELECT trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status
          FROM trips
          ORDER BY trip_id ASC;
          """;
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
          writer.WriteLine(string.Join(',', new[] {
            reader.GetInt64(0).ToString(),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6)
          }));
        }
        writer.Flush();
      }
      File.Move(temporaryPath, destinationPath, true);
    } finally {
      if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
    }
  }

  SqliteConnection OpenConnection() {
    var connection = new SqliteConnection(connectionString);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA synchronous = FULL;";
    command.ExecuteNonQuery();
    return connection;
  }

  static void CreateSchema(SqliteConnection connection) {
    using var command = connection.CreateCommand();
    command.CommandText = """
      PRAGMA journal_mode = WAL;
      CREATE TABLE IF NOT EXISTS trips (
        trip_id INTEGER PRIMARY KEY,
        student_id TEXT NOT NULL,
        trip_date TEXT NOT NULL,
        time_out TEXT NOT NULL,
        time_in TEXT NOT NULL,
        duration_seconds TEXT NOT NULL,
        status TEXT NOT NULL
      );
      """;
    command.ExecuteNonQuery();
  }

  static void ImportLegacyCsv(SqliteConnection connection, string? legacyCsvPath) {
    if (string.IsNullOrEmpty(legacyCsvPath) || !File.Exists(legacyCsvPath)) return;

    try {
      foreach (var row in File.ReadLines(legacyCsvPath).Skip(1)) {
        var fields = row.Split(',');
        if (fields.Length != 7 || !long.TryParse(fields[0], out var tripId) || tripId <= 0) continue;

        using var command = connection.CreateCommand();
        command.CommandText = """
          INSERT OR IGNORE INTO trips
            (trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status)
          VALUES ($id, $studentId, $date, $timeOut, $timeIn, $duration, $status);
          """;
        command.Parameters.AddWithValue("$id", tripId);
        command.Parameters.AddWithValue("$studentId", fields[1]);
        command.Parameters.AddWithValue("$date", fields[2]);
        command.Parameters.AddWithValue("$timeOut", fields[3]);
        command.Parameters.AddWithValue("$timeIn", fields[4]);
        command.Parameters.AddWithValue("$duration", fields[5]);
        command.Parameters.AddWithValue("$status", fields[6]);
        command.ExecuteNonQuery();
      }
    } catch (IOException) {
      // The legacy CSV is optional; a file open in Excel must not block startup.
    } catch (UnauthorizedAccessException) {
      // The legacy CSV is optional; a protected folder must not block startup.
    }
  }
}
