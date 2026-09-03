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
                    else if (action == "Send" && cmd.ContainsKey("Data"))
                    {
                        var data = cmd["Data"];
                        DispatchQueue.MainQueue.DispatchAsync(() => managerDelegate.Send(data));
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
            var name = peripheral.Name ?? "Hallzee";
            var inUse = name.EndsWith("-INUSE", StringComparison.OrdinalIgnoreCase);
            if (inUse) name = name[..^6];
            if (!foundDevices.ContainsKey(idStr))
            {
                foundDevices[idStr] = name;
                EmitEvent("Discovered", new { Id = idStr, Name = name, IsInUse = inUse });
            }
        }
        
        public void Connect(string id)
        {
            Disconnect();
            if (centralManager == null) return;
            
            var uuid = new NSUuid(id);
            var peripherals = centralManager.RetrievePeripheralsWithIdentifiers(uuid);
            if (peripherals.Length > 0)
            {
                targetPeripheral = peripherals[0];
                peripheralDelegate = new PeripheralDelegate();
                targetPeripheral.Delegate = peripheralDelegate;
                centralManager.ConnectPeripheral(targetPeripheral);
            }
            else
            {
                EmitEvent("Error", new { Message = "Peripheral not found." });
            }
        }
        
        public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
        {
            peripheral.DiscoverServices(new[] { ServiceUuid });
        }
        
        public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        {
            EmitEvent("Error", new { Message = $"Failed to connect: {error?.LocalizedDescription}" });
        }
        
        public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        {
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
                targetPeripheral.Delegate = null;
                centralManager.CancelPeripheralConnection(targetPeripheral);
            }
            targetPeripheral = null;
            txCharacteristic = null;
            rxCharacteristic = null;
            peripheralDelegate = null;
        }
        
        public void Send(string data)
        {
            if (targetPeripheral == null || rxCharacteristic == null)
            {
                EmitEvent("Error", new { Message = "BLE write requested before the terminal was ready." });
                return;
            }

            data = data.TrimEnd('\r', '\n') + "\n";
            var bytes = Encoding.UTF8.GetBytes(data);
            
            for (var offset = 0; offset < bytes.Length; offset += 20)
            {
                var length = Math.Min(20, bytes.Length - offset);
                var chunk = bytes.Skip(offset).Take(length).ToArray();
                var nsData = NSData.FromArray(chunk);
                
                targetPeripheral.WriteValue(nsData, rxCharacteristic, CBCharacteristicWriteType.WithoutResponse);
            }
        }
    }
    
    class PeripheralDelegate : CBPeripheralDelegate
    {
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
    }
}
