namespace BathroomSync.Core;

public sealed record TerminalDevice(
  string Id,
  string Name,
  bool IsPaired,
  bool IsInUse = false,
  int? Rssi = null
) {
  public string SignalStrengthText => Rssi is null ? "Signal unavailable" : $"{Rssi} dBm";
  public string SignalQuality => Rssi switch {
    >= -60 => "Excellent",
    >= -70 => "Good",
    >= -80 => "Fair",
    null => "Unknown",
    _ => "Weak"
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
