using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BathroomSync.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

sealed class BluetoothConnectionManager : ITerminalConnection {
  const string TerminalName = "Bathroom-Terminal";
  static readonly Guid ServiceUuid = Guid.Parse("005924a2-c6e5-4340-9bb8-22d9dd37a283");
  static readonly Guid TxUuid = Guid.Parse("44a359f3-9215-4189-a3cb-e7ce18ad40d6");
  static readonly Guid RxUuid = Guid.Parse("e80f9559-49eb-47bc-af04-8e92e98ced56");
  static readonly TimeSpan DiscoveryWindow = TimeSpan.FromSeconds(4);

  readonly SemaphoreSlim writeLock = new(1, 1);
  BluetoothLEDevice? bluetoothDevice;
  GattDeviceService? service;
  GattCharacteristic? txCharacteristic;
  GattCharacteristic? rxCharacteristic;
  bool isDisconnecting;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;
  public bool IsConnected => bluetoothDevice?.ConnectionStatus == BluetoothConnectionStatus.Connected;

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    var found = new Dictionary<ulong, TerminalDevice>();
    var watcher = new BluetoothLEAdvertisementWatcher {
      ScanningMode = BluetoothLEScanningMode.Active
    };
    watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(ServiceUuid);
    watcher.Received += (_, args) => {
      var name = string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
        ? TerminalName
        : args.Advertisement.LocalName;
      lock (found) {
        found[args.BluetoothAddress] = new TerminalDevice(
          args.BluetoothAddress.ToString("X12"), name, false
        );
      }
    };

    watcher.Start();
    try {
      await Task.Delay(DiscoveryWindow);
    } finally {
      watcher.Stop();
    }

    lock (found) return found.Values.ToList();
  }

  public async Task ConnectAsync(TerminalDevice terminal) {
    await DisconnectAsync();
    if (!ulong.TryParse(terminal.Id, System.Globalization.NumberStyles.HexNumber, null, out var address))
      throw new InvalidOperationException("The saved kiosk Bluetooth address is invalid.");

    bluetoothDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(address)
      ?? throw new InvalidOperationException("Windows could not open Bathroom-Terminal over BLE.");
    bluetoothDevice.ConnectionStatusChanged += HandleConnectionStatusChanged;

    var services = await bluetoothDevice.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Cached);
    if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
      throw new InvalidOperationException("Bathroom-Terminal did not expose the Hallzee BLE sync service.");
    service = services.Services[0];

    var txResult = await service.GetCharacteristicsForUuidAsync(TxUuid, BluetoothCacheMode.Cached);
    var rxResult = await service.GetCharacteristicsForUuidAsync(RxUuid, BluetoothCacheMode.Cached);
    if (txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0 ||
        rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0)
      throw new InvalidOperationException("Bathroom-Terminal BLE characteristics are unavailable.");

    txCharacteristic = txResult.Characteristics[0];
    rxCharacteristic = rxResult.Characteristics[0];
    txCharacteristic.ValueChanged += HandleValueChanged;
    var notifyStatus = await txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
      GattClientCharacteristicConfigurationDescriptorValue.Notify
    );
    if (notifyStatus != GattCommunicationStatus.Success)
      throw new InvalidOperationException("Windows could not subscribe to Bathroom-Terminal updates.");
  }

  public async Task SendAsync(string command) {
    if (rxCharacteristic is null) throw new InvalidOperationException("Bathroom-Terminal is not connected.");
    var bytes = Encoding.UTF8.GetBytes(command + "\n");

    await writeLock.WaitAsync();
    try {
      // Default BLE ATT payload is 20 bytes. Chunk commands so time sync and
      // future settings commands work before/without MTU negotiation.
      for (var offset = 0; offset < bytes.Length; offset += 20) {
        var length = Math.Min(20, bytes.Length - offset);
        using var writer = new DataWriter();
        writer.WriteBytes(bytes.Skip(offset).Take(length).ToArray());
        var result = await rxCharacteristic.WriteValueWithResultAsync(
          writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse
        );
        if (result.Status != GattCommunicationStatus.Success)
          throw new InvalidOperationException($"BLE write failed ({result.Status}).");
      }
    } finally {
      writeLock.Release();
    }
  }

  void HandleValueChanged(GattCharacteristic _, GattValueChangedEventArgs args) {
    var reader = DataReader.FromBuffer(args.CharacteristicValue);
    var bytes = new byte[args.CharacteristicValue.Length];
    reader.ReadBytes(bytes);
    TextReceived?.Invoke(this, Encoding.UTF8.GetString(bytes));
  }

  void HandleConnectionStatusChanged(BluetoothLEDevice sender, object args) {
    if (!isDisconnecting && sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
      ConnectionLost?.Invoke(this, "Bathroom-Terminal disconnected.");
  }

  public Task DisconnectAsync() {
    isDisconnecting = true;
    if (txCharacteristic is not null) txCharacteristic.ValueChanged -= HandleValueChanged;
    if (bluetoothDevice is not null) bluetoothDevice.ConnectionStatusChanged -= HandleConnectionStatusChanged;
    txCharacteristic = null;
    rxCharacteristic = null;
    service?.Dispose();
    bluetoothDevice?.Dispose();
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
