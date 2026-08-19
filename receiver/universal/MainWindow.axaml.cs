using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class MainWindow : Window {
  public MainWindow() => InitializeComponent();

  async void FindTerminal(object? sender, RoutedEventArgs eventArgs) {
    if (DataContext is SyncViewModel viewModel) await viewModel.FindAsync();
  }

  async void SyncNow(object? sender, RoutedEventArgs eventArgs) {
    if (DataContext is SyncViewModel viewModel) await viewModel.SyncAsync();
  }

  async void OpenExportFolder(object? sender, RoutedEventArgs eventArgs) {
    if (DataContext is not SyncViewModel viewModel) return;
    await viewModel.OpenExportFolderAsync();
  }

  async void SaveCsvAs(object? sender, RoutedEventArgs eventArgs) {
    if (DataContext is not SyncViewModel viewModel) return;
    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save bathroom trip export",
      SuggestedFileName = "bathroom_trips.csv",
      DefaultExtension = "csv",
      FileTypeChoices = [new FilePickerFileType("CSV files") { Patterns = ["*.csv"] }]
    });
    if (file is not null) await viewModel.SaveCsvAsAsync(file.Path.LocalPath);
  }
}
