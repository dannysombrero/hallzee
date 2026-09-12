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
      Assert.False(viewModel.FindTerminalsModal.IsAwaitingPairingCode);
      await viewModel.ConnectAndSyncAsync();
      Assert.False(viewModel.IsConnected);
      Assert.False(connection.IsConnected);
      Assert.True(viewModel.FindTerminalsModal.IsAwaitingPairingCode);
      Assert.Equal("Not Paired", viewModel.FindTerminalsModal.SelectedDevice!.PairingStatusText);
      Assert.DoesNotContain(connection.SentCommands, command => command.StartsWith("CLAIM,"));
      viewModel.FindTerminalsModal.PairingPasskey = "807481";

      await viewModel.ConnectAndSyncAsync();

      Assert.True(viewModel.IsConnected);
      Assert.False(viewModel.FindTerminalsModal.IsAwaitingPairingCode);
      Assert.Empty(viewModel.FindTerminalsModal.PairingPasskey);
      Assert.Equal("Currently Paired", viewModel.FindTerminalsModal.SelectedDevice!.PairingStatusText);
      Assert.Equal(2, connection.ConnectCount);
      Assert.Equal(2, connection.SentCommands.Count(command => command.StartsWith("HELLO,2,")));
      Assert.Contains(connection.SentCommands, command => command.StartsWith("HELLO,2,"));
      Assert.Contains(connection.SentCommands, command => command.StartsWith("CLAIM,2,"));
      Assert.Contains(connection.SentCommands, command => command.StartsWith("CLAIM_COMMIT,2,"));
      Assert.DoesNotContain(connection.SentCommands, command => command == "HELLO,1");
      Assert.Contains(connection.SentCommands, command => command.StartsWith("TIME_CURSOR,"));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public async Task OtherOwnersTerminalDoesNotPromptForCodeOrAttemptClaim() {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeOtherOwner", Guid.NewGuid().ToString("N"));
    var connection = new FakeV2Connection { Claimed = true };
    try {
      using var vm = new MainViewModel(connection, folder, false);
      await vm.FindTerminalsModal.ScanAsync();
      Assert.Equal("Paired to other device", vm.FindTerminalsModal.SelectedDevice!.PairingStatusText);
      await vm.ConnectAndSyncAsync();
      Assert.False(vm.IsConnected);
      Assert.False(connection.IsConnected);
      Assert.False(vm.FindTerminalsModal.IsAwaitingPairingCode);
      Assert.Contains("Paired to other device", vm.FindTerminalsModal.StatusText);
      Assert.DoesNotContain(connection.SentCommands, command =>
        command.StartsWith("CLAIM,") || command.StartsWith("AUTH,") || command == "GET_SETTINGS");
    } finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
  }

  [Fact]
  public async Task SavedOwnerReconnectsAfterAppRestartWithoutPairingCode() {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeRestartPairing", Guid.NewGuid().ToString("N"));
    var credentials = new InMemoryTerminalCredentialStore();
    try {
      using (var first = new MainViewModel(new FakeV2Connection(), folder, false, credentials)) {
        await first.FindTerminalsModal.ScanAsync();
        first.FindTerminalsModal.PairingPasskey = "807481";
        await first.ConnectAndSyncAsync();
        Assert.True(first.IsConnected);
        await first.DisconnectAsync();
      }
      var reconnected = new FakeV2Connection { Claimed = true };
      using var restarted = new MainViewModel(reconnected, folder, false, credentials);
      var deadline = DateTime.UtcNow.AddSeconds(2);
      while (!restarted.IsConnected && DateTime.UtcNow < deadline) await Task.Delay(10);
      Assert.True(restarted.IsConnected);
      Assert.Contains(reconnected.SentCommands, command => command.StartsWith("AUTH,2,"));
      Assert.DoesNotContain(reconnected.SentCommands, command => command.StartsWith("CLAIM,"));
      Assert.Equal(0, reconnected.DiscoverCount);
      await restarted.FindTerminalsModal.ScanAsync();
      Assert.True(restarted.IsConnected);
      Assert.Equal("Currently Paired", restarted.FindTerminalsModal.SelectedDevice!.PairingStatusText);
      reconnected.HideConnectedAdvertisement = true;
      await restarted.FindTerminalsModal.ScanAsync();
      Assert.Single(restarted.FindTerminalsModal.Devices);
      Assert.Equal("Currently Paired", restarted.FindTerminalsModal.SelectedDevice!.PairingStatusText);
      reconnected.HideConnectedAdvertisement = false;
      // A factory-reset terminal reports unclaimed even if a stale local key exists.
      reconnected.Claimed = false;
      await restarted.FindTerminalsModal.ScanAsync();
      Assert.Equal("Not Paired", restarted.FindTerminalsModal.SelectedDevice!.PairingStatusText);
    } finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
  }

  [Fact]
  public async Task MatchingSuffixAndOsBondDoNotEstablishLocalOwnership() {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeDiscoveryIdentity", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var credentials = new InMemoryTerminalCredentialStore();
    credentials.SaveOwnerKey(TerminalId, new byte[32]);
    var repository = new ProfileAndPolicySqliteRepository(Path.Combine(folder, "hallzee-trips.db"));
    repository.SaveTerminal(new TerminalDeviceConfig(TerminalId, "Synthetic Terminal", TransportId: "different-transport"));
    var connection = new FakeV2Connection { Claimed = true, AdvertisedOsPaired = true };
    try {
      using var vm = new MainViewModel(connection, folder, false, credentials);
      await vm.FindTerminalsModal.ScanAsync();
      Assert.Equal("Paired to other device", vm.FindTerminalsModal.SelectedDevice!.PairingStatusText);
      connection.AdvertisesPairingStatus = false;
      await vm.FindTerminalsModal.ScanAsync();
      Assert.Equal("Unable to check pairing", vm.FindTerminalsModal.SelectedDevice!.PairingStatusText);
    } finally { Directory.Delete(folder, true); }
  }

  [Fact]
  public async Task ClaimedTerminalReconnectsDirectlyAfterConnectionLoss() {
    var folder = Path.Combine(
      Path.GetTempPath(), "HallzeeV2MainViewModelTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var connection = new FakeV2Connection();

    try {
      using var viewModel = new MainViewModel(connection, folder, isPreviewMode: false);
      await viewModel.FindTerminalsModal.ScanAsync();
      viewModel.FindTerminalsModal.PairingPasskey = "807481";
      await viewModel.ConnectAndSyncAsync();

      var connectionCount = connection.ConnectCount;
      connection.RaiseConnectionLost();

      var deadline = DateTime.UtcNow.AddSeconds(2);
      while (connection.ConnectCount == connectionCount && DateTime.UtcNow < deadline)
        await Task.Delay(20);

      Assert.True(connection.ConnectCount > connectionCount);
      Assert.Equal(1, connection.DiscoverCount);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public async Task DesktopCheckinStoresOnlyTerminalRecordEvenWhenLiveTripIsReplayed() {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeCheckinTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var connection = new FakeV2Connection();
    try {
      using var vm = new MainViewModel(connection, folder, isPreviewMode: false);
      await vm.FindTerminalsModal.ScanAsync();
      vm.FindTerminalsModal.PairingPasskey = "807481";
      await vm.ConnectAndSyncAsync();
      var checkout = DateTimeOffset.Now.AddMinutes(-2);
      connection.Emit($"EVENT,CHECKOUT,001234,{checkout.ToUnixTimeSeconds()}\n");
      await vm.CheckInActivePassAsync();
      Assert.Contains("MANUAL_CHECKIN,001234", connection.SentCommands);
      Assert.True(vm.ActivePass.IsOccupied); // Until the terminal confirms it.
      Assert.DoesNotContain(vm.Dashboard.RecentTrips, t => t.StudentId == "001234" && t.StatusText != "Out");
      var trip = $"42,001234,{checkout.LocalDateTime:yyyy-MM-dd},{checkout.LocalDateTime:HH:mm:ss},{DateTime.Now:HH:mm:ss},120,MANUAL";
      connection.Emit($"EVENT,CHECKIN,001234,120\nLIVE_TRIP,{trip}\n");
      connection.Emit($"LIVE_TRIP,{trip}\n");
      Assert.False(vm.ActivePass.IsOccupied);
      Assert.Single(vm.Dashboard.RecentTrips, t => t.StudentId == "001234");
    } finally { Directory.Delete(folder, true); }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnpairClearsOwnershipOnlyAfterTerminalConfirms(bool rejectRelease) {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeUnpairTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var connection = new FakeV2Connection { RejectRelease = rejectRelease };
    var credentials = new InMemoryTerminalCredentialStore();
    try {
      using var vm = new MainViewModel(connection, folder, false, credentials);
      await vm.FindTerminalsModal.ScanAsync();
      vm.FindTerminalsModal.PairingPasskey = "807481";
      await vm.ConnectAndSyncAsync();
      Assert.Equal(TerminalId, vm.DeviceUniqueId);
      Assert.Equal("CONNECTED", vm.DeviceConnectionStatus);
      var db = Path.Combine(folder, "hallzee-trips.db");
      var repo = new ProfileAndPolicySqliteRepository(db);
      var trips = new TripSqliteRepository(db);
      trips.Store(TerminalId, "1,001234,2026-09-08,08:00:00,08:01:00,60,COMPLETED");
      var connectCount = connection.ConnectCount;
      await vm.DisconnectAndUnpairAsync();
      Assert.Equal(rejectRelease, credentials.TryGetOwnerKey(TerminalId, out _));
      Assert.Equal(rejectRelease, vm.IsConnected);
      Assert.Equal(rejectRelease ? TerminalId : null, repo.GetAssignedTerminalId("default"));
      Assert.Equal(1, trips.GetLatestTripId(TerminalId));
      if (rejectRelease) Assert.Contains("not confirmed", vm.TerminalSettingsModal.StatusMessage);
      else {
        Assert.False(vm.HasReconnectCandidate);
        Assert.Equal("No device paired", vm.DeviceUniqueId);
        Assert.Equal("No Device Paired", vm.TerminalSettingsModal.TerminalName);
        connection.RaiseConnectionLost();
        Assert.Equal(connectCount, connection.ConnectCount);
        using var restarted = new MainViewModel(new FakeV2Connection(), folder, false, credentials);
        Assert.False(restarted.HasReconnectCandidate);
      }
    } finally { Directory.Delete(folder, true); }
  }

  [Fact]
  public async Task DeviceNameUsesExplicitEditAndPreservesPairingMetadata() {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeRenameTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var connection = new FakeV2Connection();
    try {
      using var vm = new MainViewModel(connection, folder, false);
      await vm.FindTerminalsModal.ScanAsync();
      vm.FindTerminalsModal.PairingPasskey = "807481";
      await vm.ConnectAndSyncAsync();
      vm.TerminalSettingsModal.BeginNameEdit();
      vm.TerminalSettingsModal.EditedTerminalName = "Cancelled";
      vm.TerminalSettingsModal.CancelNameEdit();
      Assert.Equal("Hallzee-A1B2", vm.TerminalSettingsModal.TerminalName);
      Assert.DoesNotContain(connection.SentCommands, c => c.StartsWith("SET,TERMINAL_NAME,"));
      vm.TerminalSettingsModal.BeginNameEdit();
      vm.TerminalSettingsModal.EditedTerminalName = "Room 204";
      await vm.SaveTerminalNameAsync();
      Assert.False(vm.TerminalSettingsModal.IsEditingName);
      Assert.Equal("Hallzee Desktop Client · Room 204", vm.WindowTitle);
      await vm.ApplyTerminalSettingsAsync();
      Assert.Single(connection.SentCommands, c => c.StartsWith("SET,TERMINAL_NAME,"));
      var saved = new ProfileAndPolicySqliteRepository(Path.Combine(folder, "hallzee-trips.db")).GetTerminal(TerminalId)!;
      Assert.Equal("transport-v2", saved.TransportId);
      Assert.Equal("CLAIMED", saved.ClaimStatus);
      vm.ActivePass.SetOccupied("1234", "Avery", DateTime.Now);
      Assert.False(vm.CanUnpairDevice);
      await vm.DisconnectAndUnpairAsync();
      Assert.DoesNotContain("RELEASE_OWNER", connection.SentCommands);
      Assert.True(vm.IsConnected);
    } finally { Directory.Delete(folder, true); }
  }

  [Fact]
  public async Task RejectedDeviceNameShowsActionableGuidance() {
    var folder = Path.Combine(Path.GetTempPath(), "HallzeeRenameErrorTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var connection = new FakeV2Connection { RejectName = true };
    try {
      using var vm = new MainViewModel(connection, folder, false);
      await vm.FindTerminalsModal.ScanAsync();
      vm.FindTerminalsModal.PairingPasskey = "807481";
      await vm.ConnectAndSyncAsync();
      vm.TerminalSettingsModal.BeginNameEdit();
      vm.TerminalSettingsModal.EditedTerminalName = "Room #204";

      await vm.SaveTerminalNameAsync();

      Assert.True(vm.TerminalSettingsModal.IsEditingName);
      Assert.Contains("1–24 characters", vm.TerminalSettingsModal.StatusMessage);
      Assert.DoesNotContain("SETTINGS_ERROR", vm.TerminalSettingsModal.StatusMessage);
    } finally { Directory.Delete(folder, true); }
  }

  [Fact]
  public async Task FirmwareVersionIsFreshOnlyAfterQueryAndCachedAcrossRestart() {
    var folder=Path.Combine(Path.GetTempPath(),"HallzeeFirmwareUi",Guid.NewGuid().ToString("N"));
    var credentials=new InMemoryTerminalCredentialStore(); var connection=new FakeV2Connection();
    try {
      using(var vm=new MainViewModel(connection,folder,false,credentials)) {
        await vm.FindTerminalsModal.ScanAsync(); vm.FindTerminalsModal.PairingPasskey="807481"; await vm.ConnectAndSyncAsync();
        Assert.Contains("Unknown",vm.FirmwareVersionDisplay); Assert.False(vm.CanLoadFirmware);
        await vm.RefreshFirmwareInfoAsync();
        Assert.Equal("1.0.0 (test-fw)",vm.FirmwareVersionDisplay); Assert.True(vm.CanLoadFirmware);
        await vm.DisconnectAsync();
        Assert.Equal("Last seen: 1.0.0 (test-fw)",vm.FirmwareVersionDisplay); Assert.False(vm.CanLoadFirmware);
      }
      using var restarted=new MainViewModel(new FakeV2Connection(),folder,false,credentials);
      Assert.Equal("Last seen: 1.0.0 (test-fw)",restarted.FirmwareVersionDisplay);
      Assert.False(restarted.CanInstallFirmware);
    } finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
  }

  [Fact]
  public async Task UpdateCheckStaysAvailableWhileTerminalVersionQueryIsPending() {
    var folder=Path.Combine(Path.GetTempPath(),"HallzeeUpdateCheckUi",Guid.NewGuid().ToString("N"));
    var connection=new FakeV2Connection { HoldFirmwareInfo=true };
    try {
      using var vm=new MainViewModel(connection,folder,false);
      Assert.True(vm.CanCheckUpdates);
      await vm.FindTerminalsModal.ScanAsync();
      vm.FindTerminalsModal.PairingPasskey="807481";
      await vm.ConnectAndSyncAsync();
      var refresh=vm.RefreshFirmwareInfoAsync();
      Assert.Contains("GET_FIRMWARE_INFO",connection.SentCommands);
      Assert.True(vm.IsDeviceBusy);
      Assert.False(vm.CanLoadFirmware);
      Assert.True(vm.CanCheckUpdates);
      connection.Emit("FIRMWARE_INFO,1,1.0.0,test-fw,esp32-ili9341-r1,ota-v1,1572864,1,CONFIRMED,1\n");
      await refresh;
      Assert.False(vm.IsDeviceBusy);
      Assert.True(vm.CanLoadFirmware);
      Assert.True(vm.CanCheckUpdates);
    } finally { Directory.Delete(folder,true); }
  }

  sealed class FakeV2Connection : ITerminalConnection {
    bool connected;
    public bool Claimed { get; set; }
    public bool AdvertisesPairingStatus { get; set; } = true;
    public bool AdvertisedOsPaired { get; init; }
    public bool HideConnectedAdvertisement { get; set; }
    public bool IsConnected => connected;
    public bool RejectRelease { get; init; }
    public bool RejectName { get; init; }
    public bool HoldFirmwareInfo { get; init; }

    public event EventHandler<string>? TextReceived;
    public event EventHandler<string>? ConnectionLost;
    public List<string> SentCommands { get; } = new();
    public int ConnectCount { get; private set; }
    public int DiscoverCount { get; private set; }

    public Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
      DiscoverCount++;
      if (HideConnectedAdvertisement && connected)
        return Task.FromResult<IReadOnlyList<TerminalDevice>>(Array.Empty<TerminalDevice>());
      IReadOnlyList<TerminalDevice> devices = new[] {
        new TerminalDevice("transport-v2", "Hallzee-A1B2", AdvertisedOsPaired,
          IsClaimed: AdvertisesPairingStatus ? Claimed : null,
          TerminalSuffix: AdvertisesPairingStatus ? "E5F6" : null)
      };
      return Task.FromResult(devices);
    }

    public Task ConnectAsync(TerminalDevice terminal) {
      connected = true;
      ConnectCount++;
      return Task.CompletedTask;
    }

    public Task SendAsync(string command) {
      if (!connected) throw new InvalidOperationException("Fake terminal is disconnected.");
      SentCommands.Add(command);

      if (command.StartsWith("HELLO,2,")) {
        Emit($"IDENTITY,2,{TerminalId},E5F6,{(Claimed ? "CLAIMED" : "UNCLAIMED")},AVAILABLE,{IdentityNonce}\n");
      } else if (command.StartsWith("CLAIM,2,")) {
        Emit($"CLAIM_OK,2,{TerminalId},{CommitNonce}\n");
      } else if (command.StartsWith("CLAIM_COMMIT,2,")) {
        Claimed = true;
        Emit($"AUTH_OK,2,{TerminalId},Hallzee-A1B2\n");
      } else if (command.StartsWith("AUTH,2,") && Claimed) {
        Emit($"AUTH_OK,2,{TerminalId},Hallzee-A1B2\n");
      } else if (command == "RELEASE_OWNER") {
        if (RejectRelease) Emit("ERROR,OWNER_RELEASE_FAILED\n");
        else { Claimed = false; Emit("OWNER_RELEASED\n"); }
      } else if (command.StartsWith("SET,TERMINAL_NAME,") && RejectName) {
        Emit("SETTINGS_ERROR,TERMINAL_NAME,INVALID_VALUE\n");
      } else if (command.StartsWith("SET,TERMINAL_NAME,") || command.StartsWith("SET,MAX_ID_LENGTH,")) {
        Emit("SETTINGS_ACK," + command[4..] + "\n");
      } else if (command == "GET_FIRMWARE_INFO") {
        if(!HoldFirmwareInfo) Emit("FIRMWARE_INFO,1,1.0.0,test-fw,esp32-ili9341-r1,ota-v1,1572864,1,CONFIRMED,1\n");
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

    public void RaiseConnectionLost() {
      connected = false;
      ConnectionLost?.Invoke(this, "Fake connection lost.");
    }

    public void Emit(string text) => TextReceived?.Invoke(this, text);
  }
}
