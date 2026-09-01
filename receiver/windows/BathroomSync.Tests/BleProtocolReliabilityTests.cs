using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class BleProtocolReliabilityTests {
  const string Trip7 = "7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0";

  [Fact]
  public void AcceptsVersionedReadyAndCanStartWhenReadyNotificationWasMissed() {
    var session = new SyncSession(new DurableRepository());
    session.Start();

    Assert.Equal(
      SyncStatus.Synchronizing,
      session.ProcessReceivedData("HALLZEE_READY,1\n").Status
    );

    session.Start();
    var update = session.ProcessReceivedData("SYNC_BEGIN,0\nSYNC_END\n");
    Assert.Equal(SyncStatus.Complete, update.Status);
  }

  [Fact]
  public void ReassemblesSettingsResponsesAndReportsApplyErrors() {
    var session = new SyncSession(new DurableRepository());
    session.Start();

    var partial = session.ProcessReceivedData("SETTINGS,MAX_ID_LENG");
    var current = session.ProcessReceivedData("TH,10\n");
    var applied = session.ProcessReceivedData("SETTINGS_ACK,MAX_ID_LENGTH,8\n");
    var rejected = session.ProcessReceivedData(
      "SETTINGS_ERROR,MAX_ID_LENGTH,ACTIVE_ID_TOO_LONG\n"
    );

    Assert.Null(partial.MaxStudentIdLength);
    Assert.Equal(10, current.MaxStudentIdLength);
    Assert.False(current.SettingsApplied);
    Assert.Equal(8, applied.MaxStudentIdLength);
    Assert.True(applied.SettingsApplied);
    Assert.Equal("ACTIVE_ID_TOO_LONG", rejected.SettingsError);
  }

  [Fact]
  public void BuildsOnlyValidStudentIdSettingCommands() {
    Assert.Equal(
      "SET,MAX_ID_LENGTH,8",
      KioskSettingsProtocol.BuildStudentIdLengthCommand(8)
    );
    Assert.Throws<ArgumentOutOfRangeException>(
      () => KioskSettingsProtocol.BuildStudentIdLengthCommand(3)
    );
    Assert.Throws<ArgumentOutOfRangeException>(
      () => KioskSettingsProtocol.BuildStudentIdLengthCommand(17)
    );
  }

  [Fact]
  public void ReassemblesLargeHistoryDeliveredInTwentyByteChunks() {
    var repository = new DurableRepository();
    var session = new SyncSession(repository);
    session.Start();
    var wireText = new System.Text.StringBuilder("SYNC_BEGIN,250\n");
    for (var id = 1; id <= 250; id++) {
      wireText.Append($"TRIP,{id},ID{id},2026-01-01,08:00:00,08:10:00,600,COMPLETE,0\n");
    }
    wireText.Append("SYNC_END\n");

    var acknowledgements = new List<string>();
    SyncUpdate? last = null;
    for (var offset = 0; offset < wireText.Length; offset += 20) {
      var length = Math.Min(20, wireText.Length - offset);
      last = session.ProcessReceivedData(wireText.ToString(offset, length));
      acknowledgements.AddRange(last.OutboundCommands);
    }

    Assert.Equal(250, repository.Count);
    Assert.Equal(250, acknowledgements.Count);
    Assert.Equal("ACK,1", acknowledgements[0]);
    Assert.Equal("ACK,250", acknowledgements[^1]);
    Assert.Equal(SyncStatus.Complete, last?.Status);
    Assert.Equal(250, last?.SavedTripCount);
  }

  [Fact]
  public void RestartAfterInterruptedTransferSafelyAcknowledgesDuplicate() {
    var repository = new DurableRepository();
    var session = new SyncSession(repository);
    session.Start();

    var first = session.ProcessReceivedData($"SYNC_BEGIN,2\nTRIP,{Trip7}\n");
    Assert.Equal(new[] { "ACK,7" }, first.OutboundCommands);

    // Simulate a dropped connection before the kiosk receives the ACK.
    session.Start();
    var retry = session.ProcessReceivedData($"SYNC_BEGIN,2\nTRIP,{Trip7}\n");
    Assert.Equal(new[] { "ACK,7" }, retry.OutboundCommands);
    Assert.Equal(1, repository.Count);
  }

  [Fact]
  public void PartialCommandDataIsDiscardedWhenANewSessionStarts() {
    var session = new SyncSession(new DurableRepository());
    session.Start();
    session.ProcessReceivedData("TRIP,7,ID1");
    session.Start();

    var update = session.ProcessReceivedData("SYNC_BEGIN,0\nSYNC_END\n");
    Assert.Empty(update.OutboundCommands);
    Assert.Equal(SyncStatus.Complete, update.Status);
  }

  sealed class DurableRepository : ITripRepository {
    readonly HashSet<long> ids = new();
    public int Count => ids.Count;

    public TripStoreResult Store(string payload) {
      var separator = payload.IndexOf(',');
      if (separator <= 0 || !long.TryParse(payload[..separator], out var id) || id <= 0)
        return TripStoreResult.Invalid;
      return ids.Add(id) ? TripStoreResult.Saved : TripStoreResult.Duplicate;
    }
  }
}
