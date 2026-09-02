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

  void OnManualCheckInClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("ManualCheckIn");
    }
  }

  void OnCheckInStudentClick(object? sender, RoutedEventArgs e) {
    if (sender is Button button && button.Tag is string studentId && DataContext is MainViewModel vm) {
      _ = vm.CheckInStudentAsync(studentId);
    }
  }

  void OnManageRosterClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("Roster");
    }
  }

  void OnManagePoliciesClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("Policies");
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
      vm.ExportTrips(exportPath);
    }
  }
}
