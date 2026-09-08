using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BathroomSync.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

sealed class BluetoothConnectionManager : ITerminalConnection, ITerminalPairingPasskeySink {
  const string TerminalName = "Hallzee";
  static readonly Guid ServiceUuid = Guid.Parse("005924a2-c6e5-4340-9bb8-22d9dd37a283");
  static readonly Guid TxUuid = Guid.Parse("44a359f3-9215-4189-a3cb-e7ce18ad40d6");
  static readonly Guid RxUuid = Guid.Parse("e80f9559-49eb-47bc-af04-8e92e98ced56");
  static readonly TimeSpan DiscoveryWindow = TimeSpan.FromSeconds(4);
  static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(45);
  static readonly TimeSpan ProtectedWriteTimeout = TimeSpan.FromSeconds(60);

  static readonly ConcurrentDictionary<ulong, BluetoothAddressType> DiscoveredAddressTypes = new();

  readonly SemaphoreSlim writeLock = new(1, 1);
  BluetoothLEDevice? bluetoothDevice;
  GattDeviceService? service;
  GattCharacteristic? txCharacteristic;
  GattCharacteristic? rxCharacteristic;
  bool isDisconnecting;
  bool isConnecting;
  string? pairingPasskey;

  public event EventHandler<string>? TextReceived;
  public event EventHandler<string>? ConnectionLost;
  public bool IsConnected => bluetoothDevice?.ConnectionStatus == BluetoothConnectionStatus.Connected;

  public void SetPairingPasskey(string? value) {
    pairingPasskey = string.IsNullOrWhiteSpace(value)
      ? null
      : TerminalIdentityProtocol.NormalizePairingPasskey(value);
  }

  public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() {
    await DisconnectAsync();
    var found = new Dictionary<ulong, TerminalDevice>();
    var watcher = new BluetoothLEAdvertisementWatcher {
      ScanningMode = BluetoothLEScanningMode.Active
    };
    watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(ServiceUuid);

    void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher _, BluetoothLEAdvertisementReceivedEventArgs args) {
      DiscoveredAddressTypes[args.BluetoothAddress] = args.BluetoothAddressType;
      var name = string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
        ? TerminalName
        : args.Advertisement.LocalName;
      var inUse = name.EndsWith("-INUSE", StringComparison.OrdinalIgnoreCase);
      var displayName = inUse ? name[..^6] : name;
      lock (found) {
        // Service-only advertisements can follow the name-bearing scan response.
        if (string.IsNullOrWhiteSpace(args.Advertisement.LocalName) && found.TryGetValue(args.BluetoothAddress, out var previous)) {
          found[args.BluetoothAddress] = previous with { Rssi = args.RawSignalStrengthInDBm };
          return;
        }
        found[args.BluetoothAddress] = new TerminalDevice(
          args.BluetoothAddress.ToString("X12"), displayName, false, inUse,
          args.RawSignalStrengthInDBm
        );
      }
    }

    watcher.Received += OnAdvertisementReceived;
    watcher.Start();
    try {
      await Task.Delay(DiscoveryWindow);
    } finally {
      watcher.Stop();
      watcher.Received -= OnAdvertisementReceived;
    }

    lock (found) return found.Values.ToList();
  }

  public async Task ConnectAsync(TerminalDevice terminal) {
    await DisconnectAsync();
    var cleanId = terminal.Id.Replace(":", "").Replace("-", "").Trim();
    if (!ulong.TryParse(cleanId, System.Globalization.NumberStyles.HexNumber, null, out var address))
      throw new InvalidOperationException("The saved kiosk Bluetooth address is invalid.");

    isConnecting = true;
    try {
      // Determine preferred address type. ESP32 BLE peripherals predominantly use Random address.
      // If we discovered the device in this session, use its reported address type; otherwise default to Random.
      var primaryType = DiscoveredAddressTypes.TryGetValue(address, out var type) && type != BluetoothAddressType.Unspecified
        ? type
        : BluetoothAddressType.Random;
      var fallbackType = primaryType == BluetoothAddressType.Random
        ? BluetoothAddressType.Public
        : BluetoothAddressType.Random;

      Exception? firstException = null;
      try {
        using var cts = new CancellationTokenSource(HandshakeTimeout);
        await AttemptConnectWithRetriesAsync(address, primaryType, cts.Token);
      } catch (Exception ex) {
        firstException = ex;
        await CleanupFailedConnectionAsync();
      }

      if (bluetoothDevice is null && firstException is not null) {
        try {
          using var cts = new CancellationTokenSource(HandshakeTimeout);
          await AttemptConnectWithRetriesAsync(address, fallbackType, cts.Token);
        } catch (Exception ex) {
          await CleanupFailedConnectionAsync();
          throw new InvalidOperationException(
            $"BLE connection failed: {DescribeException(firstException)} (retry with {fallbackType}: {DescribeException(ex)})",
            ex
          );
        }
      }

      if (bluetoothDevice is null) {
        throw new InvalidOperationException("Windows could not open Hallzee over BLE.");
      }

      // Attach disconnection monitor ONLY after the full handshake and notification enablement succeeded.
      bluetoothDevice.ConnectionStatusChanged += HandleConnectionStatusChanged;
    } catch (Exception exception) when (exception is not InvalidOperationException) {
      await DisconnectAsync();
      throw new InvalidOperationException($"BLE connection failed: {DescribeException(exception)}", exception);
    } finally {
      isConnecting = false;
    }
  }

  async Task AttemptConnectWithRetriesAsync(
    ulong address,
    BluetoothAddressType addressType,
    CancellationToken cancellationToken) {
    Exception? lastException = null;
    for (var attempt = 1; attempt <= 3; attempt++) {
      try {
        await AttemptConnectAsync(address, addressType, cancellationToken);
        return;
      } catch (Exception exception) {
        lastException = exception;
        await CleanupFailedConnectionAsync();
        // Never repeat an OS pairing ceremony automatically. The caller must
        // decide whether to retry a failed passkey operation.
        if (pairingPasskey is not null || attempt == 3) throw;
        await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
      }
    }
    throw lastException ?? new InvalidOperationException("Windows could not connect to Hallzee.");
  }

  async Task AttemptConnectAsync(ulong address, BluetoothAddressType addressType, CancellationToken cancellationToken) {
    var dev = await BluetoothLEDevice.FromBluetoothAddressAsync(address, addressType).AsTask(cancellationToken)
      ?? throw new InvalidOperationException($"Windows could not open Hallzee device ({addressType}).");

    if (pairingPasskey is not null) {
      await PairWithDisplayedPasskeyAsync(dev, pairingPasskey, cancellationToken);
      dev.Dispose();
      dev = await BluetoothLEDevice.FromBluetoothAddressAsync(address, addressType).AsTask(cancellationToken)
        ?? throw new InvalidOperationException($"Windows could not reopen Hallzee after pairing ({addressType}).");
    }
    bluetoothDevice = dev;

    // The advertisement is available before Windows has populated its GATT cache.
    // Reading uncached here both establishes the link and avoids a stale/missing
    // service result on a first-time BLE connection.
    var services = await dev.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached).AsTask(cancellationToken);
    if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
      throw ConnectionFailure("reading the Hallzee BLE sync service", services.Status, services.ProtocolError);
    service = services.Services[0];

    var access = await service.RequestAccessAsync().AsTask(cancellationToken);
    if (access != Windows.Devices.Enumeration.DeviceAccessStatus.Allowed)
      throw new InvalidOperationException($"Windows denied access to Hallzee's BLE service ({access}).");

    var txResult = await service.GetCharacteristicsForUuidAsync(TxUuid, BluetoothCacheMode.Uncached).AsTask(cancellationToken);
    if (txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0)
      throw ConnectionFailure("reading the terminal-to-PC characteristic", txResult.Status, txResult.ProtocolError);

    var rxResult = await service.GetCharacteristicsForUuidAsync(RxUuid, BluetoothCacheMode.Uncached).AsTask(cancellationToken);
    if (rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0)
      throw ConnectionFailure("reading the PC-to-terminal characteristic", rxResult.Status, rxResult.ProtocolError);

    txCharacteristic = txResult.Characteristics[0];
    rxCharacteristic = rxResult.Characteristics[0];
    txCharacteristic.ProtectionLevel = GattProtectionLevel.EncryptionAndAuthenticationRequired;
    rxCharacteristic.ProtectionLevel = GattProtectionLevel.EncryptionAndAuthenticationRequired;
    txCharacteristic.ValueChanged += HandleValueChanged;
    var notifyStatus = await txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
      GattClientCharacteristicConfigurationDescriptorValue.Notify
    ).AsTask(cancellationToken);
    if (notifyStatus != GattCommunicationStatus.Success)
      throw new InvalidOperationException($"Windows could not subscribe to Hallzee updates ({notifyStatus}). Check that the terminal firmware includes the BLE notification descriptor.");
  }

  static async Task PairWithDisplayedPasskeyAsync(
    BluetoothLEDevice device,
    string passkey,
    CancellationToken cancellationToken) {
    var pairing = device.DeviceInformation.Pairing;
    if (pairing.IsPaired) {
      var unpairResult = await pairing.UnpairAsync().AsTask(cancellationToken);
      if (unpairResult.Status is not (DeviceUnpairingResultStatus.Unpaired or DeviceUnpairingResultStatus.AlreadyUnpaired)) {
        throw new InvalidOperationException($"Windows could not remove the stale Hallzee pairing ({unpairResult.Status}).");
      }
    }

    var custom = pairing.Custom;
    void HandlePairingRequested(DeviceInformationCustomPairing _, DevicePairingRequestedEventArgs args) {
      if (args.PairingKind == DevicePairingKinds.ProvidePin) {
        args.Accept(passkey);
      } else if (args.PairingKind == DevicePairingKinds.ConfirmOnly) {
        args.Accept();
      }
    }

    custom.PairingRequested += HandlePairingRequested;
    try {
      var result = await custom.PairAsync(
        DevicePairingKinds.ProvidePin | DevicePairingKinds.ConfirmOnly,
        DevicePairingProtectionLevel.EncryptionAndAuthentication
      ).AsTask(cancellationToken);
      if (result.Status is not (DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired)) {
        throw new InvalidOperationException($"Windows rejected the Hallzee passkey ({result.Status}).");
      }
    } finally {
      custom.PairingRequested -= HandlePairingRequested;
    }
  }

  Task CleanupFailedConnectionAsync() {
    if (txCharacteristic is not null) txCharacteristic.ValueChanged -= HandleValueChanged;
    txCharacteristic = null;
    rxCharacteristic = null;
    service?.Dispose();
    bluetoothDevice?.Dispose();
    service = null;
    bluetoothDevice = null;
    return Task.CompletedTask;
  }

  static InvalidOperationException ConnectionFailure(string action, GattCommunicationStatus status, byte? protocolError) {
    var protocolDetail = protocolError is null ? string.Empty : $", protocol error 0x{protocolError:X2}";
    return new InvalidOperationException($"BLE connection failed while {action} ({status}{protocolDetail}).");
  }

  static string DescribeException(Exception exception) {
    var message = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
    return exception.HResult == 0 ? message : $"{message} (0x{exception.HResult:X8})";
  }

  public async Task SendAsync(string command) {
    if (rxCharacteristic is null) throw new InvalidOperationException("Hallzee is not connected.");
    var normalized = command.EndsWith('\n') ? command : command + "\n";
    var bytes = Encoding.UTF8.GetBytes(normalized);

    await writeLock.WaitAsync();
    try {
      // Default BLE ATT payload is 20 bytes. Chunk commands so time sync and
      // future settings commands work before/without MTU negotiation.
      for (var offset = 0; offset < bytes.Length; offset += 20) {
        var length = Math.Min(20, bytes.Length - offset);
        using var writer = new DataWriter();
        writer.WriteBytes(bytes.AsSpan(offset, length).ToArray());
        // Authentication commands are sent over an encrypted/MITM-protected
        // characteristic. An acknowledged write makes Windows negotiate that
        // protection and reports a failure instead of silently dropping data.
        using var writeTimeout = new CancellationTokenSource(ProtectedWriteTimeout);
        var result = await rxCharacteristic.WriteValueWithResultAsync(
          writer.DetachBuffer(), GattWriteOption.WriteWithResponse
        ).AsTask(writeTimeout.Token);
        if (result.Status != GattCommunicationStatus.Success)
          throw new InvalidOperationException($"BLE write failed ({result.Status}).");

      }
    } finally {
      writeLock.Release();
    }
  }

  void HandleValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) {
    if (!ReferenceEquals(sender, txCharacteristic)) return;
    var reader = DataReader.FromBuffer(args.CharacteristicValue);
    var bytes = new byte[args.CharacteristicValue.Length];
    reader.ReadBytes(bytes);
    TextReceived?.Invoke(this, Encoding.UTF8.GetString(bytes));
  }

  void HandleConnectionStatusChanged(BluetoothLEDevice sender, object args) {
    if (isDisconnecting || isConnecting ||
        bluetoothDevice is null ||
        sender.BluetoothAddress != bluetoothDevice.BluetoothAddress ||
        sender.ConnectionStatus != BluetoothConnectionStatus.Disconnected) return;

    ConnectionLost?.Invoke(this, "Hallzee disconnected.");
    _ = DisconnectAsync();
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
