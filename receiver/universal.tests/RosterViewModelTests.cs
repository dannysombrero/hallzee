using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class RosterViewModelTests : IDisposable {
  readonly string tempDbPath;
  readonly RosterSqliteRepository rosterRepository;
  readonly ProfileAndPolicySqliteRepository profileRepository;
  readonly RosterService rosterService;
  readonly RosterViewModel viewModel;

  public RosterViewModelTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeeRosterVmTests", $"{Guid.NewGuid():N}.db");
    profileRepository = new ProfileAndPolicySqliteRepository(tempDbPath);
    rosterRepository = new RosterSqliteRepository(tempDbPath);
    rosterService = new RosterService(rosterRepository);
    viewModel = new RosterViewModel(rosterService);
  }

  public void Dispose() {
    try {
      if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
      var dir = Path.GetDirectoryName(tempDbPath);
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    } catch { }
  }

  [Fact]
  public void ManualAddPreservesLeadingZerosAndRejectsDuplicateOrInvalidIds() {
    viewModel.NewStudentId = " 001234 ";
    viewModel.NewFirstName = " Avery ";
    viewModel.NewLastName = "Chen";
    viewModel.NewGrade = "7";
    viewModel.NewPeriod = "Period 2";
    Assert.True(viewModel.AddStudent("default"));
    var student = Assert.Single(rosterService.GetRoster("default"));
    Assert.Equal("001234", student.StudentId);
    Assert.Equal("Avery Chen", student.FullName);
    Assert.Equal("Period 2", student.ClassPeriod);
    viewModel.NewStudentId = "001234";
    viewModel.NewFirstName = "Different";
    Assert.False(viewModel.AddStudent("default"));
    Assert.Equal("Avery Chen", rosterService.LookupStudent("default", "001234")!.FullName);
    viewModel.NewStudentId = "12,34";
    Assert.False(viewModel.AddStudent("default"));
    viewModel.NewStudentId = "9999";
    viewModel.NewFirstName = "";
    Assert.False(viewModel.AddStudent("default"));
    Assert.Single(rosterService.GetRoster("default"));
  }

  [Fact]
  public void TwoPhaseImportFlowPreviewsAndImportsSuccessfully() {
    var csvPath = Path.Combine(Path.GetDirectoryName(tempDbPath)!, "test_roster.csv");
    File.WriteAllText(csvPath, """
      "Student ID","First Name","Last Name","Grade","Class"
      "10482","Elena","Rostova","11","Chemistry"
      "8821","Marcus","Sterling","12","Chemistry"
      """);

    try {
      // Phase 1: Preview CSV
      var previewStarted = viewModel.StartCsvImport(csvPath);
      Assert.True(previewStarted);
      Assert.True(viewModel.IsImportPreviewActive);
      Assert.NotNull(viewModel.PreviewData);
      Assert.Equal(2, viewModel.PreviewData.SampleRows.Count);
      Assert.Equal("Student ID", viewModel.SelectedIdColumn);
      Assert.True(viewModel.CanConfirmImport);

      // Phase 2: Confirm Import
      var result = viewModel.ConfirmImport("default");
      Assert.NotNull(result);
      Assert.Equal(2, result.ImportedCount);
      Assert.False(viewModel.IsImportPreviewActive);
      Assert.Equal(2, viewModel.TotalStudents);
      Assert.Equal(2, viewModel.Students.Count);
      Assert.Equal("Elena Rostova", viewModel.Students[0].FullName);

      // Delete student
      Assert.True(viewModel.HasStudents);
      Assert.False(viewModel.HasNoStudents);
      viewModel.DeleteStudent("default", "10482");
      Assert.Equal(1, viewModel.TotalStudents);
      Assert.True(viewModel.HasStudents);
      Assert.False(viewModel.HasNoStudents);
      viewModel.DeleteStudent("default", "8821");
      Assert.Equal(0, viewModel.TotalStudents);
      Assert.False(viewModel.HasStudents);
      Assert.True(viewModel.HasNoStudents);
    } finally {
      if (File.Exists(csvPath)) File.Delete(csvPath);
    }
  }

  [Fact]
  public void CancelImportClearsPreviewState() {
    var csvPath = Path.Combine(Path.GetDirectoryName(tempDbPath)!, "test_cancel.csv");
    File.WriteAllText(csvPath, "ID,Name\n101,Test Student");

    try {
      viewModel.StartCsvImport(csvPath);
      Assert.True(viewModel.IsImportPreviewActive);

      viewModel.CancelImport();
      Assert.False(viewModel.IsImportPreviewActive);
      Assert.Null(viewModel.PreviewData);
    } finally {
      if (File.Exists(csvPath)) File.Delete(csvPath);
    }
  }

  [Fact]
  public void RejectsNumbersWorkbookWithExportInstructions() {
    var numbersPath = Path.Combine(Path.GetDirectoryName(tempDbPath)!, "roster.numbers");
    File.WriteAllText(numbersPath, "not CSV data");

    try {
      Assert.False(viewModel.StartCsvImport(numbersPath));
      Assert.False(viewModel.IsImportPreviewActive);
      Assert.Contains("Export To → CSV", viewModel.ImportStatusMessage);
    } finally {
      if (File.Exists(numbersPath)) File.Delete(numbersPath);
    }
  }

  [Fact]
  public void RosterSortingTogglesOrderCorrectly() {
    profileRepository.SaveProfile(new ClassroomProfile("prof-sort", "Sorting Class"));
    rosterRepository.SaveStudents("prof-sort", new[] {
      new RosterStudent("101", "prof-sort", "Zara", "Young", "12", "Period 3"),
      new RosterStudent("102", "prof-sort", "Aaron", "Blake", "9", "Period 1"),
      new RosterStudent("103", "prof-sort", "Chloe", "Adams", "11", "Period 2")
    });

    viewModel.Refresh("prof-sort");
    Assert.Equal(3, viewModel.Students.Count);

    // Default: Student name Ascending
    Assert.Equal("Aaron Blake", viewModel.Students[0].FullName);
    Assert.Equal("Zara Young", viewModel.Students[2].FullName);

    // Toggle to Student name Descending
    viewModel.ToggleSort("Student");
    Assert.Equal("Zara Young", viewModel.Students[0].FullName);
    Assert.Equal("Aaron Blake", viewModel.Students[2].FullName);

    // Sort by Grade Ascending
    viewModel.ToggleSort("Grade");
    Assert.Equal("11", viewModel.Students[0].Grade); // Grade sorting: "11", "12", "9" alphabetically or "9"
    Assert.Equal(" ▲", viewModel.GradeSortIndicator);

    // Sort by Period Ascending
    viewModel.ToggleSort("Period");
    Assert.Equal("Period 1", viewModel.Students[0].ClassPeriod);
    Assert.Equal(" ▲", viewModel.PeriodSortIndicator);
  }
}
