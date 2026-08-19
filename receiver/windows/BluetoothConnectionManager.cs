using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

sealed record TerminalDevice(string Id, string Name, bool IsPaired) {
  public override string ToString() => IsPaired ? $"{Name} (paired)" : Name;
}

sealed class BluetoothConnectionManager : IDisposable {
  const string TerminalName = "Bathroom-Terminal";
  const string PairingPin = "1234";

  readonly SemaphoreSlim writeLock = new(1, 1);
  BluetoothDevice? bluetoothDevice;
  RfcommDeviceService? service;
  StreamSocket? socket;
  DataReader? reader;
  DataWriter? writer;
  CancellationTokenSource? readCancellation;
  bool isDisconnecting;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;
  public bool IsConnected => socket is not null;

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    var devices = new Dictionary<string, TerminalDevice>();
    foreach (var isPaired in new[] { true, false }) {
      var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(isPaired);
      var discovered = await DeviceInformation.FindAllAsync(selector);
      foreach (var device in discovered.Where(device => device.Name == TerminalName)) {
        devices[device.Id] = new TerminalDevice(device.Id, device.Name, device.Pairing.IsPaired);
      }
    }
    return devices.Values.OrderByDescending(device => device.IsPaired).ToList();
  }

  public async Task ConnectAsync(TerminalDevice terminal) {
    await DisconnectAsync();
    await PairIfNeededAsync(terminal);

    bluetoothDevice = await BluetoothDevice.FromIdAsync(terminal.Id)
      ?? throw new InvalidOperationException("Windows could not open Bathroom-Terminal.");
    var services = await bluetoothDevice.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
    service = services.Services.FirstOrDefault(candidate => candidate.ServiceId.Uuid == RfcommServiceId.SerialPort.Uuid)
      ?? throw new InvalidOperationException("Bathroom-Terminal did not expose its Serial Port service.");

    var access = await service.RequestAccessAsync();
    if (access != DeviceAccessStatus.Allowed) {
      throw new InvalidOperationException($"Windows denied access to Bathroom-Terminal ({access}).");
    }

    socket = new StreamSocket();
    await socket.ConnectAsync(
      service.ConnectionHostName,
      service.ConnectionServiceName,
      SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication
    );

    reader = new DataReader(socket.InputStream) { UnicodeEncoding = UnicodeEncoding.Utf8, InputStreamOptions = InputStreamOptions.Partial };
    writer = new DataWriter(socket.OutputStream) { UnicodeEncoding = UnicodeEncoding.Utf8 };
    readCancellation = new CancellationTokenSource();
    _ = ReadLoopAsync(readCancellation.Token);
  }

  public async Task SendAsync(string command) {
    if (writer is null) throw new InvalidOperationException("Bathroom-Terminal is not connected.");

    await writeLock.WaitAsync();
    try {
      writer.WriteString(command + "\n");
      await writer.StoreAsync();
    } finally {
      writeLock.Release();
    }
  }

  async Task PairIfNeededAsync(TerminalDevice terminal) {
    var device = await DeviceInformation.CreateFromIdAsync(terminal.Id);
    if (device.Pairing.IsPaired) return;
    if (!device.Pairing.CanPair) throw new InvalidOperationException("Bathroom-Terminal cannot be paired right now.");

    var customPairing = device.Pairing.Custom;
    TypedEventHandler<DeviceInformationCustomPairing, DevicePairingRequestedEventArgs> requestHandler = (_, request) => {
      if (request.PairingKind == DevicePairingKinds.ProvidePin) request.Accept(PairingPin);
      else request.Accept();
    };

    customPairing.PairingRequested += requestHandler;
    try {
      var result = await customPairing.PairAsync(DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ProvidePin);
      if (result.Status is not DevicePairingResultStatus.Paired and not DevicePairingResultStatus.AlreadyPaired) {
        throw new InvalidOperationException($"Pairing failed ({result.Status}).");
      }
    } finally {
      customPairing.PairingRequested -= requestHandler;
    }
  }

  async Task ReadLoopAsync(CancellationToken cancellationToken) {
    try {
      while (!cancellationToken.IsCancellationRequested && reader is not null) {
        var length = await reader.LoadAsync(256);
        if (length == 0) throw new InvalidOperationException("Bathroom-Terminal disconnected.");
        TextReceived?.Invoke(this, reader.ReadString(length));
      }
    } catch (Exception exception) when (!isDisconnecting && !cancellationToken.IsCancellationRequested) {
      ConnectionLost?.Invoke(this, exception.Message);
    }
  }

  public Task DisconnectAsync() {
    isDisconnecting = true;
    readCancellation?.Cancel();
    readCancellation?.Dispose();
    readCancellation = null;
    reader?.Dispose();
    writer?.Dispose();
    socket?.Dispose();
    service?.Dispose();
    bluetoothDevice?.Dispose();
    reader = null;
    writer = null;
    socket = null;
    service = null;
    bluetoothDevice = null;
    isDisconnecting = false;
    return Task.CompletedTask;
  }

  public void Dispose() {
    _ = DisconnectAsync();
    writeLock.Dispose();
  }
}
