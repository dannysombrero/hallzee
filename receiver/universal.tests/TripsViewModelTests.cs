using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class TripsViewModelTests : IDisposable {
  readonly string tempDbPath;
  readonly TripSqliteRepository tripRepository;
  readonly TripsViewModel viewModel;

  public TripsViewModelTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeeTripsVmTests", $"{Guid.NewGuid():N}.db");
    tripRepository = new TripSqliteRepository(tempDbPath);
    viewModel = new TripsViewModel(tripRepository, new RosterService(new RosterSqliteRepository(tempDbPath)));
  }

  public void Dispose() {
    var directory = Path.GetDirectoryName(tempDbPath);
    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
  }

  [Fact]
  public void DefaultAllFilterShowsStoredTrips() {
    Assert.Equal(new[] { "ALL", "COMPLETED", "MANUAL_RESET" }, viewModel.StatusOptions);
    Assert.Equal("ALL", viewModel.SelectedStatus);
    Assert.Equal(TripStoreResult.Saved, tripRepository.Store(
      "1,10482,2026-09-02,09:00:00,09:06:00,360,COMPLETE"));

    viewModel.Refresh("default");

    Assert.Equal(1, viewModel.TotalTrips);
    Assert.Single(viewModel.Trips);
    Assert.Equal("10482", viewModel.Trips[0].StudentId);
  }
}
