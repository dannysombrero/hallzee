namespace BathroomSync.Core;

public sealed record TerminalDevice(string Id, string Name, bool IsPaired) {
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
