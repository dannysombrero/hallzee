using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class ActivePassViewModelTests {
  [Fact]
  public void InitialStateIsUnknownAndEmpty() {
    var vm = new ActivePassViewModel();
    Assert.False(vm.IsOccupied);
    Assert.Null(vm.StudentId);
    Assert.Null(vm.StudentName);
    Assert.Equal("No active pass", vm.DisplayName);
    Assert.Equal("00:00", vm.ElapsedFormatted);
    Assert.True(vm.IsStatusUnknown);
    Assert.Equal("Pass Status Unknown", vm.StatusText);
    Assert.Equal("STATUS UNKNOWN", vm.BadgeText);
    Assert.Equal("#64748B", vm.StatusColor);
  }

  [Fact]
  public void SetOccupiedSetsStateAndCalculatesElapsed() {
    var vm = new ActivePassViewModel();
    var checkout = DateTime.Now.AddMinutes(-3).AddSeconds(-15);
    vm.SetOccupied("10482", "Elena Rostova", checkout);

    Assert.True(vm.IsOccupied);
    Assert.False(vm.IsStatusUnknown);
    Assert.Equal("10482", vm.StudentId);
    Assert.Equal("Elena Rostova", vm.StudentName);
    Assert.Equal("Elena Rostova", vm.DisplayName);
    Assert.Equal("Student Out of Class", vm.StatusText);
    Assert.Equal("PASS OCCUPIED", vm.BadgeText);
    Assert.Equal("#D97706", vm.StatusColor);
    Assert.True(vm.ElapsedSeconds >= 195);
    Assert.StartsWith("03:", vm.ElapsedFormatted);
  }

  [Fact]
  public void SetOccupiedWithoutNameFallsBackToHashId() {
    var vm = new ActivePassViewModel();
    vm.SetOccupied("99281", null, DateTime.Now);

    Assert.True(vm.IsOccupied);
    Assert.Equal("#99281", vm.DisplayName);
  }

  [Fact]
  public void SetAvailableClearsOccupiedState() {
    var vm = new ActivePassViewModel();
    vm.SetOccupied("10482", "Elena Rostova", DateTime.Now);
    Assert.True(vm.IsOccupied);

    vm.SetAvailable();
    Assert.False(vm.IsOccupied);
    Assert.Null(vm.StudentId);
    Assert.Equal("00:00", vm.ElapsedFormatted);
    Assert.Equal("No active pass", vm.DisplayName);
    Assert.False(vm.IsStatusUnknown);
  }

  [Fact]
  public void SetUnknownClearsTheLastKnownPass() {
    var vm = new ActivePassViewModel();
    vm.SetOccupied("10482", "Elena Rostova", DateTime.Now);

    vm.SetUnknown();

    Assert.True(vm.IsStatusUnknown);
    Assert.False(vm.IsOccupied);
    Assert.Equal("Unknown", vm.DurationDisplay);
  }

  [Fact]
  public void TickIncrementsElapsedTimeWhenOccupied() {
    var vm = new ActivePassViewModel();
    vm.SetOccupied("10482", "Elena Rostova", DateTime.Now.AddSeconds(-5));
    var initial = vm.ElapsedSeconds;

    vm.Tick();
    Assert.True(vm.ElapsedSeconds >= initial);
  }
}
