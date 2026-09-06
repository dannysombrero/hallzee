using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class MainWindow : Window {
  StudentsOutWindow? studentsOutWindow;

  public MainWindow() {
    InitializeComponent();
    Closed += (_, _) => studentsOutWindow?.CloseForShutdown();
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

  async void OnReconnectClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) await vm.ReconnectCandidateAsync();
  }

  void OnDismissReconnectClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.DismissReconnectPrompt();
  }

  void OnStudentsOutClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not MainViewModel viewModel) return;
    studentsOutWindow ??= new StudentsOutWindow { DataContext = viewModel };
    studentsOutWindow.Show();
    studentsOutWindow.Activate();
  }
}
