using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class DashboardView : UserControl {
  public DashboardView() {
    InitializeComponent();
  }

  void OnSyncClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      _ = vm.SyncNowAsync();
    }
  }

  void OnScanForDevicesClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("FindTerminals");
    }
  }

  void OnConfigureNodeClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("TerminalSettings");
    }
  }

  void OnToggleOccupancyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      _ = vm.CheckInActivePassAsync();
    }
  }

  void OnManageRosterClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("Roster");
    }
  }

  void OnViewAllClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("Trips");
    }
  }

  void OnExportCsvClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      var exportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        $"hallzee_trips_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
      );
      vm.TripsModal.ExportCsv(vm.ActiveProfile.ProfileId, exportPath);
    }
  }
}
