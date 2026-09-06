using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class PoliciesModalView : UserControl {
  public PoliciesModalView() {
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

  async void OnSavePolicyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.Save(vm.ActiveProfile.ProfileId);
      vm.Dashboard.Refresh(vm.ActiveProfile.ProfileId);
      await vm.ApplyPolicyCapacityAsync();
    }
  }

  void OnToggleCreateProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.ToggleCreateProfile();
    }
  }

  void OnCancelCreateProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.IsCreatingProfile = false;
      vm.PolicyModal.NewProfileName = "";
    }
  }

  void OnSaveAsProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CreateProfileFromPolicy();
    }
  }

  void OnTogglePeriodSortClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.ToggleSortOrder();
    }
  }

  void OnAddPeriodClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      var nextIndex = vm.PolicyModal.Periods.Count + 1;
      vm.PolicyModal.AddPeriod(vm.ActiveProfile.ProfileId, $"Period {nextIndex}", "08:00 AM", "08:50 AM");
    }
  }

  void OnPlaySoundPreviewClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.PlayAlertSoundPreview();
    }
  }

  void OnEditPeriodClick(object? sender, RoutedEventArgs e) {
    if (sender is Button btn && btn.Tag is BellPeriodItemViewModel period) {
      period.StartEdit();
    }
  }

  void OnSavePeriodClick(object? sender, RoutedEventArgs e) {
    if (sender is Button btn && btn.Tag is BellPeriodItemViewModel period) {
      period.SaveEdit();
    }
  }

  void OnCancelPeriodClick(object? sender, RoutedEventArgs e) {
    if (sender is Button btn && btn.Tag is BellPeriodItemViewModel period) {
      period.CancelEdit();
    }
  }

  void OnRemovePeriodClick(object? sender, RoutedEventArgs e) {
    if (sender is Button btn && DataContext is MainViewModel vm) {
      if (btn.Tag is BellPeriodItemViewModel periodItem) {
        vm.PolicyModal.RemovePeriod(periodItem);
      } else if (btn.Tag is BellSchedulePeriod period) {
        vm.PolicyModal.RemovePeriod(period);
      }
    }
  }
}
