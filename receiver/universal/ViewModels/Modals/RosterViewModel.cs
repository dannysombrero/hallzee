using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class RosterViewModel : INotifyPropertyChanged {
  readonly IRosterService rosterService;
  string? currentImportFilePath;
  bool isImportPreviewActive;
  bool isImportBusy;
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
  string sortColumn = "Student";
  bool sortAscending = true;


  public RosterViewModel(IRosterService rosterService) {
    this.rosterService = rosterService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<RosterStudent> Students { get; } = new();
  public ObservableCollection<string> AvailableHeaders { get; } = new();

  public string SortColumn => sortColumn;
  public bool SortAscending => sortAscending;

  public string StudentSortIndicator => sortColumn == "Student" ? (sortAscending ? " ▲" : " ▼") : "";
  public string GradeSortIndicator => sortColumn == "Grade" ? (sortAscending ? " ▲" : " ▼") : "";
  public string PeriodSortIndicator => sortColumn == "Period" ? (sortAscending ? " ▲" : " ▼") : "";

  public void ToggleSort(string column) {
    if (sortColumn == column) {
      sortAscending = !sortAscending;
    } else {
      sortColumn = column;
      sortAscending = true;
    }
    ApplySort();
    OnPropertyChanged(nameof(StudentSortIndicator));
    OnPropertyChanged(nameof(GradeSortIndicator));
    OnPropertyChanged(nameof(PeriodSortIndicator));
  }

  void ApplySort() {
    var items = Students.ToList();
    IEnumerable<RosterStudent> sorted = sortColumn switch {
      "Grade" => sortAscending ? items.OrderBy(s => s.Grade).ThenBy(s => s.FullName) : items.OrderByDescending(s => s.Grade).ThenBy(s => s.FullName),
      "Period" => sortAscending ? items.OrderBy(s => s.ClassPeriod).ThenBy(s => s.FullName) : items.OrderByDescending(s => s.ClassPeriod).ThenBy(s => s.FullName),
      _ => sortAscending ? items.OrderBy(s => s.FullName).ThenBy(s => s.StudentId) : items.OrderByDescending(s => s.FullName).ThenByDescending(s => s.StudentId)
    };
    Students.Clear();
    foreach (var s in sorted) Students.Add(s);
  }

  public int TotalStudents {
    get => totalStudents;
    private set {
      totalStudents = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasStudents));
      OnPropertyChanged(nameof(HasNoStudents));
    }
  }

  public bool HasStudents => TotalStudents > 0;
  public bool HasNoStudents => TotalStudents == 0;

  public bool IsImportPreviewActive {
    get => isImportPreviewActive;
    private set { isImportPreviewActive = value; OnPropertyChanged(); }
  }

  public bool IsImportBusy {
    get => isImportBusy;
    private set { isImportBusy = value; OnPropertyChanged(); }
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
    ApplySort();
    TotalStudents = Students.Count;
  }

  public void SetErrorMessage(string message) {
    ImportStatusMessage = message;
    ImportStatusColor = "#EF4444";
  }

  public async Task<bool> StartCsvImportAsync(string filePath) {
    if (!File.Exists(filePath)) {
      ImportStatusMessage = $"File not found: {filePath}";
      ImportStatusColor = "#EF4444";
      return false;
    }

    var extension = Path.GetExtension(filePath);
    if (extension.Equals(".numbers", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)) {
      ImportStatusMessage = "This is a spreadsheet workbook, not a CSV. In Numbers, choose File → Export To → CSV, then import the exported .csv file.";
      ImportStatusColor = "#EF4444";
      return false;
    }

    currentImportFilePath = filePath;
    IsImportBusy = true;
    ImportStatusMessage = "Analyzing CSV file...";
    ImportStatusColor = "#0284C7";

    try {
      var preview = await Task.Run(() => {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return rosterService.PreviewRosterCsv(reader, sampleSize: 3);
      });

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
    } catch (Exception ex) {
      ImportStatusMessage = $"Failed to read CSV: {ex.Message}";
      ImportStatusColor = "#EF4444";
      return false;
    } finally {
      IsImportBusy = false;
    }
  }

  public bool StartCsvImport(string filePath) {
    return StartCsvImportAsync(filePath).GetAwaiter().GetResult();
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

    try {
      using var stream = new FileStream(currentImportFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var reader = new StreamReader(stream);
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
    } catch (Exception ex) {
      ImportStatusMessage = $"Import failed: {ex.Message}";
      ImportStatusColor = "#EF4444";
      return null;
    }
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
