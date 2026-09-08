namespace BathroomSync.Core;

public sealed record TerminalDevice(
  string Id,
  string Name,
  bool IsPaired,
  bool IsInUse = false,
  int? Rssi = null
) {
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

  public override string ToString() => IsPaired ? $"{Name} (paired)" : Name;
}

public interface ITerminalConnection : IDisposable {
  event EventHandler<string>? TextReceived;
  event EventHandler<string>? ConnectionLost;

  Task<IReadOnlyList<TerminalDevice>> DiscoverAsync();
  Task ConnectAsync(TerminalDevice terminal);
  Task SendAsync(string command);
  Task DisconnectAsync();
}

public interface ITerminalPairingPasskeySink {
  void SetPairingPasskey(string? pairingPasskey);
}

public sealed class TerminalBondRepairRequiredException : InvalidOperationException {
  public TerminalBondRepairRequiredException(string message)
    : base("The operating system removed the Bluetooth bond. Repair the Bluetooth connection, then reconnect. " + message) { }
}
