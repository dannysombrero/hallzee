using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class FindTerminalsViewModel : INotifyPropertyChanged {
  readonly ITerminalConnection connection;
  readonly Func<TerminalDevice, TerminalDevice>? resolvePairingStatus;
  readonly Func<TerminalDevice?>? currentTerminal;
  bool isScanning;
  bool isConnecting;
  TerminalDevice? selectedDevice;
  string statusText = "Searching for devices...";

  public FindTerminalsViewModel(ITerminalConnection connection,
      Func<TerminalDevice, TerminalDevice>? resolvePairingStatus = null,
      Func<TerminalDevice?>? currentTerminal = null) {
    this.connection = connection;
    this.resolvePairingStatus = resolvePairingStatus;
    this.currentTerminal = currentTerminal;
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
      if (selectedDevice?.Id != value?.Id) ResetPairingPrompt();
      selectedDevice = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanConnect));
    }
  }

  public bool CanConnect => SelectedDevice != null && !IsBusy &&
    (!IsAwaitingPairingCode || (PairingPasskey.Length == 6 && PairingPasskey.All(char.IsAsciiDigit)));

  public string ConnectButtonText => IsConnecting ? "Connecting…"
    : IsAwaitingPairingCode ? "Pair & Connect" : "Connect";

  bool isAwaitingPairingCode;
  public bool IsAwaitingPairingCode {
    get => isAwaitingPairingCode;
    private set {
      isAwaitingPairingCode = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ConnectButtonText));
      OnPropertyChanged(nameof(CanConnect));
      OnPropertyChanged(nameof(Title));
    }
  }

  public string Title => IsAwaitingPairingCode ? "Pair Terminal" : "Find Nearby Terminals";

  public void RequestPairingCode() => IsAwaitingPairingCode = true;

  public void ResetPairingPrompt() {
    IsAwaitingPairingCode = false;
    PairingPasskey = "";
  }

  public void SetConnecting(bool value) => IsConnecting = value;

  public void UpdateDevice(TerminalDevice updated) {
    var wasSelected = SelectedDevice?.Id == updated.Id;
    var enteredCode = PairingPasskey;
    var awaitingCode = IsAwaitingPairingCode;
    var previous = Devices.FirstOrDefault(device => device.Id == updated.Id);
    if (previous != null) Devices[Devices.IndexOf(previous)] = updated;
    if (wasSelected) {
      // List selection may temporarily clear when a record is replaced.
      SelectedDevice = updated;
      PairingPasskey = enteredCode;
      IsAwaitingPairingCode = awaitingCode;
    }
  }

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
      OnPropertyChanged(nameof(CanConnect));
    }
  }

  string pairingPasskey = "";

  public void SetStatus(string message) => StatusText = message;

  public async Task ScanAsync() {
    if (IsBusy) return;
    ResetPairingPrompt();
    IsScanning = true;
    StatusText = "Searching for devices...";
    Devices.Clear();

    try {
      var found = await connection.DiscoverAsync();
      foreach (var d in found) Devices.Add(resolvePairingStatus?.Invoke(d) ?? d);
      // Connected peripherals may stop advertising. Keep the authenticated
      // terminal visible without disconnecting it just to discover it again.
      var current = currentTerminal?.Invoke();
      if (current != null && Devices.All(device => device.Id != current.Id)) Devices.Insert(0, current);
      SelectedDevice = Devices.FirstOrDefault();
      StatusText = Devices.Count > 0
        ? "Select a terminal to connect. New terminals ask for the code shown in pairing mode; currently paired terminals reconnect without a code."
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
