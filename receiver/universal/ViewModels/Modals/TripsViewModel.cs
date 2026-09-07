using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class TripsViewModel : INotifyPropertyChanged {
  readonly TripSqliteRepository tripRepository;
  string searchText = "";
  string selectedStatus = "All";
  int totalTrips;
  string? exportStatusMessage;
  string? activeProfileId;
  string sortColumn = "Date";
  bool sortAscending = false;

  public TripsViewModel(TripSqliteRepository tripRepository, IRosterService rosterService) {
    this.tripRepository = tripRepository;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public string SortColumn => sortColumn;
  public bool SortAscending => sortAscending;

  string GetIndicator(string col) {
    bool isMatch = sortColumn == col
      || (col == "StudentName" && sortColumn == "Student")
      || (col == "Student" && sortColumn == "StudentName")
      || (col == "Departed" && sortColumn == "TimeOut")
      || (col == "Returned" && sortColumn == "TimeIn");
    return isMatch ? (sortAscending ? " ▲" : " ▼") : " ⇅";
  }

  public string TripIdSortIndicator => GetIndicator("TripId");
  public string StudentIdSortIndicator => GetIndicator("StudentId");
  public string StudentNameSortIndicator => GetIndicator("StudentName");
  public string DepartedSortIndicator => GetIndicator("Departed");
  public string ReturnedSortIndicator => GetIndicator("Returned");
  public string DurationSortIndicator => GetIndicator("Duration");
  public string StatusSortIndicator => GetIndicator("Status");

  // Backwards compatibility indicators
  public string StudentSortIndicator => GetIndicator("Student");
  public string DateSortIndicator => GetIndicator("Date");
  public string TimeOutSortIndicator => GetIndicator("TimeOut");
  public string TimeInSortIndicator => GetIndicator("TimeIn");

  public void ToggleSort(string column) {
    if (sortColumn == column) {
      sortAscending = !sortAscending;
    } else {
      sortColumn = column;
      sortAscending = column switch {
        "StudentId" => true,
        "StudentName" => true,
        "Student" => true,
        "Status" => true,
        _ => false
      };
    }
    ApplySort();
    NotifySortIndicators();
  }

  void NotifySortIndicators() {
    OnPropertyChanged(nameof(TripIdSortIndicator));
    OnPropertyChanged(nameof(StudentIdSortIndicator));
    OnPropertyChanged(nameof(StudentNameSortIndicator));
    OnPropertyChanged(nameof(DepartedSortIndicator));
    OnPropertyChanged(nameof(ReturnedSortIndicator));
    OnPropertyChanged(nameof(DurationSortIndicator));
    OnPropertyChanged(nameof(StatusSortIndicator));
    OnPropertyChanged(nameof(StudentSortIndicator));
    OnPropertyChanged(nameof(DateSortIndicator));
    OnPropertyChanged(nameof(TimeOutSortIndicator));
    OnPropertyChanged(nameof(TimeInSortIndicator));
  }

  void ApplySort() {
    var items = Trips.ToList();
    IEnumerable<EnrichedTripRecord> sorted = sortColumn switch {
      "TripId" => sortAscending ? items.OrderBy(t => t.TripId) : items.OrderByDescending(t => t.TripId),
      "StudentId" => sortAscending ? items.OrderBy(t => t.StudentId) : items.OrderByDescending(t => t.StudentId),
      "StudentName" or "Student" => sortAscending ? items.OrderBy(t => t.DisplayName).ThenBy(t => t.StudentId) : items.OrderByDescending(t => t.DisplayName).ThenByDescending(t => t.StudentId),
      "Date" => sortAscending ? items.OrderBy(t => t.TripDate).ThenBy(t => t.TimeOut) : items.OrderByDescending(t => t.TripDate).ThenByDescending(t => t.TimeOut),
      "Departed" or "TimeOut" => sortAscending ? items.OrderBy(t => t.TimeOut) : items.OrderByDescending(t => t.TimeOut),
      "Returned" or "TimeIn" => sortAscending ? items.OrderBy(t => t.TimeIn) : items.OrderByDescending(t => t.TimeIn),
      "Duration" => sortAscending ? items.OrderBy(t => t.DurationSeconds) : items.OrderByDescending(t => t.DurationSeconds),
      "Status" => sortAscending ? items.OrderBy(t => t.Status) : items.OrderByDescending(t => t.Status),
      _ => items
    };
    Trips.Clear();
    foreach (var t in sorted) Trips.Add(t);
  }

  public ObservableCollection<EnrichedTripRecord> Trips { get; } = new();
  string selectedTimeframe = "All Time";
  string selectedDuration = "All Durations";

  public IReadOnlyList<string> TimeframeOptions { get; } = new[] {
    "All Time", "Today", "This Week", "This Month"
  };

  public IReadOnlyList<string> DurationOptions { get; } = new[] {
    "All Durations", "Under 5m", "5m – 10m", "Over 10m"
  };

  public IReadOnlyList<string> StatusOptions { get; } = new[] {
    "All Statuses", "Completed", "Manual"
  };

  public string SearchText {
    get => searchText;
    set {
      if (searchText != value) {
        searchText = value;
        OnPropertyChanged();
        RefreshIfLoaded();
      }
    }
  }

  public string SelectedTimeframe {
    get => selectedTimeframe;
    set {
      if (selectedTimeframe != value) {
        selectedTimeframe = value;
        OnPropertyChanged();
        RefreshIfLoaded();
      }
    }
  }

  public string SelectedDuration {
    get => selectedDuration;
    set {
      if (selectedDuration != value) {
        selectedDuration = value;
        OnPropertyChanged();
        RefreshIfLoaded();
      }
    }
  }

  public string SelectedStatus {
    get => selectedStatus;
    set {
      if (selectedStatus != value) {
        selectedStatus = value;
        OnPropertyChanged();
        RefreshIfLoaded();
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
    activeProfileId = profileId;

    var now = DateTime.Now;
    string? startDate = null;
    string? endDate = null;

    if (SelectedTimeframe == "Today") {
      startDate = now.ToString("yyyy-MM-dd");
      endDate = startDate;
    } else if (SelectedTimeframe == "This Week") {
      var daysFromMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
      startDate = now.Date.AddDays(-daysFromMonday).ToString("yyyy-MM-dd");
      endDate = now.ToString("yyyy-MM-dd");
    } else if (SelectedTimeframe == "This Month") {
      startDate = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
      endDate = now.ToString("yyyy-MM-dd");
    }

    var statusFilter = SelectedStatus switch {
      "Completed" or "COMPLETED" => "COMPLETED",
      "Manual" or "MANUAL" or "MANUAL_RESET" => "MANUAL",
      _ => null
    };

    var filter = new TripQueryFilter(
      SearchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
      Status: statusFilter,
      StartDate: startDate,
      EndDate: endDate,
      ProfileId: profileId,
      Limit: 200
    );

    var rawTrips = tripRepository.QueryTrips(filter);

    if (SelectedDuration == "Under 5m") {
      rawTrips = rawTrips.Where(t => t.DurationSeconds < 300).ToList();
    } else if (SelectedDuration == "5m – 10m") {
      rawTrips = rawTrips.Where(t => t.DurationSeconds >= 300 && t.DurationSeconds <= 600).ToList();
    } else if (SelectedDuration == "Over 10m") {
      rawTrips = rawTrips.Where(t => t.DurationSeconds > 600).ToList();
    }

    Trips.Clear();
    foreach (var trip in rawTrips) {
      Trips.Add(trip);
    }
    ApplySort();
    TotalTrips = Trips.Count;
  }

  void RefreshIfLoaded() {
    if (!string.IsNullOrWhiteSpace(activeProfileId)) Refresh(activeProfileId);
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
