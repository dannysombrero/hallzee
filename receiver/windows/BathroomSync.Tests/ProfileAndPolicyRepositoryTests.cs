using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class ProfileAndPolicyRepositoryTests {
  [Fact]
  public void ProfileCrudAndActiveProfileSwitchingWork() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "profile_test.db");

    try {
      Directory.CreateDirectory(folder);
      var repo = new ProfileAndPolicySqliteRepository(dbPath);

      // Default profile should exist and be active
      var active = repo.GetActiveProfile();
      Assert.NotNull(active);
      Assert.Equal("default", active.ProfileId);
      Assert.True(active.IsActive);

      // Add new profile
      repo.SaveProfile(new ClassroomProfile("p1", "Period 1 Math", IsActive: false));
      var all = repo.GetAllProfiles();
      Assert.Equal(2, all.Count);

      // Switch active profile
      repo.SetActiveProfile("p1");
      active = repo.GetActiveProfile();
      Assert.NotNull(active);
      Assert.Equal("p1", active.ProfileId);

      var syncTime = new DateTime(2026, 9, 7, 14, 30, 0, DateTimeKind.Utc);
      repo.SaveLastSuccessfulSync("p1", syncTime);
      Assert.Equal(syncTime, repo.GetLastSuccessfulSync("p1"));

      // Delete custom profile
      repo.DeleteProfile("p1");
      all = repo.GetAllProfiles();
      Assert.Single(all);
      Assert.Equal("default", all[0].ProfileId);

      // Default profile deletion is protected
      repo.DeleteProfile("default");
      all = repo.GetAllProfiles();
      Assert.Single(all);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void PolicyRuleAndBellScheduleCrudWork() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "policy_test.db");

    try {
      Directory.CreateDirectory(folder);
      var repo = new ProfileAndPolicySqliteRepository(dbPath);

      // Default policy rule exists
      var policy = repo.GetPolicyRule("default");
      Assert.Equal(1, policy.MaxSimultaneousPasses);
      Assert.Equal(420, policy.DurationWarningSeconds);
      Assert.Equal(2, policy.MaxDailyPassesPerStudent);
      Assert.Equal(10, policy.LockoutStartMinutes);
      Assert.Equal(10, policy.LockoutEndMinutes);
      Assert.Equal("Windows", policy.ClassPassPolicyMode);
      Assert.Equal("Allow", policy.MiddleWindowAction);
      Assert.False(policy.BellTimeRulesDisabled);
      Assert.True(policy.BellTransitionEnabled);
      Assert.True(policy.WarningSoundEnabled);
      Assert.Equal(80, policy.WarningSoundVolume);

      // Update policy rule
      var customPolicy = new PolicyRule(
        RuleId: "custom_rule",
        ProfileId: "default",
        MaxSimultaneousPasses: 2,
        DurationWarningSeconds: 600,
        MaxDailyPassesPerStudent: 3,
        LockoutStartMinutes: 5,
        LockoutEndMinutes: 5,
        FirstWindowAction: "Lock",
        LastWindowAction: "Allow",
        AlertSound: "Bell",
        TerminalEnforcementEnabled: true,
        ClassPassPolicyMode: "NoPasses",
        MiddleWindowAction: "Lock",
        BellTimeRulesDisabled: true,
        BellTransitionEnabled: false,
        WarningSoundEnabled: false,
        WarningSoundVolume: 45
      );
      repo.SavePolicyRule(customPolicy);

      var updatedPolicy = repo.GetPolicyRule("default");
      Assert.Equal(2, updatedPolicy.MaxSimultaneousPasses);
      Assert.Equal(600, updatedPolicy.DurationWarningSeconds);
      Assert.Equal(3, updatedPolicy.MaxDailyPassesPerStudent);
      Assert.Equal(5, updatedPolicy.LockoutStartMinutes);
      Assert.Equal(5, updatedPolicy.LockoutEndMinutes);
      Assert.Equal("Lock", updatedPolicy.FirstWindowAction);
      Assert.Equal("Allow", updatedPolicy.LastWindowAction);
      Assert.Equal("Bell", updatedPolicy.AlertSound);
      Assert.True(updatedPolicy.TerminalEnforcementEnabled);
      Assert.Equal("NoPasses", updatedPolicy.ClassPassPolicyMode);
      Assert.Equal("Lock", updatedPolicy.MiddleWindowAction);
      Assert.True(updatedPolicy.BellTimeRulesDisabled);
      Assert.False(updatedPolicy.BellTransitionEnabled);
      Assert.False(updatedPolicy.WarningSoundEnabled);
      Assert.Equal(45, updatedPolicy.WarningSoundVolume);

      // Bell Schedule
      var periods = new[] {
        new BellSchedulePeriod("p1", "default", "Period 1", "08:30", "09:25", "Mon,Tue,Thu,Fri", "Regular", "Chemistry 1"),
        new BellSchedulePeriod("p2", "default", "Period 2", "09:30", "10:25", "1,2,3,4,5")
      };
      repo.SaveBellSchedule("default", periods);

      var savedPeriods = repo.GetBellSchedule("default");
      Assert.Equal(2, savedPeriods.Count);
      Assert.Equal("Period 1", savedPeriods[0].PeriodName);
      Assert.Equal("08:30", savedPeriods[0].StartTime);
      Assert.Equal("Period 2", savedPeriods[1].PeriodName);
      Assert.Equal("Regular", savedPeriods[0].ScheduleName);
      Assert.Equal("Mon,Tue,Thu,Fri", savedPeriods[0].DaysOfWeek);
      Assert.Equal("Chemistry 1", savedPeriods[0].ClassSection);

      repo.SaveScheduleExceptions("default", new[] {
        new ScheduleException("ex1", "default", "2026-09-09", "Early Release"),
        new ScheduleException("ex2", "default", "2026-09-10", "", true)
      });
      var exceptions = repo.GetScheduleExceptions("default");
      Assert.Equal(2, exceptions.Count);
      Assert.Equal("Early Release", exceptions[0].ScheduleName);
      Assert.True(exceptions[1].IsNoSchool);

      // Re-saving replaces schedule
      repo.SaveBellSchedule("default", new[] {
        new BellSchedulePeriod("p_single", "default", "Block 1", "08:00", "10:00", "1,3,5")
      });
      var replaced = repo.GetBellSchedule("default");
      Assert.Single(replaced);
      Assert.Equal("Block 1", replaced[0].PeriodName);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void TerminalConfigurationCrudWorks() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "terminal_test.db");

    try {
      Directory.CreateDirectory(folder);
      var repo = new ProfileAndPolicySqliteRepository(dbPath);

      var legacyTerminals = repo.GetAllTerminals();
      Assert.Single(legacyTerminals);
      Assert.Equal("LEGACY-DEFAULT", legacyTerminals[0].TerminalId);

      var terminal = new TerminalDeviceConfig(
        TerminalId: "hallzee_01",
        CustomName: "Door Kiosk 204",
        BleAddress: "AA:BB:CC:DD:EE:FF",
        LastSeenAt: DateTime.UtcNow,
        MaxIdLength: 8
      );
      repo.SaveTerminal(terminal);

      var list = repo.GetAllTerminals();
      Assert.Equal(2, list.Count);
      var savedTerminal = Assert.Single(list, item => item.TerminalId == "hallzee_01");
      Assert.Equal("Door Kiosk 204", savedTerminal.CustomName);
      Assert.Equal(8, savedTerminal.MaxIdLength);

      var fetched = repo.GetTerminal("hallzee_01");
      Assert.NotNull(fetched);
      Assert.Equal("AA:BB:CC:DD:EE:FF", fetched.BleAddress);

      // Update terminal
      repo.SaveTerminal(terminal with { CustomName = "Room 204 (Front)" });
      fetched = repo.GetTerminal("hallzee_01");
      Assert.NotNull(fetched);
      Assert.Equal("Room 204 (Front)", fetched.CustomName);

      // Delete terminal
      repo.DeleteTerminal("hallzee_01");
      Assert.Single(repo.GetAllTerminals());
      Assert.Equal("LEGACY-DEFAULT", repo.GetAllTerminals()[0].TerminalId);
      Assert.Null(repo.GetTerminal("hallzee_01"));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }
}
