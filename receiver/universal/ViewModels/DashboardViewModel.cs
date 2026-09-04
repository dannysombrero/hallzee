using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged {
  readonly TripSqliteRepository tripRepository;
  readonly IRosterService rosterService;
  int totalTripsToday;
  double averageDurationMinutes;
  string quickSearchText = "";
  readonly List<DashboardActivityItem> liveActiveTrips = new();

  int thresholdMinutes = 8;
  string exceededFilterText = "";
  string exceededSortBy = "Period";
  string currentProfileId = "default";
  readonly List<ExceededTimeStudentItem> allExceededStudents = new();

  public DashboardViewModel(
    TripSqliteRepository tripRepository,
    IRosterService rosterService,
    ActivePassViewModel activePass
  ) {
    this.tripRepository = tripRepository;
    this.rosterService = rosterService;
    ActivePass = activePass;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ActivePassViewModel ActivePass { get; }
  public ObservableCollection<DashboardActivityItem> RecentTrips { get; } = new();
  public ObservableCollection<AdditionalActivePassViewModel> AdditionalActiveTrips { get; } = new();
  public ObservableCollection<ExceededTimeStudentItem> ExceededStudents { get; } = new();

  public int TotalTripsToday {
    get => totalTripsToday;
    private set { totalTripsToday = value; OnPropertyChanged(); }
  }

  public double AverageDurationMinutes {
    get => averageDurationMinutes;
    private set { averageDurationMinutes = value; OnPropertyChanged(); OnPropertyChanged(nameof(AverageDurationFormatted)); }
  }

  public string AverageDurationFormatted => $"{AverageDurationMinutes:F1} min";

  public string QuickSearchText {
    get => quickSearchText;
    set {
      if (quickSearchText != value) {
        quickSearchText = value;
        OnPropertyChanged();
      }
    }
  }

  public int ThresholdMinutes {
    get => thresholdMinutes;
    set {
      if (thresholdMinutes != value && value > 0) {
        thresholdMinutes = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(ThresholdBadgeText));
        RefreshExceededTimeStudents();
      }
    }
  }

  public string ThresholdBadgeText => $"> {thresholdMinutes}m";

  public string ExceededFilterText {
    get => exceededFilterText;
    set {
      if (exceededFilterText != value) {
        exceededFilterText = value;
        OnPropertyChanged();
        ApplyExceededFilterAndSort();
      }
    }
  }

  public string ExceededSortBy {
    get => exceededSortBy;
    set {
      if (exceededSortBy != value) {
        exceededSortBy = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(IsSortedByPeriod));
        OnPropertyChanged(nameof(IsSortedByName));
        OnPropertyChanged(nameof(IsSortedByCount));
        ApplyExceededFilterAndSort();
      }
    }
  }

  public bool IsSortedByPeriod => string.Equals(exceededSortBy, "Period", StringComparison.OrdinalIgnoreCase);
  public bool IsSortedByName => string.Equals(exceededSortBy, "Name", StringComparison.OrdinalIgnoreCase);
  public bool IsSortedByCount => string.Equals(exceededSortBy, "Count", StringComparison.OrdinalIgnoreCase);

  public bool HasNoExceededStudents => ExceededStudents.Count == 0;

  public void SetSortBy(string sortField) {
    ExceededSortBy = sortField;
  }

  public void Refresh(string profileId) {
    currentProfileId = profileId;
    var todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var summary = tripRepository.GetTripSummary(startDate: todayStr, endDate: todayStr, profileId: profileId);

    TotalTripsToday = summary.TotalTrips;
    AverageDurationMinutes = summary.AverageDurationSeconds > 0
      ? summary.AverageDurationSeconds / 60.0
      : 0;

    var filter = new TripQueryFilter(
      SearchText: string.IsNullOrWhiteSpace(QuickSearchText) ? null : QuickSearchText.Trim(),
      ProfileId: profileId,
      Limit: liveActiveTrips.Count > 0 ? 15 - Math.Min(15, liveActiveTrips.Count) : 15,
      OrderBy: "activity DESC"
    );
    var raw = tripRepository.QueryTrips(filter);

    RecentTrips.Clear();
    foreach (var activeTrip in liveActiveTrips.AsEnumerable().Reverse()) RecentTrips.Add(activeTrip);
    if (liveActiveTrips.Count == 0 && ActivePass.IsOccupied && !string.IsNullOrWhiteSpace(ActivePass.StudentId)) {
      RecentTrips.Add(DashboardActivityItem.FromActivePass(ActivePass));
    }
    foreach (var t in raw) RecentTrips.Add(DashboardActivityItem.FromTrip(t));

    RefreshExceededTimeStudents();
  }

  public void RefreshExceededTimeStudents() {
    allExceededStudents.Clear();
    var thresholdSecs = thresholdMinutes * 60;

    // Fetch trips for profile to evaluate threshold
    var trips = tripRepository.QueryTrips(new TripQueryFilter(
      ProfileId: currentProfileId,
      Limit: 1000,
      OrderBy: "activity DESC"
    ));

    var exceededTrips = trips.Where(t => t.DurationSeconds > thresholdSecs).ToList();
    var grouped = exceededTrips.GroupBy(t => t.StudentId);

    foreach (var g in grouped) {
      var studentId = g.Key;
      var name = g.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.FullName))?.FullName;
      var period = g.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.ClassPeriod))?.ClassPeriod;

      if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(period)) {
        var rosterStudent = rosterService.LookupStudent(currentProfileId, studentId);
        if (string.IsNullOrWhiteSpace(name)) name = rosterStudent?.FullName;
        if (string.IsNullOrWhiteSpace(period)) period = rosterStudent?.ClassPeriod;
      }

      var count = g.Count();
      var maxDur = g.Max(t => t.DurationSeconds);
      var tripDetails = g
        .OrderByDescending(t => t.TripDate)
        .ThenByDescending(t => t.TimeOut)
        .Select(t => new ExceededTripDetail(
          Date: FormatTripDate(t.TripDate),
          TimeOut: string.IsNullOrWhiteSpace(t.TimeOut) || t.TimeOut == "—" ? "--:--" : t.TimeOut,
          TimeIn: string.IsNullOrWhiteSpace(t.TimeIn) || t.TimeIn == "—" ? "--:--" : t.TimeIn,
          FormattedDuration: t.FormattedDuration
        ))
        .ToList();

      allExceededStudents.Add(new ExceededTimeStudentItem {
        StudentId = studentId,
        DisplayName = !string.IsNullOrWhiteSpace(name) ? name : $"#{studentId}",
        ClassPeriod = !string.IsNullOrWhiteSpace(period) ? period : "—",
        ExceededCount = count,
        MaxDurationSeconds = maxDur,
        Trips = tripDetails
      });
    }

    ApplyExceededFilterAndSort();
  }

  static string FormatTripDate(string rawDate) {
    if (DateTime.TryParse(rawDate, out var dt)) {
      return dt.ToString("MMM d");
    }
    return string.IsNullOrWhiteSpace(rawDate) ? "—" : rawDate;
  }

  void ApplyExceededFilterAndSort() {
    IEnumerable<ExceededTimeStudentItem> items = allExceededStudents;
    if (!string.IsNullOrWhiteSpace(exceededFilterText)) {
      var filter = exceededFilterText.Trim();
      items = items.Where(s =>
        s.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        s.StudentId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        s.ClassPeriod.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    items = exceededSortBy switch {
      "Name" => items.OrderBy(s => s.DisplayName).ThenBy(s => s.ClassPeriod),
      "Count" => items.OrderByDescending(s => s.ExceededCount).ThenBy(s => s.DisplayName),
      _ => items.OrderBy(s => s.ClassPeriod).ThenBy(s => s.DisplayName)
    };

    ExceededStudents.Clear();
    foreach (var item in items) ExceededStudents.Add(item);
    OnPropertyChanged(nameof(HasNoExceededStudents));
  }

  public void RegisterLiveCheckout(string studentId, string? studentName, DateTime? checkoutTime) {
    liveActiveTrips.RemoveAll(item => item.StudentId == studentId);
    liveActiveTrips.Add(DashboardActivityItem.FromLiveCheckout(studentId, studentName, checkoutTime));
    RefreshAdditionalActiveTrips();
  }

  public void ResolveLiveCheckout(string studentId) {
    liveActiveTrips.RemoveAll(item => item.StudentId == studentId);
    RefreshAdditionalActiveTrips();
  }

  public void ClearLiveCheckouts() {
    liveActiveTrips.Clear();
    AdditionalActiveTrips.Clear();
  }

  public void RefreshAdditionalActiveTrips() {
    AdditionalActiveTrips.Clear();
    foreach (var trip in liveActiveTrips.Where(trip => trip.StudentId != ActivePass.StudentId)) {
      AdditionalActiveTrips.Add(new AdditionalActivePassViewModel(trip));
    }
  }

  public void TickLiveActivePasses() {
    foreach (var pass in AdditionalActiveTrips) pass.Tick();
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}

public sealed record DashboardActivityItem(
  string StudentId,
  string DisplayName,
  string TimeOut,
  string TimeIn,
  string DurationText,
  string StatusText,
  string StatusBackground,
  string StatusForeground,
  string AvatarBackground
) {
  public string SortTimestamp { get; init; } = "";
  public DateTime? CheckoutTime { get; init; }
  public static DashboardActivityItem FromActivePass(ActivePassViewModel pass) => new(
    pass.StudentId!,
    pass.DisplayName,
    pass.DepartTime ?? "—",
    "—",
    pass.DurationDisplay,
    "Out",
    "#F59E0B",
    "White",
    "#FEF3C7"
  ) { SortTimestamp = pass.DepartTime ?? "" };

  public static DashboardActivityItem FromLiveCheckout(string studentId, string? studentName, DateTime? checkoutTime) => new(
    studentId,
    string.IsNullOrWhiteSpace(studentName) ? $"#{studentId}" : studentName,
    (checkoutTime ?? DateTime.Now).ToString("h:mm tt"),
    "—",
    "Out",
    "Out",
    "#F59E0B",
    "White",
    "#FEF3C7"
  ) { SortTimestamp = (checkoutTime ?? DateTime.Now).ToString("O"), CheckoutTime = checkoutTime ?? DateTime.Now };

  public static DashboardActivityItem FromTrip(EnrichedTripRecord trip) => new(
    trip.StudentId,
    trip.DisplayName,
    string.IsNullOrWhiteSpace(trip.TimeOut) ? "—" : trip.TimeOut,
    string.IsNullOrWhiteSpace(trip.TimeIn) ? "—" : trip.TimeIn,
    trip.FormattedDuration,
    "Returned",
    "#10B981",
    "White",
    "#E0F2FE"
  );
}

public sealed class AdditionalActivePassViewModel : INotifyPropertyChanged {
  readonly DateTime checkoutTime;
  public AdditionalActivePassViewModel(DashboardActivityItem item) {
    StudentId = item.StudentId;
    DisplayName = item.DisplayName;
    TimeOut = item.TimeOut;
    checkoutTime = item.CheckoutTime ?? DateTime.Now;
    Tick();
  }
  public event PropertyChangedEventHandler? PropertyChanged;
  public string StudentId { get; }
  public string DisplayName { get; }
  public string TimeOut { get; }
  public string ElapsedDisplay { get; private set; } = "0m 00s";
  public void Tick() {
    var elapsed = (int)Math.Max(0, (DateTime.Now - checkoutTime).TotalSeconds);
    var next = $"{elapsed / 60}m {elapsed % 60:D2}s";
    if (next == ElapsedDisplay) return;
    ElapsedDisplay = next;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ElapsedDisplay)));
  }
}

public sealed record ExceededTripDetail(
  string Date,
  string TimeOut,
  string TimeIn,
  string FormattedDuration
);

public sealed class ExceededTimeStudentItem {
  public string StudentId { get; init; } = "";
  public string DisplayName { get; init; } = "";
  public string ClassPeriod { get; init; } = "—";
  public int ExceededCount { get; init; }
  public string ExceededCountText => ExceededCount == 1 ? "1 time" : $"{ExceededCount} times";
  public int MaxDurationSeconds { get; init; }
  public string MaxDurationFormatted => EnrichedTripRecord.FormatDuration(MaxDurationSeconds);
  public string PeriodDisplay => string.IsNullOrWhiteSpace(ClassPeriod) || ClassPeriod == "—" ? "General" : ClassPeriod;
  public string SubtitleText => $"ID: #{StudentId} • {PeriodDisplay}";
  public IReadOnlyList<ExceededTripDetail> Trips { get; init; } = Array.Empty<ExceededTripDetail>();
}
