using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class PoliciesModalView : UserControl {
  public PoliciesModalView() {
    InitializeComponent();
  }

  void OnCloseClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CloseModal();
    }
  }

  async void OnSavePolicyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.Save(vm.ActiveProfile.ProfileId);
      vm.Dashboard.Refresh(vm.ActiveProfile.ProfileId);
      await vm.ApplyPolicyCapacityAsync();
    }
  }

  void OnSaveAsProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CreateProfileFromPolicy();
    }
  }

  void OnAddPeriodClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      var nextIndex = vm.PolicyModal.Periods.Count + 1;
      vm.PolicyModal.AddPeriod(vm.ActiveProfile.ProfileId, $"Period {nextIndex}", "08:00 AM", "08:50 AM");
    }
  }

  void OnRemovePeriodClick(object? sender, RoutedEventArgs e) {
    if (sender is Button btn && btn.Tag is BellSchedulePeriod period && DataContext is MainViewModel vm) {
      vm.PolicyModal.RemovePeriod(period);
    }
  }
}
