using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class TripsModalView : UserControl {
  public TripsModalView() {
    InitializeComponent();
  }

  void OnCloseClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CloseModal();
    }
  }

  void OnFilterClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TripsModal.Refresh(vm.ActiveProfile.ProfileId);
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

  void OnSortStudentClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TripsModal.ToggleSort("Student");
  }

  void OnSortDateClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TripsModal.ToggleSort("Date");
  }

  void OnSortTimeOutClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TripsModal.ToggleSort("TimeOut");
  }

  void OnSortTimeInClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TripsModal.ToggleSort("TimeIn");
  }

  void OnSortDurationClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TripsModal.ToggleSort("Duration");
  }

  void OnSortStatusClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TripsModal.ToggleSort("Status");
  }
}
