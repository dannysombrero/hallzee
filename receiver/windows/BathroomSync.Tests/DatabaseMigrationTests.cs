using BathroomSync.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BathroomSync.Tests;

public sealed class DatabaseMigrationTests {
  // These databases are deleted at the end of each test. Pooled connections
  // retain native handles after Dispose, which prevents deletion on Windows.
  // Disable pooling per connection rather than clearing other tests' pools.
  [Fact]
  public void FreshDatabaseInitializesToCurrentVersionDirectly() {
    var dbPath = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"), "fresh.db");
    var dir = Path.GetDirectoryName(dbPath)!;

    try {
      Directory.CreateDirectory(dir);
      using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
      connection.Open();

      DatabaseMigrator.Migrate(connection);

      Assert.Equal(DatabaseMigrator.CurrentSchemaVersion, DatabaseMigrator.GetCurrentVersion(connection));

      // Verify tables exist
      using var cmd = connection.CreateCommand();
      cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('trips', 'profiles', 'roster_students', 'policy_rules', 'terminals', 'bell_schedules', 'desktop_passes');";
      Assert.Equal(7L, (long)(cmd.ExecuteScalar() ?? 0L));

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
      using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString())) {
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

      using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString())) {
        connection.Open();
        Assert.Equal(DatabaseMigrator.CurrentSchemaVersion, DatabaseMigrator.GetCurrentVersion(connection));

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
      using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
      connection.Open();

      DatabaseMigrator.Migrate(connection);
      DatabaseMigrator.Migrate(connection);
      DatabaseMigrator.Migrate(connection);

      Assert.Equal(DatabaseMigrator.CurrentSchemaVersion, DatabaseMigrator.GetCurrentVersion(connection));
    } finally {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void MigratesToVersion8AddingPolicyColumns() {
    var dbPath = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"), "v8_migration.db");
    var dir = Path.GetDirectoryName(dbPath)!;

    try {
      Directory.CreateDirectory(dir);
      using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
      connection.Open();

      // Run migrator to bring DB to latest
      DatabaseMigrator.Migrate(connection);
      Assert.Equal(8, DatabaseMigrator.CurrentSchemaVersion);
      Assert.Equal(8, DatabaseMigrator.GetCurrentVersion(connection));

      // Verify columns exist on policy_rules
      var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      using (var cmd = connection.CreateCommand()) {
        cmd.CommandText = "PRAGMA table_info(policy_rules);";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
          columns.Add(reader.GetString(1));
        }
      }

      Assert.Contains("class_pass_policy_mode", columns);
      Assert.Contains("middle_window_action", columns);
      Assert.Contains("bell_transition_enabled", columns);
      Assert.Contains("warning_sound_enabled", columns);
      Assert.Contains("warning_sound_volume", columns);
      Assert.Contains("bell_rules_disabled", columns);

      // Verify default row in policy_rules
      using (var cmd = connection.CreateCommand()) {
        cmd.CommandText = "SELECT class_pass_policy_mode, middle_window_action, bell_transition_enabled, warning_sound_enabled, warning_sound_volume, bell_rules_disabled FROM policy_rules WHERE profile_id = 'default';";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Windows", reader.GetString(0));
        Assert.Equal("Allow", reader.GetString(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
        Assert.Equal(80L, reader.GetInt64(4));
        Assert.Equal(0L, reader.GetInt64(5));
      }
    } finally {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }
}
