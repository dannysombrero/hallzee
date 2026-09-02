using Microsoft.Data.Sqlite;

namespace BathroomSync.Core;

public interface IProfileRepository {
  IReadOnlyList<ClassroomProfile> GetAllProfiles();
  ClassroomProfile? GetActiveProfile();
  void SetActiveProfile(string profileId);
  void SaveProfile(ClassroomProfile profile);
  void DeleteProfile(string profileId);
}

public interface IPolicyRepository {
  PolicyRule GetPolicyRule(string profileId);
  void SavePolicyRule(PolicyRule rule);
  IReadOnlyList<BellSchedulePeriod> GetBellSchedule(string profileId);
  void SaveBellSchedule(string profileId, IEnumerable<BellSchedulePeriod> periods);
}

public interface ITerminalRepository {
  IReadOnlyList<TerminalDeviceConfig> GetAllTerminals();
  TerminalDeviceConfig? GetTerminal(string terminalId);
  void SaveTerminal(TerminalDeviceConfig terminal);
  void DeleteTerminal(string terminalId);
}

public sealed class ProfileAndPolicySqliteRepository : IProfileRepository, IPolicyRepository, ITerminalRepository {
  readonly string connectionString;

  public ProfileAndPolicySqliteRepository(string databasePath) {
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
  }

  // --- Profile Operations ---

  public IReadOnlyList<ClassroomProfile> GetAllProfiles() {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT profile_id, name, is_active, created_at, updated_at FROM profiles ORDER BY name ASC;";

    var list = new List<ClassroomProfile>();
    using var reader = command.ExecuteReader();
    while (reader.Read()) {
      list.Add(ReadProfile(reader));
    }
    return list;
  }

  public ClassroomProfile? GetActiveProfile() {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT profile_id, name, is_active, created_at, updated_at FROM profiles WHERE is_active = 1 LIMIT 1;";

    using var reader = command.ExecuteReader();
    if (reader.Read()) return ReadProfile(reader);

    // Fallback: return default profile if none marked active
    using var fallbackCmd = connection.CreateCommand();
    fallbackCmd.CommandText = "SELECT profile_id, name, is_active, created_at, updated_at FROM profiles LIMIT 1;";
    using var fallbackReader = fallbackCmd.ExecuteReader();
    return fallbackReader.Read() ? ReadProfile(fallbackReader) : null;
  }

  public void SetActiveProfile(string profileId) {
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    try {
      using var resetCmd = connection.CreateCommand();
      resetCmd.Transaction = transaction;
      resetCmd.CommandText = "UPDATE profiles SET is_active = 0;";
      resetCmd.ExecuteNonQuery();

      using var setCmd = connection.CreateCommand();
      setCmd.Transaction = transaction;
      setCmd.CommandText = "UPDATE profiles SET is_active = 1, updated_at = datetime('now') WHERE profile_id = $id;";
      setCmd.Parameters.AddWithValue("$id", profileId);
      setCmd.ExecuteNonQuery();

      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  public void SaveProfile(ClassroomProfile profile) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      INSERT INTO profiles (profile_id, name, is_active, created_at, updated_at)
      VALUES ($id, $name, $isActive, datetime('now'), datetime('now'))
      ON CONFLICT(profile_id) DO UPDATE SET
        name = excluded.name,
        is_active = excluded.is_active,
        updated_at = datetime('now');
      """;
    command.Parameters.AddWithValue("$id", profile.ProfileId);
    command.Parameters.AddWithValue("$name", profile.Name);
    command.Parameters.AddWithValue("$isActive", profile.IsActive ? 1 : 0);
    command.ExecuteNonQuery();
  }

  public void DeleteProfile(string profileId) {
    if (string.Equals(profileId, "default", StringComparison.OrdinalIgnoreCase)) return; // Protect default profile

    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM profiles WHERE profile_id = $id;";
    command.Parameters.AddWithValue("$id", profileId);
    command.ExecuteNonQuery();
  }

  // --- Policy Operations ---

  public PolicyRule GetPolicyRule(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      SELECT rule_id, profile_id, max_simultaneous_passes, duration_warning_seconds, max_daily_passes_per_student, lockout_start_minutes, lockout_end_minutes, first_window_action, last_window_action, alert_sound
      FROM policy_rules
      WHERE profile_id = $profileId
      LIMIT 1;
      """;
    command.Parameters.AddWithValue("$profileId", profileId);

    using var reader = command.ExecuteReader();
    if (reader.Read()) {
      return new PolicyRule(
        RuleId: reader.GetString(0),
        ProfileId: reader.GetString(1),
        MaxSimultaneousPasses: reader.GetInt32(2),
        DurationWarningSeconds: reader.GetInt32(3),
        MaxDailyPassesPerStudent: reader.GetInt32(4),
        LockoutStartMinutes: reader.GetInt32(5),
        LockoutEndMinutes: reader.GetInt32(6),
        FirstWindowAction: reader.GetString(7),
        LastWindowAction: reader.GetString(8),
        AlertSound: reader.GetString(9)
      );
    }

    return new PolicyRule(RuleId: $"rule_{profileId}", ProfileId: profileId);
  }

  public void SavePolicyRule(PolicyRule rule) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      INSERT INTO policy_rules 
        (rule_id, profile_id, max_simultaneous_passes, duration_warning_seconds, max_daily_passes_per_student, lockout_start_minutes, lockout_end_minutes, first_window_action, last_window_action, alert_sound)
      VALUES 
        ($ruleId, $profileId, $maxSimul, $durWarn, $maxDaily, $lockStart, $lockEnd, $firstAction, $lastAction, $alertSound)
      ON CONFLICT(profile_id) DO UPDATE SET
        rule_id = excluded.rule_id,
        max_simultaneous_passes = excluded.max_simultaneous_passes,
        duration_warning_seconds = excluded.duration_warning_seconds,
        max_daily_passes_per_student = excluded.max_daily_passes_per_student,
        lockout_start_minutes = excluded.lockout_start_minutes,
        lockout_end_minutes = excluded.lockout_end_minutes,
        first_window_action = excluded.first_window_action,
        last_window_action = excluded.last_window_action,
        alert_sound = excluded.alert_sound;
      """;
    command.Parameters.AddWithValue("$ruleId", rule.RuleId);
    command.Parameters.AddWithValue("$profileId", rule.ProfileId);
    command.Parameters.AddWithValue("$maxSimul", rule.MaxSimultaneousPasses);
    command.Parameters.AddWithValue("$durWarn", rule.DurationWarningSeconds);
    command.Parameters.AddWithValue("$maxDaily", rule.MaxDailyPassesPerStudent);
    command.Parameters.AddWithValue("$lockStart", rule.LockoutStartMinutes);
    command.Parameters.AddWithValue("$lockEnd", rule.LockoutEndMinutes);
    command.Parameters.AddWithValue("$firstAction", rule.FirstWindowAction);
    command.Parameters.AddWithValue("$lastAction", rule.LastWindowAction);
    command.Parameters.AddWithValue("$alertSound", rule.AlertSound);
    command.ExecuteNonQuery();
  }

  public IReadOnlyList<BellSchedulePeriod> GetBellSchedule(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      SELECT schedule_id, profile_id, period_name, start_time, end_time, days_of_week, schedule_name
      FROM bell_schedules
      WHERE profile_id = $profileId
      ORDER BY start_time ASC;
      """;
    command.Parameters.AddWithValue("$profileId", profileId);

    var list = new List<BellSchedulePeriod>();
    using var reader = command.ExecuteReader();
    while (reader.Read()) {
      list.Add(new BellSchedulePeriod(
        ScheduleId: reader.GetString(0),
        ProfileId: reader.GetString(1),
        PeriodName: reader.GetString(2),
        StartTime: reader.GetString(3),
        EndTime: reader.GetString(4),
        DaysOfWeek: reader.GetString(5),
        ScheduleName: reader.GetString(6)
      ));
    }
    return list;
  }

  public void SaveBellSchedule(string profileId, IEnumerable<BellSchedulePeriod> periods) {
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    try {
      using var clearCmd = connection.CreateCommand();
      clearCmd.Transaction = transaction;
      clearCmd.CommandText = "DELETE FROM bell_schedules WHERE profile_id = $profileId;";
      clearCmd.Parameters.AddWithValue("$profileId", profileId);
      clearCmd.ExecuteNonQuery();

      using var insertCmd = connection.CreateCommand();
      insertCmd.Transaction = transaction;
      insertCmd.CommandText = """
        INSERT INTO bell_schedules (schedule_id, profile_id, period_name, start_time, end_time, days_of_week, schedule_name)
        VALUES ($id, $profileId, $name, $start, $end, $days, $scheduleName);
        """;
      var pId = insertCmd.Parameters.Add("$id", SqliteType.Text);
      var pProfile = insertCmd.Parameters.Add("$profileId", SqliteType.Text);
      var pName = insertCmd.Parameters.Add("$name", SqliteType.Text);
      var pStart = insertCmd.Parameters.Add("$start", SqliteType.Text);
      var pEnd = insertCmd.Parameters.Add("$end", SqliteType.Text);
      var pDays = insertCmd.Parameters.Add("$days", SqliteType.Text);
      var pScheduleName = insertCmd.Parameters.Add("$scheduleName", SqliteType.Text);

      pProfile.Value = profileId;

      foreach (var period in periods) {
        pId.Value = string.IsNullOrWhiteSpace(period.ScheduleId) ? Guid.NewGuid().ToString("N") : period.ScheduleId;
        pName.Value = period.PeriodName;
        pStart.Value = period.StartTime;
        pEnd.Value = period.EndTime;
        pDays.Value = period.DaysOfWeek;
        pScheduleName.Value = period.ScheduleName;
        insertCmd.ExecuteNonQuery();
      }

      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  // --- Terminal Operations ---

  public IReadOnlyList<TerminalDeviceConfig> GetAllTerminals() {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT terminal_id, custom_name, ble_address, last_seen_at, max_id_length FROM terminals ORDER BY custom_name ASC;";

    var list = new List<TerminalDeviceConfig>();
    using var reader = command.ExecuteReader();
    while (reader.Read()) {
      list.Add(ReadTerminal(reader));
    }
    return list;
  }

  public TerminalDeviceConfig? GetTerminal(string terminalId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT terminal_id, custom_name, ble_address, last_seen_at, max_id_length FROM terminals WHERE terminal_id = $id LIMIT 1;";
    command.Parameters.AddWithValue("$id", terminalId);

    using var reader = command.ExecuteReader();
    return reader.Read() ? ReadTerminal(reader) : null;
  }

  public void SaveTerminal(TerminalDeviceConfig terminal) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      INSERT INTO terminals (terminal_id, custom_name, ble_address, last_seen_at, max_id_length)
      VALUES ($id, $name, $addr, $lastSeen, $maxLen)
      ON CONFLICT(terminal_id) DO UPDATE SET
        custom_name = excluded.custom_name,
        ble_address = excluded.ble_address,
        last_seen_at = excluded.last_seen_at,
        max_id_length = excluded.max_id_length;
      """;
    command.Parameters.AddWithValue("$id", terminal.TerminalId);
    command.Parameters.AddWithValue("$name", terminal.CustomName);
    command.Parameters.AddWithValue("$addr", (object?)terminal.BleAddress ?? DBNull.Value);
    command.Parameters.AddWithValue("$lastSeen", terminal.LastSeenAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
    command.Parameters.AddWithValue("$maxLen", terminal.MaxIdLength);
    command.ExecuteNonQuery();
  }

  public void DeleteTerminal(string terminalId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM terminals WHERE terminal_id = $id;";
    command.Parameters.AddWithValue("$id", terminalId);
    command.ExecuteNonQuery();
  }

  static ClassroomProfile ReadProfile(SqliteDataReader reader) {
    var id = reader.GetString(0);
    var name = reader.GetString(1);
    var isActive = reader.GetInt32(2) == 1;
    var createdStr = reader.IsDBNull(3) ? null : reader.GetString(3);
    var updatedStr = reader.IsDBNull(4) ? null : reader.GetString(4);

    DateTime? createdAt = DateTime.TryParse(createdStr, out var ca) ? ca : null;
    DateTime? updatedAt = DateTime.TryParse(updatedStr, out var ua) ? ua : null;

    return new ClassroomProfile(
      ProfileId: id,
      Name: name,
      IsActive: isActive,
      CreatedAt: createdAt,
      UpdatedAt: updatedAt
    );
  }

  static TerminalDeviceConfig ReadTerminal(SqliteDataReader reader) {
    var id = reader.GetString(0);
    var name = reader.GetString(1);
    var addr = reader.IsDBNull(2) ? null : reader.GetString(2);
    var lastSeenStr = reader.IsDBNull(3) ? null : reader.GetString(3);
    var maxLen = reader.IsDBNull(4) ? 10 : reader.GetInt32(4);

    DateTime? lastSeen = DateTime.TryParse(lastSeenStr, out var ls) ? ls : null;

    return new TerminalDeviceConfig(
      TerminalId: id,
      CustomName: name,
      BleAddress: addr,
      LastSeenAt: lastSeen,
      MaxIdLength: maxLen
    );
  }

  SqliteConnection OpenConnection() {
    var connection = new SqliteConnection(connectionString);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA synchronous = FULL; PRAGMA foreign_keys = ON;";
    command.ExecuteNonQuery();
    return connection;
  }
}
