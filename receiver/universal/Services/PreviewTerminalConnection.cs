using BathroomSync.Core;

namespace BathroomSync.Universal.Services;

public sealed class PreviewTerminalConnection : ITerminalConnection {
  bool connected;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;
  public List<string> SentCommands { get; } = new();

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    await Task.Delay(50);
    return [new TerminalDevice("preview-hallzee", "Hallzee", true)];
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
    } else if (command.StartsWith("TIME_CURSOR,", StringComparison.Ordinal)) {
      await Task.Delay(25);
      TextReceived?.Invoke(this, "TIME_ACK,OK\nSYNC_BEGIN,0\nSYNC_END\n");
    } else if (command.StartsWith("TIME,", StringComparison.Ordinal)) {
      await Task.Delay(25);
      TextReceived?.Invoke(this, "TIME_ACK,OK\nSYNC_BEGIN,0\nSYNC_END\n");
    } else if (command == "SYNC_ALL") {
      await Task.Delay(25);
      TextReceived?.Invoke(this, "SYNC_BEGIN,0\nSYNC_END\n");
    }
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
