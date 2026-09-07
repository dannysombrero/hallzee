using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class PolicyScheduleServiceTests {
  readonly PolicyScheduleService service = new();

  [Fact]
  public void DateExceptionSelectsAlternateScheduleAndClassSection() {
    var periods = new[] {
      new BellSchedulePeriod("regular", "teacher", "Period 1", "08:00", "09:00", ScheduleName: "Regular", ClassSection: "Biology 1"),
      new BellSchedulePeriod("early", "teacher", "Period 1", "08:30", "09:10", ScheduleName: "Early Release", ClassSection: "Biology 1")
    };
    var exceptions = new[] { new ScheduleException("e1", "teacher", "2026-09-09", "Early Release") };

    var resolved = service.ResolvePeriod(new DateTime(2026, 9, 9, 8, 45, 0), periods, exceptions);

    Assert.NotNull(resolved);
    Assert.Equal("Early Release", resolved.ScheduleName);
    Assert.Equal("Biology 1", resolved.ClassSection);
    Assert.Equal("early", resolved.Period.ScheduleId);
  }

  [Fact]
  public void NoSchoolExceptionReturnsNoPeriod() {
    var periods = new[] { new BellSchedulePeriod("regular", "teacher", "Period 1", "08:00", "09:00") };
    var exceptions = new[] { new ScheduleException("e1", "teacher", "2026-09-09", "", true) };
    Assert.Null(service.ResolvePeriod(new DateTime(2026, 9, 9, 8, 30, 0), periods, exceptions));
  }

  [Fact]
  public void NamedTemplateCanBeAssignedToAWeekdayWithoutProfileSwitching() {
    var periods = new[] {
      new BellSchedulePeriod("regular", "teacher", "Period 1", "08:00", "09:00", "Mon,Tue,Thu,Fri", "Regular"),
      new BellSchedulePeriod("wednesday", "teacher", "Block 1", "08:30", "10:00", "Wed", "Wednesday", "Chemistry 3")
    };
    var resolved = service.ResolvePeriod(new DateTime(2026, 9, 9, 9, 0, 0), periods);
    Assert.NotNull(resolved);
    Assert.Equal("Wednesday", resolved.ScheduleName);
    Assert.Equal("Chemistry 3", resolved.ClassSection);
  }

  [Theory]
  [InlineData(8, 5, BellWindowDecision.Lock)]
  [InlineData(8, 30, BellWindowDecision.Allow)]
  [InlineData(8, 55, BellWindowDecision.Warn)]
  public void FirstAndLastWindowActionsAreEvaluated(int hour, int minute, BellWindowDecision expected) {
    var period = new BellSchedulePeriod("regular", "teacher", "Period 1", "08:00", "09:00");
    var moment = new DateTime(2026, 9, 8, hour, minute, 0);
    var resolved = service.ResolvePeriod(moment, new[] { period });
    var rule = new PolicyRule("rule", "teacher", FirstWindowAction: "Lock", LastWindowAction: "Warn");
    Assert.Equal(expected, service.Evaluate(moment, resolved, rule));
  }

  [Fact]
  public void TerminalTransferIsOffByDefaultAndUsesResolvedDateWindowsWhenEnabled() {
    var periods = new[] { new BellSchedulePeriod("p", "teacher", "Period 1", "08:00", "09:00", "Mon,Tue,Wed,Thu,Fri") };
    var disabled = BellPolicyProtocol.BuildTransfer(new DateTime(2026, 9, 7), new PolicyRule("r", "teacher"), periods, Array.Empty<ScheduleException>());
    Assert.Equal(new[] { "POLICY_BEGIN,0", "POLICY_COMMIT,0" }, disabled);

    var enabledRule = new PolicyRule("r", "teacher", LockoutStartMinutes: 10, LockoutEndMinutes: 5,
      FirstWindowAction: "Lock", LastWindowAction: "Warn", TerminalEnforcementEnabled: true);
    var enabled = BellPolicyProtocol.BuildTransfer(new DateTime(2026, 9, 7), enabledRule, periods, Array.Empty<ScheduleException>());
    Assert.Contains("POLICY_WINDOW,20260907,480,540,490,535,2,1", enabled);
    Assert.Equal("POLICY_BEGIN,1", enabled[0]);
    Assert.StartsWith("POLICY_COMMIT,", enabled[^1]);
  }
}
