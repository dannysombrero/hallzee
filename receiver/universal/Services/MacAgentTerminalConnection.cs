using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BathroomSync.Core;
using Avalonia.Threading;

namespace BathroomSync.Universal.Services;

public class MacAgentTerminalConnection : ITerminalConnection
{
    public event EventHandler<string>? TerminalDiscovered;
    public event EventHandler<string>? TextReceived;
    public event EventHandler<string>? ConnectionLost;

    private Process? agentProcess;
    private TcpListener? tcpListener;
    private TcpClient? tcpClient;
    private StreamWriter? agentInput;
    private StreamReader? agentOutput;
    private bool isConnected;
    private TaskCompletionSource<bool>? connectionTcs;
    private TaskCompletionSource<IReadOnlyList<TerminalDevice>>? discoveryTcs;
    private List<TerminalDevice> discoveredDevices = new();

    public async Task ConnectAsync(TerminalDevice terminal)
    {
        await EnsureAgentRunning();
        if (agentInput == null)
        {
            throw new InvalidOperationException("The macOS Bluetooth helper is unavailable.");
        }

        var connectionAttempt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connectionTcs = connectionAttempt;
        SendCommand("Connect", new Dictionary<string, string> { { "Id", terminal.Id } });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var registration = timeout.Token.Register(() =>
            connectionAttempt.TrySetException(new TimeoutException(
                "Timed out waiting for the terminal's BLE notifications to become ready.")));

        try
        {
            await connectionAttempt.Task;
        }
        finally
        {
            if (ReferenceEquals(connectionTcs, connectionAttempt)) connectionTcs = null;
        }
    }

    public Task DisconnectAsync()
    {
        if (isConnected)
        {
            SendCommand("Disconnect", null);
            isConnected = false;
        }
        connectionTcs?.TrySetCanceled();
        connectionTcs = null;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TerminalDevice>> DiscoverAsync()
    {
        await EnsureAgentRunning();
        discoveredDevices.Clear();
        discoveryTcs = new TaskCompletionSource<IReadOnlyList<TerminalDevice>>();
        SendCommand("Discover", null);
        return await discoveryTcs.Task;
    }

    public async Task SendAsync(string text)
    {
        if (!isConnected) return;
        await EnsureAgentRunning();
        Console.WriteLine($"[PC -> MAC -> ESP32] {text}");
        SendCommand("Send", new Dictionary<string, string> { { "Data", text } });
        await Task.Delay(100); // Prevent overflowing the ESP32 UART RX ring buffer
    }

    public void Dispose()
    {
        DisconnectAsync();
        if (tcpListener != null) { try { tcpListener.Stop(); } catch { } }
        if (tcpClient != null) { try { tcpClient.Dispose(); } catch { } }
        if (agentProcess != null)
        {
            try { agentProcess.Kill(); } catch { }
            agentProcess.Dispose();
        }
    }

    private async Task EnsureAgentRunning()
    {
        if (agentProcess != null && !agentProcess.HasExited) return;

        try
        {
            tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;

            var devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "MacBLEAgent", "bin", "Debug", "net8.0-macos", "osx-arm64", "BathroomSync.MacBLEAgent.app", "Contents", "MacOS", "BathroomSync.MacBLEAgent"));
            
            var releasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BathroomSync.MacBLEAgent.app", "Contents", "MacOS", "BathroomSync.MacBLEAgent");

            var path = File.Exists(releasePath) ? releasePath : devPath;

            if (!File.Exists(path))
            {
                devPath = devPath.Replace("osx-arm64", "osx-x64");
                if (File.Exists(devPath)) path = devPath;
                else throw new FileNotFoundException($"Could not find MacBLEAgent executable at {path}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = port.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            agentProcess = Process.Start(startInfo);
            if (agentProcess == null) throw new InvalidOperationException("Failed to start MacBLEAgent process.");

            // Wait for agent to connect back
            tcpClient = await tcpListener.AcceptTcpClientAsync();
            var stream = tcpClient.GetStream();
            agentOutput = new StreamReader(stream, System.Text.Encoding.UTF8);
            agentInput = new StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };

            _ = ReadAgentOutputAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Agent Error: {ex.Message}");
        }
    }

    private async Task ReadAgentOutputAsync()
    {
        if (agentOutput == null) return;
        try
        {
            while (true)
            {
                var line = await agentOutput.ReadLineAsync();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                HandleAgentEvent(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Agent stream error: {ex.Message}");
        }
    }

    private void SendCommand(string action, Dictionary<string, string>? data)
    {
        if (agentInput == null) return;
        
        var payload = data ?? new Dictionary<string, string>();
        payload["Action"] = action;

        var json = JsonSerializer.Serialize(payload);
        agentInput.WriteLine(json);
        agentInput.Flush();
    }

    private void HandleAgentEvent(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return;

        try
        {
            var msg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rawJson);
            if (msg == null || !msg.ContainsKey("Event") || !msg.ContainsKey("Data")) return;

            var eventName = msg["Event"].GetString();
            var data = msg["Data"];

            Dispatcher.UIThread.Post(() =>
            {
                switch (eventName)
                {
                    case "Discovered":
                        var id = data.GetProperty("Id").GetString();
                        var name = data.GetProperty("Name").GetString();
                        if (id != null) {
                            discoveredDevices.Add(new TerminalDevice(id, name ?? "Hallzee", false));
                        }
                        break;
                    case "DiscoverComplete":
                        if (discoveryTcs != null) {
                            discoveryTcs.TrySetResult(new List<TerminalDevice>(discoveredDevices));
                            discoveryTcs = null;
                        }
                        break;
                    case "TextReceived":
                        var text = data.GetProperty("Text").GetString();
                        if (text != null) 
                        {
                            Console.WriteLine($"[ESP32 -> MAC -> PC] {text.Replace("\n", "\\n")}");
                            TextReceived?.Invoke(this, text);
                        }
                        break;
                    case "Connected":
                        if (connectionTcs == null)
                        {
                            // The connection completed after its caller timed out or cancelled.
                            // Close it so a late callback cannot revive stale UI state.
                            SendCommand("Disconnect", null);
                            isConnected = false;
                            break;
                        }
                        isConnected = true;
                        connectionTcs?.TrySetResult(true);
                        connectionTcs = null;
                        break;
                    case "ConnectionLost":
                        ConnectionLost?.Invoke(this, "Disconnected from Agent");
                        isConnected = false;
                        connectionTcs?.TrySetException(new IOException("Hallzee disconnected while connecting."));
                        connectionTcs = null;
                        break;
                    case "Error":
                        var errMsg = data.GetProperty("Message").GetString() ?? "Unknown error";
                        Console.WriteLine($"Agent Error: {errMsg}");
                        if (discoveryTcs != null) {
                            discoveryTcs.TrySetException(new Exception(errMsg));
                            discoveryTcs = null;
                        } else {
                            connectionTcs?.TrySetException(new Exception(errMsg));
                            connectionTcs = null;
                            // If we get an error while syncing or trying to connect, abort!
                            ConnectionLost?.Invoke(this, errMsg);
                            isConnected = false;
                        }
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse agent event: {rawJson}. Exception: {ex.Message}");
        }
    }
}
