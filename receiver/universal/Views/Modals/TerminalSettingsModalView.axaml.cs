using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class TerminalSettingsModalView : UserControl {
  public TerminalSettingsModalView() {
    InitializeComponent();
  }

  void OnCloseClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CloseModal();
    }
  }

  void OnProfileTabClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SelectProfileSubmenu();
    }
  }

  void OnDeviceTabClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SelectDeviceSubmenu();
    }
  }

  void OnCreateProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CreateProfileFromPolicy();
    }
  }

  void OnOpenRosterClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("Roster");
    }
  }

  void OnOpenPoliciesClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.OpenModal("Policies");
    }
  }

  async void OnApplyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      await vm.TerminalSettingsModal.ApplySettingsAsync();
    }
  }
}
