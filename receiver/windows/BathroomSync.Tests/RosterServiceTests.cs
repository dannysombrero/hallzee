using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class RosterServiceTests : IDisposable {
  readonly string tempDbPath;
  readonly RosterSqliteRepository repository;
  readonly ProfileAndPolicySqliteRepository profileRepository;
  readonly RosterService service;

  public RosterServiceTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeeRosterServiceTests", $"{Guid.NewGuid():N}.db");
    profileRepository = new ProfileAndPolicySqliteRepository(tempDbPath);
    profileRepository.SaveProfile(new ClassroomProfile("chem-profile", "Chemistry Period 3"));
    repository = new RosterSqliteRepository(tempDbPath);
    service = new RosterService(repository);
  }

  public void Dispose() {
    try {
      if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
      var dir = Path.GetDirectoryName(tempDbPath);
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    } catch { }
  }

  [Fact]
  public void EndToEndPreviewAndImportFlow() {
    var csv = """
      "Student ID","First Name","Last Name","Grade","Class"
      "10482","Elena","Rostova","11","Chemistry"
      "8821","Marcus","Sterling","12","Chemistry"
      """;

    using var previewReader = new StringReader(csv);
    var preview = service.PreviewRosterCsv(previewReader, sampleSize: 2);

    Assert.Equal(2, preview.SampleRows.Count);
    Assert.Equal("Student ID", preview.SuggestedMapping.StudentIdColumn);

    using var importReader = new StringReader(csv);
    var result = service.ImportRoster("chem-profile", importReader, preview.SuggestedMapping);

    Assert.Equal(2, result.ImportedCount);
    Assert.Equal(2, service.CountStudents("chem-profile"));

    var student = service.LookupStudent("chem-profile", "10482");
    Assert.NotNull(student);
    Assert.Equal("Elena Rostova", student.FullName);
    Assert.Equal("11", student.Grade);
    Assert.Equal("Chemistry", student.ClassPeriod);
  }

  [Fact]
  public void ClearExistingOptionWipesPreviousRosterOnImport() {
    var initialCsv = """
      ID,Name
      1001,Old Student
      """;
    using var initialReader = new StringReader(initialCsv);
    service.ImportRosterAuto("default", initialReader);
    Assert.Equal(1, service.CountStudents("default"));

    var newCsv = """
      ID,Name
      2001,New Student
      2002,Another Student
      """;
    using var newReader = new StringReader(newCsv);
    service.ImportRosterAuto("default", newReader, clearExisting: true);

    Assert.Equal(2, service.CountStudents("default"));
    Assert.Null(service.LookupStudent("default", "1001"));
    Assert.NotNull(service.LookupStudent("default", "2001"));
  }

  [Fact]
  public void EnrichesTripRecordsWithRosterNamesAndFallsBackSafely() {
    var rosterCsv = """
      ID,FirstName,LastName,Grade,Period
      10482,Elena,Rostova,11,P3
      """;
    using var reader = new StringReader(rosterCsv);
    service.ImportRosterAuto("default", reader);

    var trips = new[] {
      new TripRecord(
        TripId: 1,
        StudentId: "10482",
        TripDate: "2026-09-01",
        TimeOut: "09:00:00",
        TimeIn: "09:07:00",
        DurationSeconds: 420,
        Status: "COMPLETED",
        SyncedAt: DateTime.UtcNow,
        TerminalId: "Hallzee-East"
      ),
      new TripRecord(
        TripId: 2,
        StudentId: "99999", // Unmatched in roster
        TripDate: "2026-09-01",
        TimeOut: "10:00:00",
        TimeIn: "10:05:00",
        DurationSeconds: 300,
        Status: "COMPLETED",
        SyncedAt: DateTime.UtcNow,
        TerminalId: "Hallzee-East"
      )
    };

    var enriched = service.EnrichTrips("default", trips);

    Assert.Equal(2, enriched.Count);

    // Matched student
    Assert.Equal("Elena Rostova", enriched[0].FullName);
    Assert.Equal("Elena Rostova", enriched[0].DisplayName);
    Assert.Equal("11", enriched[0].Grade);
    Assert.Equal("P3", enriched[0].ClassPeriod);

    // Unmatched student
    Assert.Null(enriched[1].FullName);
    Assert.Equal("#99999", enriched[1].DisplayName); // Safe fallback
    Assert.Null(enriched[1].Grade);
    Assert.Null(enriched[1].ClassPeriod);
  }

  [Fact]
  public void ImportFromFileHandlesMissingFileGracefully() {
    var result = service.ImportRosterFromFile("default", "/non/existent/file.csv");
    Assert.Equal(0, result.ImportedCount);
    Assert.True(result.HasErrors);
    Assert.Contains("File not found", result.Errors[0].ErrorMessage);
  }

  [Fact]
  public void ImportFromFileSucceedsWhenFileIsConcurrentlyOpen() {
    var tempCsv = Path.Combine(Path.GetTempPath(), $"hallzee_concurrent_{Guid.NewGuid():N}.csv");
    try {
      File.WriteAllText(tempCsv, "ID,Name\n4401,Open File Student\n");

      // Simulate Excel or another viewer having the file open with FileShare.ReadWrite
      using var concurrentStream = new FileStream(tempCsv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

      var result = service.ImportRosterFromFile("default", tempCsv);
      Assert.Equal(1, result.ImportedCount);
      Assert.NotNull(service.LookupStudent("default", "4401"));
    } finally {
      try { if (File.Exists(tempCsv)) File.Delete(tempCsv); } catch { }
    }
  }
}

