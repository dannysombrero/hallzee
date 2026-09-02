using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class App : Application {
  public override void OnFrameworkInitializationCompleted() {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      var connection = CreateConnection();
      var viewModel = new MainViewModel(connection, isPreviewMode: connection is PreviewTerminalConnection);
      desktop.MainWindow = new MainWindow {
        DataContext = viewModel
      };
      desktop.MainWindow.Closed += (_, _) => viewModel.Dispose();
    }
    base.OnFrameworkInitializationCompleted();
  }

  static BathroomSync.Core.ITerminalConnection CreateConnection() {
    return new BathroomSync.Universal.Services.UniversalBluetoothConnectionManager();
  }
}
