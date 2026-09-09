using System.Globalization;
using Microsoft.Data.Sqlite;

namespace BathroomSync.Core;

public sealed record DesktopPass(string PassId, string ProfileId, string StudentId, string StudentName,
  DateTime CheckoutAt, string Period, string Destination, string Purpose, string? ScheduleName = null);

public sealed partial class TripSqliteRepository {
  public void StartDesktopPass(DesktopPass pass) {
    if (string.IsNullOrWhiteSpace(pass.PassId) || string.IsNullOrWhiteSpace(pass.ProfileId) ||
        string.IsNullOrWhiteSpace(pass.StudentId)) throw new ArgumentException("A pass needs an ID, workspace, and student identity.");
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      INSERT INTO desktop_passes
        (pass_id, profile_id, student_id, student_name, checkout_at, period, destination, purpose, schedule_name)
      VALUES ($pass, $profile, $student, $name, $checkout, $period, $destination, $purpose, $schedule);
      """;
    command.Parameters.AddWithValue("$pass", pass.PassId);
    command.Parameters.AddWithValue("$profile", pass.ProfileId);
    command.Parameters.AddWithValue("$student", pass.StudentId);
    command.Parameters.AddWithValue("$name", pass.StudentName);
    command.Parameters.AddWithValue("$checkout", pass.CheckoutAt.ToString("O", CultureInfo.InvariantCulture));
    command.Parameters.AddWithValue("$period", pass.Period);
    command.Parameters.AddWithValue("$destination", pass.Destination);
    command.Parameters.AddWithValue("$purpose", pass.Purpose);
    command.Parameters.AddWithValue("$schedule", (object?)pass.ScheduleName ?? DBNull.Value);
    command.ExecuteNonQuery();
  }

  public DesktopPass? GetActiveDesktopPass(string profileId) {
    using var connection = OpenConnection();
    return ReadDesktopPass(connection, null, profileId);
  }

  static DesktopPass? ReadDesktopPass(SqliteConnection connection, SqliteTransaction? transaction, string profileId) {
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
      SELECT pass_id, profile_id, student_id, student_name, checkout_at, period, destination, purpose, schedule_name
      FROM desktop_passes WHERE profile_id = $profile AND completed_at IS NULL;
      """;
    command.Parameters.AddWithValue("$profile", profileId);
    using var reader = command.ExecuteReader();
    return !reader.Read() ? null : new DesktopPass(reader.GetString(0), reader.GetString(1), reader.GetString(2),
      reader.GetString(3), DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
      reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8));
  }

  // Reserve the write transaction before reading. Concurrent/repeated check-ins
  // see a completed pass and cannot allocate another history record.
  public bool CompleteDesktopPass(string profileId, string passId, DateTime returnedAt) {
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction(deferred: false);
    var pass = ReadDesktopPass(connection, transaction, profileId);
    if (pass == null || pass.PassId != passId) return false;
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT COALESCE(MAX(trip_id), 0) + 1 FROM trips WHERE terminal_id = 'DESKTOP';";
    var tripId = (long)command.ExecuteScalar()!;
    command.CommandText = """
      INSERT INTO trips (terminal_id, trip_id, student_id, manual_name, trip_date, time_out, time_in,
        duration_seconds, status, synced_at, profile_id, schedule_name, class_section)
      VALUES ('DESKTOP', $trip, $student, $name, $date, $out, $in, $duration, 'MANUAL', datetime('now'),
        $profile, $schedule, $period);
      UPDATE desktop_passes SET completed_at = $returned, trip_id = $trip WHERE pass_id = $pass;
      """;
    command.Parameters.AddWithValue("$trip", tripId);
    command.Parameters.AddWithValue("$student", pass.StudentId);
    command.Parameters.AddWithValue("$name", string.IsNullOrWhiteSpace(pass.StudentName) ? DBNull.Value : pass.StudentName);
    command.Parameters.AddWithValue("$date", pass.CheckoutAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    command.Parameters.AddWithValue("$out", pass.CheckoutAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    command.Parameters.AddWithValue("$in", returnedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    command.Parameters.AddWithValue("$duration", (long)Math.Max(0, (returnedAt - pass.CheckoutAt).TotalSeconds));
    command.Parameters.AddWithValue("$profile", pass.ProfileId);
    command.Parameters.AddWithValue("$schedule", (object?)pass.ScheduleName ?? DBNull.Value);
    command.Parameters.AddWithValue("$period", pass.Period);
    command.Parameters.AddWithValue("$returned", returnedAt.ToString("O", CultureInfo.InvariantCulture));
    command.Parameters.AddWithValue("$pass", pass.PassId);
    command.ExecuteNonQuery();
    transaction.Commit();
    return true;
  }
}
