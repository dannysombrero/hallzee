using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class ManualCheckInModalView : UserControl {
  public ManualCheckInModalView() {
    InitializeComponent();
  }

  void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e) {
    var properties = e.GetCurrentPoint(this).Properties;
    if (properties.IsLeftButtonPressed && ReferenceEquals(e.Source, sender) && DataContext is MainViewModel vm) {
      e.Handled = true;
      vm.CloseModal();
    }
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
