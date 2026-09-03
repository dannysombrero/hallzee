namespace BathroomSync.Core;

public sealed record TerminalDevice(string Id, string Name, bool IsPaired, bool IsInUse = false) {
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
