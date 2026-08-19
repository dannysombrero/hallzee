using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class SyncSessionTests {
  [Fact]
  public void RepositoryCreatesTheCsvHeaderOnFirstRun() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var csv = Path.Combine(folder, "trips.csv");

    try {
      _ = new TripCsvRepository(csv);
      Assert.Equal(new[] { TripCsvRepository.Header }, File.ReadAllLines(csv));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void BuffersPartialLinesAndRecognizesTerminalReadiness() {
    var session = new SyncSession(new RecordingRepository());
    session.Start();

    var partial = session.ProcessReceivedData("BATHROOM_TERMINAL_");
    var complete = session.ProcessReceivedData("READY\n");

    Assert.Null(partial.Status);
    Assert.Equal(SyncStatus.Synchronizing, complete.Status);
  }

  [Fact]
  public void SavesNewTripsAcknowledgesDuplicatesAndCompletesFullSync() {
    var repository = new RecordingRepository();
    var session = new SyncSession(repository);
    session.Start();

    var first = session.ProcessReceivedData("TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0\n");
    var duplicate = session.ProcessReceivedData("TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0\n");
    var requestFullHistory = session.ProcessReceivedData("SYNC_END\n");
    var complete = session.ProcessReceivedData("SYNC_END\n");

    Assert.Equal(new[] { "ACK,7" }, first.OutboundCommands);
    Assert.Equal(new[] { "ACK,7" }, duplicate.OutboundCommands);
    Assert.Single(repository.StoredPayloads);
    Assert.Equal(new[] { "SYNC_ALL" }, requestFullHistory.OutboundCommands);
    Assert.Equal(SyncStatus.Complete, complete.Status);
    Assert.Equal(1, complete.SavedTripCount);
  }

  [Fact]
  public void RejectsMalformedTripsWithoutAcknowledgingThem() {
    var session = new SyncSession(new RecordingRepository());
    session.Start();

    var update = session.ProcessReceivedData("TRIP,invalid\nUNKNOWN_MESSAGE\n");

    Assert.Empty(update.OutboundCommands);
    Assert.Contains("Ignored malformed trip record.", update.Logs);
  }

  [Fact]
  public void RepositoryCreatesHeaderImportsExistingIdsAndWritesOnlyNewRecords() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var csv = Path.Combine(folder, "trips.csv");

    try {
      Directory.CreateDirectory(folder);
      File.WriteAllText(csv, TripCsvRepository.Header + "\n4,OLD,2026-01-01,08:00:00,08:10:00,600,COMPLETE\n");
      var repository = new TripCsvRepository(csv);

      Assert.Equal(TripStoreResult.Duplicate, repository.Store("4,OLD,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"));
      Assert.Equal(TripStoreResult.Saved, repository.Store("5,NEW,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"));
      Assert.Equal(TripStoreResult.Invalid, repository.Store("0,INVALID"));
      Assert.Equal(new[] {
        TripCsvRepository.Header,
        "4,OLD,2026-01-01,08:00:00,08:10:00,600,COMPLETE",
        "5,NEW,2026-01-01,09:00:00,09:10:00,600,COMPLETE"
      }, File.ReadAllLines(csv));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  sealed class RecordingRepository : ITripRepository {
    readonly HashSet<string> ids = new();
    public List<string> StoredPayloads { get; } = new();

    public TripStoreResult Store(string payload) {
      var separator = payload.IndexOf(',');
      if (separator <= 0 || !long.TryParse(payload[..separator], out var tripId) || tripId <= 0) {
        return TripStoreResult.Invalid;
      }
      if (!ids.Add(payload[..separator])) return TripStoreResult.Duplicate;
      StoredPayloads.Add(payload);
      return TripStoreResult.Saved;
    }
  }
}
