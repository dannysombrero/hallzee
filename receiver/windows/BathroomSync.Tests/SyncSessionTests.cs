using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class SyncSessionTests {
  [Fact]
  public void RepositoryCreatesTheCsvHeaderOnFirstRun() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var csv = Path.Combine(folder, "trips.csv");

    try {
      var repository = new TripSqliteRepository(Path.Combine(folder, "trips.db"));
      repository.ExportCsv(csv);
      Assert.Equal(new[] { TripSqliteRepository.CsvHeader }, File.ReadAllLines(csv));
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
  public void DoesNotAcknowledgeATripWhenStorageIsUnavailable() {
    var session = new SyncSession(new UnavailableRepository());
    session.Start();

    var update = session.ProcessReceivedData("TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0\n");

    Assert.True(update.StorageUnavailable);
    Assert.Empty(update.OutboundCommands);
    Assert.Contains("Trip was not acknowledged because the CSV could not be saved.", update.Logs);
  }

  [Fact]
  public void RepositoryImportsLegacyCsvAndExportsSortedRecords() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var csv = Path.Combine(folder, "trips.csv");

    try {
      Directory.CreateDirectory(folder);
      File.WriteAllText(csv, TripSqliteRepository.CsvHeader + "\n4,OLD,2026-01-01,08:00:00,08:10:00,600,COMPLETE\n");
      var repository = new TripSqliteRepository(Path.Combine(folder, "trips.db"), csv);

      Assert.Equal(TripStoreResult.Duplicate, repository.Store("4,OLD,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"));
      Assert.Equal(TripStoreResult.Saved, repository.Store("10,TEN,2026-01-01,10:00:00,10:10:00,600,COMPLETE,0"));
      Assert.Equal(TripStoreResult.Saved, repository.Store("9,NINE,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"));
      Assert.Equal(TripStoreResult.Invalid, repository.Store("0,INVALID"));
      var exported = Path.Combine(folder, "exported.csv");
      repository.ExportCsv(exported);
      Assert.Equal(new[] {
        TripSqliteRepository.CsvHeader,
        "4,OLD,2026-01-01,08:00:00,08:10:00,600,COMPLETE",
        "9,NINE,2026-01-01,09:00:00,09:10:00,600,COMPLETE",
        "10,TEN,2026-01-01,10:00:00,10:10:00,600,COMPLETE"
      }, File.ReadAllLines(exported));

      var savedCopy = Path.Combine(folder, "saved-copy.csv");
      repository.ExportCsv(savedCopy);
      Assert.Equal(File.ReadAllLines(exported), File.ReadAllLines(savedCopy));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void RepositoryExportsRecordsInNumericTripIdOrder() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var csv = Path.Combine(folder, "trips.csv");

    try {
      Directory.CreateDirectory(folder);
      File.WriteAllText(csv, TripSqliteRepository.CsvHeader + "\n10,TEN,2026-01-01,10:00:00,10:10:00,600,COMPLETE\n9,NINE,2026-01-01,09:00:00,09:10:00,600,COMPLETE\n");
      var repository = new TripSqliteRepository(Path.Combine(folder, "trips.db"), csv);

      repository.ExportCsv(csv);

      Assert.Equal(new[] {
        TripSqliteRepository.CsvHeader,
        "9,NINE,2026-01-01,09:00:00,09:10:00,600,COMPLETE",
        "10,TEN,2026-01-01,10:00:00,10:10:00,600,COMPLETE"
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

  sealed class UnavailableRepository : ITripRepository {
    public TripStoreResult Store(string payload) => TripStoreResult.Unavailable;
  }
}
