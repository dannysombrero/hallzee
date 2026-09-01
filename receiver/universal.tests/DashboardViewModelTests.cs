using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class DashboardViewModelTests : IDisposable {
  readonly string tempDbPath;
  readonly TripSqliteRepository tripRepository;
  readonly RosterSqliteRepository rosterRepository;
  readonly RosterService rosterService;
  readonly ActivePassViewModel activePass;
  readonly DashboardViewModel viewModel;

  public DashboardViewModelTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeeDashVmTests", $"{Guid.NewGuid():N}.db");
    tripRepository = new TripSqliteRepository(tempDbPath);
    rosterRepository = new RosterSqliteRepository(tempDbPath);
    rosterService = new RosterService(rosterRepository);
    activePass = new ActivePassViewModel();
    viewModel = new DashboardViewModel(tripRepository, rosterService, activePass);
  }

  public void Dispose() {
    try {
      if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
      var dir = Path.GetDirectoryName(tempDbPath);
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    } catch { }
  }

  [Fact]
  public void RefreshCalculatesMetricsAndLoadsRecentTrips() {
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var result = tripRepository.Store($"1,10482,{today},09:00:00,09:06:00,360,COMPLETED");
    Assert.Equal(TripStoreResult.Saved, result);

    viewModel.Refresh("default");

    Assert.Equal(1, viewModel.TotalTripsToday);
    Assert.Equal(6.0, viewModel.AverageDurationMinutes);
    Assert.Equal("6.0 min", viewModel.AverageDurationFormatted);
    Assert.Single(viewModel.RecentTrips);
    Assert.Equal("10482", viewModel.RecentTrips[0].StudentId);
  }
}
