using System;
using System.IO;
using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class TripsViewModelTests : IDisposable {
  readonly string tempDbPath;
  readonly TripSqliteRepository tripRepository;
  readonly RosterSqliteRepository rosterRepository;
  readonly RosterService rosterService;
  readonly TripsViewModel viewModel;

  public TripsViewModelTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeeTripsVmTests", $"{Guid.NewGuid():N}.db");
    tripRepository = new TripSqliteRepository(tempDbPath);
    rosterRepository = new RosterSqliteRepository(tempDbPath);
    rosterService = new RosterService(rosterRepository);
    viewModel = new TripsViewModel(tripRepository, rosterService);

    var profileRepository = new ProfileAndPolicySqliteRepository(tempDbPath);
    profileRepository.SaveProfile(new ClassroomProfile("prof-1", "Room 101"));

    rosterRepository.SaveStudents("prof-1", new[] {
      new RosterStudent("101", "prof-1", "Alice", "Brown", "10", "Period 1"),
      new RosterStudent("102", "prof-1", "Bob", "Smith", "11", "Period 2"),
      new RosterStudent("103", "prof-1", "Charlie", "Davis", "12", "Period 3")
    });

    // Seed 3 trips with different times and durations
    tripRepository.Store("1,101,2026-09-01,08:10:00,08:15:00,300,COMPLETED");
    tripRepository.Store("2,102,2026-09-02,09:30:00,09:40:00,600,COMPLETED");
    tripRepository.Store("3,103,2026-09-02,09:00:00,09:02:00,120,COMPLETED");
  }

  public void Dispose() {
    try {
      if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
      var dir = Path.GetDirectoryName(tempDbPath);
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    } catch { }
  }

  [Fact]
  public void TripsSortingTogglesColumnsCorrectly() {
    viewModel.Refresh("prof-1");
    Assert.Equal(3, viewModel.Trips.Count);

    // Default: Date / time descending (newest first)
    Assert.Equal(2L, viewModel.Trips[0].TripId);

    // Sort by Student Ascending
    viewModel.ToggleSort("Student");
    Assert.Equal("Alice Brown", viewModel.Trips[0].DisplayName);
    Assert.Equal("Charlie Davis", viewModel.Trips[2].DisplayName);
    Assert.Equal(" ▲", viewModel.StudentSortIndicator);

    // Toggle Student to Descending
    viewModel.ToggleSort("Student");
    Assert.Equal("Charlie Davis", viewModel.Trips[0].DisplayName);
    Assert.Equal("Alice Brown", viewModel.Trips[2].DisplayName);
    Assert.Equal(" ▼", viewModel.StudentSortIndicator);

    // Sort by Duration (default descending = longest first)
    viewModel.ToggleSort("Duration");
    Assert.Equal(600, viewModel.Trips[0].DurationSeconds);
    Assert.Equal(120, viewModel.Trips[2].DurationSeconds);
    Assert.Equal(" ▼", viewModel.DurationSortIndicator);

    // Sort by TimeOut
    viewModel.ToggleSort("TimeOut");
    Assert.Equal("09:30:00", viewModel.Trips[0].TimeOut);

    // Sort by Date Descending on first click (newest date first)
    viewModel.ToggleSort("Date");
    Assert.Equal(2L, viewModel.Trips[0].TripId);
    Assert.Equal("2026-09-02", viewModel.Trips[0].TripDate);
    Assert.Equal("Sep 2, 2026", viewModel.Trips[0].FormattedTripDate);
    Assert.Equal(" ▼", viewModel.DateSortIndicator);

    // Toggle Date to Ascending on second click (oldest date first)
    viewModel.ToggleSort("Date");
    Assert.Equal(1L, viewModel.Trips[0].TripId);
    Assert.Equal("2026-09-01", viewModel.Trips[0].TripDate);
    Assert.Equal("Sep 1, 2026", viewModel.Trips[0].FormattedTripDate);
    Assert.Equal(" ▲", viewModel.DateSortIndicator);
  }
}
