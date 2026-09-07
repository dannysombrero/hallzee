using Microsoft.Data.Sqlite;

namespace BathroomSync.Core;

public interface IRosterRepository {
  IReadOnlyList<RosterStudent> GetRoster(string profileId);
  void SaveStudents(string profileId, IEnumerable<RosterStudent> students);
  void DeleteStudent(string profileId, string studentId);
  void ClearRoster(string profileId);
  RosterStudent? FindStudent(string profileId, string studentId);
  int CountStudents(string profileId);
}

public sealed class RosterSqliteRepository : IRosterRepository {
  readonly string connectionString;

  public RosterSqliteRepository(string databasePath) {
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

  public IReadOnlyList<RosterStudent> GetRoster(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      SELECT r.student_id, r.profile_id, r.first_name, r.last_name, r.grade,
             COALESCE((SELECT group_concat(e.class_section, ', ') FROM roster_enrollments e WHERE e.profile_id = r.profile_id AND e.student_id = r.student_id), r.class_period),
             r.created_at, r.updated_at
      FROM roster_students r
      WHERE r.profile_id = $profileId
      ORDER BY last_name ASC, first_name ASC;
      """;
    command.Parameters.AddWithValue("$profileId", profileId);

    var list = new List<RosterStudent>();
    using var reader = command.ExecuteReader();
    while (reader.Read()) {
      list.Add(ReadStudent(reader));
    }
    return list;
  }

  public void SaveStudents(string profileId, IEnumerable<RosterStudent> students) {
    var studentList = students.ToList();
    using var connection = OpenConnection();
    using var transaction = connection.BeginTransaction();
    try {
      using var command = connection.CreateCommand();
      command.Transaction = transaction;
      command.CommandText = """
        INSERT INTO roster_students 
          (student_id, profile_id, first_name, last_name, grade, class_period, created_at, updated_at)
        VALUES 
          ($studentId, $profileId, $firstName, $lastName, $grade, $classPeriod, datetime('now'), datetime('now'))
        ON CONFLICT(student_id, profile_id) DO UPDATE SET
          first_name = excluded.first_name,
          last_name = excluded.last_name,
          grade = excluded.grade,
          class_period = excluded.class_period,
          updated_at = datetime('now');
        """;

      var pStudentId = command.Parameters.Add("$studentId", SqliteType.Text);
      var pProfileId = command.Parameters.Add("$profileId", SqliteType.Text);
      var pFirstName = command.Parameters.Add("$firstName", SqliteType.Text);
      var pLastName = command.Parameters.Add("$lastName", SqliteType.Text);
      var pGrade = command.Parameters.Add("$grade", SqliteType.Text);
      var pPeriod = command.Parameters.Add("$classPeriod", SqliteType.Text);

      using var enrollment = connection.CreateCommand();
      enrollment.Transaction = transaction;
      enrollment.CommandText = "INSERT OR IGNORE INTO roster_enrollments (profile_id, student_id, class_section) VALUES ($profileId, $studentId, $classSection);";
      var eProfile = enrollment.Parameters.Add("$profileId", SqliteType.Text);
      var eStudent = enrollment.Parameters.Add("$studentId", SqliteType.Text);
      var eSection = enrollment.Parameters.Add("$classSection", SqliteType.Text);
      eProfile.Value = profileId;

      pProfileId.Value = profileId;

      foreach (var studentId in studentList.Select(item => item.StudentId.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
        using var clearEnrollment = connection.CreateCommand();
        clearEnrollment.Transaction = transaction;
        clearEnrollment.CommandText = "DELETE FROM roster_enrollments WHERE profile_id = $profileId AND student_id = $studentId;";
        clearEnrollment.Parameters.AddWithValue("$profileId", profileId);
        clearEnrollment.Parameters.AddWithValue("$studentId", studentId);
        clearEnrollment.ExecuteNonQuery();
      }

      foreach (var s in studentList) {
        pStudentId.Value = s.StudentId.Trim();
        pFirstName.Value = s.FirstName.Trim();
        pLastName.Value = s.LastName.Trim();
        pGrade.Value = (object?)s.Grade ?? DBNull.Value;
        pPeriod.Value = (object?)s.ClassPeriod ?? DBNull.Value;
        command.ExecuteNonQuery();
        if (!string.IsNullOrWhiteSpace(s.ClassPeriod)) {
          eStudent.Value = s.StudentId.Trim();
          eSection.Value = s.ClassPeriod.Trim();
          enrollment.ExecuteNonQuery();
        }
      }

      transaction.Commit();
    } catch {
      transaction.Rollback();
      throw;
    }
  }

  public void DeleteStudent(string profileId, string studentId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM roster_students WHERE profile_id = $profileId AND student_id = $studentId;";
    command.Parameters.AddWithValue("$profileId", profileId);
    command.Parameters.AddWithValue("$studentId", studentId);
    command.ExecuteNonQuery();
  }

  public void ClearRoster(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM roster_students WHERE profile_id = $profileId;";
    command.Parameters.AddWithValue("$profileId", profileId);
    command.ExecuteNonQuery();
  }

  public RosterStudent? FindStudent(string profileId, string studentId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
      SELECT r.student_id, r.profile_id, r.first_name, r.last_name, r.grade,
             COALESCE((SELECT group_concat(e.class_section, ', ') FROM roster_enrollments e WHERE e.profile_id = r.profile_id AND e.student_id = r.student_id), r.class_period),
             r.created_at, r.updated_at
      FROM roster_students r
      WHERE r.profile_id = $profileId AND r.student_id = $studentId
      LIMIT 1;
      """;
    command.Parameters.AddWithValue("$profileId", profileId);
    command.Parameters.AddWithValue("$studentId", studentId);

    using var reader = command.ExecuteReader();
    return reader.Read() ? ReadStudent(reader) : null;
  }

  public int CountStudents(string profileId) {
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM roster_students WHERE profile_id = $profileId;";
    command.Parameters.AddWithValue("$profileId", profileId);
    return Convert.ToInt32(command.ExecuteScalar() ?? 0);
  }

  static RosterStudent ReadStudent(SqliteDataReader reader) {
    var studentId = reader.GetString(0);
    var profileId = reader.GetString(1);
    var firstName = reader.GetString(2);
    var lastName = reader.GetString(3);
    var grade = reader.IsDBNull(4) ? null : reader.GetString(4);
    var classPeriod = reader.IsDBNull(5) ? null : reader.GetString(5);
    var createdAtStr = reader.IsDBNull(6) ? null : reader.GetString(6);
    var updatedAtStr = reader.IsDBNull(7) ? null : reader.GetString(7);

    DateTime? createdAt = DateTime.TryParse(createdAtStr, out var ca) ? ca : null;
    DateTime? updatedAt = DateTime.TryParse(updatedAtStr, out var ua) ? ua : null;

    return new RosterStudent(
      StudentId: studentId,
      ProfileId: profileId,
      FirstName: firstName,
      LastName: lastName,
      Grade: grade,
      ClassPeriod: classPeriod,
      CreatedAt: createdAt,
      UpdatedAt: updatedAt
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
