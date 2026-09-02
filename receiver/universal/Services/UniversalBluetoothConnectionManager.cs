using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BathroomSync.Core;
using InTheHand.Bluetooth;

namespace BathroomSync.Universal.Services;

sealed class UniversalBluetoothConnectionManager : ITerminalConnection {
  const string TerminalName = "Hallzee";
  static readonly BluetoothUuid ServiceUuid = BluetoothUuid.FromGuid(Guid.Parse("005924a2-c6e5-4340-9bb8-22d9dd37a283"));
  static readonly BluetoothUuid TxUuid = BluetoothUuid.FromGuid(Guid.Parse("44a359f3-9215-4189-a3cb-e7ce18ad40d6"));
  static readonly BluetoothUuid RxUuid = BluetoothUuid.FromGuid(Guid.Parse("e80f9559-49eb-47bc-af04-8e92e98ced56"));
  static readonly TimeSpan DiscoveryWindow = TimeSpan.FromSeconds(4);

  readonly SemaphoreSlim writeLock = new(1, 1);
  BluetoothDevice? bluetoothDevice;
  GattService? service;
  GattCharacteristic? txCharacteristic;
  GattCharacteristic? rxCharacteristic;
  bool isDisconnecting;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;
  public bool IsConnected => bluetoothDevice?.Gatt.IsConnected ?? false;

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    await DisconnectAsync();
    var found = new Dictionary<string, TerminalDevice>();

    void OnAdvertisementReceived(object? sender, BluetoothAdvertisingEvent args) {
      if (args.Device != null) {
        var name = string.IsNullOrWhiteSpace(args.Device.Name) ? TerminalName : args.Device.Name;
        lock (found) {
          found[args.Device.Id] = new TerminalDevice(args.Device.Id, name, false);
        }
      }
    }

    Bluetooth.AdvertisementReceived += OnAdvertisementReceived;
    var scanOptions = new BluetoothLEScanOptions { AcceptAllAdvertisements = true };
    await Bluetooth.RequestLEScanAsync(scanOptions);

    try {
      await Task.Delay(DiscoveryWindow);
    } finally {
      Bluetooth.AdvertisementReceived -= OnAdvertisementReceived;
      // InTheHand stops scanning internally when you remove handlers or cancel
    }

    lock (found) return found.Values.ToList();
  }

  public async Task ConnectAsync(TerminalDevice terminal) {
    await DisconnectAsync();
    
    try {
      bluetoothDevice = await BluetoothDevice.FromIdAsync(terminal.Id) 
          ?? throw new InvalidOperationException("Could not open Hallzee over BLE.");
      
      bluetoothDevice.GattServerDisconnected += HandleConnectionStatusChanged;

      await bluetoothDevice.Gatt.ConnectAsync();
      
      service = await bluetoothDevice.Gatt.GetPrimaryServiceAsync(ServiceUuid);
      if (service == null)
        throw new InvalidOperationException("BLE connection failed while reading the Hallzee BLE sync service.");

      txCharacteristic = await service.GetCharacteristicAsync(TxUuid);
      if (txCharacteristic == null)
        throw new InvalidOperationException("BLE connection failed while reading the terminal-to-PC characteristic.");

      rxCharacteristic = await service.GetCharacteristicAsync(RxUuid);
      if (rxCharacteristic == null)
        throw new InvalidOperationException("BLE connection failed while reading the PC-to-terminal characteristic.");

      txCharacteristic.CharacteristicValueChanged += HandleValueChanged;
      await txCharacteristic.StartNotificationsAsync();
    } catch (Exception exception) {
      await DisconnectAsync();
      throw new InvalidOperationException($"BLE connection failed: {exception.Message}", exception);
    }
  }

  public async Task SendAsync(string command) {
    if (rxCharacteristic is null) throw new InvalidOperationException("Hallzee is not connected.");
    var bytes = Encoding.UTF8.GetBytes(command + "\n");

    await writeLock.WaitAsync();
    try {
      for (var offset = 0; offset < bytes.Length; offset += 20) {
        var length = Math.Min(20, bytes.Length - offset);
        var chunk = bytes.Skip(offset).Take(length).ToArray();
        await rxCharacteristic.WriteValueWithoutResponseAsync(chunk);
      }
    } finally {
      writeLock.Release();
    }
  }

  void HandleValueChanged(object? sender, GattCharacteristicValueChangedEventArgs args) {
    var bytes = args.Value;
    if (bytes != null) {
      TextReceived?.Invoke(this, Encoding.UTF8.GetString(bytes));
    }
  }

  void HandleConnectionStatusChanged(object? sender, EventArgs args) {
    if (isDisconnecting) return;
    ConnectionLost?.Invoke(this, "Hallzee disconnected.");
    _ = DisconnectAsync();
  }

  public Task DisconnectAsync() {
    isDisconnecting = true;
    if (txCharacteristic != null) {
      txCharacteristic.CharacteristicValueChanged -= HandleValueChanged;
      _ = txCharacteristic.StopNotificationsAsync();
    }
    if (bluetoothDevice != null) {
      bluetoothDevice.GattServerDisconnected -= HandleConnectionStatusChanged;
      bluetoothDevice.Gatt.Disconnect();
    }
    txCharacteristic = null;
    rxCharacteristic = null;
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
