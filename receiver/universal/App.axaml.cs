using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class App : Application {
  public override void OnFrameworkInitializationCompleted() {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      desktop.MainWindow = new MainWindow {
        DataContext = new SyncViewModel(new PreviewTerminalConnection())
      };
    }
    base.OnFrameworkInitializationCompleted();
  }
}
