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
    var sounds = new[] {
      "Chime", "Bell", "Soft alert", "Marimba", "Subtle Ping", "Digital Watch", "Gentle Knock", "Harp Ascend"
    };

    foreach (var sound in sounds) {
      var wav = SoundService.GenerateWav(sound, 0.8);
      Assert.NotEmpty(wav);
      Assert.Equal((byte)'R', wav[0]);
      Assert.Equal((byte)'I', wav[1]);
      Assert.Equal((byte)'F', wav[2]);
      Assert.Equal((byte)'F', wav[3]);
    }

    // Zero volume returns empty bytes
    var silentWav = SoundService.GenerateWav("Chime", 0.0);
    Assert.Empty(silentWav);

    // Scaling volume produces valid wav
    var quietWav = SoundService.GenerateWav("Chime", 0.2);
    Assert.NotEmpty(quietWav);
    Assert.Equal((byte)'R', quietWav[0]);
  }

  [Fact]
  public void ApplyPresetConfiguresWindowsAndActionsAccurately() {
    // 10/10 lockout preset
    viewModel.ApplyPreset("10/10");
    Assert.Equal(10, viewModel.FirstWindowMinutes);
    Assert.Equal("Lock", viewModel.FirstWindowAction);
    Assert.Equal("Allow", viewModel.MiddleWindowAction);
    Assert.Equal(10, viewModel.LastWindowMinutes);
    Assert.Equal("Lock", viewModel.LastWindowAction);
    Assert.Equal("#F43F5E", viewModel.FirstWindowBadgeColor);
    Assert.Equal("#059669", viewModel.MiddleWindowBadgeColor);
    Assert.Equal("#F43F5E", viewModel.LastWindowBadgeColor);

    // Start and end only preset (middle locked)
    viewModel.ApplyPreset("startend");
    Assert.Equal(10, viewModel.FirstWindowMinutes);
    Assert.Equal("Allow", viewModel.FirstWindowAction);
    Assert.Equal("Lock", viewModel.MiddleWindowAction);
    Assert.Equal(10, viewModel.LastWindowMinutes);
    Assert.Equal("Allow", viewModel.LastWindowAction);
    Assert.Equal("#059669", viewModel.FirstWindowBadgeColor);
    Assert.Equal("#F43F5E", viewModel.MiddleWindowBadgeColor);
    Assert.Equal("#059669", viewModel.LastWindowBadgeColor);

    // Warning windows preset
    viewModel.ApplyPreset("warning");
    Assert.Equal(10, viewModel.FirstWindowMinutes);
    Assert.Equal("Warn", viewModel.FirstWindowAction);
    Assert.Equal("Allow", viewModel.MiddleWindowAction);
    Assert.Equal(10, viewModel.LastWindowMinutes);
    Assert.Equal("Warn", viewModel.LastWindowAction);
    Assert.Equal("#D97706", viewModel.FirstWindowBadgeColor);
    Assert.Equal("#059669", viewModel.MiddleWindowBadgeColor);
    Assert.Equal("#D97706", viewModel.LastWindowBadgeColor);
  }

  [Fact]
  public void PolicyViewModelModeSelectionAndTimelineSummary() {
    // Default mode is Windows
    Assert.Equal("Windows", viewModel.ClassPassPolicyMode);
    Assert.True(viewModel.IsWindowsMode);
    Assert.False(viewModel.IsNoPassesMode);
    Assert.False(viewModel.IsNoRulesMode);

    viewModel.ClassPassPolicyMode = "NoPasses";
    Assert.False(viewModel.IsWindowsMode);
    Assert.True(viewModel.IsNoPassesMode);
    Assert.False(viewModel.IsNoRulesMode);
    Assert.Equal("Passes Locked (100% of period)", viewModel.TimelineSummaryText);

    viewModel.ClassPassPolicyMode = "NoRules";
    Assert.False(viewModel.IsWindowsMode);
    Assert.False(viewModel.IsNoPassesMode);
    Assert.True(viewModel.IsNoRulesMode);
    Assert.Equal("Open Pass Access (No rules)", viewModel.TimelineSummaryText);

    viewModel.BellTimeRulesDisabled = true;
    Assert.Equal("Open Pass Access (No rules)", viewModel.TimelineSummaryText);
    viewModel.BellTimeRulesDisabled = false;

    viewModel.ClassPassPolicyMode = "Windows";
    viewModel.FirstWindowMinutes = 7;
    viewModel.FirstWindowAction = "Warn";
    viewModel.MiddleWindowAction = "Lock";
    viewModel.LastWindowMinutes = 8;
    viewModel.LastWindowAction = "Allow";

    Assert.Contains("First 7m (Warn)", viewModel.TimelineSummaryText);
    Assert.Contains("Middle (Lock)", viewModel.TimelineSummaryText);
    Assert.Contains("Last 8m (Allow)", viewModel.TimelineSummaryText);
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

  [Fact]
  public void RenameProfileStateTogglesAndCancelsCorrectly() {
    Assert.False(viewModel.IsRenamingProfile);
    Assert.Empty(viewModel.RenameProfileName);

    viewModel.ToggleRenameProfile("Room 101");
    Assert.True(viewModel.IsRenamingProfile);
    Assert.Equal("Room 101", viewModel.RenameProfileName);
    Assert.False(viewModel.IsCreatingProfile);

    // Toggling create cancels rename
    viewModel.ToggleCreateProfile();
    Assert.False(viewModel.IsRenamingProfile);
    Assert.True(viewModel.IsCreatingProfile);

    // Toggling rename cancels create
    viewModel.ToggleRenameProfile("Room 202");
    Assert.True(viewModel.IsRenamingProfile);
    Assert.Equal("Room 202", viewModel.RenameProfileName);
    Assert.False(viewModel.IsCreatingProfile);

    // Cancel
    viewModel.CancelRenameProfile();
    Assert.False(viewModel.IsRenamingProfile);
    Assert.Empty(viewModel.RenameProfileName);
  }

  [Fact]
  public void StatusMessageTriggersHasStatusMessage() {
    Assert.False(viewModel.HasStatusMessage);

    viewModel.StatusMessage = "Workspace renamed to \"Chemistry AP\".";
    Assert.True(viewModel.HasStatusMessage);

    viewModel.StatusMessage = "";
    Assert.False(viewModel.HasStatusMessage);
  }
}
