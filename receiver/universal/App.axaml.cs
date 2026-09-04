using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class App : Application {
  public override void OnFrameworkInitializationCompleted() {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      var connection = CreateConnection();
      var previewMode = connection is PreviewTerminalConnection;
      var viewModel = new MainViewModel(
        connection,
        isPreviewMode: previewMode,
        credentialStore: PlatformTerminalCredentialStoreFactory.Create(previewMode));
      desktop.MainWindow = new MainWindow {
        DataContext = viewModel
      };
      desktop.MainWindow.Closed += (_, _) => viewModel.Dispose();
    }
    base.OnFrameworkInitializationCompleted();
  }

  static BathroomSync.Core.ITerminalConnection CreateConnection() {
#if WINDOWS_BLUETOOTH
    return new BluetoothConnectionManager();
#else
    if (OperatingSystem.IsMacOS())
      return new BathroomSync.Universal.Services.MacAgentTerminalConnection();
    return new PreviewTerminalConnection();
#endif
  }
}
