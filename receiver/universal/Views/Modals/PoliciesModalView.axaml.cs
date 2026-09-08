using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
      await vm.ApplyPolicySettingsAsync();
    }
  }

  bool transferringWorkspace;

  async void OnExportWorkspaceClick(object? sender, RoutedEventArgs e) {
    if (transferringWorkspace || DataContext is not MainViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
    transferringWorkspace = true;
    try {
      await Task.Yield();
      var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
        Title = "Export Hallzee Workspace", SuggestedFileName = "workspace.hallzee.json",
        DefaultExtension = "json", FileTypeChoices = new[] { WorkspaceFileType }
      });
      if (file == null) return;
      var json = vm.ExportWorkspace();
      await using (var stream = await file.OpenWriteAsync()) {
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
      }
      vm.PolicyModal.StatusMessage = "Workspace exported with rules, schedules, and date exceptions.";
    } catch (Exception ex) {
      vm.PolicyModal.StatusMessage = $"Export failed: {ex.Message}";
    } finally { transferringWorkspace = false; }
  }

  async void OnImportWorkspaceClick(object? sender, RoutedEventArgs e) {
    if (transferringWorkspace || DataContext is not MainViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
    transferringWorkspace = true;
    try {
      await Task.Yield();
      var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
        Title = "Import Hallzee Workspace", AllowMultiple = false, FileTypeFilter = new[] { WorkspaceFileType }
      });
      if (files.Count == 0) return;
      await using var stream = await files[0].OpenReadAsync();
      using var reader = new StreamReader(stream);
      var json = await reader.ReadToEndAsync();
      vm.ImportWorkspace(json);
    } catch (Exception ex) {
      vm.PolicyModal.StatusMessage = $"Import failed: {ex.Message}";
    } finally { transferringWorkspace = false; }
  }

  static FilePickerFileType WorkspaceFileType => new("Hallzee workspace") { Patterns = new[] { "*.json" } };

  void OnToggleCreateProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.ToggleCreateProfile();
    }
  }

  void OnCancelCreateProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.CancelCreateProfile();
    }
  }

  void OnSaveAsProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CreateProfileFromPolicy();
    }
  }

  void OnToggleRenameProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.ToggleRenameProfile(vm.ActiveProfile?.Name ?? "");
    }
  }

  void OnCancelRenameProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.PolicyModal.CancelRenameProfile();
    }
  }

  void OnSaveRenameProfileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.RenameActiveProfile();
    }
  }

  void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e) {
    if (e.Key == Key.Enter && DataContext is MainViewModel vm) {
      e.Handled = true;
      vm.RenameActiveProfile();
    } else if (e.Key == Key.Escape && DataContext is MainViewModel vmEscape) {
      e.Handled = true;
      vmEscape.PolicyModal.CancelRenameProfile();
    }
  }

  void OnNewProfileTextBoxKeyDown(object? sender, KeyEventArgs e) {
    if (e.Key == Key.Enter && DataContext is MainViewModel vm) {
      e.Handled = true;
      vm.CreateProfileFromPolicy();
    } else if (e.Key == Key.Escape && DataContext is MainViewModel vmEscape) {
      e.Handled = true;
      vmEscape.PolicyModal.CancelCreateProfile();
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

  void OnAddExceptionClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.PolicyModal.AddException(vm.ActiveProfile.ProfileId);
  }

  void OnRemoveExceptionClick(object? sender, RoutedEventArgs e) {
    if (sender is Button { Tag: ScheduleExceptionItemViewModel item } && DataContext is MainViewModel vm) {
      vm.PolicyModal.RemoveException(item);
    }
  }
}
