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

public record RosterColumnMapping(
  string? StudentIdColumn = null,
  string? FirstNameColumn = null,
  string? LastNameColumn = null,
  string? FullNameColumn = null,
  string? GradeColumn = null,
  string? ClassPeriodColumn = null
) {
  public bool HasIdMapping => !string.IsNullOrWhiteSpace(StudentIdColumn);
  public bool HasSplitNameMapping => !string.IsNullOrWhiteSpace(FirstNameColumn) && !string.IsNullOrWhiteSpace(LastNameColumn);
  public bool HasFullNameMapping => !string.IsNullOrWhiteSpace(FullNameColumn);
  public bool HasNameMapping => HasSplitNameMapping || HasFullNameMapping;
  public bool IsValid => HasIdMapping && HasNameMapping;
}

public record RosterPreviewData(
  IReadOnlyList<string> Headers,
  IReadOnlyList<IReadOnlyList<string>> SampleRows,
  int TotalRowCount,
  RosterColumnMapping SuggestedMapping,
  IReadOnlyList<string> MissingRequiredFields
);

public record RosterRowError(
  int RowNumber,
  string Field,
  string ErrorMessage,
  string? RawLine = null
);

public record RosterImportResult(
  int TotalRows,
  int ImportedCount,
  int SkippedCount,
  IReadOnlyList<RosterRowError> Errors,
  IReadOnlyList<RosterStudent> ValidStudents
) {
  public bool HasErrors => Errors.Count > 0;
  public bool Success => ImportedCount > 0 && Errors.Count == 0;
}
