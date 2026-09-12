namespace BathroomSync.Core;

// Discovery hints only. HELLO verifies the full identity and AUTH/CLAIM
// establishes ownership before any classroom data or settings are accessed.
public static class TerminalAdvertisementProtocol {
  public const ushort CompanyId = 0xFFFF;

  public static bool TryParse(ReadOnlySpan<byte> payload, out bool isClaimed, out string? suffix) {
    isClaimed = false;
    suffix = null;
    if (payload.Length != 6 || payload[0] != (byte)'H' || payload[1] != (byte)'Z' ||
        payload[2] != 1 || (payload[3] & ~1) != 0) return false;
    isClaimed = (payload[3] & 1) != 0;
    suffix = Convert.ToHexString(payload[4..]);
    return true;
  }
}
