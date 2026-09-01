using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class FindTerminalsViewModel : INotifyPropertyChanged {
  readonly ITerminalConnection connection;
  bool isScanning;
  TerminalDevice? selectedDevice;
  string statusText = "Ready to discover nearby Hallzee terminals.";

  public FindTerminalsViewModel(ITerminalConnection connection) {
    this.connection = connection;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<TerminalDevice> Devices { get; } = new();

  public bool IsScanning {
    get => isScanning;
    private set { isScanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanScan)); }
  }

  public bool CanScan => !IsScanning;

  public TerminalDevice? SelectedDevice {
    get => selectedDevice;
    set { selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConnect)); }
  }

  public bool CanConnect => SelectedDevice != null && !IsScanning;

  public string StatusText {
    get => statusText;
    private set { statusText = value; OnPropertyChanged(); }
  }

  public async Task ScanAsync() {
    IsScanning = true;
    StatusText = "Scanning for Bluetooth Low Energy terminals…";
    Devices.Clear();

    try {
      var found = await connection.DiscoverAsync();
      foreach (var d in found) Devices.Add(d);
      SelectedDevice = Devices.FirstOrDefault();
      StatusText = Devices.Count > 0
        ? $"Found {Devices.Count} terminal(s). Select a device to connect."
        : "No terminals found nearby. Ensure kiosk is powered on.";
    } catch (Exception ex) {
      StatusText = $"Scan failed: {ex.Message}";
    } finally {
      IsScanning = false;
    }
  }

  public async Task<bool> ConnectAsync() {
    if (SelectedDevice == null) return false;
    StatusText = $"Connecting to {SelectedDevice.Name}…";
    try {
      await connection.ConnectAsync(SelectedDevice);
      StatusText = $"Connected to {SelectedDevice.Name}!";
      return true;
    } catch (Exception ex) {
      StatusText = $"Connection failed: {ex.Message}";
      return false;
    }
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
