using System.Text.Json;
using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

// Literal vectors shared with Web Crypto tests. Never generate expected proofs here.
public sealed class WebClientContractTests {
  static JsonDocument Fixture(string name) => JsonDocument.Parse(File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "web-contracts", name + ".json")));

  [Fact]
  public void IdentityProofsMatchSharedLiteralVectors() {
    using var fixture = Fixture("identity");
    foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
      string Field(string name) => row.GetProperty(name).GetString()!;
      var id = Field("terminalId");
      var client = Field("clientId");
      var identity = new TerminalIdentity(id, id[^4..], false, Field("nonce"), false);
      Assert.Equal(Field("claimProof"), TerminalIdentityProtocol.ComputeClaimProof(Field("passkey"), client.ToLowerInvariant(), identity));
      var key = Convert.ToHexString(TerminalIdentityProtocol.DeriveOwnerKey(Field("passkey"), id, client));
      Assert.Equal(Field("ownerKeyHex"), key);
      Assert.Equal(Field("authProof"), TerminalIdentityProtocol.ComputeAuthProof(key, id, client, Field("nonce")));
      Assert.Equal(Field("commitProof"), TerminalIdentityProtocol.ComputeAuthProof(key, id, client, Field("commitNonce")));
    }
  }

  [Fact]
  public void ActiveSnapshotsMatchSharedFixture() {
    using var fixture = Fixture("active-passes");
    foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
      var line = row.GetProperty("line").GetString()!;
      var count = row.GetProperty("count").GetInt32();
      if (line.StartsWith("ACTIVE_PASSES")) {
        Assert.True(ActivePassProtocol.TryParseActivePassesResponse(line, out var passes));
        Assert.Equal(count, passes!.Count);
      } else {
        Assert.True(ActivePassProtocol.TryParseActivePassResponse(line, out var pass));
        Assert.Equal(count == 0 ? ActivePassStatus.Available : ActivePassStatus.Occupied, pass!.Status);
      }
    }
  }

  [Fact]
  public void RosterNamesAndLeadingZerosMatchSharedFixture() {
    using var fixture = Fixture("roster");
    foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
      var parsed = RosterCsvParser.ParseAuto(new StringReader(row.GetProperty("csv").GetString()!), "w");
      var student = Assert.Single(parsed.ValidStudents);
      Assert.Equal(row.GetProperty("studentId").GetString(), student.StudentId);
      Assert.Equal(row.GetProperty("firstName").GetString(), student.FirstName);
      Assert.Equal(row.GetProperty("lastName").GetString(), student.LastName);
    }
  }

  [Fact]
  public void PolicyDecisionsAndWireBytesMatchSharedFixture() {
    using var fixture = Fixture("policies");
    var periods = new[] {new BellSchedulePeriod("p", "w", "One", "08:00", "09:00", "Mon")};
    var rule = new PolicyRule("r", "w", LockoutStartMinutes: 10, LockoutEndMinutes: 5,
      FirstWindowAction: "Lock", LastWindowAction: "Warn", TerminalEnforcementEnabled: true);
    var service = new PolicyScheduleService();
    foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray()) {
      var at = new DateTime(2026, 9, 7, row.GetProperty("hour").GetInt32(), row.GetProperty("minute").GetInt32(), 0);
      Assert.Equal(row.GetProperty("decision").GetString(), service.Evaluate(at, service.ResolvePeriod(at, periods), rule).ToString());
    }
    Assert.Contains(fixture.RootElement.GetProperty("transfer").GetString(),
      BellPolicyProtocol.BuildTransfer(new DateTime(2026, 9, 7), rule, periods, Array.Empty<ScheduleException>()));
  }
}
