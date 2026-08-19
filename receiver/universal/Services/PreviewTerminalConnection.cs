using BathroomSync.Core;

namespace BathroomSync.Universal.Services;

public sealed class PreviewTerminalConnection : ITerminalConnection {
  bool connected;
  bool fullHistoryRequested;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    await Task.Delay(500);
    return [new TerminalDevice("preview-bathroom-terminal", "Bathroom-Terminal", true)];
  }

  public async Task ConnectAsync(TerminalDevice terminal) {
    await Task.Delay(350);
    connected = true;
    fullHistoryRequested = false;
    TextReceived?.Invoke(this, "BATHROOM_TERMINAL_READY\n");
  }

  public async Task SendAsync(string command) {
    if (!connected) throw new InvalidOperationException("Preview terminal is not connected.");

    if (command.StartsWith("TIME,")) {
      await Task.Delay(250);
      TextReceived?.Invoke(this, "TIME_ACK,OK\nSYNC_END\n");
    } else if (command == "SYNC_ALL" && !fullHistoryRequested) {
      fullHistoryRequested = true;
      await Task.Delay(250);
      TextReceived?.Invoke(this, "SYNC_END\n");
    }
  }

  public Task DisconnectAsync() {
    connected = false;
    return Task.CompletedTask;
  }

  public void Dispose() {
    _ = DisconnectAsync();
  }
}
