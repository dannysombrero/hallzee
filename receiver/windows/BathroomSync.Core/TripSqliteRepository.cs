using System.Text;
using Microsoft.Data.Sqlite;

namespace BathroomSync.Core;

public enum TripStoreResult {
  Saved,
  Duplicate,
  Invalid,
  Unavailable
}

public interface ITripStore {
  TripStoreResult Store(string payload);
  TripStoreResult Store(string terminalId, string payload) => Store(payload);
}

public interface ITripRepository : ITripStore {
  long GetLatestTripId() => 0L;
  long GetLatestTripId(string terminalId) => GetLatestTripId();
  void ExportCsv(string destinationPath) { }
  IReadOnlyList<EnrichedTripRecord> GetRecentTrips(int count = 10, string? profileId = null) => Array.Empty<EnrichedTripRecord>();
  IReadOnlyList<EnrichedTripRecord> GetRecentTrips(string terminalId, int count = 10, string? profileId = null) =>
    GetRecentTrips(count, profileId);
  IReadOnlyList<EnrichedTripRecord> QueryTrips(TripQueryFilter filter) => Array.Empty<EnrichedTripRecord>();
  int CountTrips(TripQueryFilter filter) => 0;
  TripSummary GetTripSummary(string? startDate = null, string? endDate = null, string? profileId = null) => new(0, 0, 0, 0, 0.0);
  void ExportEnrichedCsv(string destinationPath, string? profileId = null) { }
  void AssignTripContext(string terminalId, long tripId, string profileId, string? scheduleName, string? classSection) { }
}

public sealed class TripSqliteRepository : ITripRepository {
  public const string CsvHeader = "trip_id,student_id,date,time_out,time_in,duration_seconds,status";
  public const string EnrichedCsvHeader = "trip_id,student_id,student_name,class_section,schedule_name,grade,trip_date,time_out,time_in,duration_seconds,status,terminal_id,synced_at";

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
    DatabaseMigrator.Migrate(connection);
    ImportLegacyCsv(connection, legacyCsvPath);
  }

  public TripStoreResult Store(string payload) {
    var fields = payload.Split(',');
    if (fields.Length < 7 || !long.TryParse(fields[0], out var tripId) || tripId <= 0) {
      return TripStoreResult.Invalid;
    }

    // Legacy callers may include a terminal ID as the ninth field. New sync
    // callers must use Store(terminalId, payload) so identity comes from the
    // authenticated session instead of an untrusted payload field.
    var terminalId = fields.Length >= 9 && !string.IsNullOrWhiteSpace(fields[8]) ? fields[8] : "LEGACY-DEFAULT";
    if (string.Equals(terminalId, "DEFAULT", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(terminalId, "0", StringComparison.Ordinal)) {
      terminalId = "LEGACY-DEFAULT";
    }
    return Store(terminalId, payload);
  }

  public TripStoreResult Store(string terminalId, string payload) {
    return StoreInternal(terminalId, payload, null);
  }

  public TripStoreResult StoreManual(string terminalId, string payload, string? manualName) {
    return StoreInternal(terminalId, payload, string.IsNullOrWhiteSpace(manualName) ? null : manualName.Trim());
  }

  public void AssignTripContext(string terminalId, long tripId, string profileId, string? scheduleName, string? classSection) {
    if (string.IsNullOrWhiteSpace(terminalId) || tripId <= 0 || string.IsNullOrWhiteSpace(profileId)) return;
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "UPDATE trips SET profile_id = $profileId, schedule_name = $scheduleName, class_section = $classSection WHERE terminal_id = $terminalId AND trip_id = $tripId;";
    command.Parameters.AddWithValue("$profileId", profileId);
    command.Parameters.AddWithValue("$scheduleName", (object?)scheduleName ?? DBNull.Value);
    command.Parameters.AddWithValue("$classSection", (object?)classSection ?? DBNull.Value);
    command.Parameters.AddWithValue("$terminalId", terminalId);
    command.Parameters.AddWithValue("$tripId", tripId);
    command.ExecuteNonQuery();
  }

  TripStoreResult StoreInternal(string terminalId, string payload, string? manualName) {
    if (string.IsNullOrWhiteSpace(terminalId)) return TripStoreResult.Invalid;

    var fields = payload.Split(',');
    if (fields.Length < 7 || !long.TryParse(fields[0], out var tripId) || tripId <= 0) {
      return TripStoreResult.Invalid;
    }

    try {
      using var connection = OpenConnection();
      using var command = connection.CreateCommand();
      command.CommandText = """
        INSERT OR IGNORE INTO trips
          (trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status, terminal_id, synced_at, manual_name)
        VALUES ($id, $studentId, $date, $timeOut, $timeIn, $duration, $status, $terminalId, datetime('now'), $manualName);
        """;
      command.Parameters.AddWithValue("$id", tripId);
      command.Parameters.AddWithValue("$studentId", fields[1]);
      command.Parameters.AddWithValue("$date", fields[2]);
      command.Parameters.AddWithValue("$timeOut", fields[3]);
      command.Parameters.AddWithValue("$timeIn", fields[4]);
      command.Parameters.AddWithValue("$duration", fields[5]);
      command.Parameters.AddWithValue("$status", fields[6]);
      command.Parameters.AddWithValue("$terminalId", terminalId);
      command.Parameters.AddWithValue("$manualName", (object?)manualName ?? DBNull.Value);
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

  public long GetLatestTripId(string terminalId) {
    if (string.IsNullOrWhiteSpace(terminalId)) return 0L;
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT COALESCE(MAX(trip_id), 0) FROM trips WHERE terminal_id = $terminalId;";
    command.Parameters.AddWithValue("$terminalId", terminalId);
    return (long)(command.ExecuteScalar() ?? 0L);
  }

  public IReadOnlyList<EnrichedTripRecord> GetRecentTrips(int count = 10, string? profileId = null) {
    return QueryTrips(new TripQueryFilter(
      Limit: count,
      Offset: 0,
      ProfileId: profileId,
      OrderBy: "trip_id DESC"
    ));
  }

  public IReadOnlyList<EnrichedTripRecord> GetRecentTrips(string terminalId, int count = 10, string? profileId = null) {
    if (string.IsNullOrWhiteSpace(terminalId)) return Array.Empty<EnrichedTripRecord>();
    return QueryTrips(new TripQueryFilter(
      Limit: count,
      Offset: 0,
      ProfileId: profileId,
      TerminalId: terminalId,
      OrderBy: "trip_id DESC"
    ));
  }

  public IReadOnlyList<EnrichedTripRecord> QueryTrips(TripQueryFilter filter) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();

    var (whereClause, parameters) = BuildWhereClause(filter);
    var profileParam = string.IsNullOrEmpty(filter.ProfileId) ? "default" : filter.ProfileId;

    var orderClause = filter.OrderBy switch {
      "trip_id ASC" => "t.trip_id ASC",
      "trip_date DESC" => "t.trip_date DESC, t.time_out DESC",
      "trip_date ASC" => "t.trip_date ASC, t.time_out ASC",
      "activity DESC" => "t.trip_date DESC, COALESCE(NULLIF(t.time_in, ''), t.time_out) DESC, t.trip_id DESC",
      "duration DESC" => "CAST(t.duration_seconds AS INTEGER) DESC",
      "duration ASC" => "CAST(t.duration_seconds AS INTEGER) ASC",
      _ => "t.trip_id DESC"
    };

    command.CommandText = $"""
      SELECT 
        t.trip_id,
        t.student_id,
        t.trip_date,
        t.time_out,
        t.time_in,
        t.duration_seconds,
        t.status,
        COALESCE(t.synced_at, datetime('now')),
        COALESCE(t.terminal_id, 'DEFAULT'),
        r.first_name,
        r.last_name,
        r.grade,
        COALESCE(NULLIF(t.class_section, ''),
          (SELECT group_concat(e.class_section, ', ') FROM roster_enrollments e WHERE e.profile_id = r.profile_id AND e.student_id = r.student_id),
          r.class_period),
        t.manual_name,
        t.schedule_name
      FROM trips t
      LEFT JOIN roster_students r 
        ON t.student_id = r.student_id 
        AND r.profile_id = $activeProfileId
      {whereClause}
      ORDER BY {orderClause}
      LIMIT $limit OFFSET $offset;
      """;

    command.Parameters.AddWithValue("$activeProfileId", profileParam);
    command.Parameters.AddWithValue("$limit", Math.Max(1, filter.Limit));
    command.Parameters.AddWithValue("$offset", Math.Max(0, filter.Offset));

    foreach (var (name, value) in parameters) {
      command.Parameters.AddWithValue(name, value);
    }

    var results = new List<EnrichedTripRecord>();
    using var reader = command.ExecuteReader();
    while (reader.Read()) {
      results.Add(ReadEnrichedTrip(reader));
    }
    return results;
  }

  public int CountTrips(TripQueryFilter filter) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();

    var (whereClause, parameters) = BuildWhereClause(filter);
    var profileParam = string.IsNullOrEmpty(filter.ProfileId) ? "default" : filter.ProfileId;

    command.CommandText = $"""
      SELECT COUNT(*)
      FROM trips t
      LEFT JOIN roster_students r 
        ON t.student_id = r.student_id 
        AND r.profile_id = $activeProfileId
      {whereClause};
      """;

    command.Parameters.AddWithValue("$activeProfileId", profileParam);
    foreach (var (name, value) in parameters) {
      command.Parameters.AddWithValue(name, value);
    }

    return Convert.ToInt32(command.ExecuteScalar() ?? 0);
  }

  public TripSummary GetTripSummary(string? startDate = null, string? endDate = null, string? profileId = null) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();

    var whereClauses = new List<string>();
    if (!string.IsNullOrWhiteSpace(startDate)) {
      whereClauses.Add("trip_date >= $startDate");
      command.Parameters.AddWithValue("$startDate", startDate);
    }
    if (!string.IsNullOrWhiteSpace(endDate)) {
      whereClauses.Add("trip_date <= $endDate");
      command.Parameters.AddWithValue("$endDate", endDate);
    }
    if (!string.IsNullOrWhiteSpace(profileId)) {
      whereClauses.Add("(profile_id = $profileId OR profile_id = 'default')");
      command.Parameters.AddWithValue("$profileId", profileId);
    }

    var whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

    command.CommandText = $"""
      SELECT 
        COUNT(*) as total_count,
        SUM(CASE WHEN UPPER(status) IN ('COMPLETE', 'COMPLETED') THEN 1 ELSE 0 END) as completed_count,
        SUM(CASE WHEN UPPER(status) = 'MANUAL_RESET' THEN 1 ELSE 0 END) as reset_count,
        COUNT(DISTINCT student_id) as unique_students,
        COALESCE(AVG(CAST(duration_seconds AS INTEGER)), 0.0) as avg_duration
      FROM trips
      {whereSql};
      """;

    using var reader = command.ExecuteReader();
    if (reader.Read()) {
      return new TripSummary(
        TotalTrips: reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetInt64(0)),
        CompletedTrips: reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1)),
        ManualResetTrips: reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2)),
        UniqueStudents: reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3)),
        AverageDurationSeconds: reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4)
      );
    }

    return new TripSummary(0, 0, 0, 0, 0.0);
  }

  public void ExportCsv(string destinationPath) {
    var directory = Path.GetDirectoryName(destinationPath);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    var temporaryPath = destinationPath + ".tmp";
    try {
      {
        using var writer = new StreamWriter(temporaryPath, false, Encoding.UTF8);
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

  public void ExportEnrichedCsv(string destinationPath, string? profileId = null) {
    var directory = Path.GetDirectoryName(destinationPath);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    var temporaryPath = destinationPath + ".tmp";
    try {
      {
        using var writer = new StreamWriter(temporaryPath, false, Encoding.UTF8);
        writer.WriteLine(EnrichedCsvHeader);
        var trips = QueryTrips(new TripQueryFilter(Limit: int.MaxValue, ProfileId: profileId, OrderBy: "trip_id ASC"));
        foreach (var t in trips) {
          writer.WriteLine(string.Join(',', new[] {
            t.TripId.ToString(),
            EscapeCsvField(t.StudentId),
            EscapeCsvField(t.FullName ?? ""),
            EscapeCsvField(t.ClassPeriod ?? ""),
            EscapeCsvField(t.ScheduleName ?? ""),
            EscapeCsvField(t.Grade ?? ""),
            EscapeCsvField(t.TripDate),
            EscapeCsvField(t.TimeOut),
            EscapeCsvField(t.TimeIn),
            t.DurationSeconds.ToString(),
            EscapeCsvField(t.Status),
            EscapeCsvField(t.TerminalId),
            EscapeCsvField(t.SyncedAt.ToString("yyyy-MM-dd HH:mm:ss"))
          }));
        }
        writer.Flush();
      }
      File.Move(temporaryPath, destinationPath, true);
    } finally {
      if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
    }
  }

  static string EscapeCsvField(string field) {
    if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r')) {
      return $"\"{field.Replace("\"", "\"\"")}\"";
    }
    return field;
  }

  static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(TripQueryFilter filter) {
    var conditions = new List<string>();
    var parameters = new Dictionary<string, object>();

    if (!string.IsNullOrWhiteSpace(filter.SearchText)) {
      conditions.Add("(t.student_id LIKE $search OR r.first_name LIKE $search OR r.last_name LIKE $search OR (r.first_name || ' ' || r.last_name) LIKE $search)");
      parameters["$search"] = $"%{filter.SearchText.Trim()}%";
    }

    if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "ALL", StringComparison.OrdinalIgnoreCase)) {
      if (string.Equals(filter.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) || string.Equals(filter.Status, "COMPLETE", StringComparison.OrdinalIgnoreCase)) {
        conditions.Add("UPPER(t.status) IN ('COMPLETE', 'COMPLETED')");
      } else if (string.Equals(filter.Status, "MANUAL", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(filter.Status, "MANUAL_RESET", StringComparison.OrdinalIgnoreCase)) {
        conditions.Add("UPPER(t.status) IN ('MANUAL', 'MANUAL_RESET')");
      } else {
        conditions.Add("UPPER(t.status) = $status");
        parameters["$status"] = filter.Status.ToUpperInvariant();
      }
    }

    if (!string.IsNullOrWhiteSpace(filter.StartDate)) {
      conditions.Add("t.trip_date >= $startDate");
      parameters["$startDate"] = filter.StartDate;
    }

    if (!string.IsNullOrWhiteSpace(filter.EndDate)) {
      conditions.Add("t.trip_date <= $endDate");
      parameters["$endDate"] = filter.EndDate;
    }

    if (!string.IsNullOrWhiteSpace(filter.TerminalId)) {
      conditions.Add("t.terminal_id = $terminalId");
      parameters["$terminalId"] = filter.TerminalId;
    }

    if (!string.IsNullOrWhiteSpace(filter.ProfileId)) {
      conditions.Add("(t.profile_id = $profileId OR t.profile_id = 'default')");
      parameters["$profileId"] = filter.ProfileId;
    }

    var sql = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    return (sql, parameters);
  }

  static EnrichedTripRecord ReadEnrichedTrip(SqliteDataReader reader) {
    var tripId = reader.GetInt64(0);
    var studentId = reader.GetString(1);
    var tripDate = reader.GetString(2);
    var timeOut = reader.GetString(3);
    var timeIn = reader.GetString(4);
    var rawDuration = reader.GetString(5);
    int.TryParse(rawDuration, out var duration);
    var status = reader.GetString(6);
    var syncedAtStr = reader.IsDBNull(7) ? null : reader.GetString(7);
    var syncedAt = DateTime.TryParse(syncedAtStr, out var parsedSync) ? parsedSync : DateTime.UtcNow;
    var terminalId = reader.IsDBNull(8) ? "DEFAULT" : reader.GetString(8);
    var firstName = reader.IsDBNull(9) ? null : reader.GetString(9);
    var lastName = reader.IsDBNull(10) ? null : reader.GetString(10);
    var grade = reader.IsDBNull(11) ? null : reader.GetString(11);
    var classPeriod = reader.IsDBNull(12) ? null : reader.GetString(12);
    var manualName = reader.IsDBNull(13) ? null : reader.GetString(13);
    var scheduleName = reader.IsDBNull(14) ? null : reader.GetString(14);

    return new EnrichedTripRecord(
      TripId: tripId,
      StudentId: studentId,
      TripDate: tripDate,
      TimeOut: timeOut,
      TimeIn: timeIn,
      DurationSeconds: duration,
      Status: status,
      SyncedAt: syncedAt,
      TerminalId: terminalId,
      FirstName: firstName,
      LastName: lastName,
      Grade: grade,
      ClassPeriod: classPeriod,
      ManualName: manualName,
      ScheduleName: scheduleName
    );
  }

  SqliteConnection OpenConnection() {
    var connection = new SqliteConnection(connectionString);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA synchronous = FULL;";
    command.ExecuteNonQuery();
    return connection;
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
            (trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status, terminal_id, synced_at)
          VALUES ($id, $studentId, $date, $timeOut, $timeIn, $duration, $status, 'LEGACY-DEFAULT', datetime('now'));
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
