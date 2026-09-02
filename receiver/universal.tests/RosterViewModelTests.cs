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
      viewModel.DeleteStudent("default", "10482");
      Assert.Equal(1, viewModel.TotalStudents);
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
}
