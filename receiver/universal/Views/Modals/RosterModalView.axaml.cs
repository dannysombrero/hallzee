using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal.Views;

public partial class RosterModalView : UserControl {
  public RosterModalView() {
    InitializeComponent();
  }

  void OnCloseClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.CloseModal();
    }
  }

  async void OnImportCsvClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not MainViewModel vm) return;

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel == null) return;

    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
      Title = "Select Student Roster CSV (export Numbers files as CSV first)",
      AllowMultiple = false,
      FileTypeFilter = new[] {
        new Avalonia.Platform.Storage.FilePickerFileType("CSV roster files") {
          Patterns = new[] { "*.csv" }
        }
      }
    });

    if (files.Count > 0) {
      var path = files[0].Path.LocalPath;
      vm.RosterModal.StartCsvImport(path);
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
}
