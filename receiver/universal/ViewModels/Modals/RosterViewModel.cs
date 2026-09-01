using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class RosterViewModel : INotifyPropertyChanged {
  readonly IRosterService rosterService;
  string? currentImportFilePath;
  bool isImportPreviewActive;
  RosterPreviewData? previewData;
  string? selectedIdColumn;
  string? selectedFirstNameColumn;
  string? selectedLastNameColumn;
  string? selectedFullNameColumn;
  string? selectedGradeColumn;
  string? selectedPeriodColumn;
  string? importStatusMessage;
  string? importStatusColor;
  int totalStudents;

  public RosterViewModel(IRosterService rosterService) {
    this.rosterService = rosterService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<RosterStudent> Students { get; } = new();
  public ObservableCollection<string> AvailableHeaders { get; } = new();

  public int TotalStudents {
    get => totalStudents;
    private set { totalStudents = value; OnPropertyChanged(); }
  }

  public bool IsImportPreviewActive {
    get => isImportPreviewActive;
    private set { isImportPreviewActive = value; OnPropertyChanged(); }
  }

  public RosterPreviewData? PreviewData {
    get => previewData;
    private set { previewData = value; OnPropertyChanged(); }
  }

  public string? SelectedIdColumn {
    get => selectedIdColumn;
    set { selectedIdColumn = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfirmImport)); }
  }

  public string? SelectedFirstNameColumn {
    get => selectedFirstNameColumn;
    set { selectedFirstNameColumn = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfirmImport)); }
  }

  public string? SelectedLastNameColumn {
    get => selectedLastNameColumn;
    set { selectedLastNameColumn = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfirmImport)); }
  }

  public string? SelectedFullNameColumn {
    get => selectedFullNameColumn;
    set { selectedFullNameColumn = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfirmImport)); }
  }

  public string? SelectedGradeColumn {
    get => selectedGradeColumn;
    set { selectedGradeColumn = value; OnPropertyChanged(); }
  }

  public string? SelectedPeriodColumn {
    get => selectedPeriodColumn;
    set { selectedPeriodColumn = value; OnPropertyChanged(); }
  }

  public string? ImportStatusMessage {
    get => importStatusMessage;
    private set { importStatusMessage = value; OnPropertyChanged(); }
  }

  public string? ImportStatusColor {
    get => importStatusColor;
    private set { importStatusColor = value; OnPropertyChanged(); }
  }

  public bool CanConfirmImport {
    get {
      var hasId = !string.IsNullOrWhiteSpace(SelectedIdColumn);
      var hasSplitName = !string.IsNullOrWhiteSpace(SelectedFirstNameColumn) && !string.IsNullOrWhiteSpace(SelectedLastNameColumn);
      var hasFullName = !string.IsNullOrWhiteSpace(SelectedFullNameColumn);
      return hasId && (hasSplitName || hasFullName);
    }
  }

  public void Refresh(string profileId) {
    var list = rosterService.GetRoster(profileId);
    Students.Clear();
    foreach (var student in list) {
      Students.Add(student);
    }
    TotalStudents = Students.Count;
  }

  public bool StartCsvImport(string filePath) {
    if (!File.Exists(filePath)) {
      ImportStatusMessage = $"File not found: {filePath}";
      ImportStatusColor = "#EF4444";
      return false;
    }

    currentImportFilePath = filePath;
    using var reader = new StreamReader(filePath);
    var preview = rosterService.PreviewRosterCsv(reader, sampleSize: 3);

    if (preview.Headers.Count == 0 || preview.TotalRowCount == 0) {
      ImportStatusMessage = "CSV file is empty or contains no valid rows.";
      ImportStatusColor = "#EF4444";
      return false;
    }

    PreviewData = preview;
    AvailableHeaders.Clear();
    AvailableHeaders.Add("(None)");
    foreach (var h in preview.Headers) AvailableHeaders.Add(h);

    SelectedIdColumn = preview.SuggestedMapping.StudentIdColumn;
    SelectedFirstNameColumn = preview.SuggestedMapping.FirstNameColumn;
    SelectedLastNameColumn = preview.SuggestedMapping.LastNameColumn;
    SelectedFullNameColumn = preview.SuggestedMapping.FullNameColumn;
    SelectedGradeColumn = preview.SuggestedMapping.GradeColumn;
    SelectedPeriodColumn = preview.SuggestedMapping.ClassPeriodColumn;

    IsImportPreviewActive = true;
    ImportStatusMessage = $"Found {preview.TotalRowCount} student rows. Verify column bindings below.";
    ImportStatusColor = "#0284C7";
    return true;
  }

  public RosterImportResult? ConfirmImport(string profileId, bool clearExisting = false) {
    if (string.IsNullOrEmpty(currentImportFilePath) || !File.Exists(currentImportFilePath)) {
      ImportStatusMessage = "No CSV file selected.";
      ImportStatusColor = "#EF4444";
      return null;
    }

    var mapping = new RosterColumnMapping(
      StudentIdColumn: SelectedIdColumn == "(None)" ? null : SelectedIdColumn,
      FirstNameColumn: SelectedFirstNameColumn == "(None)" ? null : SelectedFirstNameColumn,
      LastNameColumn: SelectedLastNameColumn == "(None)" ? null : SelectedLastNameColumn,
      FullNameColumn: SelectedFullNameColumn == "(None)" ? null : SelectedFullNameColumn,
      GradeColumn: SelectedGradeColumn == "(None)" ? null : SelectedGradeColumn,
      ClassPeriodColumn: SelectedPeriodColumn == "(None)" ? null : SelectedPeriodColumn
    );

    using var reader = new StreamReader(currentImportFilePath);
    var result = rosterService.ImportRoster(profileId, reader, mapping, clearExisting);

    if (result.Success || result.ImportedCount > 0) {
      ImportStatusMessage = $"Successfully imported {result.ImportedCount} students. ({result.SkippedCount} skipped)";
      ImportStatusColor = "#10B981";
      IsImportPreviewActive = false;
      currentImportFilePath = null;
      Refresh(profileId);
    } else {
      ImportStatusMessage = $"Import failed: {result.Errors.FirstOrDefault()?.ErrorMessage ?? "Unknown error"}";
      ImportStatusColor = "#EF4444";
    }

    return result;
  }

  public void CancelImport() {
    IsImportPreviewActive = false;
    currentImportFilePath = null;
    PreviewData = null;
    ImportStatusMessage = null;
  }

  public void DeleteStudent(string profileId, string studentId) {
    rosterService.DeleteStudent(profileId, studentId);
    Refresh(profileId);
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
