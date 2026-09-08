using Microsoft.Data.Sqlite;
using System.Globalization;

namespace BathroomSync.Core;

public interface IProfileRepository {
  IReadOnlyList<ClassroomProfile> GetAllProfiles();
  ClassroomProfile? GetActiveProfile();
  void SetActiveProfile(string profileId);
  void SaveProfile(ClassroomProfile profile);
  void DeleteProfile(string profileId);
  DateTime? GetLastSuccessfulSync(string profileId);
  void SaveLastSuccessfulSync(string profileId, DateTime syncedAt);
}

public interface IPolicyRepository {
  PolicyRule GetPolicyRule(string profileId);
  void SavePolicyRule(PolicyRule rule);
  IReadOnlyList<BellSchedulePeriod> GetBellSchedule(string profileId);
  void SaveBellSchedule(string profileId, IEnumerable<BellSchedulePeriod> periods);
  IReadOnlyList<ScheduleException> GetScheduleExceptions(string profileId);
  void SaveScheduleExceptions(string profileId, IEnumerable<ScheduleException> exceptions);
}

public interface ITerminalRepository {
  IReadOnlyList<TerminalDeviceConfig> GetAllTerminals();
  TerminalDeviceConfig? GetTerminal(string terminalId);
  void SaveTerminal(TerminalDeviceConfig terminal);
  void DeleteTerminal(string terminalId);
  string GetOrCreateClientId();
  void SaveTerminalTransport(string terminalId, string transportId, DateTime lastSeenAt);
  void AssignTerminalToProfile(string profileId, string terminalId);
  string? GetAssignedTerminalId(string profileId);
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
    using var transaction = connection.BeginTransaction();
    SaveProfile(profile, connection, transaction);
    transaction.Commit();
  }

  static void SaveProfile(ClassroomProfile profile, SqliteConnection connection, SqliteTransaction transaction) {
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
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

  public DateTime? GetLastSuccessfulSync(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT last_successful_sync_at FROM sync_state WHERE profile_id = $profileId LIMIT 1;";
    command.Parameters.AddWithValue("$profileId", profileId);
    var value = command.ExecuteScalar()?.ToString();
    return DateTime.TryParse(
      value,
      CultureInfo.InvariantCulture,
      DateTimeStyles.RoundtripKind,
      out var parsed
    ) ? parsed : null;
  }

  public void SaveLastSuccessfulSync(string profileId, DateTime syncedAt) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO sync_state (profile_id, last_successful_sync_at) VALUES ($profileId, $syncedAt) ON CONFLICT(profile_id) DO UPDATE SET last_successful_sync_at = excluded.last_successful_sync_at;";
    command.Parameters.AddWithValue("$profileId", profileId);
    command.Parameters.AddWithValue("$syncedAt", syncedAt.ToString("O"));
    command.ExecuteNonQuery();
  }

  // --- Policy Operations ---

  public PolicyRule GetPolicyRule(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      SELECT rule_id, profile_id, max_simultaneous_passes, duration_warning_seconds, max_daily_passes_per_student, lockout_start_minutes, lockout_end_minutes, first_window_action, last_window_action, alert_sound, terminal_enforcement_enabled
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
        AlertSound: reader.GetString(9),
        TerminalEnforcementEnabled: reader.GetInt32(10) != 0
      );
    }

    return new PolicyRule(RuleId: $"rule_{profileId}", ProfileId: profileId);
  }

  public void SavePolicyRule(PolicyRule rule) {
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    SavePolicyRule(rule, connection, transaction);
    transaction.Commit();
  }

  static void SavePolicyRule(PolicyRule rule, SqliteConnection connection, SqliteTransaction transaction) {
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
      INSERT INTO policy_rules 
        (rule_id, profile_id, max_simultaneous_passes, duration_warning_seconds, max_daily_passes_per_student, lockout_start_minutes, lockout_end_minutes, first_window_action, last_window_action, alert_sound, terminal_enforcement_enabled)
      VALUES 
        ($ruleId, $profileId, $maxSimul, $durWarn, $maxDaily, $lockStart, $lockEnd, $firstAction, $lastAction, $alertSound, $terminalEnforcement)
      ON CONFLICT(profile_id) DO UPDATE SET
        rule_id = excluded.rule_id,
        max_simultaneous_passes = excluded.max_simultaneous_passes,
        duration_warning_seconds = excluded.duration_warning_seconds,
        max_daily_passes_per_student = excluded.max_daily_passes_per_student,
        lockout_start_minutes = excluded.lockout_start_minutes,
        lockout_end_minutes = excluded.lockout_end_minutes,
        first_window_action = excluded.first_window_action,
        last_window_action = excluded.last_window_action,
        alert_sound = excluded.alert_sound,
        terminal_enforcement_enabled = excluded.terminal_enforcement_enabled;
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
    command.Parameters.AddWithValue("$terminalEnforcement", rule.TerminalEnforcementEnabled ? 1 : 0);
    command.ExecuteNonQuery();
  }

  public IReadOnlyList<BellSchedulePeriod> GetBellSchedule(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      SELECT schedule_id, profile_id, period_name, start_time, end_time, days_of_week, schedule_name, class_section
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
        ScheduleName: reader.GetString(6),
        ClassSection: reader.GetString(7)
      ));
    }
    return list;
  }

  public void SaveBellSchedule(string profileId, IEnumerable<BellSchedulePeriod> periods) {
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    SaveBellSchedule(profileId, periods, connection, transaction);
    transaction.Commit();
  }

  static void SaveBellSchedule(string profileId, IEnumerable<BellSchedulePeriod> periods, SqliteConnection connection, SqliteTransaction transaction) {
    using var clearCmd = connection.CreateCommand();
    clearCmd.Transaction = transaction;
    clearCmd.CommandText = "DELETE FROM bell_schedules WHERE profile_id = $profileId;";
    clearCmd.Parameters.AddWithValue("$profileId", profileId);
    clearCmd.ExecuteNonQuery();

    using var insertCmd = connection.CreateCommand();
    insertCmd.Transaction = transaction;
    insertCmd.CommandText = """
      INSERT INTO bell_schedules (schedule_id, profile_id, period_name, start_time, end_time, days_of_week, schedule_name, class_section)
      VALUES ($id, $profileId, $name, $start, $end, $days, $scheduleName, $classSection);
      """;
    var pId = insertCmd.Parameters.Add("$id", SqliteType.Text);
    var pProfile = insertCmd.Parameters.Add("$profileId", SqliteType.Text);
    var pName = insertCmd.Parameters.Add("$name", SqliteType.Text);
    var pStart = insertCmd.Parameters.Add("$start", SqliteType.Text);
    var pEnd = insertCmd.Parameters.Add("$end", SqliteType.Text);
    var pDays = insertCmd.Parameters.Add("$days", SqliteType.Text);
    var pScheduleName = insertCmd.Parameters.Add("$scheduleName", SqliteType.Text);
    var pClassSection = insertCmd.Parameters.Add("$classSection", SqliteType.Text);

    pProfile.Value = profileId;

    foreach (var period in periods) {
      pId.Value = string.IsNullOrWhiteSpace(period.ScheduleId) ? Guid.NewGuid().ToString("N") : period.ScheduleId;
      pName.Value = period.PeriodName;
      pStart.Value = period.StartTime;
      pEnd.Value = period.EndTime;
      pDays.Value = period.DaysOfWeek;
      pScheduleName.Value = period.ScheduleName;
      pClassSection.Value = period.ClassSection;
      insertCmd.ExecuteNonQuery();
    }

  }

  public IReadOnlyList<ScheduleException> GetScheduleExceptions(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT exception_id, profile_id, exception_date, schedule_name, is_no_school FROM schedule_exceptions WHERE profile_id = $profileId ORDER BY exception_date;";
    command.Parameters.AddWithValue("$profileId", profileId);
    var result = new List<ScheduleException>();
    using var reader = command.ExecuteReader();
    while (reader.Read()) {
      result.Add(new ScheduleException(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4) != 0));
    }
    return result;
  }

  public void SaveScheduleExceptions(string profileId, IEnumerable<ScheduleException> exceptions) {
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    SaveScheduleExceptions(profileId, exceptions, connection, transaction);
    transaction.Commit();
  }

  static void SaveScheduleExceptions(string profileId, IEnumerable<ScheduleException> exceptions, SqliteConnection connection, SqliteTransaction transaction) {
    using (var clear = connection.CreateCommand()) {
      clear.Transaction = transaction;
      clear.CommandText = "DELETE FROM schedule_exceptions WHERE profile_id = $profileId;";
      clear.Parameters.AddWithValue("$profileId", profileId);
      clear.ExecuteNonQuery();
    }
    foreach (var item in exceptions) {
      using var insert = connection.CreateCommand();
      insert.Transaction = transaction;
      insert.CommandText = "INSERT INTO schedule_exceptions (exception_id, profile_id, exception_date, schedule_name, is_no_school) VALUES ($id, $profileId, $date, $name, $noSchool);";
      insert.Parameters.AddWithValue("$id", string.IsNullOrWhiteSpace(item.ExceptionId) ? Guid.NewGuid().ToString("N") : item.ExceptionId);
      insert.Parameters.AddWithValue("$profileId", profileId);
      insert.Parameters.AddWithValue("$date", item.ExceptionDate);
      insert.Parameters.AddWithValue("$name", item.ScheduleName ?? "");
      insert.Parameters.AddWithValue("$noSchool", item.IsNoSchool ? 1 : 0);
      insert.ExecuteNonQuery();
    }
  }

  public ClassroomProfile ImportWorkspace(WorkspacePackage package) {
    WorkspaceTransfer.Validate(package);
    var id = Guid.NewGuid().ToString("N");
    var profile = new ClassroomProfile(id, package.Name.Trim());
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    SaveProfile(profile, connection, transaction);
    SavePolicyRule(package.Rule with { ProfileId = id, RuleId = $"rule-{id}" }, connection, transaction);
    SaveBellSchedule(id, package.Periods.Select(p => new BellSchedulePeriod(
      Guid.NewGuid().ToString("N"), id, p.PeriodName, p.StartTime, p.EndTime,
      p.DaysOfWeek, p.ScheduleName, p.ClassSection)), connection, transaction);
    SaveScheduleExceptions(id, package.Exceptions.Select(e => e with {
      ExceptionId = Guid.NewGuid().ToString("N"), ProfileId = id
    }), connection, transaction);
    transaction.Commit();
    return profile;
  }

  // --- Terminal Operations ---

  public IReadOnlyList<TerminalDeviceConfig> GetAllTerminals() {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT terminal_id, custom_name, transport_id, ble_address, protocol_version, claim_status, last_seen_at, max_id_length FROM terminals ORDER BY custom_name ASC;";

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
    command.CommandText = "SELECT terminal_id, custom_name, transport_id, ble_address, protocol_version, claim_status, last_seen_at, max_id_length FROM terminals WHERE terminal_id = $id LIMIT 1;";
    command.Parameters.AddWithValue("$id", terminalId);

    using var reader = command.ExecuteReader();
    return reader.Read() ? ReadTerminal(reader) : null;
  }

  public void SaveTerminal(TerminalDeviceConfig terminal) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      INSERT INTO terminals (terminal_id, custom_name, transport_id, ble_address, protocol_version, claim_status, last_seen_at, max_id_length)
      VALUES ($id, $name, $transportId, $addr, $protocolVersion, $claimStatus, $lastSeen, $maxLen)
      ON CONFLICT(terminal_id) DO UPDATE SET
        custom_name = excluded.custom_name,
        transport_id = excluded.transport_id,
        ble_address = excluded.ble_address,
        protocol_version = excluded.protocol_version,
        claim_status = excluded.claim_status,
        last_seen_at = excluded.last_seen_at,
        max_id_length = excluded.max_id_length;
      """;
    command.Parameters.AddWithValue("$id", terminal.TerminalId);
    command.Parameters.AddWithValue("$name", terminal.CustomName);
    command.Parameters.AddWithValue("$transportId", (object?)terminal.TransportId ?? DBNull.Value);
    command.Parameters.AddWithValue("$addr", (object?)terminal.BleAddress ?? DBNull.Value);
    command.Parameters.AddWithValue("$protocolVersion", terminal.ProtocolVersion);
    command.Parameters.AddWithValue("$claimStatus", terminal.ClaimStatus);
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

  public string GetOrCreateClientId() {
    using var connection = OpenConnection();
    using (var read = connection.CreateCommand()) {
      read.CommandText = "SELECT client_id FROM client_installation WHERE singleton = 1 LIMIT 1;";
      var existing = read.ExecuteScalar() as string;
      if (!string.IsNullOrWhiteSpace(existing)) return existing;
    }

    var clientId = Guid.NewGuid().ToString("D").ToUpperInvariant();
    using var insert = connection.CreateCommand();
    insert.CommandText = """
      INSERT OR IGNORE INTO client_installation (singleton, client_id, created_at)
      VALUES (1, $clientId, datetime('now'));
      """;
    insert.Parameters.AddWithValue("$clientId", clientId);
    insert.ExecuteNonQuery();

    using var reread = connection.CreateCommand();
    reread.CommandText = "SELECT client_id FROM client_installation WHERE singleton = 1 LIMIT 1;";
    return (string?)reread.ExecuteScalar() ?? clientId;
  }

  public void SaveTerminalTransport(string terminalId, string transportId, DateTime lastSeenAt) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      UPDATE terminals
      SET transport_id = $transportId, last_seen_at = $lastSeenAt
      WHERE terminal_id = $terminalId;
      """;
    command.Parameters.AddWithValue("$terminalId", terminalId);
    command.Parameters.AddWithValue("$transportId", transportId);
    command.Parameters.AddWithValue("$lastSeenAt", lastSeenAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    command.ExecuteNonQuery();
  }

  public void AssignTerminalToProfile(string profileId, string terminalId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      INSERT INTO profile_terminal_assignments (profile_id, terminal_id, assigned_at)
      VALUES ($profileId, $terminalId, datetime('now'))
      ON CONFLICT(profile_id) DO UPDATE SET
        terminal_id = excluded.terminal_id,
        assigned_at = excluded.assigned_at;
      """;
    command.Parameters.AddWithValue("$profileId", profileId);
    command.Parameters.AddWithValue("$terminalId", terminalId);
    command.ExecuteNonQuery();
  }

  public string? GetAssignedTerminalId(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT terminal_id FROM profile_terminal_assignments WHERE profile_id = $profileId LIMIT 1;";
    command.Parameters.AddWithValue("$profileId", profileId);
    return command.ExecuteScalar() as string;
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
    var transportId = reader.IsDBNull(2) ? null : reader.GetString(2);
    var addr = reader.IsDBNull(3) ? null : reader.GetString(3);
    var protocolVersion = reader.IsDBNull(4) ? 1 : reader.GetInt32(4);
    var claimStatus = reader.IsDBNull(5) ? "UNKNOWN" : reader.GetString(5);
    var lastSeenStr = reader.IsDBNull(6) ? null : reader.GetString(6);
    var maxLen = reader.IsDBNull(7) ? 10 : reader.GetInt32(7);

    DateTime? lastSeen = DateTime.TryParse(lastSeenStr, out var ls) ? ls : null;

    return new TerminalDeviceConfig(
      TerminalId: id,
      CustomName: name,
      BleAddress: addr,
      LastSeenAt: lastSeen,
      MaxIdLength: maxLen,
      ProtocolVersion: protocolVersion,
      ClaimStatus: claimStatus,
      TransportId: transportId
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
