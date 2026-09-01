using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class RosterCsvParserTests {
  [Fact]
  public void ParsesPowerSchoolStyleCsvWithSplitNames() {
    var csv = """
      Student_Number,First_Name,Last_Name,Grade_Level,Period
      10482,Elena,Rostova,11,Period 3
      8821,Marcus,Sterling,12,Period 3
      """;

    using var reader = new StringReader(csv);
    var preview = RosterCsvParser.Analyze(reader, sampleSize: 2);

    Assert.Equal(5, preview.Headers.Count);
    Assert.Equal(2, preview.SampleRows.Count);
    Assert.Equal(2, preview.TotalRowCount);
    Assert.True(preview.SuggestedMapping.IsValid);
    Assert.Equal("Student_Number", preview.SuggestedMapping.StudentIdColumn);
    Assert.Equal("First_Name", preview.SuggestedMapping.FirstNameColumn);
    Assert.Equal("Last_Name", preview.SuggestedMapping.LastNameColumn);
    Assert.Equal("Grade_Level", preview.SuggestedMapping.GradeColumn);
    Assert.Equal("Period", preview.SuggestedMapping.ClassPeriodColumn);
    Assert.Empty(preview.MissingRequiredFields);

    using var parseReader = new StringReader(csv);
    var result = RosterCsvParser.ParseWithMapping(parseReader, preview.SuggestedMapping, "default");

    Assert.Equal(2, result.ImportedCount);
    Assert.False(result.HasErrors);
    Assert.Equal("Elena Rostova", result.ValidStudents[0].FullName);
    Assert.Equal("11", result.ValidStudents[0].Grade);
    Assert.Equal("Period 3", result.ValidStudents[0].ClassPeriod);
    Assert.Equal("Marcus Sterling", result.ValidStudents[1].FullName);
  }

  [Fact]
  public void ParsesInfiniteCampusStyleCsvWithCombinedQuotedNames() {
    var csv = """
      "SIS ID","Student Name","Grade","Section"
      "10482","Rostova, Elena","11","Chem-P3"
      "8821","Sterling, Marcus A.","12","Chem-P3"
      """;

    using var reader = new StringReader(csv);
    var preview = RosterCsvParser.Analyze(reader);

    Assert.True(preview.SuggestedMapping.IsValid);
    Assert.Equal("SIS ID", preview.SuggestedMapping.StudentIdColumn);
    Assert.Equal("Student Name", preview.SuggestedMapping.FullNameColumn);
    Assert.Equal("Grade", preview.SuggestedMapping.GradeColumn);
    Assert.Equal("Section", preview.SuggestedMapping.ClassPeriodColumn);

    using var parseReader = new StringReader(csv);
    var result = RosterCsvParser.ParseWithMapping(parseReader, preview.SuggestedMapping, "default");

    Assert.Equal(2, result.ImportedCount);
    Assert.Equal("Elena", result.ValidStudents[0].FirstName);
    Assert.Equal("Rostova", result.ValidStudents[0].LastName);
    Assert.Equal("Elena Rostova", result.ValidStudents[0].FullName);

    Assert.Equal("Marcus A.", result.ValidStudents[1].FirstName);
    Assert.Equal("Sterling", result.ValidStudents[1].LastName);
    Assert.Equal("Marcus A. Sterling", result.ValidStudents[1].FullName);
  }

  [Fact]
  public void ParsesGoogleClassroomStyleCsvWithFirstLastOrder() {
    var csv = """
      Identifier,Name,Class
      10482,Elena Rostova,Chemistry
      8821,Marcus Sterling,Chemistry
      """;

    using var reader = new StringReader(csv);
    var result = RosterCsvParser.ParseAuto(reader, "default");

    Assert.Equal(2, result.ImportedCount);
    Assert.Equal("Elena", result.ValidStudents[0].FirstName);
    Assert.Equal("Rostova", result.ValidStudents[0].LastName);
    Assert.Equal("Elena Rostova", result.ValidStudents[0].FullName);
  }

  [Fact]
  public void AllowsUserToOverrideOrConfirmCustomColumnMapping() {
    var csv = """
      CustomCode,PupilGiven,PupilFamily,YearGroup,Room
      9901,Alexander,Hamilton,10,Room 101
      """;

    using var reader = new StringReader(csv);
    var preview = RosterCsvParser.Analyze(reader);

    // Provide explicit custom mapping
    var customMapping = new RosterColumnMapping(
      StudentIdColumn: "CustomCode",
      FirstNameColumn: "PupilGiven",
      LastNameColumn: "PupilFamily",
      GradeColumn: "YearGroup",
      ClassPeriodColumn: "Room"
    );

    Assert.True(customMapping.IsValid);

    using var parseReader = new StringReader(csv);
    var result = RosterCsvParser.ParseWithMapping(parseReader, customMapping, "profile-custom");

    Assert.Equal(1, result.ImportedCount);
    Assert.Equal("9901", result.ValidStudents[0].StudentId);
    Assert.Equal("Alexander Hamilton", result.ValidStudents[0].FullName);
    Assert.Equal("10", result.ValidStudents[0].Grade);
    Assert.Equal("Room 101", result.ValidStudents[0].ClassPeriod);
  }

  [Fact]
  public void HandlesEscapedQuotesCommasAndWhitespaceInFields() {
    var csv = """
      "ID","Full Name"
      "101","Smith, ""John"" J."
      "102","O'Connor, Liam"
      """;

    using var reader = new StringReader(csv);
    var result = RosterCsvParser.ParseAuto(reader, "default");

    Assert.Equal(2, result.ImportedCount);
    Assert.Equal("\"John\" J.", result.ValidStudents[0].FirstName);
    Assert.Equal("Smith", result.ValidStudents[0].LastName);
    Assert.Equal("Liam", result.ValidStudents[1].FirstName);
    Assert.Equal("O'Connor", result.ValidStudents[1].LastName);
  }

  [Fact]
  public void ReportsErrorsForMissingIdOrMissingNameWhileImportingValidRows() {
    var csv = """
      StudentID,FirstName,LastName
      1001,John,Doe
      ,NoId,Person
      1003,,
      1004,Jane,Smith
      """;

    using var reader = new StringReader(csv);
    var result = RosterCsvParser.ParseAuto(reader, "default");

    Assert.Equal(2, result.ImportedCount);
    Assert.Equal(2, result.SkippedCount);
    Assert.Equal(4, result.TotalRows);
    Assert.Equal(2, result.Errors.Count);

    Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Field == "StudentID");
    Assert.Contains(result.Errors, e => e.RowNumber == 4 && e.Field == "Name");

    Assert.Equal("John Doe", result.ValidStudents[0].FullName);
    Assert.Equal("Jane Smith", result.ValidStudents[1].FullName);
  }

  [Fact]
  public void RejectsEmptyOrMissingRequiredHeaders() {
    var emptyCsv = "";
    using var emptyReader = new StringReader(emptyCsv);
    var emptyResult = RosterCsvParser.ParseAuto(emptyReader, "default");
    Assert.Equal(0, emptyResult.ImportedCount);
    Assert.True(emptyResult.HasErrors);

    var noIdCsv = """
      FirstName,LastName,Grade
      John,Doe,10
      """;
    using var noIdReader = new StringReader(noIdCsv);
    var noIdResult = RosterCsvParser.ParseAuto(noIdReader, "default");
    Assert.Equal(0, noIdResult.ImportedCount);
    Assert.True(noIdResult.HasErrors);
  }
}
