namespace BathroomSync.Core;

public sealed record TerminalDevice(
  string Id,
  string Name,
  bool IsPaired,
  bool IsInUse = false,
  int? Rssi = null,
  bool? IsClaimed = null,
  string? TerminalId = null,
  string? TerminalSuffix = null
) {
  // IsPaired means this installation holds an owner credential; OS Bluetooth
  // bonds and advertised device names do not establish Hallzee ownership.
  public string PairingStatusText => IsClaimed == false ? "Not Paired"
    : IsPaired ? "Currently Paired"
    : IsClaimed == true ? "Paired to other device"
    : "Unable to check pairing";

  public string SignalStrengthText => Rssi is null ? "Signal unavailable" : $"{Rssi} dBm";
  public int SignalBarCount => Rssi switch {
    >= -60 => 4,
    >= -70 => 3,
    >= -80 => 2,
    null => 0,
    _ => 1
  };
  public bool HasSignalBar1 => SignalBarCount >= 1;
  public bool HasSignalBar2 => SignalBarCount >= 2;
  public bool HasSignalBar3 => SignalBarCount >= 3;
  public bool HasSignalBar4 => SignalBarCount >= 4;
  public string SignalQuality => SignalBarCount switch {
    4 => "Excellent",
    3 => "Good",
    2 => "Fair",
    1 => "Weak",
    _ => "Unknown"
  };

  public override string ToString() => $"{Name} · {PairingStatusText}";
}

public interface ITerminalConnection : IDisposable {
  event EventHandler<string>? TextReceived;
  event EventHandler<string>? ConnectionLost;

  Task<IReadOnlyList<TerminalDevice>> DiscoverAsync();
  Task ConnectAsync(TerminalDevice terminal);
  Task SendAsync(string command);
  Task DisconnectAsync();
}

public sealed class TerminalBondRepairRequiredException : InvalidOperationException {
  public TerminalBondRepairRequiredException(string message)
    : base("The operating system removed the Bluetooth bond. Repair the Bluetooth connection, then reconnect. " + message) { }
}

public interface ITerminalBinaryConnection {
  Task SendBinaryAsync(byte[] frame);
}
