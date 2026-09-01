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

  async void OnApplyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      await vm.TerminalSettingsModal.ApplySettingsAsync();
    }
  }
}
