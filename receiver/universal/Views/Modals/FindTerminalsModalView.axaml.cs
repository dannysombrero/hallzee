using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class FindTerminalsModalView : UserControl {
  public FindTerminalsModalView() {
    InitializeComponent();
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
