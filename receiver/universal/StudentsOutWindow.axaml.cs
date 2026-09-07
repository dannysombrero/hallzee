using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BathroomSync.Universal.ViewModels;

namespace BathroomSync.Universal;

public partial class StudentsOutWindow : Window {
  bool closeForShutdown;

  public StudentsOutWindow() {
    InitializeComponent();
    SetupPlatformTitleBar();
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

  void SetupPlatformTitleBar() {
    if (OperatingSystem.IsMacOS()) {
      CustomTrafficLights.IsVisible = false;
      TitleText.Margin = new Thickness(74, 0, 0, 0);
    } else {
      CustomTrafficLights.IsVisible = true;
      TitleText.Margin = new Thickness(6, 0, 0, 0);
    }
  }

  void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) {
    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
      BeginMoveDrag(e);
    }
  }

  void OnCloseDotClick(object? sender, RoutedEventArgs e) {
    Hide();
  }

  void OnMinimizeDotClick(object? sender, RoutedEventArgs e) {
    WindowState = WindowState.Minimized;
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
