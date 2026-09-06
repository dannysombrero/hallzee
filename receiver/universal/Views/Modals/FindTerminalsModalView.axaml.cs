using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class FindTerminalsModalView : UserControl {
  public FindTerminalsModalView() {
    InitializeComponent();
  }

  void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e) {
    var properties = e.GetCurrentPoint(this).Properties;
    if (properties.IsLeftButtonPressed && ReferenceEquals(e.Source, sender) && DataContext is MainViewModel vm) {
      e.Handled = true;
      vm.CloseModal();
    }
  }

  void OnCloseClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CloseModal();
    }
  }

  async void OnScanClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      await vm.FindTerminalsModal.ScanAsync();
    }
  }

  async void OnConnectClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      await vm.ConnectAndSyncAsync();
    }
  }
}
