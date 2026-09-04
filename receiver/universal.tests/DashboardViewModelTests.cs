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

  [Fact]
  public void RefreshPlacesTheActivePassAtTheTopOfRecentActivity() {
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
    tripRepository.Store($"1,10482,{today},09:00:00,09:06:00,360,COMPLETE");
    activePass.SetOccupied("10483", "Avery Chen", DateTime.Now.AddMinutes(-2));

    viewModel.Refresh("default");

    Assert.Equal("10483", viewModel.RecentTrips[0].StudentId);
    Assert.Equal("Out", viewModel.RecentTrips[0].StatusText);
    Assert.Equal("—", viewModel.RecentTrips[0].TimeIn);
    Assert.Equal("10482", viewModel.RecentTrips[1].StudentId);
    Assert.Equal("Returned", viewModel.RecentTrips[1].StatusText);
  }

  [Fact]
  public void RefreshKeepsEveryLiveCheckoutUntilThatStudentReturns() {
    viewModel.RegisterLiveCheckout("1001", "Avery Chen", DateTime.Now.AddMinutes(-3));
    viewModel.RegisterLiveCheckout("1002", "Jordan Lee", DateTime.Now.AddMinutes(-1));

    viewModel.Refresh("default");

    Assert.Equal(2, viewModel.RecentTrips.Count);
    Assert.Equal("1002", viewModel.RecentTrips[0].StudentId);
    Assert.Equal("1001", viewModel.RecentTrips[1].StudentId);
    Assert.All(viewModel.RecentTrips, trip => Assert.Equal("Out", trip.StatusText));
    viewModel.ResolveLiveCheckout("1001");
    viewModel.Refresh("default");
    Assert.Single(viewModel.RecentTrips);
    Assert.Equal("1002", viewModel.RecentTrips[0].StudentId);
    Assert.Single(viewModel.AdditionalActiveTrips);
    Assert.Equal("1002", viewModel.AdditionalActiveTrips[0].StudentId);
  }

  [Fact]
  public void ExceededTimeStudentsIdentifiedGroupedAndFilteredCorrectly() {
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
    // Seed roster
    rosterRepository.SaveStudents("default", new[] {
      new RosterStudent("10482", "default", "Elena", "Rostova", "11", "Period 3"),
      new RosterStudent("10484", "default", "Lucas", "Silva", "10", "Period 1")
    });

    // 10482: 2 trips over 8m (660s = 11m, 720s = 12m)
    tripRepository.Store($"1,10482,{today},09:00:00,09:11:00,660,COMPLETED");
    tripRepository.Store($"2,10482,{today},11:00:00,11:12:00,720,COMPLETED");

    // 10483: 1 trip under 8m (360s = 6m)
    tripRepository.Store($"3,10483,{today},10:00:00,10:06:00,360,COMPLETED");

    // 10484: 1 trip over 8m (600s = 10m)
    tripRepository.Store($"4,10484,{today},12:00:00,12:10:00,600,COMPLETED");

    viewModel.Refresh("default");

    // Two students exceeded threshold (10482 and 10484)
    Assert.Equal(2, viewModel.ExceededStudents.Count);
    Assert.False(viewModel.HasNoExceededStudents);

    // Default sort is Period: Period 1 (Lucas) before Period 3 (Elena)
    Assert.Equal("Lucas Silva", viewModel.ExceededStudents[0].DisplayName);
    Assert.Equal("Period 1", viewModel.ExceededStudents[0].ClassPeriod);
    Assert.Equal(1, viewModel.ExceededStudents[0].ExceededCount);

    Assert.Equal("Elena Rostova", viewModel.ExceededStudents[1].DisplayName);
    Assert.Equal("Period 3", viewModel.ExceededStudents[1].ClassPeriod);
    Assert.Equal(2, viewModel.ExceededStudents[1].ExceededCount);
    Assert.Equal("2 times", viewModel.ExceededStudents[1].ExceededCountText);
    Assert.Equal("12m 00s", viewModel.ExceededStudents[1].MaxDurationFormatted);
    Assert.Equal(2, viewModel.ExceededStudents[1].Trips.Count);
    Assert.Equal("12m 00s", viewModel.ExceededStudents[1].Trips[0].FormattedDuration);

    // Sort by count descending: Elena (2) before Lucas (1)
    viewModel.SetSortBy("Count");
    Assert.True(viewModel.IsSortedByCount);
    Assert.False(viewModel.IsSortedByPeriod);
    Assert.Equal("Elena Rostova", viewModel.ExceededStudents[0].DisplayName);
    Assert.Equal("Lucas Silva", viewModel.ExceededStudents[1].DisplayName);

    // Filter by name or period
    viewModel.ExceededFilterText = "Lucas";
    Assert.Single(viewModel.ExceededStudents);
    Assert.Equal("Lucas Silva", viewModel.ExceededStudents[0].DisplayName);

    viewModel.ExceededFilterText = "Period 3";
    Assert.Single(viewModel.ExceededStudents);
    Assert.Equal("Elena Rostova", viewModel.ExceededStudents[0].DisplayName);

    viewModel.ExceededFilterText = "NonExistent";
    Assert.Empty(viewModel.ExceededStudents);
    Assert.True(viewModel.HasNoExceededStudents);
  }
}
