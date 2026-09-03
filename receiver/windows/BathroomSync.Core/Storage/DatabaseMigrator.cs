using Microsoft.Data.Sqlite;

namespace BathroomSync.Core;

public static class DatabaseMigrator {
  public const int CurrentSchemaVersion = 4;

  public static void Migrate(SqliteConnection connection) {
    EnsureMigrationTable(connection);
    var currentVersion = GetCurrentVersion(connection);

    if (currentVersion < 1) {
      ApplyMigration1(connection);
    }
    if (currentVersion < 2) {
      ApplyMigration2(connection);
    }
    if (currentVersion < 3) {
      ApplyMigration3(connection);
    }
    if (currentVersion < 4) {
      ApplyMigration4(connection);
    }
  }

  public static int GetCurrentVersion(SqliteConnection connection) {
    EnsureMigrationTable(connection);
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
    var result = cmd.ExecuteScalar();
    return result is null || result is DBNull ? 0 : Convert.ToInt32(result);
  }

  static void EnsureMigrationTable(SqliteConnection connection) {
    using var cmd = connection.CreateCommand();
    cmd.CommandText = """
      PRAGMA journal_mode = WAL;
      CREATE TABLE IF NOT EXISTS schema_migrations (
        version INTEGER PRIMARY KEY,
        applied_at TEXT NOT NULL,
        description TEXT NOT NULL
      );
      """;
    cmd.ExecuteNonQuery();
  }

  static void ApplyMigration1(SqliteConnection connection) {
    using var transaction = connection.BeginTransaction();
    try {
      using var cmd = connection.CreateCommand();
      cmd.Transaction = transaction;
      cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS trips (
          trip_id INTEGER PRIMARY KEY,
          student_id TEXT NOT NULL,
          trip_date TEXT NOT NULL,
          time_out TEXT NOT NULL,
          time_in TEXT NOT NULL,
          duration_seconds TEXT NOT NULL,
          status TEXT NOT NULL
        );
        INSERT OR IGNORE INTO schema_migrations (version, applied_at, description)
        VALUES (1, datetime('now'), 'Initial trips table');
        """;
      cmd.ExecuteNonQuery();
      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  static void ApplyMigration2(SqliteConnection connection) {
    using var transaction = connection.BeginTransaction();
    try {
      // Check existing columns in trips
      var hasSyncedAt = false;
      var hasTerminalId = false;

      using (var infoCmd = connection.CreateCommand()) {
        infoCmd.Transaction = transaction;
        infoCmd.CommandText = "PRAGMA table_info(trips);";
        using var reader = infoCmd.ExecuteReader();
        while (reader.Read()) {
          var colName = reader.GetString(1);
          if (string.Equals(colName, "synced_at", StringComparison.OrdinalIgnoreCase)) hasSyncedAt = true;
          if (string.Equals(colName, "terminal_id", StringComparison.OrdinalIgnoreCase)) hasTerminalId = true;
        }
      }

      using (var alterCmd = connection.CreateCommand()) {
        alterCmd.Transaction = transaction;
        if (!hasSyncedAt) {
          alterCmd.CommandText = """
            ALTER TABLE trips ADD COLUMN synced_at TEXT NOT NULL DEFAULT '';
            UPDATE trips SET synced_at = datetime('now') WHERE synced_at = '';
            """;
          alterCmd.ExecuteNonQuery();
        }
        if (!hasTerminalId) {
          alterCmd.CommandText = "ALTER TABLE trips ADD COLUMN terminal_id TEXT NOT NULL DEFAULT 'DEFAULT';";
          alterCmd.ExecuteNonQuery();
        }
      }

      using (var ddlCmd = connection.CreateCommand()) {
        ddlCmd.Transaction = transaction;
        ddlCmd.CommandText = """
          CREATE INDEX IF NOT EXISTS idx_trips_date ON trips(trip_date);
          CREATE INDEX IF NOT EXISTS idx_trips_student ON trips(student_id);

          CREATE TABLE IF NOT EXISTS profiles (
            profile_id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            is_active INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at TEXT NOT NULL DEFAULT (datetime('now'))
          );

          INSERT OR IGNORE INTO profiles (profile_id, name, is_active)
          VALUES ('default', 'Default Classroom', 1);

          CREATE TABLE IF NOT EXISTS roster_students (
            student_id TEXT NOT NULL,
            profile_id TEXT NOT NULL,
            first_name TEXT NOT NULL,
            last_name TEXT NOT NULL,
            grade TEXT,
            class_period TEXT,
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (student_id, profile_id),
            FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
          );
          CREATE INDEX IF NOT EXISTS idx_roster_name ON roster_students(last_name, first_name);

          CREATE TABLE IF NOT EXISTS terminals (
            terminal_id TEXT PRIMARY KEY,
            custom_name TEXT NOT NULL,
            ble_address TEXT,
            last_seen_at TEXT,
            max_id_length INTEGER DEFAULT 10
          );

          CREATE TABLE IF NOT EXISTS policy_rules (
            rule_id TEXT PRIMARY KEY,
            profile_id TEXT NOT NULL UNIQUE,
            max_simultaneous_passes INTEGER DEFAULT 1,
            duration_warning_seconds INTEGER DEFAULT 420,
            max_daily_passes_per_student INTEGER DEFAULT 2,
            lockout_start_minutes INTEGER DEFAULT 10,
            lockout_end_minutes INTEGER DEFAULT 10,
            FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
          );

          INSERT OR IGNORE INTO policy_rules (rule_id, profile_id, max_simultaneous_passes, duration_warning_seconds, max_daily_passes_per_student, lockout_start_minutes, lockout_end_minutes)
          VALUES ('default_policy', 'default', 1, 420, 2, 10, 10);

          CREATE TABLE IF NOT EXISTS bell_schedules (
            schedule_id TEXT PRIMARY KEY,
            profile_id TEXT NOT NULL,
            period_name TEXT NOT NULL,
            start_time TEXT NOT NULL,
            end_time TEXT NOT NULL,
            days_of_week TEXT NOT NULL DEFAULT '1,2,3,4,5',
            FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
          );

          INSERT OR REPLACE INTO schema_migrations (version, applied_at, description)
          VALUES (2, datetime('now'), 'Relational tables (profiles, rosters, terminals, policies, schedules)');
          """;
        ddlCmd.ExecuteNonQuery();
      }

      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  static void ApplyMigration3(SqliteConnection connection) {
    using var transaction = connection.BeginTransaction();
    try {
      var policyColumns = GetColumnNames(connection, transaction, "policy_rules");
      var scheduleColumns = GetColumnNames(connection, transaction, "bell_schedules");
      using var command = connection.CreateCommand();
      command.Transaction = transaction;
      if (!policyColumns.Contains("first_window_action")) {
        command.CommandText = "ALTER TABLE policy_rules ADD COLUMN first_window_action TEXT NOT NULL DEFAULT 'Warn';";
        command.ExecuteNonQuery();
      }
      if (!policyColumns.Contains("last_window_action")) {
        command.CommandText = "ALTER TABLE policy_rules ADD COLUMN last_window_action TEXT NOT NULL DEFAULT 'Warn';";
        command.ExecuteNonQuery();
      }
      if (!policyColumns.Contains("alert_sound")) {
        command.CommandText = "ALTER TABLE policy_rules ADD COLUMN alert_sound TEXT NOT NULL DEFAULT 'Chime';";
        command.ExecuteNonQuery();
      }
      if (!scheduleColumns.Contains("schedule_name")) {
        command.CommandText = "ALTER TABLE bell_schedules ADD COLUMN schedule_name TEXT NOT NULL DEFAULT 'Regular';";
        command.ExecuteNonQuery();
      }
      command.CommandText = "INSERT OR REPLACE INTO schema_migrations (version, applied_at, description) VALUES (3, datetime('now'), 'Bell-window policies, sounds, and named schedules');";
      command.ExecuteNonQuery();
      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  static void ApplyMigration4(SqliteConnection connection) {
    using var transaction = connection.BeginTransaction();
    try {
      using (var command = connection.CreateCommand()) {
        command.Transaction = transaction;
        command.CommandText = """
          CREATE TABLE IF NOT EXISTS terminals_v4 (
            terminal_id TEXT PRIMARY KEY,
            custom_name TEXT NOT NULL,
            transport_id TEXT,
            ble_address TEXT,
            protocol_version INTEGER NOT NULL DEFAULT 2,
            claim_status TEXT NOT NULL DEFAULT 'UNKNOWN',
            last_seen_at TEXT,
            max_id_length INTEGER NOT NULL DEFAULT 10
          );

          INSERT OR IGNORE INTO terminals_v4
            (terminal_id, custom_name, transport_id, ble_address, protocol_version, claim_status, last_seen_at, max_id_length)
          SELECT terminal_id, custom_name, NULL, ble_address, 1, 'LEGACY', last_seen_at, COALESCE(max_id_length, 10)
          FROM terminals;

          INSERT OR IGNORE INTO terminals_v4
            (terminal_id, custom_name, protocol_version, claim_status, max_id_length)
          VALUES ('LEGACY-DEFAULT', 'Legacy imported trips', 1, 'LEGACY', 10);

          DROP TABLE terminals;
          ALTER TABLE terminals_v4 RENAME TO terminals;

          CREATE INDEX IF NOT EXISTS idx_terminals_transport_id ON terminals(transport_id);
          """;
        command.ExecuteNonQuery();
      }

      var tripColumns = GetColumnNames(connection, transaction, "trips");
      if (!tripColumns.Contains("terminal_id")) {
        using var addTerminalId = connection.CreateCommand();
        addTerminalId.Transaction = transaction;
        addTerminalId.CommandText = "ALTER TABLE trips ADD COLUMN terminal_id TEXT NOT NULL DEFAULT 'LEGACY-DEFAULT';";
        addTerminalId.ExecuteNonQuery();
      }

      using (var command = connection.CreateCommand()) {
        command.Transaction = transaction;
        command.CommandText = """
          CREATE TABLE trips_v4 (
            terminal_id TEXT NOT NULL,
            trip_id INTEGER NOT NULL,
            student_id TEXT NOT NULL,
            trip_date TEXT NOT NULL,
            time_out TEXT NOT NULL,
            time_in TEXT NOT NULL,
            duration_seconds TEXT NOT NULL,
            status TEXT NOT NULL,
            synced_at TEXT NOT NULL,
            PRIMARY KEY (terminal_id, trip_id)
          );

          INSERT OR IGNORE INTO trips_v4
            (terminal_id, trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status, synced_at)
          SELECT
            CASE
              WHEN terminal_id IS NULL OR trim(terminal_id) = '' OR terminal_id = 'DEFAULT'
              THEN 'LEGACY-DEFAULT'
              ELSE terminal_id
            END,
            trip_id,
            student_id,
            trip_date,
            time_out,
            time_in,
            duration_seconds,
            status,
            COALESCE(NULLIF(synced_at, ''), datetime('now'))
          FROM trips;

          DROP TABLE trips;
          ALTER TABLE trips_v4 RENAME TO trips;
          CREATE INDEX IF NOT EXISTS idx_trips_date ON trips(trip_date);
          CREATE INDEX IF NOT EXISTS idx_trips_student ON trips(student_id);
          CREATE INDEX IF NOT EXISTS idx_trips_terminal_id ON trips(terminal_id);

          CREATE TABLE IF NOT EXISTS client_installation (
            singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
            client_id TEXT NOT NULL UNIQUE,
            created_at TEXT NOT NULL
          );

          CREATE TABLE IF NOT EXISTS profile_terminal_assignments (
            profile_id TEXT PRIMARY KEY,
            terminal_id TEXT NOT NULL,
            assigned_at TEXT NOT NULL,
            FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE,
            FOREIGN KEY (terminal_id) REFERENCES terminals(terminal_id) ON DELETE RESTRICT
          );
          CREATE INDEX IF NOT EXISTS idx_profile_terminal_terminal
            ON profile_terminal_assignments(terminal_id);

          INSERT OR REPLACE INTO schema_migrations (version, applied_at, description)
          VALUES (4, datetime('now'), 'Stable terminal identity, exclusive claim metadata, and terminal-scoped trips');
          """;
        command.ExecuteNonQuery();
      }

      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  static HashSet<string> GetColumnNames(SqliteConnection connection, SqliteTransaction transaction, string tableName) {
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = $"PRAGMA table_info({tableName});";
    using var reader = command.ExecuteReader();
    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while (reader.Read()) columns.Add(reader.GetString(1));
    return columns;
  }
}
