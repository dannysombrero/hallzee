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
  int maxDailyPasses = 2;

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
  public ObservableCollection<DailyLimitStudentItem> DailyLimitStudents { get; } = new();

  public int MaxDailyPasses {
    get => maxDailyPasses;
    set { if (maxDailyPasses != value) { maxDailyPasses = Math.Max(1, value); OnPropertyChanged(); RefreshDailyLimits(); } }
  }
  public bool HasDailyLimitAlerts => DailyLimitStudents.Count > 0;
  public string DailyLimitSummary => DailyLimitStudents.Count switch {
    0 => "No students have reached today's guideline.",
    1 => $"{DailyLimitStudents[0].DisplayName} reached today's {MaxDailyPasses}-trip guideline.",
    _ => $"{DailyLimitStudents.Count} students reached today's {MaxDailyPasses}-trip guideline."
  };

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

  string selectedTimeframe = "Last 2 Weeks";

  public IReadOnlyList<string> TimeframeOptions { get; } = new[] {
    "Last 2 Weeks", "Today", "This Week", "This Month", "All Time"
  };

  public string SelectedTimeframe {
    get => selectedTimeframe;
    set {
      if (selectedTimeframe != value && !string.IsNullOrWhiteSpace(value)) {
        selectedTimeframe = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(SelectedTimeframeSummaryText));
        OnPropertyChanged(nameof(ExceededSubtitleText));
        RefreshExceededTimeStudents();
      }
    }
  }

  public string SelectedTimeframeSummaryText => selectedTimeframe switch {
    "All Time" => "all time",
    "Today" => "today",
    "This Week" => "this week",
    "Last 2 Weeks" => "the last 2 weeks",
    "This Month" => "this month",
    _ => selectedTimeframe.ToLowerInvariant()
  };

  public string ExceededSubtitleText =>
    $"Students whose hall pass trips exceeded {thresholdMinutes}m within {SelectedTimeframeSummaryText}.";

  public int ThresholdMinutes {
    get => thresholdMinutes;
    set {
      if (thresholdMinutes != value && value > 0) {
        thresholdMinutes = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(ThresholdBadgeText));
        OnPropertyChanged(nameof(ExceededSubtitleText));
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

  public void SetThreshold(int minutes) {
    if (minutes > 0) {
      ThresholdMinutes = minutes;
    }
  }

  public void ResetThresholdToPolicy(int policyMinutes) {
    if (policyMinutes > 0) {
      ThresholdMinutes = policyMinutes;
    }
  }


  public void Refresh(string profileId) {
    currentProfileId = profileId;
    var localTodayStr = DateTime.Now.ToString("yyyy-MM-dd");
    var todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var summary = tripRepository.GetTripSummary(startDate: localTodayStr, endDate: localTodayStr, profileId: profileId);
    if (summary.TotalTrips == 0 && localTodayStr != todayStr) {
      summary = tripRepository.GetTripSummary(startDate: todayStr, endDate: todayStr, profileId: profileId);
    }

    TotalTripsToday = summary.TotalTrips;
    AverageDurationMinutes = summary.AverageDurationSeconds > 0
      ? summary.AverageDurationSeconds / 60.0
      : 0;

    var filter = new TripQueryFilter(
      SearchText: string.IsNullOrWhiteSpace(QuickSearchText) ? null : QuickSearchText.Trim(),
      ProfileId: profileId,
      StartDate: localTodayStr,
      EndDate: localTodayStr,
      Limit: 100,
      OrderBy: "activity DESC"
    );
    var raw = tripRepository.QueryTrips(filter);
    if (raw.Count == 0 && localTodayStr != todayStr) {
      raw = tripRepository.QueryTrips(filter with { StartDate = todayStr, EndDate = todayStr });
    }

    RecentTrips.Clear();
    foreach (var activeTrip in liveActiveTrips.AsEnumerable().Reverse()) RecentTrips.Add(activeTrip);
    if (liveActiveTrips.Count == 0 && ActivePass.IsOccupied && !string.IsNullOrWhiteSpace(ActivePass.StudentId)) {
      RecentTrips.Add(DashboardActivityItem.FromActivePass(ActivePass));
    }
    foreach (var t in raw) RecentTrips.Add(DashboardActivityItem.FromTrip(t));

    RefreshDailyLimits();

    RefreshExceededTimeStudents();
  }

  void RefreshDailyLimits(IReadOnlyList<EnrichedTripRecord>? todayTrips = null) {
    if (string.IsNullOrWhiteSpace(currentProfileId)) return;
    if (todayTrips == null) {
      var today = DateTime.Now.ToString("yyyy-MM-dd");
      todayTrips = tripRepository.QueryTrips(new TripQueryFilter(ProfileId: currentProfileId, StartDate: today, EndDate: today, Limit: 10000));
    }
    DailyLimitStudents.Clear();
    foreach (var group in todayTrips.GroupBy(trip => trip.StudentId).Where(group => group.Count() >= MaxDailyPasses).OrderByDescending(group => group.Count())) {
      var first = group.First();
      var name = first.FullName ?? rosterService.LookupStudent(currentProfileId, group.Key)?.FullName ?? $"#{group.Key}";
      DailyLimitStudents.Add(new DailyLimitStudentItem(group.Key, name, group.Count(), MaxDailyPasses));
    }
    OnPropertyChanged(nameof(HasDailyLimitAlerts));
    OnPropertyChanged(nameof(DailyLimitSummary));
  }

  public void RefreshExceededTimeStudents() {
    allExceededStudents.Clear();
    var thresholdSecs = thresholdMinutes * 60;

    var now = DateTime.Now;
    string? startDate = null;

    if (selectedTimeframe == "Today") {
      var minDate = now.Date < DateTime.UtcNow.Date ? now.Date : DateTime.UtcNow.Date;
      startDate = minDate.ToString("yyyy-MM-dd");
    } else if (selectedTimeframe == "This Week") {
      var daysFromMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
      startDate = now.Date.AddDays(-daysFromMonday).ToString("yyyy-MM-dd");
    } else if (selectedTimeframe == "Last 2 Weeks") {
      startDate = now.Date.AddDays(-14).ToString("yyyy-MM-dd");
    } else if (selectedTimeframe == "This Month") {
      startDate = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
    }

    // Fetch trips for profile to evaluate threshold within timeframe
    var trips = tripRepository.QueryTrips(new TripQueryFilter(
      ProfileId: currentProfileId,
      StartDate: startDate,
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

  public static DashboardActivityItem FromTrip(EnrichedTripRecord trip) {
    var isManual = string.Equals(trip.Status, "MANUAL", StringComparison.OrdinalIgnoreCase);
    return new DashboardActivityItem(
      trip.StudentId,
      trip.DisplayName,
      string.IsNullOrWhiteSpace(trip.TimeOut) ? "—" : trip.TimeOut,
      string.IsNullOrWhiteSpace(trip.TimeIn) ? "—" : trip.TimeIn,
      trip.FormattedDuration,
      isManual ? "Manual" : "Returned",
      isManual ? "#64748B" : "#10B981",
      "White",
      isManual ? "#E2E8F0" : "#E0F2FE"
    );
  }
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

public sealed record DailyLimitStudentItem(string StudentId, string DisplayName, int TripCount, int Limit) {
  public string Summary => $"{DisplayName}: {TripCount} trips (guideline {Limit})";
}
