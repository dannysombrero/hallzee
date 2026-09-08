using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class WorkspaceTransferTests : IDisposable {
  readonly string folder = Path.Combine(Path.GetTempPath(), "HallzeeWorkspaceTests", Guid.NewGuid().ToString("N"));

  [Fact]
  public void RoundTripCreatesIndependentWorkspaceWithRulesSchedulesAndExceptions() {
    var source = new ProfileAndPolicySqliteRepository(Path.Combine(folder, "source.db"));
    var original = source.GetActiveProfile()!;
    source.SavePolicyRule(new PolicyRule("rule-default", "default", 3, 600, 4, 5, 7, "Lock", "Allow", "Chime", true));
    source.SaveBellSchedule("default", new[] {
      new BellSchedulePeriod("schedule-1", "default", "Science", "08:00 AM", "08:45 AM", "Mon,Tue", "Regular", "7A"),
      new BellSchedulePeriod("schedule-2", "default", "Science", "09:00 AM", "09:30 AM", "", "Late start", "7A")
    });
    source.SaveScheduleExceptions("default", new[] {
      new ScheduleException("exception-1", "default", "2026-09-10", "Late start"),
      new ScheduleException("exception-2", "default", "2026-09-11", "", true)
    });
    var json = WorkspaceTransfer.Export(original, source);
    var target = new ProfileAndPolicySqliteRepository(Path.Combine(folder, "target.db"));
    var imported = target.ImportWorkspace(WorkspaceTransfer.Parse(json));
    var another = target.ImportWorkspace(WorkspaceTransfer.Parse(json));
    Assert.NotEqual(original.ProfileId, imported.ProfileId);
    Assert.NotEqual(imported.ProfileId, another.ProfileId);
    Assert.Equal("default", target.GetActiveProfile()!.ProfileId);
    Assert.Equal(source.GetPolicyRule("default") with { ProfileId = imported.ProfileId, RuleId = $"rule-{imported.ProfileId}" }, target.GetPolicyRule(imported.ProfileId));
    var periods = target.GetBellSchedule(imported.ProfileId);
    Assert.Equal(2, periods.Count);
    Assert.All(periods, p => { Assert.Equal("7A", p.ClassSection); Assert.Equal(imported.ProfileId, p.ProfileId); });
    Assert.DoesNotContain(periods, p => p.ScheduleId == "schedule-1");
    Assert.Equal(2, target.GetScheduleExceptions(imported.ProfileId).Count);
    Assert.Null(target.GetAssignedTerminalId(imported.ProfileId));
    Assert.DoesNotContain(target.GetAllTerminals(), t => t.ClaimStatus == "CLAIMED");
    Assert.Empty(new RosterSqliteRepository(Path.Combine(folder, "target.db")).GetRoster(imported.ProfileId));
  }

  [Fact]
  public void InvalidOrUnsupportedImportLeavesExistingWorkspaceUntouched() {
    var repo = new ProfileAndPolicySqliteRepository(Path.Combine(folder, "test.db"));
    var package = WorkspaceTransfer.Parse(WorkspaceTransfer.Export(repo.GetActiveProfile()!, repo));
    Assert.Throws<ArgumentException>(() => repo.ImportWorkspace(package with { Version = 99 }));
    Assert.Throws<ArgumentException>(() => repo.ImportWorkspace(package with { Rule = package.Rule with { MaxSimultaneousPasses = 0 } }));
    Assert.Throws<ArgumentException>(() => repo.ImportWorkspace(package with { Periods = new[] {
      new BellSchedulePeriod("x", "default", "Class", "invalid", "09:00 AM")
    } }));
    Assert.Single(repo.GetAllProfiles());
    Assert.Equal("default", repo.GetActiveProfile()!.ProfileId);
  }

  public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}
