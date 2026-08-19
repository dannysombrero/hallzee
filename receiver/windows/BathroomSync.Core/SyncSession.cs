using System.Text;

namespace BathroomSync.Core;

public enum SyncStatus {
  WaitingForTerminal,
  Synchronizing,
  Complete
}

public sealed class SyncUpdate {
  public List<string> Logs { get; } = new();
  public List<string> OutboundCommands { get; } = new();
  public SyncStatus? Status { get; set; }
  public int? SavedTripCount { get; set; }
  public bool StorageUnavailable { get; set; }
}

public sealed class SyncSession {
  private readonly ITripRepository repository;
  private readonly StringBuilder input = new();
  private bool requestedFullHistory;
  private int savedTripCount;

  public SyncSession(ITripRepository repository) {
    this.repository = repository;
  }

  public void Start() {
    input.Clear();
    requestedFullHistory = false;
    savedTripCount = 0;
  }

  public SyncUpdate ProcessReceivedData(string text) {
    var update = new SyncUpdate();
    input.Append(text);

    while (true) {
      var newlineIndex = input.ToString().IndexOf('\n');
      if (newlineIndex < 0) {
        return update;
      }

      var line = input.ToString(0, newlineIndex).Trim();
      input.Remove(0, newlineIndex + 1);
      ProcessLine(line, update);
    }
  }

  private void ProcessLine(string line, SyncUpdate update) {
    update.Logs.Add($"ESP32: {line}");

    if (line == "BATHROOM_TERMINAL_READY") {
      update.Status = SyncStatus.Synchronizing;
      return;
    }

    if (line.StartsWith("TRIP,", StringComparison.Ordinal)) {
      StoreTrip(line[5..], update);
      return;
    }

    if (line != "SYNC_END") {
      return;
    }

    if (!requestedFullHistory) {
      requestedFullHistory = true;
      update.OutboundCommands.Add("SYNC_ALL");
      update.Logs.Add("Requesting full history...");
      return;
    }

    update.Status = SyncStatus.Complete;
    update.SavedTripCount = savedTripCount;
  }

  private void StoreTrip(string payload, SyncUpdate update) {
    var result = repository.Store(payload);
    if (result == TripStoreResult.Invalid) {
      update.Logs.Add("Ignored malformed trip record.");
      return;
    }
    if (result == TripStoreResult.Unavailable) {
      update.StorageUnavailable = true;
      update.Logs.Add("Trip was not acknowledged because the CSV could not be saved.");
      return;
    }

    var tripId = payload[..payload.IndexOf(',')];
    update.OutboundCommands.Add($"ACK,{tripId}");
    if (result == TripStoreResult.Saved) {
      savedTripCount++;
      update.Logs.Add($"Saved trip {tripId}");
    }
  }
}
