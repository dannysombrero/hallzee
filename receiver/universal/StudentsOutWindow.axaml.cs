using Avalonia.Controls;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class StudentsOutWindow : Window {
  bool closeForShutdown;

  public StudentsOutWindow() {
    InitializeComponent();
    Closing += (_, eventArgs) => {
      if (!closeForShutdown) {
        eventArgs.Cancel = true;
        Hide();
      }
    };
  }

  public void CloseForShutdown() {
    closeForShutdown = true;
    Close();
  }

  async void OnCheckInActiveClick(object? sender, RoutedEventArgs eventArgs) {
    if (DataContext is MainViewModel viewModel) await viewModel.CheckInActivePassAsync();
  }

  async void OnCheckInStudentClick(object? sender, RoutedEventArgs eventArgs) {
    if (sender is Button { Tag: string studentId } && DataContext is MainViewModel viewModel) {
      await viewModel.CheckInStudentAsync(studentId);
    }
  }
}
