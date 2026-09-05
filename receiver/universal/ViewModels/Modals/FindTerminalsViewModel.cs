using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class FindTerminalsViewModel : INotifyPropertyChanged {
  readonly ITerminalConnection connection;
  bool isScanning;
  bool isConnecting;
  TerminalDevice? selectedDevice;
  string statusText = "Searching for devices...";

  public FindTerminalsViewModel(ITerminalConnection connection) {
    this.connection = connection;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<TerminalDevice> Devices { get; } = new();

  public bool IsScanning {
    get => isScanning;
    private set {
      isScanning = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanScan));
      OnPropertyChanged(nameof(CanConnect));
      OnPropertyChanged(nameof(IsBusy));
    }
  }

  public bool IsConnecting {
    get => isConnecting;
    private set {
      isConnecting = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanScan));
      OnPropertyChanged(nameof(CanConnect));
      OnPropertyChanged(nameof(IsBusy));
      OnPropertyChanged(nameof(ConnectButtonText));
    }
  }

  public bool IsBusy => IsScanning || IsConnecting;

  public bool CanScan => !IsBusy;

  public TerminalDevice? SelectedDevice {
    get => selectedDevice;
    set {
      selectedDevice = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanConnect));
    }
  }

  public bool CanConnect => SelectedDevice != null && !IsBusy;

  public string ConnectButtonText => IsConnecting ? "Connecting…" : "Connect & Sync";

  public string StatusText {
    get => statusText;
    private set { statusText = value; OnPropertyChanged(); }
  }

  public string PairingPasskey {
    get => pairingPasskey;
    set {
      if (pairingPasskey == value) return;
      pairingPasskey = value;
      OnPropertyChanged();
    }
  }

  string pairingPasskey = "";

  public void SetStatus(string message) => StatusText = message;

  public async Task ScanAsync() {
    IsScanning = true;
    StatusText = "Searching for devices...";
    Devices.Clear();

    try {
      var found = await connection.DiscoverAsync();
      foreach (var d in found) Devices.Add(d);
      SelectedDevice = Devices.FirstOrDefault();
      StatusText = Devices.Count > 0
        ? $"Found {Devices.Count} terminal(s) nearby. In Use kiosks can only be reconnected by their owner."
        : "No terminals found nearby. Ensure kiosk is powered on.";
    } catch (Exception ex) {
      StatusText = $"Scan failed: {ex.Message}";
    } finally {
      IsScanning = false;
    }
  }

  public async Task<bool> ConnectAsync() {
    if (SelectedDevice == null) return false;
    IsConnecting = true;
    StatusText = $"Connecting to {SelectedDevice.Name}…";
    try {
      await connection.ConnectAsync(SelectedDevice);
      StatusText = $"Connected to {SelectedDevice.Name}!";
      return true;
    } catch (Exception ex) {
      StatusText = $"Connection failed: {ex.Message}";
      return false;
    } finally {
      IsConnecting = false;
    }
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
