using BathroomSync.Core;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class MainViewModelTests : IDisposable {
  readonly string tempFolder;
  readonly PreviewTerminalConnection connection;
  readonly MainViewModel viewModel;

  public MainViewModelTests() {
    tempFolder = Path.Combine(Path.GetTempPath(), "HallzeeMainViewModelTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempFolder);
    connection = new PreviewTerminalConnection();
    viewModel = new MainViewModel(connection, tempFolder, isPreviewMode: true);
  }

  public void Dispose() {
    viewModel.Dispose();
    try {
      if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
    } catch { }
  }

  [Fact]
  public void InitializesWithDefaultProfileAndClosedModals() {
    Assert.NotNull(viewModel.ActiveProfile);
    Assert.NotEmpty(viewModel.Profiles);
    Assert.False(viewModel.IsModalOpen);
    Assert.Equal("None", viewModel.ActiveModal);
    Assert.False(viewModel.IsConnected);
    Assert.Equal("OFFLINE", viewModel.ConnectionStatusText);
  }

  [Fact]
  public void OpensAndClosesModalsCorrectly() {
    viewModel.OpenModal("Trips");
    Assert.True(viewModel.IsModalOpen);
    Assert.True(viewModel.IsTripsModalVisible);
    Assert.False(viewModel.IsRosterModalVisible);

    viewModel.OpenModal("Roster");
    Assert.True(viewModel.IsRosterModalVisible);
    Assert.False(viewModel.IsTripsModalVisible);

    viewModel.OpenModal("Policies");
    Assert.True(viewModel.IsPoliciesModalVisible);

    viewModel.OpenModal("TerminalSettings");
    Assert.True(viewModel.IsTerminalSettingsModalVisible);

    viewModel.OpenModal("FindTerminals");
    Assert.True(viewModel.IsFindTerminalsModalVisible);

    viewModel.CloseModal();
    Assert.False(viewModel.IsModalOpen);
    Assert.Equal("None", viewModel.ActiveModal);
  }

  [Fact]
  public async Task ConnectAndSyncWorkflowUpdatesConnectionAndStatus() {
    await viewModel.FindTerminalsModal.ScanAsync();
    Assert.NotEmpty(viewModel.FindTerminalsModal.Devices);

    await viewModel.ConnectAndSyncAsync();

    Assert.True(viewModel.IsConnected);
    Assert.Equal("BLE CONNECTED", viewModel.ConnectionStatusText);
    Assert.Contains("HELLO,1", connection.SentCommands);
    Assert.Contains("GET_ACTIVE_PASS\n", connection.SentCommands);
    Assert.Contains("GET_SETTINGS\n", connection.SentCommands);

    await viewModel.DisconnectAsync();
    Assert.False(viewModel.IsConnected);
    Assert.Equal("OFFLINE", viewModel.ConnectionStatusText);
  }
}
