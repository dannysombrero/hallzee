using System;
using System.IO;
using BathroomSync.Core;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class PolicyViewModelTests : IDisposable {
  readonly string tempDbPath;
  readonly ProfileAndPolicySqliteRepository policyRepository;
  readonly PolicyViewModel viewModel;

  public PolicyViewModelTests() {
    tempDbPath = Path.Combine(Path.GetTempPath(), "HallzeePolicyVmTests", $"{Guid.NewGuid():N}.db");
    policyRepository = new ProfileAndPolicySqliteRepository(tempDbPath);
    viewModel = new PolicyViewModel(policyRepository);
  }

  public void Dispose() {
    try {
      if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
      var dir = Path.GetDirectoryName(tempDbPath);
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    } catch { }
  }

  [Fact]
  public void BellPeriodItemViewModelParsesAndFormatsDaysCorrectly() {
    var period = new BellSchedulePeriod(
      ScheduleId: "test-1",
      ProfileId: "prof-1",
      PeriodName: "Period 1",
      StartTime: "08:00 AM",
      EndTime: "08:50 AM",
      DaysOfWeek: "Mon,Wed,Fri",
      ScheduleName: "Regular"
    );

    var item = new BellPeriodItemViewModel(period);

    Assert.True(item.IsMonday);
    Assert.False(item.IsTuesday);
    Assert.True(item.IsWednesday);
    Assert.False(item.IsThursday);
    Assert.True(item.IsFriday);
    Assert.Equal("Mon,Wed,Fri", item.DaysOfWeek);
    Assert.Equal("Mon, Wed, Fri", item.FormattedDaysSummary);
    Assert.Equal("08:00 AM – 08:50 AM", item.DisplayTimeRange);

    // Toggle Tuesday and uncheck Friday
    item.IsTuesday = true;
    item.IsFriday = false;
    Assert.Equal("Mon,Tue,Wed", item.DaysOfWeek);
    Assert.Equal("Mon, Tue, Wed", item.FormattedDaysSummary);
  }

  [Fact]
  public void BellPeriodItemViewModelAllDaysSummary() {
    var period = new BellSchedulePeriod(
      ScheduleId: "test-all",
      ProfileId: "prof-1",
      PeriodName: "Period 2",
      StartTime: "09:00 AM",
      EndTime: "09:50 AM",
      DaysOfWeek: "Mon,Tue,Wed,Thu,Fri"
    );

    var item = new BellPeriodItemViewModel(period);
    Assert.Equal("Mon – Fri (All days)", item.FormattedDaysSummary);

    item.IsMonday = false;
    item.IsTuesday = false;
    item.IsWednesday = false;
    item.IsThursday = false;
    item.IsFriday = false;
    Assert.Equal("No days selected", item.FormattedDaysSummary);
  }

  [Fact]
  public void BellPeriodItemViewModelEditAndSaveWorkflow() {
    var period = new BellSchedulePeriod(
      ScheduleId: "test-edit",
      ProfileId: "prof-1",
      PeriodName: "Advisory",
      StartTime: "10:00 AM",
      EndTime: "10:30 AM",
      DaysOfWeek: "Wed"
    );

    var item = new BellPeriodItemViewModel(period, isEditing: false);
    Assert.False(item.IsEditing);

    item.StartEdit();
    Assert.True(item.IsEditing);

    item.PeriodName = "Homeroom";
    item.SaveEdit();
    Assert.False(item.IsEditing);
    Assert.Equal("Homeroom", item.PeriodName);

    var model = item.ToModel();
    Assert.Equal("Homeroom", model.PeriodName);
    Assert.Equal("Wed", model.DaysOfWeek);
  }

  [Fact]
  public void AddPeriodCreatesInEditModeAndSavesThroughViewModel() {
    policyRepository.SaveProfile(new ClassroomProfile("prof-1", "Room 101"));
    viewModel.AddPeriod("prof-1", "Period 1", "08:00 AM", "08:50 AM");
    Assert.Single(viewModel.Periods);

    var added = viewModel.Periods[0];
    Assert.True(added.IsEditing);
    Assert.Equal("Period 1", added.PeriodName);
    Assert.Equal("Mon,Tue,Wed,Thu,Fri", added.DaysOfWeek);

    // Save policy
    viewModel.Save("prof-1");
    Assert.False(added.IsEditing);

    // Re-load in a fresh view model
    var vm2 = new PolicyViewModel(policyRepository);
    vm2.Refresh("prof-1");
    Assert.Single(vm2.Periods);
    Assert.Equal("Period 1", vm2.Periods[0].PeriodName);
    Assert.False(vm2.Periods[0].IsEditing);
  }

  [Fact]
  public void SoundServiceGeneratesValidWavDataForSupportedSounds() {
    var chimeWav = SoundService.GenerateWav("Chime");
    Assert.NotEmpty(chimeWav);
    Assert.Equal((byte)'R', chimeWav[0]);
    Assert.Equal((byte)'I', chimeWav[1]);
    Assert.Equal((byte)'F', chimeWav[2]);
    Assert.Equal((byte)'F', chimeWav[3]);

    var bellWav = SoundService.GenerateWav("Bell");
    Assert.NotEmpty(bellWav);

    var softAlertWav = SoundService.GenerateWav("Soft alert");
    Assert.NotEmpty(softAlertWav);
  }

  [Theory]
  [InlineData(0, "0m 00s")]
  [InlineData(45, "0m 45s")]
  [InlineData(60, "1m 00s")]
  [InlineData(360, "6m 00s")]
  [InlineData(425, "7m 05s")]
  [InlineData(3600, "1h 0m 00s")]
  [InlineData(3665, "1h 1m 05s")]
  public void EnrichedTripRecordFormatsDurationAccurately(int seconds, string expected) {
    Assert.Equal(expected, EnrichedTripRecord.FormatDuration(seconds));
  }

  [Fact]
  public void CustomPeriodNameAndTimesSaveThroughEditProperties() {
    var period = new BellSchedulePeriod("p-1", "prof-1", "Period 1", "08:00 AM", "08:50 AM");
    var item = new BellPeriodItemViewModel(period);
    item.StartEdit();

    item.EditName = "Period 3 (Chemistry AP)";
    item.EditStartTime = "09:15 AM";
    item.EditEndTime = "10:05 AM";
    item.SaveEdit();

    Assert.Equal("Period 3 (Chemistry AP)", item.PeriodName);
    Assert.Equal("09:15 AM", item.StartTime);
    Assert.Equal("10:05 AM", item.EndTime);
    Assert.Equal("09:15 AM – 10:05 AM", item.DisplayTimeRange);
  }

  [Fact]
  public void PeriodsAutomaticallySortChronologicallyAndToggleOrder() {
    policyRepository.SaveProfile(new ClassroomProfile("prof-1", "Room 101"));
    
    // Add out-of-order periods
    viewModel.AddPeriod("prof-1", "Period 3", "10:10 AM", "11:00 AM");
    viewModel.AddPeriod("prof-1", "Period 1", "08:00 AM", "08:50 AM");
    viewModel.AddPeriod("prof-1", "Period 2", "09:00 AM", "09:50 AM");

    // Default: earliest first
    Assert.True(viewModel.IsEarliestFirst);
    Assert.Equal("Period 1", viewModel.Periods[0].PeriodName);
    Assert.Equal("Period 2", viewModel.Periods[1].PeriodName);
    Assert.Equal("Period 3", viewModel.Periods[2].PeriodName);

    // Toggle: latest first
    viewModel.ToggleSortOrder();
    Assert.False(viewModel.IsEarliestFirst);
    Assert.Equal("Period 3", viewModel.Periods[0].PeriodName);
    Assert.Equal("Period 2", viewModel.Periods[1].PeriodName);
    Assert.Equal("Period 1", viewModel.Periods[2].PeriodName);

    // Toggle back
    viewModel.ToggleSortOrder();
    Assert.True(viewModel.IsEarliestFirst);
    Assert.Equal("Period 1", viewModel.Periods[0].PeriodName);
  }
}
