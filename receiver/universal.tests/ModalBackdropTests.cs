using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;
using BathroomSync.Universal.Views;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class ModalBackdropTests : IDisposable {
  readonly string tempFolder;
  readonly PreviewTerminalConnection connection;
  readonly MainViewModel viewModel;

  public ModalBackdropTests() {
    tempFolder = Path.Combine(Path.GetTempPath(), "HallzeeModalBackdropTests", Guid.NewGuid().ToString("N"));
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

  [Theory]
  [InlineData(typeof(TripsModalView), "Trips")]
  [InlineData(typeof(RosterModalView), "Roster")]
  [InlineData(typeof(PoliciesModalView), "Policies")]
  [InlineData(typeof(TerminalSettingsModalView), "TerminalSettings")]
  [InlineData(typeof(FindTerminalsModalView), "FindTerminals")]
  [InlineData(typeof(ManualCheckInModalView), "ManualCheckIn")]
  public void AllModalsHaveBackdropPointerHandler(Type viewType, string modalName) {
    var method = viewType.GetMethod("OnBackdropPointerPressed", BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(method);

    var view = (UserControl)Activator.CreateInstance(viewType)!;
    view.DataContext = viewModel;

    viewModel.OpenModal(modalName);
    Assert.True(viewModel.IsModalOpen);
    Assert.Equal(modalName, viewModel.ActiveModal);
  }
}
