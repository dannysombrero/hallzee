using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class TerminalSettingsModalView : UserControl {
  public TerminalSettingsModalView() {
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

  void OnSaveClassroomInfoClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SaveClassroomInfo();
    }
  }

  async void OnApplyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      await vm.TerminalSettingsModal.ApplySettingsAsync();
    }
  }
}
