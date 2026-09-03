namespace BathroomSync.Core;

public interface ITerminalCredentialStore {
  bool TryGetOwnerKey(string terminalId, out byte[] ownerKey);
  void SaveOwnerKey(string terminalId, ReadOnlySpan<byte> ownerKey);
  void DeleteOwnerKey(string terminalId);
}

public sealed class InMemoryTerminalCredentialStore : ITerminalCredentialStore {
  readonly Dictionary<string, byte[]> keys = new(StringComparer.OrdinalIgnoreCase);

  public bool TryGetOwnerKey(string terminalId, out byte[] ownerKey) {
    if (keys.TryGetValue(terminalId, out var stored)) {
      ownerKey = stored.ToArray();
      return true;
    }
    ownerKey = Array.Empty<byte>();
    return false;
  }

  public void SaveOwnerKey(string terminalId, ReadOnlySpan<byte> ownerKey) {
    if (ownerKey.Length != 32) throw new ArgumentException("Owner key must be 32 bytes.", nameof(ownerKey));
    keys[terminalId] = ownerKey.ToArray();
  }

  public void DeleteOwnerKey(string terminalId) {
    if (!keys.Remove(terminalId, out var key)) return;
    Array.Clear(key);
  }
}
