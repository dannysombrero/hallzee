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
    result = tripRepository.Store($"2,10483,{today},10:00:00,10:11:00,660,COMPLETED");
    Assert.Equal(TripStoreResult.Saved, result);

    viewModel.Refresh("default");

    Assert.Equal(2, viewModel.TotalTripsToday);
    Assert.Equal(8.5, viewModel.AverageDurationMinutes);
    Assert.Equal("8.5 min", viewModel.AverageDurationFormatted);
    Assert.Equal(2, viewModel.RecentTrips.Count);
    Assert.Equal("10483", viewModel.RecentTrips[0].StudentId);
    Assert.Equal("10482", viewModel.RecentTrips[1].StudentId);
  }
}
