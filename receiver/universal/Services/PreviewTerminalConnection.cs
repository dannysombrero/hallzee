using BathroomSync.Core;

namespace BathroomSync.Universal.Services;

public sealed class PreviewTerminalConnection : ITerminalConnection {
  bool connected;
  int maxStudentIdLength = 10;
  string? activeStudentId;
  long activeEpoch;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;
  public List<string> SentCommands { get; } = new();

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    await Task.Delay(600);
    return [
      new TerminalDevice("EAST-204", "Room 204 Door Kiosk (East-204)", true)
    ];
  }

  public async Task ConnectAsync(TerminalDevice terminal) {
    await Task.Delay(35);
    connected = true;
  }

  public async Task SendAsync(string command) {
    if (!connected) throw new InvalidOperationException("Preview terminal is not connected.");
    SentCommands.Add(command);

    if (command == "HELLO,1") {
      TextReceived?.Invoke(this, "HALLZEE_READY,1\n");
    } else if (command == "GET_SETTINGS") {
      TextReceived?.Invoke(this, $"SETTINGS,MAX_ID_LENGTH,{maxStudentIdLength}\n");
    } else if (command.StartsWith("SET,MAX_ID_LENGTH,", StringComparison.Ordinal)) {
      if (int.TryParse(command.Substring(18).Trim(), out var val) && val >= 4 && val <= 16) {
        maxStudentIdLength = val;
        TextReceived?.Invoke(this, $"SETTINGS_ACK,MAX_ID_LENGTH,{maxStudentIdLength}\n");
      } else {
        TextReceived?.Invoke(this, "SETTINGS_ERROR,MAX_ID_LENGTH,INVALID_VALUE\n");
      }
    } else if (command == "GET_ACTIVE_PASS") {
      if (!string.IsNullOrEmpty(activeStudentId) && activeEpoch > 0) {
        TextReceived?.Invoke(this, $"ACTIVE_PASS,{activeStudentId},{activeEpoch}\n");
      } else {
        TextReceived?.Invoke(this, "ACTIVE_PASS,NONE\n");
      }
    } else if (command.StartsWith("TIME_CURSOR,", StringComparison.Ordinal)) {
      await Task.Delay(250);
      var today = DateTime.Now.ToString("yyyy-MM-dd");
      var parts = command.Split(',');
      var lastId = parts.Length >= 4 && int.TryParse(parts[3].Trim(), out var idVal) ? idVal : 0;
      if (lastId == 0) {
        var payload = $"TIME_ACK,OK\n" +
                      $"SYNC_BEGIN,4\n" +
                      $"TRIP,1,9042,{today} 08:32:00,285,COMPLETED\n" +
                      $"TRIP,2,10482,{today} 09:12:00,192,COMPLETED\n" +
                      $"TRIP,3,8831,{today} 09:44:00,340,COMPLETED\n" +
                      $"TRIP,4,11029,{today} 10:05:00,210,COMPLETED\n" +
                      $"SYNC_END\n";
        TextReceived?.Invoke(this, payload);
      } else {
        TextReceived?.Invoke(this, "TIME_ACK,OK\nSYNC_BEGIN,0\nSYNC_END\n");
      }
    } else if (command.StartsWith("TIME,", StringComparison.Ordinal)) {
      await Task.Delay(100);
      TextReceived?.Invoke(this, "TIME_ACK,OK\nSYNC_BEGIN,0\nSYNC_END\n");
    } else if (command == "SYNC_ALL") {
      await Task.Delay(100);
      TextReceived?.Invoke(this, "SYNC_BEGIN,0\nSYNC_END\n");
    }
  }

  public void SetActivePass(string? studentId, long epoch = 0) {
    activeStudentId = studentId;
    activeEpoch = epoch > 0 ? epoch : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  }

  public void SimulateCheckout(string studentId, long? epoch = null) {
    var ts = epoch ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    SetActivePass(studentId, ts);
    TextReceived?.Invoke(this, $"EVENT,CHECKOUT,{studentId},{ts}\n");
  }

  public void SimulateCheckin(string studentId, long durationSeconds = 300) {
    SetActivePass(null, 0);
    TextReceived?.Invoke(this, $"EVENT,CHECKIN,{studentId},{durationSeconds}\n");
  }

  public void SimulateReset(string studentId, long durationSeconds = 0) {
    SetActivePass(null, 0);
    TextReceived?.Invoke(this, $"EVENT,RESET,{studentId},{durationSeconds}\n");
  }

  public Task DisconnectAsync() {
    connected = false;
    return Task.CompletedTask;
  }

  public void SimulateConnectionLoss(string detail = "Preview terminal disconnected.") {
    connected = false;
    ConnectionLost?.Invoke(this, detail);
  }

  public void Dispose() {
    _ = DisconnectAsync();
  }
}
