using BathroomSync.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BathroomSync.Tests;

public sealed class DatabaseMigrationTests {
  [Fact]
  public void FreshDatabaseInitializesToCurrentVersionDirectly() {
    var dbPath = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"), "fresh.db");
    var dir = Path.GetDirectoryName(dbPath)!;

    try {
      Directory.CreateDirectory(dir);
      using var connection = new SqliteConnection($"Data Source={dbPath}");
      connection.Open();

      DatabaseMigrator.Migrate(connection);

      Assert.Equal(DatabaseMigrator.CurrentSchemaVersion, DatabaseMigrator.GetCurrentVersion(connection));

      // Verify tables exist
      using var cmd = connection.CreateCommand();
      cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('trips', 'profiles', 'roster_students', 'policy_rules', 'terminals', 'bell_schedules');";
      Assert.Equal(6L, (long)(cmd.ExecuteScalar() ?? 0L));

      // Verify default profile and policy exist
      cmd.CommandText = "SELECT name FROM profiles WHERE profile_id = 'default';";
      Assert.Equal("Default Classroom", cmd.ExecuteScalar());

      cmd.CommandText = "SELECT max_simultaneous_passes FROM policy_rules WHERE profile_id = 'default';";
      Assert.Equal(1L, (long)(cmd.ExecuteScalar() ?? 0L));
    } finally {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void MigratesLegacyV1DatabaseSeamlesslyPreservingData() {
    var dbPath = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"), "legacy_upgrade.db");
    var dir = Path.GetDirectoryName(dbPath)!;

    try {
      Directory.CreateDirectory(dir);
      using (var connection = new SqliteConnection($"Data Source={dbPath}")) {
        connection.Open();

        // Create legacy schema manually
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
          CREATE TABLE trips (
            trip_id INTEGER PRIMARY KEY,
            student_id TEXT NOT NULL,
            trip_date TEXT NOT NULL,
            time_out TEXT NOT NULL,
            time_in TEXT NOT NULL,
            duration_seconds TEXT NOT NULL,
            status TEXT NOT NULL
          );
          INSERT INTO trips VALUES (1, '1001', '2026-01-01', '08:00:00', '08:05:00', '300', 'COMPLETE');
          INSERT INTO trips VALUES (2, '1002', '2026-01-01', '09:00:00', '09:07:00', '420', 'MANUAL_RESET');
          """;
        cmd.ExecuteNonQuery();
      }

      // Now open via repository and run migrator
      var repo = new TripSqliteRepository(dbPath);
      Assert.Equal(2, repo.GetLatestTripId());

      using (var connection = new SqliteConnection($"Data Source={dbPath}")) {
        connection.Open();
        Assert.Equal(4, DatabaseMigrator.GetCurrentVersion(connection));

        // Check legacy data survived intact
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT count(*), terminal_id FROM trips GROUP BY terminal_id;";
        using var reader = checkCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal("LEGACY-DEFAULT", reader.GetString(1));
      }
    } finally {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void MigrationIsIdempotent() {
    var dbPath = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"), "idempotent.db");
    var dir = Path.GetDirectoryName(dbPath)!;

    try {
      Directory.CreateDirectory(dir);
      using var connection = new SqliteConnection($"Data Source={dbPath}");
      connection.Open();

      DatabaseMigrator.Migrate(connection);
      DatabaseMigrator.Migrate(connection);
      DatabaseMigrator.Migrate(connection);

      Assert.Equal(DatabaseMigrator.CurrentSchemaVersion, DatabaseMigrator.GetCurrentVersion(connection));
    } finally {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }
}
