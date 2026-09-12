using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BathroomSync.Core;
using Avalonia.Threading;

namespace BathroomSync.Universal.Services;

public class MacAgentTerminalConnection : ITerminalConnection, ITerminalBinaryConnection
{
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
    private readonly Dictionary<string, TaskCompletionSource<bool>> pendingWrites = new();

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

        // Reconnect attempts are intentionally short. The coordinator owns a
        // bounded retry window and should be able to return the UI to manual
        // recovery instead of leaving one CoreBluetooth attempt hanging.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
            if (!isConnected) SendCommand("Disconnect", null);
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
        var discoveryAttempt = new TaskCompletionSource<IReadOnlyList<TerminalDevice>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        discoveryTcs = discoveryAttempt;
        SendCommand("Discover", null);
        try
        {
            return await discoveryAttempt.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (ReferenceEquals(discoveryTcs, discoveryAttempt)) discoveryTcs = null;
        }
    }

    public Task SendAsync(string text) => SendAgentDataAsync("Send", text);
    public Task SendBinaryAsync(byte[] frame) => SendAgentDataAsync("SendBinary", Convert.ToBase64String(frame));
    private async Task SendAgentDataAsync(string action, string text)
    {
        if (!isConnected) throw new InvalidOperationException("Hallzee is not connected.");
        await EnsureAgentRunning();
        if (agentInput == null) throw new InvalidOperationException("The macOS Bluetooth helper is unavailable.");

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingWrites[requestId] = completion;
        SendCommand(action, new Dictionary<string, string> {
            { "Data", text },
            { "RequestId", requestId }
        });

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            pendingWrites.Remove(requestId);
        }
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

            var developmentRid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "osx-arm64",
                Architecture.X64 => "osx-x64",
                _ => throw new PlatformNotSupportedException("The macOS Bluetooth helper supports Apple Silicon and Intel Macs.")
            };
            var devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "MacBLEAgent", "bin", "Debug", "net8.0-macos", developmentRid, "BathroomSync.MacBLEAgent.app", "Contents", "MacOS", "BathroomSync.MacBLEAgent"));
            
            var releasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BathroomSync.MacBLEAgent.app", "Contents", "MacOS", "BathroomSync.MacBLEAgent");

            var path = File.Exists(releasePath) ? releasePath : devPath;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Could not find MacBLEAgent executable at {path}. Build the macOS helper for {developmentRid} first, or use a packaged Hallzee app.");
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
            using var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            tcpClient = await tcpListener.AcceptTcpClientAsync(startupTimeout.Token);
            var stream = tcpClient.GetStream();
            agentOutput = new StreamReader(stream, System.Text.Encoding.UTF8);
            agentInput = new StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };

            _ = ReadAgentOutputAsync();
        }
        catch (Exception ex)
        {
            tcpListener?.Stop();
            tcpListener = null;
            tcpClient?.Dispose();
            tcpClient = null;
            try { if (agentProcess is { HasExited: false }) agentProcess.Kill(); } catch { }
            agentProcess?.Dispose();
            agentProcess = null;
            agentInput = null;
            agentOutput = null;
            throw new IOException("The Mac Bluetooth helper could not start. Check Hallzee's Bluetooth permission in System Settings, then try connecting again.", ex);
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
                        bool? isClaimed = data.TryGetProperty("IsClaimed", out var claimValue) &&
                            claimValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                            ? claimValue.GetBoolean() : null;
                        var suffix = data.TryGetProperty("TerminalSuffix", out var suffixValue) &&
                            suffixValue.ValueKind == JsonValueKind.String ? suffixValue.GetString() : null;
                        var rssi = data.TryGetProperty("Rssi", out var rssiValue) && rssiValue.TryGetInt32(out var parsedRssi)
                            ? parsedRssi
                            : (int?)null;
                        if (id != null) {
                            var previous = discoveredDevices.Find(device => device.Id == id);
                            discoveredDevices.RemoveAll(device => device.Id == id);
                            discoveredDevices.Add(new TerminalDevice(id, name ?? "Hallzee", false, Rssi: rssi,
                                IsClaimed: isClaimed ?? previous?.IsClaimed,
                                TerminalSuffix: suffix ?? previous?.TerminalSuffix));
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
                            TextReceived?.Invoke(this, text);
                        }
                        break;
                    case "SendComplete":
                        var completedRequestId = data.GetProperty("RequestId").GetString();
                        if (completedRequestId != null && pendingWrites.TryGetValue(completedRequestId, out var writeCompletion))
                        {
                            writeCompletion.TrySetResult(true);
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
                        foreach (var pendingWrite in pendingWrites.Values)
                        {
                            pendingWrite.TrySetException(new IOException("Hallzee disconnected during a BLE write."));
                        }
                        break;
                    case "Error":
                        var errMsg = data.GetProperty("Message").GetString() ?? "Unknown error";
                        var failedRequestId = data.TryGetProperty("RequestId", out var requestIdValue)
                            ? requestIdValue.GetString()
                            : null;
                        Console.WriteLine($"Agent Error: {errMsg}");
                        Exception error = errMsg.Contains("Peer removed pairing information", StringComparison.OrdinalIgnoreCase)
                            ? new TerminalBondRepairRequiredException(errMsg)
                            : new IOException(errMsg);
                        if (failedRequestId != null && pendingWrites.TryGetValue(failedRequestId, out var failedWrite)) {
                            failedWrite.TrySetException(error);
                        } else if (discoveryTcs != null) {
                            discoveryTcs.TrySetException(error);
                            discoveryTcs = null;
                        } else {
                            connectionTcs?.TrySetException(error);
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
            Console.WriteLine($"Failed to parse Bluetooth helper event ({ex.GetType().Name}).");
        }
    }
}
