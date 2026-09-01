using System.Text;
using System.Text.RegularExpressions;

namespace BathroomSync.Core;

public static class RosterCsvParser {
  public static RosterPreviewData Analyze(TextReader reader, int sampleSize = 3) {
    var rawRows = ParseRawCsv(reader);
    if (rawRows.Count == 0) {
      return new RosterPreviewData(
        Headers: Array.Empty<string>(),
        SampleRows: Array.Empty<IReadOnlyList<string>>(),
        TotalRowCount: 0,
        SuggestedMapping: new RosterColumnMapping(),
        MissingRequiredFields: new[] { "Student ID", "Student Name" }
      );
    }

    var headers = rawRows[0];
    var dataRows = rawRows.Skip(1).Where(r => r.Any(cell => !string.IsNullOrWhiteSpace(cell))).ToList();
    var sampleRows = dataRows.Take(sampleSize).ToList();

    var suggestedMapping = DetectColumnMapping(headers);
    var missingFields = new List<string>();
    if (!suggestedMapping.HasIdMapping) missingFields.Add("Student ID");
    if (!suggestedMapping.HasNameMapping) missingFields.Add("Student Name (or First Name + Last Name)");

    return new RosterPreviewData(
      Headers: headers,
      SampleRows: sampleRows,
      TotalRowCount: dataRows.Count,
      SuggestedMapping: suggestedMapping,
      MissingRequiredFields: missingFields
    );
  }

  public static RosterImportResult ParseWithMapping(
    TextReader reader,
    RosterColumnMapping mapping,
    string profileId
  ) {
    var rawRows = ParseRawCsv(reader);
    if (rawRows.Count == 0) {
      return new RosterImportResult(
        TotalRows: 0,
        ImportedCount: 0,
        SkippedCount: 0,
        Errors: new[] { new RosterRowError(0, "File", "The CSV file is empty.") },
        ValidStudents: Array.Empty<RosterStudent>()
      );
    }

    var headers = rawRows[0];
    var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < headers.Count; i++) {
      var header = headers[i].Trim();
      if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header)) {
        headerMap[header] = i;
      }
    }

    int? idIdx = GetColumnIndex(mapping.StudentIdColumn, headerMap);
    int? firstIdx = GetColumnIndex(mapping.FirstNameColumn, headerMap);
    int? lastIdx = GetColumnIndex(mapping.LastNameColumn, headerMap);
    int? fullIdx = GetColumnIndex(mapping.FullNameColumn, headerMap);
    int? gradeIdx = GetColumnIndex(mapping.GradeColumn, headerMap);
    int? periodIdx = GetColumnIndex(mapping.ClassPeriodColumn, headerMap);

    if (idIdx == null) {
      return new RosterImportResult(
        TotalRows: 0,
        ImportedCount: 0,
        SkippedCount: 0,
        Errors: new[] { new RosterRowError(1, "StudentIdColumn", $"Mapped Student ID column '{mapping.StudentIdColumn}' was not found in CSV headers.") },
        ValidStudents: Array.Empty<RosterStudent>()
      );
    }

    if ((firstIdx == null || lastIdx == null) && fullIdx == null) {
      return new RosterImportResult(
        TotalRows: 0,
        ImportedCount: 0,
        SkippedCount: 0,
        Errors: new[] { new RosterRowError(1, "NameColumn", "No valid Student Name mapping found in CSV headers.") },
        ValidStudents: Array.Empty<RosterStudent>()
      );
    }

    var validStudents = new List<RosterStudent>();
    var errors = new List<RosterRowError>();
    var totalRows = 0;
    var skippedCount = 0;

    for (var rowNum = 1; rowNum < rawRows.Count; rowNum++) {
      var row = rawRows[rowNum];
      if (row.All(string.IsNullOrWhiteSpace)) {
        continue;
      }

      totalRows++;
      var fileLineNumber = rowNum + 1;

      var studentId = GetCellValue(row, idIdx.Value);
      if (string.IsNullOrWhiteSpace(studentId)) {
        errors.Add(new RosterRowError(fileLineNumber, "StudentID", "Student ID is required and cannot be empty."));
        skippedCount++;
        continue;
      }

      string firstName = "";
      string lastName = "";

      if (firstIdx != null && lastIdx != null) {
        firstName = GetCellValue(row, firstIdx.Value);
        lastName = GetCellValue(row, lastIdx.Value);
      } else if (fullIdx != null) {
        var rawName = GetCellValue(row, fullIdx.Value);
        (firstName, lastName) = SplitFullName(rawName);
      }

      if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName)) {
        errors.Add(new RosterRowError(fileLineNumber, "Name", $"Student with ID '{studentId}' has no name provided."));
        skippedCount++;
        continue;
      }

      var grade = gradeIdx != null ? GetCellValue(row, gradeIdx.Value) : null;
      var period = periodIdx != null ? GetCellValue(row, periodIdx.Value) : null;

      validStudents.Add(new RosterStudent(
        StudentId: studentId.Trim(),
        ProfileId: profileId,
        FirstName: firstName.Trim(),
        LastName: lastName.Trim(),
        Grade: string.IsNullOrWhiteSpace(grade) ? null : grade.Trim(),
        ClassPeriod: string.IsNullOrWhiteSpace(period) ? null : period.Trim()
      ));
    }

    return new RosterImportResult(
      TotalRows: totalRows,
      ImportedCount: validStudents.Count,
      SkippedCount: skippedCount,
      Errors: errors,
      ValidStudents: validStudents
    );
  }

  public static RosterImportResult ParseAuto(TextReader reader, string profileId) {
    var content = reader.ReadToEnd();
    using var analyzeReader = new StringReader(content);
    var preview = Analyze(analyzeReader);

    if (!preview.SuggestedMapping.IsValid) {
      return new RosterImportResult(
        TotalRows: preview.TotalRowCount,
        ImportedCount: 0,
        SkippedCount: preview.TotalRowCount,
        Errors: preview.MissingRequiredFields.Select(f => new RosterRowError(1, "Header", $"Missing required column mapping for '{f}'.")).ToList(),
        ValidStudents: Array.Empty<RosterStudent>()
      );
    }

    using var parseReader = new StringReader(content);
    return ParseWithMapping(parseReader, preview.SuggestedMapping, profileId);
  }

  public static RosterColumnMapping DetectColumnMapping(IReadOnlyList<string> headers) {
    string? idCol = null;
    string? firstCol = null;
    string? lastCol = null;
    string? fullCol = null;
    string? gradeCol = null;
    string? periodCol = null;

    for (var i = 0; i < headers.Count; i++) {
      var raw = headers[i].Trim();
      var norm = NormalizeHeader(raw);

      if (idCol == null && IsStudentIdHeader(norm)) {
        idCol = raw;
      } else if (firstCol == null && IsFirstNameHeader(norm)) {
        firstCol = raw;
      } else if (lastCol == null && IsLastNameHeader(norm)) {
        lastCol = raw;
      } else if (fullCol == null && IsFullNameHeader(norm)) {
        fullCol = raw;
      } else if (gradeCol == null && IsGradeHeader(norm)) {
        gradeCol = raw;
      } else if (periodCol == null && IsPeriodHeader(norm)) {
        periodCol = raw;
      }
    }

    // If we found both first and last, prefer split name over full name
    if (!string.IsNullOrEmpty(firstCol) && !string.IsNullOrEmpty(lastCol)) {
      fullCol = null;
    }

    return new RosterColumnMapping(
      StudentIdColumn: idCol,
      FirstNameColumn: firstCol,
      LastNameColumn: lastCol,
      FullNameColumn: fullCol,
      GradeColumn: gradeCol,
      ClassPeriodColumn: periodCol
    );
  }

  public static (string FirstName, string LastName) SplitFullName(string fullName) {
    if (string.IsNullOrWhiteSpace(fullName)) return ("", "");

    var trimmed = fullName.Trim();
    if (trimmed.Contains(',')) {
      var parts = trimmed.Split(',', 2);
      var last = parts[0].Trim();
      var first = parts.Length > 1 ? parts[1].Trim() : "";
      return (first, last);
    }

    var lastSpace = trimmed.LastIndexOf(' ');
    if (lastSpace > 0) {
      var first = trimmed[..lastSpace].Trim();
      var last = trimmed[(lastSpace + 1)..].Trim();
      return (first, last);
    }

    return ("", trimmed);
  }

  static int? GetColumnIndex(string? columnName, Dictionary<string, int> headerMap) {
    if (string.IsNullOrWhiteSpace(columnName)) return null;
    return headerMap.TryGetValue(columnName.Trim(), out var idx) ? idx : null;
  }

  static string GetCellValue(IReadOnlyList<string> row, int index) {
    return index >= 0 && index < row.Count ? row[index] : "";
  }

  static string NormalizeHeader(string header) {
    return Regex.Replace(header.ToLowerInvariant(), @"[^a-z0-9]", "");
  }

  static bool IsStudentIdHeader(string norm) =>
    norm is "studentid" or "id" or "studentnumber" or "studentno" or "sisid" or
            "localid" or "stateid" or "identifier" or "badgeid" or "badgenumber" or
            "usernumber" or "pupilid";

  static bool IsFirstNameHeader(string norm) =>
    norm is "firstname" or "first" or "givenname" or "fname" or "studentfirstname";

  static bool IsLastNameHeader(string norm) =>
    norm is "lastname" or "last" or "surname" or "familyname" or "lname" or "studentlastname";

  static bool IsFullNameHeader(string norm) =>
    norm is "name" or "fullname" or "studentname" or "student" or "pupilname";

  static bool IsGradeHeader(string norm) =>
    norm is "grade" or "gradelevel" or "year" or "classlevel" or "cohort";

  static bool IsPeriodHeader(string norm) =>
    norm is "period" or "classperiod" or "section" or "sectionnumber" or
            "course" or "class" or "room" or "classroom";

  public static List<List<string>> ParseRawCsv(TextReader reader) {
    var rows = new List<List<string>>();
    var currentRow = new List<string>();
    var currentField = new StringBuilder();
    var inQuotes = false;

    int nextChar;
    while ((nextChar = reader.Read()) != -1) {
      var c = (char)nextChar;

      if (c == '\uFEFF') continue; // Strip UTF-8 BOM

      if (inQuotes) {
        if (c == '"') {
          var peek = reader.Peek();
          if (peek == '"') {
            reader.Read(); // Consume escaped quote
            currentField.Append('"');
          } else {
            inQuotes = false;
          }
        } else {
          currentField.Append(c);
        }
      } else {
        if (c == '"') {
          inQuotes = true;
        } else if (c == ',') {
          currentRow.Add(currentField.ToString());
          currentField.Clear();
        } else if (c == '\r') {
          if (reader.Peek() == '\n') reader.Read();
          currentRow.Add(currentField.ToString());
          currentField.Clear();
          rows.Add(currentRow);
          currentRow = new List<string>();
        } else if (c == '\n') {
          currentRow.Add(currentField.ToString());
          currentField.Clear();
          rows.Add(currentRow);
          currentRow = new List<string>();
        } else {
          currentField.Append(c);
        }
      }
    }

    if (currentField.Length > 0 || currentRow.Count > 0) {
      currentRow.Add(currentField.ToString());
      rows.Add(currentRow);
    }

    return rows;
  }
}
