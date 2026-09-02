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
  public ObservableCollection<EnrichedTripRecord> RecentTrips { get; } = new();

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
      Limit: 15,
      OrderBy: "activity DESC"
    );
    var raw = tripRepository.QueryTrips(filter);

    RecentTrips.Clear();
    foreach (var t in raw) RecentTrips.Add(t);
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
