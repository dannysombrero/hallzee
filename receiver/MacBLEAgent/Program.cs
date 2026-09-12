using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CoreBluetooth;
using Foundation;
using CoreFoundation;
using BathroomSync.Core;

namespace BathroomSync.MacBLEAgent;

class Program
{
    static StreamWriter? writer;
    
    // CoreBluetooth properties
    static CBCentralManager? centralManager;
    static CBPeripheral? targetPeripheral;
    static CBCharacteristic? txCharacteristic; // From Peripheral (read/notify)
    static CBCharacteristic? rxCharacteristic; // To Peripheral (write)
    
    static readonly CBUUID ServiceUuid = CBUUID.FromString("005924a2-c6e5-4340-9bb8-22d9dd37a283");
    static readonly CBUUID TxUuid = CBUUID.FromString("44a359f3-9215-4189-a3cb-e7ce18ad40d6");
    static readonly CBUUID RxUuid = CBUUID.FromString("e80f9559-49eb-47bc-af04-8e92e98ced56");
    
    static readonly Dictionary<string, string> foundDevices = new();
    static readonly Dictionary<string, CBPeripheral> foundPeripherals = new();

    static void Main(string[] args)
    {
        if (args.Length == 0) return;
        var port = int.Parse(args[0]);

        var client = new TcpClient();
        client.Connect("127.0.0.1", port);
        
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // CoreBluetooth requires a run loop
        // We will run the network reader on a background thread and pump the CFRunLoop on the main thread
        
        var managerDelegate = new CentralManagerDelegate();
        centralManager = new CBCentralManager(managerDelegate, DispatchQueue.MainQueue);
        
        Task.Run(async () => {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                try
                {
                    var cmd = JsonSerializer.Deserialize<Dictionary<string, string>>(line);
                    if (cmd == null || !cmd.ContainsKey("Action")) continue;

                    var action = cmd["Action"];
                    if (action == "Discover")
                    {
                        DispatchQueue.MainQueue.DispatchAsync(() => managerDelegate.Discover());
                    }
                    else if (action == "Connect" && cmd.ContainsKey("Id"))
                    {
                        var id = cmd["Id"];
                        DispatchQueue.MainQueue.DispatchAsync(() => managerDelegate.Connect(id));
                    }
                    else if ((action == "Send" || action == "SendBinary") && cmd.ContainsKey("Data"))
                    {
                        var data = cmd["Data"];
                        cmd.TryGetValue("RequestId", out var requestId);
                        DispatchQueue.MainQueue.DispatchAsync(() => managerDelegate.Send(data, requestId, action == "SendBinary"));
                    }
                    else if (action == "Disconnect")
                    {
                        DispatchQueue.MainQueue.DispatchAsync(() => managerDelegate.Disconnect());
                    }
                }
                catch (Exception ex)
                {
                    EmitEvent("Error", new { Message = ex.Message + "\n" + ex.StackTrace });
                }
            }
            
            Environment.Exit(0);
        });
        
        // Start runloop
        CFRunLoop.Main.Run();
    }
    
    public static void EmitEvent(string eventName, object payload)
    {
        if (writer == null) return;
        var data = new Dictionary<string, object> { { "Event", eventName }, { "Data", payload } };
        try 
        {
            writer.WriteLine(JsonSerializer.Serialize(data));
        } 
        catch { }
    }
    
    class CentralManagerDelegate : CBCentralManagerDelegate
    {
        private NSTimer? scanTimer;
        private CBPeripheralDelegate? peripheralDelegate;
        private bool pendingDiscover;
        private readonly Queue<(byte[] Data, string? RequestId, bool IsFinal)> pendingWriteChunks = new();
        private bool writeInProgress;
        private string? currentWriteRequestId;
        private bool currentWriteCompletesRequest;
        
        public override void UpdatedState(CBCentralManager central)
        {
            if (central.State == CBManagerState.PoweredOn)
            {
                if (pendingDiscover)
                {
                    pendingDiscover = false;
                    Discover();
                }
            }
            else if (central.State == CBManagerState.PoweredOff || central.State == CBManagerState.Unsupported || central.State == CBManagerState.Unauthorized)
            {
                EmitEvent("Error", new { Message = $"Bluetooth state is {central.State}." });
                if (pendingDiscover)
                {
                    pendingDiscover = false;
                    EmitEvent("DiscoverComplete", new { });
                }
            }
        }
        
        public void Discover()
        {
            if (centralManager == null) return;
            
            if (centralManager.State != CBManagerState.PoweredOn) 
            {
                if (centralManager.State == CBManagerState.Unknown || centralManager.State == CBManagerState.Resetting)
                {
                    pendingDiscover = true;
                }
                else
                {
                    EmitEvent("Error", new { Message = $"Cannot discover: Bluetooth state is {centralManager.State}." });
                    EmitEvent("DiscoverComplete", new { });
                }
                return;
            }
            
            foundDevices.Clear();
            foundPeripherals.Clear();
            centralManager.StopScan();
            centralManager.ScanForPeripherals(new[] { ServiceUuid });
            
            // Stop scanning after 4 seconds
            scanTimer?.Invalidate();
            scanTimer = NSTimer.CreateScheduledTimer(4.0, (t) => {
                centralManager.StopScan();
                EmitEvent("DiscoverComplete", new { });
            });
        }
        
        public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
        {
            var idStr = peripheral.Identifier.ToString();
            var name = advertisementData[CBAdvertisement.DataLocalNameKey]?.ToString() ?? peripheral.Name ?? "Hallzee";
            bool? isClaimed = null;
            string? suffix = null;
            if (advertisementData[CBAdvertisement.DataManufacturerDataKey] is NSData manufacturerData) {
                var bytes = manufacturerData.ToArray();
                // CoreBluetooth includes the two little-endian company bytes.
                if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFF &&
                    TerminalAdvertisementProtocol.TryParse(bytes.AsSpan(2), out var claimed, out var observedSuffix)) {
                    isClaimed = claimed;
                    suffix = observedSuffix;
                }
            }
            foundDevices[idStr] = name;
            foundPeripherals[idStr] = peripheral;
            EmitEvent("Discovered", new { Id = idStr, Name = name, IsClaimed = isClaimed,
                TerminalSuffix = suffix, Rssi = RSSI.Int32Value });
        }
        
        public void Connect(string id)
        {
            Disconnect();
            if (centralManager == null) return;
            
            // Prefer the peripheral returned by the current scan. Reusing a
            // CoreBluetooth-retrieved object after the terminal or macOS has
            // removed pairing information can produce a stale bond error
            // before the Hallzee protocol gets a chance to authenticate.
            if (foundPeripherals.TryGetValue(id, out var scannedPeripheral))
            {
                targetPeripheral = scannedPeripheral;
                peripheralDelegate = new PeripheralDelegate(this);
                targetPeripheral.Delegate = peripheralDelegate;
                centralManager.ConnectPeripheral(targetPeripheral);
            }
            else
            {
                var uuid = new NSUuid(id);
                var peripherals = centralManager.RetrievePeripheralsWithIdentifiers(uuid);
                if (peripherals.Length == 0)
                {
                    EmitEvent("Error", new { Message = "Peripheral not found." });
                    return;
                }
                targetPeripheral = peripherals[0];
                peripheralDelegate = new PeripheralDelegate(this);
                targetPeripheral.Delegate = peripheralDelegate;
                centralManager.ConnectPeripheral(targetPeripheral);
            }
        }
        
        public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
        {
            peripheral.DiscoverServices(new[] { ServiceUuid });
        }
        
        public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        {
            // Release the failed peripheral before the desktop starts its
            // discovery fallback. Keeping this stale target selected can make
            // the next CoreBluetooth operation a no-op.
            Disconnect();
            EmitEvent("Error", new { Message = $"Failed to connect: {error?.LocalizedDescription}" });
        }
        
        public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        {
            // Explicit disconnect already clears the target. Do not turn its
            // delayed callback into an unexpected connection-loss/retry loop.
            if (targetPeripheral == null || !string.Equals(targetPeripheral.Identifier.ToString(),
                peripheral.Identifier.ToString(), StringComparison.OrdinalIgnoreCase) ||
                peripheral.State != CBPeripheralState.Disconnected) return;
            EmitEvent("ConnectionLost", new { Message = "Hallzee disconnected." });
            Disconnect();
        }
        
        public void Disconnect()
        {
            centralManager?.StopScan();
            scanTimer?.Invalidate();
            scanTimer = null;
            if (centralManager != null && targetPeripheral != null)
            {
                // Clear the native delegate through its nullable binding.
                targetPeripheral.WeakDelegate = null;
                centralManager.CancelPeripheralConnection(targetPeripheral);
            }
            targetPeripheral = null;
            txCharacteristic = null;
            rxCharacteristic = null;
            peripheralDelegate = null;
            pendingWriteChunks.Clear();
            writeInProgress = false;
            currentWriteRequestId = null;
            currentWriteCompletesRequest = false;
        }
        
        public void Send(string data, string? requestId, bool binary = false)
        {
            if (targetPeripheral == null || rxCharacteristic == null)
            {
                EmitEvent("Error", new {
                    Message = "BLE write requested before the terminal was ready.",
                    RequestId = requestId
                });
                return;
            }

            byte[] bytes;
            try { bytes = binary ? Convert.FromBase64String(data) : Encoding.UTF8.GetBytes(data.TrimEnd('\r', '\n') + "\n"); }
            catch { EmitEvent("Error", new { Message = "Invalid binary frame", RequestId = requestId }); return; }
            if (binary && bytes.Length > 527) { EmitEvent("Error", new { Message = "Oversized binary frame", RequestId = requestId }); return; }
            int payloadSize = Math.Clamp((int)targetPeripheral.GetMaximumWriteValueLength(CBCharacteristicWriteType.WithResponse), 20, 244);
            
            for (var offset = 0; offset < bytes.Length; offset += payloadSize)
            {
                var length = Math.Min(payloadSize, bytes.Length - offset);
                var finalChunk = offset + length >= bytes.Length;
                pendingWriteChunks.Enqueue((
                    bytes.Skip(offset).Take(length).ToArray(),
                    requestId,
                    finalChunk
                ));
            }

            WriteNextChunk();
        }

        private void WriteNextChunk()
        {
            if (writeInProgress || pendingWriteChunks.Count == 0 ||
                targetPeripheral == null || rxCharacteristic == null) return;

            writeInProgress = true;
            var pending = pendingWriteChunks.Dequeue();
            currentWriteRequestId = pending.RequestId;
            currentWriteCompletesRequest = pending.IsFinal;
            var nsData = NSData.FromArray(pending.Data);
            // The firmware requires an encrypted link. A write
            // with response triggers that negotiation and gives us a callback
            // instead of allowing protected writes to disappear silently.
            targetPeripheral.WriteValue(
                nsData, rxCharacteristic, CBCharacteristicWriteType.WithResponse);
        }

        public void HandleWriteCompleted(NSError? error)
        {
            writeInProgress = false;
            var completedRequestId = currentWriteRequestId;
            var completesRequest = currentWriteCompletesRequest;
            currentWriteRequestId = null;
            currentWriteCompletesRequest = false;
            if (error != null)
            {
                pendingWriteChunks.Clear();
                EmitEvent("Error", new {
                    Message = $"BLE write failed: {error.LocalizedDescription}",
                    RequestId = completedRequestId
                });
                return;
            }

            if (completesRequest && !string.IsNullOrWhiteSpace(completedRequestId))
            {
                EmitEvent("SendComplete", new { RequestId = completedRequestId });
            }

            WriteNextChunk();
        }
    }
    
    class PeripheralDelegate : CBPeripheralDelegate
    {
        private readonly CentralManagerDelegate owner;

        public PeripheralDelegate(CentralManagerDelegate owner)
        {
            this.owner = owner;
        }

        public override void DiscoveredService(CBPeripheral peripheral, NSError? error)
        {
            if (error != null)
            {
                EmitEvent("Error", new { Message = $"BLE service discovery failed: {error.LocalizedDescription}" });
                return;
            }
            if (peripheral.Services == null)
            {
                EmitEvent("Error", new { Message = "Hallzee did not expose any BLE services." });
                return;
            }
            
            foreach (var service in peripheral.Services)
            {
                if (service.UUID.Equals(ServiceUuid))
                {
                    peripheral.DiscoverCharacteristics(new[] { TxUuid, RxUuid }, service);
                }
            }
        }
        
        public override void DiscoveredCharacteristics(CBPeripheral peripheral, CBService service, NSError? error)
        {
            if (error != null)
            {
                EmitEvent("Error", new { Message = $"BLE characteristic discovery failed: {error.LocalizedDescription}" });
                return;
            }
            if (service.Characteristics == null)
            {
                EmitEvent("Error", new { Message = "Hallzee did not expose the expected BLE characteristics." });
                return;
            }
            
            foreach (var charac in service.Characteristics)
            {
                if (charac.UUID.Equals(TxUuid))
                {
                    txCharacteristic = charac;
                    peripheral.SetNotifyValue(true, charac);
                }
                else if (charac.UUID.Equals(RxUuid))
                {
                    rxCharacteristic = charac;
                }
            }

            if (txCharacteristic == null || rxCharacteristic == null)
            {
                EmitEvent("Error", new { Message = "Hallzee's BLE service is missing its TX or RX characteristic." });
            }
        }
        
        public override void UpdatedNotificationState(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        {
            if (error != null)
            {
                EmitEvent("Error", new { Message = $"BLE notification subscription failed: {error.LocalizedDescription}" });
                return;
            }
            
            if (characteristic.UUID.Equals(TxUuid) && characteristic.IsNotifying)
            {
                Program.EmitEvent("Connected", new { Id = peripheral.Identifier.ToString() });
            }
        }
        
        public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        {
            if (error != null || characteristic.Value == null) return;
            
            if (characteristic.UUID.Equals(TxUuid))
            {
                var bytes = characteristic.Value.ToArray();
                var text = Encoding.UTF8.GetString(bytes);
                EmitEvent("TextReceived", new { Text = text });
            }
        }

        public override void WroteCharacteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        {
            if (characteristic.UUID.Equals(RxUuid))
            {
                owner.HandleWriteCompleted(error);
            }
        }
    }
}
