using System;
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

  async void OnExportCsvClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not MainViewModel vm) return;

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel == null) return;

    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions {
      Title = "Export Hall Pass Trips",
      SuggestedFileName = $"hallzee_trips_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
      DefaultExtension = "csv",
      FileTypeChoices = new[] {
        new Avalonia.Platform.Storage.FilePickerFileType("CSV Files (*.csv)") {
          Patterns = new[] { "*.csv" }
        }
      }
    });

    if (file != null) {
      var exportPath = file.Path.LocalPath;
      vm.ExportTrips(exportPath, openModal: false);
    }
  }
}
