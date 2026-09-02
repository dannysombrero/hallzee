using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class ManualCheckInModalView : UserControl {
  public ManualCheckInModalView() {
    InitializeComponent();
  }

  void OnCancelClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CloseModal();
    }
  }

  void OnSubmitClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.SubmitManualCheckIn();
    }
  }
}
