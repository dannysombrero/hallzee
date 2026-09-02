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
