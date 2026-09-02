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
        AlertSound: "Bell"
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

      // Bell Schedule
      var periods = new[] {
        new BellSchedulePeriod("p1", "default", "Period 1", "08:30", "09:25", "Mon,Tue,Thu,Fri", "Regular"),
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

      Assert.Empty(repo.GetAllTerminals());

      var terminal = new TerminalDeviceConfig(
        TerminalId: "hallzee_01",
        CustomName: "Door Kiosk 204",
        BleAddress: "AA:BB:CC:DD:EE:FF",
        LastSeenAt: DateTime.UtcNow,
        MaxIdLength: 8
      );
      repo.SaveTerminal(terminal);

      var list = repo.GetAllTerminals();
      Assert.Single(list);
      Assert.Equal("Door Kiosk 204", list[0].CustomName);
      Assert.Equal(8, list[0].MaxIdLength);

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
      Assert.Empty(repo.GetAllTerminals());
      Assert.Null(repo.GetTerminal("hallzee_01"));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }
}
