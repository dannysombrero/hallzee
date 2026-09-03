using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class TerminalV2MainViewModelTests {
  const string TerminalId = "HZ-A1B2C3D4E5F6";
  const string IdentityNonce = "00112233445566778899AABBCCDDEEFF";
  const string CommitNonce = "FFEEDDCCBBAA99887766554433221100";

  [Fact]
  public async Task SingleUnclaimedTerminalCanBeClaimedAndSynced() {
    var folder = Path.Combine(
      Path.GetTempPath(), "HallzeeV2MainViewModelTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var connection = new FakeV2Connection();

    try {
      using var viewModel = new MainViewModel(connection, folder, isPreviewMode: false);
      await viewModel.FindTerminalsModal.ScanAsync();
      viewModel.FindTerminalsModal.PairingPasskey = "807481";

      await viewModel.ConnectAndSyncAsync();

      Assert.True(viewModel.IsConnected);
      Assert.Contains(connection.SentCommands, command => command.StartsWith("HELLO,2,"));
      Assert.Contains(connection.SentCommands, command => command.StartsWith("CLAIM,2,"));
      Assert.Contains(connection.SentCommands, command => command.StartsWith("CLAIM_COMMIT,2,"));
      Assert.DoesNotContain(connection.SentCommands, command => command == "HELLO,1");
      Assert.Contains(connection.SentCommands, command => command.StartsWith("TIME_CURSOR,"));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  sealed class FakeV2Connection : ITerminalConnection {
    bool connected;

    public event EventHandler<string>? TextReceived;
    public event EventHandler<string>? ConnectionLost {
      add { }
      remove { }
    }
    public List<string> SentCommands { get; } = new();

    public Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
      IReadOnlyList<TerminalDevice> devices = new[] {
        new TerminalDevice("transport-v2", "Hallzee-A1B2", false, false)
      };
      return Task.FromResult(devices);
    }

    public Task ConnectAsync(TerminalDevice terminal) {
      connected = true;
      return Task.CompletedTask;
    }

    public Task SendAsync(string command) {
      if (!connected) throw new InvalidOperationException("Fake terminal is disconnected.");
      SentCommands.Add(command);

      if (command.StartsWith("HELLO,2,")) {
        Emit($"IDENTITY,2,{TerminalId},E5F6,UNCLAIMED,AVAILABLE,{IdentityNonce}\n");
      } else if (command.StartsWith("CLAIM,2,")) {
        Emit($"CLAIM_OK,2,{TerminalId},{CommitNonce}\n");
      } else if (command.StartsWith("CLAIM_COMMIT,2,")) {
        Emit($"AUTH_OK,2,{TerminalId},Hallzee-A1B2\n");
      } else if (command == "GET_ACTIVE_PASSES") {
        Emit("ACTIVE_PASSES\n");
      } else if (command == "GET_SETTINGS") {
        Emit("SETTINGS,MAX_ID_LENGTH,10\n");
      } else if (command.StartsWith("TIME_CURSOR,")) {
        Emit("TIME_ACK,OK\nSYNC_BEGIN,0\nSYNC_END\n");
      }

      return Task.CompletedTask;
    }

    public Task DisconnectAsync() {
      connected = false;
      return Task.CompletedTask;
    }

    public void Dispose() => connected = false;

    void Emit(string text) => TextReceived?.Invoke(this, text);
  }
}
