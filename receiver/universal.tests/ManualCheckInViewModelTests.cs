using System;
using System.IO;
using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class ManualCheckInViewModelTests : IDisposable {
  readonly string tempDbPath;
  readonly RosterSqliteRepository rosterRepository;
  readonly RosterService rosterService;
  readonly ManualCheckInViewModel viewModel;

  public ManualCheckInViewModelTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeeManualCheckInTests", $"{Guid.NewGuid():N}.db");
    rosterRepository = new RosterSqliteRepository(tempDbPath);
    rosterService = new RosterService(rosterRepository);
    viewModel = new ManualCheckInViewModel(rosterService);

    var profileRepository = new ProfileAndPolicySqliteRepository(tempDbPath);
    profileRepository.SaveProfile(new ClassroomProfile("prof-1", "Room 101"));

    // Seed student in roster
    rosterRepository.SaveStudents("prof-1", new[] {
      new RosterStudent("10482", "prof-1", "Elena", "Rostova", "11", "Period 1")
    });
  }

  public void Dispose() {
    try {
      if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
      var dir = Path.GetDirectoryName(tempDbPath);
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    } catch { }
  }

  [Fact]
  public void CanSubmitValidationRules() {
    viewModel.Reset("prof-1");
    Assert.False(viewModel.CanSubmit);

    viewModel.StudentName = "Avery Chen";
    Assert.True(viewModel.CanSubmit);

    viewModel.StudentName = "";
    viewModel.StudentId = "10482";
    Assert.True(viewModel.CanSubmit);
  }

  [Fact]
  public void AutoLookupResolvesRosterNameWhenIdProvided() {
    viewModel.Reset("prof-1");
    viewModel.StudentId = "10482";

    Assert.Equal("Elena Rostova", viewModel.StudentName);
    var details = viewModel.ResolvePassDetails();
    Assert.Equal("10482", details.resolvedId);
    Assert.Equal("Elena Rostova", details.resolvedName);
    Assert.Null(details.resolvedPeriod);
    Assert.Null(details.resolvedDestination);
    Assert.Null(details.resolvedPurpose);
  }

  [Fact]
  public void AutoLookupResolvesRosterIdWhenNameProvided() {
    viewModel.Reset("prof-1");
    viewModel.StudentName = "Elena Rostova";

    Assert.Equal("10482", viewModel.StudentId);
    var details = viewModel.ResolvePassDetails();
    Assert.Equal("10482", details.resolvedId);
    Assert.Equal("Elena Rostova", details.resolvedName);
  }

  [Fact]
  public void OptionalDetailsResolvedProperly() {
    viewModel.Reset("prof-1");
    viewModel.StudentName = "Jordan Hayes";
    viewModel.Period = "Period 3";
    viewModel.Destination = "Room 102";
    viewModel.Purpose = "Nurse / Clinic";

    var details = viewModel.ResolvePassDetails();
    Assert.Equal("Jordan Hayes", details.resolvedName);
    Assert.StartsWith("M-", details.resolvedId);
    Assert.Equal("Period 3", details.resolvedPeriod);
    Assert.Equal("Room 102", details.resolvedDestination);
    Assert.Equal("Nurse / Clinic", details.resolvedPurpose);
  }

  [Fact]
  public void ResetClearsStateToDefaults() {
    viewModel.StudentName = "Test";
    viewModel.StudentId = "999";
    viewModel.Period = "Period 2";
    viewModel.Destination = "Floor 2";
    viewModel.Purpose = "Library";
    viewModel.StatusMessage = "Some error";

    viewModel.Reset("prof-1");

    Assert.Equal("", viewModel.StudentName);
    Assert.Equal("", viewModel.StudentId);
    Assert.Equal("", viewModel.Period);
    Assert.Equal("", viewModel.Destination);
    Assert.Equal("", viewModel.Purpose);
    Assert.Null(viewModel.StatusMessage);
    Assert.False(viewModel.CanSubmit);
  }
}
