using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BathroomSync.Universal.ViewModels;


namespace BathroomSync.Universal.Views;

public partial class RosterModalView : UserControl {
  public RosterModalView() {
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

  bool isImporting;

  async void OnImportCsvClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not MainViewModel vm) return;
    if (isImporting) return;
    isImporting = true;

    try {
      var topLevel = TopLevel.GetTopLevel(this);
      if (topLevel == null) return;

      // Yield briefly to ensure pointer capture and UI click dispatch release cleanly
      // before Windows COM opens the native file dialog message loop
      await Task.Yield();

      var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
        Title = "Select Student Roster CSV (export Numbers files as CSV first)",
        AllowMultiple = false,
        FileTypeFilter = new[] {
          new Avalonia.Platform.Storage.FilePickerFileType("CSV roster files") {
            Patterns = new[] { "*.csv" }
          }
        }
      });

      // Allow the native dialog COM handle to fully release on Windows
      await Task.Delay(100);

      if (files != null && files.Count > 0) {
        var file = files[0];
        var path = file.TryGetLocalPath() ?? file.Path?.LocalPath;
        if (!string.IsNullOrEmpty(path)) {
          await vm.RosterModal.StartCsvImportAsync(path);
        }
      }
    } catch (Exception ex) {
      vm.RosterModal.SetErrorMessage($"Error opening file: {ex.Message}");
    } finally {
      isImporting = false;
    }
  }


  void OnConfirmImportClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.RosterModal.ConfirmImport(vm.ActiveProfile.ProfileId, clearExisting: false);
      vm.Dashboard.Refresh(vm.ActiveProfile.ProfileId);
    }
  }

  void OnCancelImportClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.RosterModal.CancelImport();
    }
  }

  void OnDeleteStudentClick(object? sender, RoutedEventArgs e) {
    if (sender is Button btn && btn.Tag is string studentId && DataContext is MainViewModel vm) {
      vm.RosterModal.DeleteStudent(vm.ActiveProfile.ProfileId, studentId);
      vm.Dashboard.Refresh(vm.ActiveProfile.ProfileId);
    }
  }

  void OnCreateClassClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.CreateProfileFromPolicy();
  }

  void OnSortStudentClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.RosterModal.ToggleSort("Student");
  }

  void OnSortGradeClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.RosterModal.ToggleSort("Grade");
  }

  void OnSortPeriodClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.RosterModal.ToggleSort("Period");
  }
}
