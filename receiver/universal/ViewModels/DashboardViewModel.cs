using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged {
  readonly TripSqliteRepository tripRepository;
  int totalTripsToday;
  double averageDurationMinutes;
  string quickSearchText = "";
  readonly List<DashboardActivityItem> liveActiveTrips = new();

  public DashboardViewModel(
    TripSqliteRepository tripRepository,
    IRosterService rosterService,
    ActivePassViewModel activePass
  ) {
    this.tripRepository = tripRepository;
    ActivePass = activePass;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ActivePassViewModel ActivePass { get; }
  public ObservableCollection<DashboardActivityItem> RecentTrips { get; } = new();
  public ObservableCollection<AdditionalActivePassViewModel> AdditionalActiveTrips { get; } = new();

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

  public void Refresh(string profileId) {
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
