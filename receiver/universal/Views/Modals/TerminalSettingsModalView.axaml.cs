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

  async void OnDeviceTabClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SelectDeviceSubmenu();
      await vm.RefreshFirmwareInfoAsync();
    }
  }

  void OnSaveClassroomInfoClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      vm.TerminalSettingsModal.SaveClassroomInfo();
    }
  }

  void OnEditNameClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm && vm.CanEditDevice) vm.TerminalSettingsModal.BeginNameEdit();
  }

  void OnCancelNameClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) vm.TerminalSettingsModal.CancelNameEdit();
  }

  async void OnSaveNameClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) await vm.SaveTerminalNameAsync();
  }

  async void OnDisconnectAndUnpairClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) await vm.DisconnectAndUnpairAsync();
  }

  async void OnRefreshFirmwareClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) await vm.RefreshFirmwareInfoAsync(); }
  async void OnLoadFirmwareClick(object? sender, RoutedEventArgs e) {
    if(DataContext is not MainViewModel vm || !vm.CanLoadFirmware) return;
    var top=TopLevel.GetTopLevel(this); if(top==null) return;
    var files=await top.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
      Title="Choose Hallzee firmware package", AllowMultiple=false,
      FileTypeFilter=new[]{new Avalonia.Platform.Storage.FilePickerFileType("Hallzee firmware") { Patterns=new[]{"*.hallzee-fw"} }}
    });
    if(files.Count==0) return;
    using var input=await files[0].OpenReadAsync(); using var memory=new System.IO.MemoryStream();
    try { await BathroomSync.Core.FirmwareReleases.CopyBoundedAsync(input,memory,BathroomSync.Core.FirmwarePackage.MaximumArchiveBytes); memory.Position=0; vm.LoadFirmwarePackage(memory); }
    catch(System.Exception ex) { vm.TerminalSettingsModal.SetFailure(ex.Message); }
  }
  async void OnInstallFirmwareClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) await vm.InstallFirmwareAsync(); }
  void OnCancelFirmwareClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) vm.CancelFirmwareUpdate(); }
  async void OnCheckUpdatesClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) await vm.CheckSoftwareUpdatesAsync(); }
  async void OnDownloadFirmwareClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) await vm.DownloadAndInstallFirmwareAsync(); }
  void OnFirmwareReleasesClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) vm.OpenFirmwareReleases(); }
  void OnDesktopReleaseClick(object? sender, RoutedEventArgs e) { if(DataContext is MainViewModel vm) vm.OpenDesktopRelease(); }

  async void OnApplyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm) {
      await vm.ApplyTerminalSettingsAsync();
    }
  }
}
