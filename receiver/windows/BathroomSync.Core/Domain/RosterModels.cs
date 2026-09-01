namespace BathroomSync.Core;

public record RosterStudent(
  string StudentId,
  string ProfileId,
  string FirstName,
  string LastName,
  string? Grade = null,
  string? ClassPeriod = null,
  DateTime? CreatedAt = null,
  DateTime? UpdatedAt = null
) {
  public string FullName => $"{FirstName} {LastName}".Trim();
}
