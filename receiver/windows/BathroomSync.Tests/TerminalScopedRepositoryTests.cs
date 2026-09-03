using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class TerminalScopedRepositoryTests {
  [Fact]
  public void SameTripIdFromTwoTerminalsDoesNotCollide() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "trips.db");

    try {
      Directory.CreateDirectory(folder);
      var repository = new TripSqliteRepository(dbPath);
      const string payload = "1,10482,2026-09-02,09:00:00,09:05:00,300,COMPLETED";

      Assert.Equal(TripStoreResult.Saved, repository.Store("HZ-A1B2C3D4E5F6", payload));
      Assert.Equal(TripStoreResult.Saved, repository.Store("HZ-112233445566", payload));
      Assert.Equal(1, repository.GetLatestTripId("HZ-A1B2C3D4E5F6"));
      Assert.Equal(1, repository.GetLatestTripId("HZ-112233445566"));

      var first = repository.GetRecentTrips("HZ-A1B2C3D4E5F6", 10);
      var second = repository.GetRecentTrips("HZ-112233445566", 10);
      Assert.Single(first);
      Assert.Single(second);
      Assert.Equal("HZ-A1B2C3D4E5F6", first[0].TerminalId);
      Assert.Equal("HZ-112233445566", second[0].TerminalId);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void ClientIdentityIsStableAndProfileAssignmentsCanShareATerminal() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "profiles.db");

    try {
      Directory.CreateDirectory(folder);
      var repository = new ProfileAndPolicySqliteRepository(dbPath);
      var firstClientId = repository.GetOrCreateClientId();
      var secondClientId = repository.GetOrCreateClientId();
      Assert.Equal(firstClientId, secondClientId);
      Assert.True(Guid.TryParse(firstClientId, out _));

      repository.SaveTerminal(new TerminalDeviceConfig(
        TerminalId: "HZ-A1B2C3D4E5F6",
        CustomName: "East Door",
        TransportId: "transport-1",
        ProtocolVersion: 2,
        ClaimStatus: "OWNED"
      ));
      repository.AssignTerminalToProfile("default", "HZ-A1B2C3D4E5F6");

      Assert.Equal("HZ-A1B2C3D4E5F6", repository.GetAssignedTerminalId("default"));
      var terminal = repository.GetTerminal("HZ-A1B2C3D4E5F6");
      Assert.NotNull(terminal);
      Assert.Equal("transport-1", terminal!.TransportId);
      Assert.Equal(2, terminal.ProtocolVersion);
      Assert.Equal("OWNED", terminal.ClaimStatus);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }
}
