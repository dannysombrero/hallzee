using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class SyncViewModelTests {
  [Fact]
  public async Task FindPopulatesThePreviewTerminalAndEnablesSync() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncUniversalTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);

    try {
      using var viewModel = new SyncViewModel(new PreviewTerminalConnection(), folder);

      await viewModel.FindAsync();

      Assert.Single(viewModel.Devices);
      Assert.Equal("Room 204 Door Kiosk (East-204)", viewModel.SelectedDevice?.Name);
      Assert.True(viewModel.CanSync);
      Assert.Equal("Terminal ready", viewModel.StatusTitle);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public async Task SyncUsesAHandshakeAndDurableTripCursor() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncUniversalTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);

    try {
      var connection = new PreviewTerminalConnection();
      using var viewModel = new SyncViewModel(connection, folder);
      await viewModel.FindAsync();

      await viewModel.SyncAsync();

      Assert.Contains("HELLO,1", connection.SentCommands);
      Assert.Contains(connection.SentCommands, command => command.StartsWith("TIME_CURSOR,") && command.EndsWith(",0"));
      Assert.DoesNotContain("SYNC_ALL", connection.SentCommands);
      Assert.Equal("Sync complete", viewModel.StatusTitle);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public async Task SaveCsvAsCreatesANumericallySortedExport() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncUniversalTests", Guid.NewGuid().ToString("N"));
    var exportPath = Path.Combine(folder, "exports", "trips.csv");
    Directory.CreateDirectory(folder);

    try {
      using var viewModel = new SyncViewModel(new PreviewTerminalConnection(), folder);

      await viewModel.SaveCsvAsAsync(exportPath);

      Assert.True(File.Exists(exportPath));
      Assert.Equal("trip_id,student_id,date,time_out,time_in,duration_seconds,status", File.ReadLines(exportPath).First());
      Assert.Equal("CSV saved", viewModel.StatusTitle);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void ConnectionLossIsShownWithoutCrashingTheClient() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncUniversalTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);

    try {
      var connection = new PreviewTerminalConnection();
      using var viewModel = new SyncViewModel(connection, folder);

      connection.SimulateConnectionLoss("The test terminal disconnected.");

      Assert.Equal("Connection lost", viewModel.StatusTitle);
      Assert.Equal("The test terminal disconnected.", viewModel.StatusDetail);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }
}
