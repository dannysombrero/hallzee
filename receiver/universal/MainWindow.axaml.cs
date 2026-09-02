using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class MainWindow : Window {
  public MainWindow() {
    InitializeComponent();
  }

  void OnDashboardClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.CloseModal();
  }

  void OnTripsClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.OpenModal("Trips");
  }

  void OnRosterClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.OpenModal("Roster");
  }

  void OnPoliciesClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.OpenModal("Policies");
  }

  void OnSettingsClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SelectProfileSubmenu();
      vm.OpenModal("TerminalSettings");
    }
  }

  void OnSettingsDeviceClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SelectDeviceSubmenu();
      vm.OpenModal("TerminalSettings");
    }
  }

  void OnTerminalSettingsClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.OpenModal("TerminalSettings");
  }

  void OnFindTerminalsClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.OpenModal("FindTerminals");
  }

  async void OnSyncNowClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) await vm.SyncNowAsync();
  }

  async void OnDisconnectClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) await vm.DisconnectAsync();
  }
}
