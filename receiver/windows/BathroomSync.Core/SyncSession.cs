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
  public int? TransferTotal { get; set; }
  public int? TransferredTripCount { get; set; }
  public bool StorageUnavailable { get; set; }
  public int? MaxStudentIdLength { get; set; }
  public bool SettingsApplied { get; set; }
  public string? SettingsError { get; set; }
  public ActivePassInfo? ActivePass { get; set; }
  public IReadOnlyList<ActivePassInfo>? ActivePasses { get; set; }
  public LivePassEvent? LiveEvent { get; set; }
  public bool LiveTripStored { get; set; }
}

public sealed class SyncSession {
  private readonly ITripRepository repository;
  private readonly StringBuilder input = new();
  private int savedTripCount;
  private int transferredTripCount;

  public SyncSession(ITripRepository repository) {
    this.repository = repository;
  }

  public void Start() {
    input.Clear();
    savedTripCount = 0;
    transferredTripCount = 0;
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

    if (line == "HALLZEE_READY" || line == "HALLZEE_READY,1") {
      update.Status = SyncStatus.Synchronizing;
      return;
    }
    if (TryProcessSettingsLine(line, update)) {
      return;
    }
    if (ActivePassProtocol.TryParseActivePassResponse(line, out var activeInfo)) {
      update.ActivePass = activeInfo;
      return;
    }
    if (ActivePassProtocol.TryParseActivePassesResponse(line, out var activePasses)) {
      update.ActivePasses = activePasses;
      return;
    }
    if (ActivePassProtocol.TryParseLiveEvent(line, out var liveEvt)) {
      update.LiveEvent = liveEvt;
      return;
    }
    if (line.StartsWith("SYNC_BEGIN,", StringComparison.Ordinal) && int.TryParse(line[11..], out var transferTotal) && transferTotal >= 0) {
      transferredTripCount = 0;
      update.TransferTotal = transferTotal;
      update.TransferredTripCount = transferredTripCount;
      update.Status = SyncStatus.Synchronizing;
      return;
    }

    if (line.StartsWith("TRIP,", StringComparison.Ordinal)) {
      StoreTrip(line[5..], update);
      return;
    }
    if (line.StartsWith("LIVE_TRIP,", StringComparison.Ordinal)) {
      StoreTrip(line[10..], update, acknowledge: false, isLiveTrip: true);
      return;
    }

    if (line != "SYNC_END") {
      return;
    }

    update.Status = SyncStatus.Complete;
    update.SavedTripCount = savedTripCount;
  }

  static bool TryProcessSettingsLine(string line, SyncUpdate update) {
    var fields = line.Split(',');
    if (fields.Length != 3 || fields[1] != "MAX_ID_LENGTH") return false;

    if ((fields[0] == "SETTINGS" || fields[0] == "SETTINGS_ACK") &&
        int.TryParse(fields[2], out var value) &&
        value >= KioskSettingsProtocol.MinimumStudentIdLength &&
        value <= KioskSettingsProtocol.MaximumStudentIdLength) {
      update.MaxStudentIdLength = value;
      update.SettingsApplied = fields[0] == "SETTINGS_ACK";
      return true;
    }

    if (fields[0] == "SETTINGS_ERROR") {
      update.SettingsError = fields[2];
      return true;
    }

    return false;
  }

  private void StoreTrip(string payload, SyncUpdate update, bool acknowledge = true, bool isLiveTrip = false) {
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
    if (acknowledge) update.OutboundCommands.Add($"ACK,{tripId}");
    transferredTripCount++;
    update.TransferredTripCount = transferredTripCount;
    if (result == TripStoreResult.Saved) {
      savedTripCount++;
      update.Logs.Add($"Saved trip {tripId}");
    }
    if (isLiveTrip) update.LiveTripStored = true;
  }
}
