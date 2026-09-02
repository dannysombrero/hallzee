using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class TripsViewModel : INotifyPropertyChanged {
  readonly TripSqliteRepository tripRepository;
  string searchText = "";
  string selectedStatus = "ALL";
  int totalTrips;
  string? exportStatusMessage;
  string sortColumn = "Date";
  bool sortAscending = false;

  public TripsViewModel(TripSqliteRepository tripRepository, IRosterService rosterService) {
    this.tripRepository = tripRepository;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public string SortColumn => sortColumn;
  public bool SortAscending => sortAscending;

  public string StudentSortIndicator => sortColumn == "Student" ? (sortAscending ? " ▲" : " ▼") : "";
  public string DateSortIndicator => sortColumn == "Date" ? (sortAscending ? " ▲" : " ▼") : "";
  public string TimeOutSortIndicator => sortColumn == "TimeOut" ? (sortAscending ? " ▲" : " ▼") : "";
  public string TimeInSortIndicator => sortColumn == "TimeIn" ? (sortAscending ? " ▲" : " ▼") : "";
  public string DurationSortIndicator => sortColumn == "Duration" ? (sortAscending ? " ▲" : " ▼") : "";
  public string StatusSortIndicator => sortColumn == "Status" ? (sortAscending ? " ▲" : " ▼") : "";

  public void ToggleSort(string column) {
    if (sortColumn == column) {
      sortAscending = !sortAscending;
    } else {
      sortColumn = column;
      sortAscending = column switch {
        "Student" => true,
        "Status" => true,
        _ => false
      };
    }
    ApplySort();
    NotifySortIndicators();
  }

  void NotifySortIndicators() {
    OnPropertyChanged(nameof(StudentSortIndicator));
    OnPropertyChanged(nameof(DateSortIndicator));
    OnPropertyChanged(nameof(TimeOutSortIndicator));
    OnPropertyChanged(nameof(TimeInSortIndicator));
    OnPropertyChanged(nameof(DurationSortIndicator));
    OnPropertyChanged(nameof(StatusSortIndicator));
  }

  void ApplySort() {
    var items = Trips.ToList();
    IEnumerable<EnrichedTripRecord> sorted = sortColumn switch {
      "Student" => sortAscending ? items.OrderBy(t => t.DisplayName).ThenBy(t => t.StudentId) : items.OrderByDescending(t => t.DisplayName).ThenByDescending(t => t.StudentId),
      "Date" => sortAscending ? items.OrderBy(t => t.TripDate).ThenBy(t => t.TimeOut) : items.OrderByDescending(t => t.TripDate).ThenByDescending(t => t.TimeOut),
      "TimeOut" => sortAscending ? items.OrderBy(t => t.TimeOut) : items.OrderByDescending(t => t.TimeOut),
      "TimeIn" => sortAscending ? items.OrderBy(t => t.TimeIn) : items.OrderByDescending(t => t.TimeIn),
      "Duration" => sortAscending ? items.OrderBy(t => t.DurationSeconds) : items.OrderByDescending(t => t.DurationSeconds),
      "Status" => sortAscending ? items.OrderBy(t => t.Status) : items.OrderByDescending(t => t.Status),
      _ => items
    };
    Trips.Clear();
    foreach (var t in sorted) Trips.Add(t);
  }

  public ObservableCollection<EnrichedTripRecord> Trips { get; } = new();
  public IReadOnlyList<string> StatusOptions { get; } = new[] {
    "ALL", "COMPLETED", "MANUAL_RESET"
  };

  public string SearchText {
    get => searchText;
    set {
      if (searchText != value) {
        searchText = value;
        OnPropertyChanged();
      }
    }
  }

  public string SelectedStatus {
    get => selectedStatus;
    set {
      if (selectedStatus != value) {
        selectedStatus = value;
        OnPropertyChanged();
      }
    }
  }

  public int TotalTrips {
    get => totalTrips;
    private set {
      totalTrips = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasTrips));
      OnPropertyChanged(nameof(HasNoTrips));
    }
  }

  public bool HasTrips => TotalTrips > 0;
  public bool HasNoTrips => TotalTrips == 0;

  public string? ExportStatusMessage {
    get => exportStatusMessage;
    private set { exportStatusMessage = value; OnPropertyChanged(); }
  }

  public void Refresh(string profileId) {
    var filter = new TripQueryFilter(
      SearchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
      Status: SelectedStatus == "ALL" ? null : SelectedStatus,
      ProfileId: profileId,
      Limit: 100
    );

    var rawTrips = tripRepository.QueryTrips(filter);

    Trips.Clear();
    foreach (var trip in rawTrips) {
      Trips.Add(trip);
    }
    ApplySort();
    TotalTrips = tripRepository.CountTrips(filter);
  }

  public string ExportCsv(string profileId, string exportPath) {
    var directory = Path.GetDirectoryName(exportPath);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    var filter = new TripQueryFilter(ProfileId: profileId, Limit: 100000);
    var rawTrips = tripRepository.QueryTrips(filter);

    using var writer = new StreamWriter(exportPath);
    writer.WriteLine("trip_id,student_id,student_name,grade,class_period,date,time_out,time_in,duration_seconds,status,synced_at,terminal_id");
    foreach (var t in rawTrips) {
      writer.WriteLine($"{t.TripId},{EscapeCsv(t.StudentId)},{EscapeCsv(t.DisplayName)},{EscapeCsv(t.Grade ?? "")},{EscapeCsv(t.ClassPeriod ?? "")},{t.TripDate},{t.TimeOut},{t.TimeIn},{t.DurationSeconds},{t.Status},{t.SyncedAt:yyyy-MM-dd HH:mm:ss},{EscapeCsv(t.TerminalId)}");
    }
    ExportStatusMessage = $"Exported {rawTrips.Count} trip record(s) to Downloads ({Path.GetFileName(exportPath)})";
    return exportPath;
  }

  static string EscapeCsv(string value) {
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')) {
      return $"\"{value.Replace("\"", "\"\"")}\"";
    }
    return value;
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
