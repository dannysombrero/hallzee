using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BathroomSync.Core;

public static partial class TerminalIdentityProtocol {
  public const int ProtocolVersion = 2;
  public const int TerminalIdLength = 15;
  public const int NonceByteLength = 16;
  public const int NonceHexLength = NonceByteLength * 2;
  public const int ProofHexLength = 64;
  public const int ClaimKeyLength = 16;

  static readonly Regex TerminalIdPattern = TerminalIdRegex();
  static readonly Regex HexPattern = HexRegex();
  static readonly Regex OwnerKeyPattern = OwnerKeyHexRegex();
  const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

  public static string BuildHello(string clientId) {
    var normalizedClientId = NormalizeClientId(clientId);
    return $"HELLO,{ProtocolVersion},{normalizedClientId}";
  }

  public static string BuildClaim(string clientId, string claimKey, TerminalIdentity identity) {
    var normalizedClientId = NormalizeClientId(clientId);
    var normalizedKey = NormalizeClaimKey(claimKey);
    ValidateIdentity(identity);
    return $"CLAIM,{ProtocolVersion},{normalizedClientId},{ComputeClaimProof(normalizedKey, normalizedClientId, identity)}";
  }

  public static string BuildClaimCommit(string clientId, string ownerKey, string terminalId, string commitNonce) {
    var normalizedClientId = NormalizeClientId(clientId);
    var normalizedTerminalId = NormalizeTerminalId(terminalId);
    var normalizedNonce = NormalizeNonce(commitNonce);
    return $"CLAIM_COMMIT,{ProtocolVersion},{normalizedClientId},{ComputeAuthProof(ownerKey, normalizedTerminalId, normalizedClientId, normalizedNonce)}";
  }

  public static string BuildClaimAbort(string clientId) {
    return $"CLAIM_ABORT,{ProtocolVersion},{NormalizeClientId(clientId)}";
  }

  public static string BuildAuth(string clientId, string ownerKey, TerminalIdentity identity) {
    var normalizedClientId = NormalizeClientId(clientId);
    ValidateIdentity(identity);
    return $"AUTH,{ProtocolVersion},{normalizedClientId},{ComputeAuthProof(ownerKey, identity.TerminalId, normalizedClientId, identity.Nonce)}";
  }

  public static bool TryParseIdentity(string message, out TerminalIdentity? identity) {
    identity = null;
    var fields = Split(message);
    if (fields.Length != 6 ||
        fields[0] != "IDENTITY" ||
        fields[1] != ProtocolVersion.ToString() ||
        !TryNormalizeTerminalId(fields[2], out var terminalId) ||
        !TryNormalizeNonce(fields[5], out var nonce)) {
      return false;
    }

    var suffix = fields[3].ToUpperInvariant();
    if (suffix != terminalId[^4..] || (fields[4] != "UNCLAIMED" && fields[4] != "CLAIMED")) {
      return false;
    }

    identity = new TerminalIdentity(terminalId, suffix, fields[4] == "CLAIMED", nonce);
    return true;
  }

  public static bool TryParseClaimOk(string message, out string? terminalId, out string? commitNonce) {
    terminalId = null;
    commitNonce = null;
    var fields = Split(message);
    if (fields.Length != 4 ||
        fields[0] != "CLAIM_OK" ||
        fields[1] != ProtocolVersion.ToString() ||
        !TryNormalizeTerminalId(fields[2], out var normalizedTerminalId) ||
        !TryNormalizeNonce(fields[3], out var normalizedNonce)) {
      return false;
    }

    terminalId = normalizedTerminalId;
    commitNonce = normalizedNonce;
    return true;
  }

  public static bool TryParseAuthOk(string message, out string? terminalId, out string? customName) {
    terminalId = null;
    customName = null;
    var fields = Split(message);
    if (fields.Length != 4 ||
        fields[0] != "AUTH_OK" ||
        fields[1] != ProtocolVersion.ToString() ||
        !TryNormalizeTerminalId(fields[2], out var normalizedTerminalId) ||
        !TryNormalizeTerminalName(fields[3], out var normalizedName)) {
      return false;
    }

    terminalId = normalizedTerminalId;
    customName = normalizedName;
    return true;
  }

  public static string ComputeClaimProof(string claimKey, string clientId, TerminalIdentity identity) {
    var normalizedKey = NormalizeClaimKey(claimKey);
    var normalizedClientId = NormalizeClientId(clientId);
    ValidateIdentity(identity);
    return HmacHex(
      Encoding.UTF8.GetBytes(normalizedKey),
      $"CLAIM|{ProtocolVersion}|{identity.TerminalId}|{normalizedClientId}|{identity.Nonce}"
    );
  }

  public static string ComputeAuthProof(string ownerKey, string terminalId, string clientId, string nonce) {
    var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
    var normalizedTerminalId = NormalizeTerminalId(terminalId);
    var normalizedClientId = NormalizeClientId(clientId);
    var normalizedNonce = NormalizeNonce(nonce);
    return HmacHex(
      normalizedOwnerKey,
      $"AUTH|{ProtocolVersion}|{normalizedTerminalId}|{normalizedClientId}|{normalizedNonce}"
    );
  }

  public static byte[] DeriveOwnerKey(string claimKey, string terminalId, string clientId) {
    var normalizedKey = NormalizeClaimKey(claimKey);
    var normalizedTerminalId = NormalizeTerminalId(terminalId);
    var normalizedClientId = NormalizeClientId(clientId);
    var salt = Encoding.UTF8.GetBytes(normalizedTerminalId);
    var info = Encoding.UTF8.GetBytes($"Hallzee owner v{ProtocolVersion}|{normalizedClientId}");
    return HkdfSha256(Encoding.UTF8.GetBytes(normalizedKey), salt, info, 32);
  }

  public static string NormalizeClientId(string clientId) {
    if (!Guid.TryParse(clientId, out var parsed)) {
      throw new FormatException("Client ID must be a UUID.");
    }
    return parsed.ToString("D").ToUpperInvariant();
  }

  public static string NormalizeTerminalId(string terminalId) {
    if (!TryNormalizeTerminalId(terminalId, out var normalized)) {
      throw new FormatException("Terminal ID must match HZ- followed by 12 uppercase hexadecimal characters.");
    }
    return normalized;
  }

  public static string NormalizeNonce(string nonce) {
    if (!TryNormalizeNonce(nonce, out var normalized)) {
      throw new FormatException($"Nonce must be {NonceHexLength} hexadecimal characters.");
    }
    return normalized;
  }

  public static string NormalizeClaimKey(string claimKey) {
    var normalized = claimKey.Replace("-", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
    if (normalized.Length != ClaimKeyLength ||
        normalized.Any(character => !CrockfordAlphabet.Contains(character))) {
      throw new FormatException($"Claim key must contain {ClaimKeyLength} Crockford Base32 characters.");
    }
    return normalized;
  }

  public static byte[] NormalizeOwnerKey(string ownerKey) {
    var normalized = ownerKey.Replace("-", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
    if (normalized.Length != 64 || !OwnerKeyPattern.IsMatch(normalized)) {
      throw new FormatException("Owner key must be 32 bytes represented as hexadecimal.");
    }
    return Convert.FromHexString(normalized);
  }

  public static bool TryNormalizeTerminalName(string name, out string normalized) {
    normalized = name.Trim();
    if (normalized.Length is < 1 or > 24 || normalized.Any(character =>
        !(char.IsLetterOrDigit(character) || character is ' ' or '-' or '_' or '(' or ')'))) {
      normalized = "";
      return false;
    }
    return true;
  }

  static string[] Split(string message) {
    return message.Trim().Split(',', StringSplitOptions.None);
  }

  static void ValidateIdentity(TerminalIdentity identity) {
    NormalizeTerminalId(identity.TerminalId);
    NormalizeNonce(identity.Nonce);
    if (!string.Equals(identity.TerminalSuffix, identity.TerminalId[^4..], StringComparison.OrdinalIgnoreCase)) {
      throw new FormatException("Terminal suffix does not match terminal ID.");
    }
  }

  static bool TryNormalizeTerminalId(string value, out string normalized) {
    normalized = value.Trim().ToUpperInvariant();
    return TerminalIdPattern.IsMatch(normalized);
  }

  static bool TryNormalizeNonce(string value, out string normalized) {
    normalized = value.Trim().ToUpperInvariant();
    return normalized.Length == NonceHexLength && HexPattern.IsMatch(normalized);
  }

  static string HmacHex(byte[] key, string message) {
    using var hmac = new HMACSHA256(key);
    return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
  }

  static byte[] HkdfSha256(byte[] inputKeyMaterial, byte[] salt, byte[] info, int length) {
    using var extract = new HMACSHA256(salt.Length == 0 ? new byte[32] : salt);
    var pseudorandomKey = extract.ComputeHash(inputKeyMaterial);
    var output = new byte[length];
    var previous = Array.Empty<byte>();
    var written = 0;
    byte counter = 1;

    using var expand = new HMACSHA256(pseudorandomKey);
    while (written < length) {
      expand.Initialize();
      var blockInput = new byte[previous.Length + info.Length + 1];
      Buffer.BlockCopy(previous, 0, blockInput, 0, previous.Length);
      Buffer.BlockCopy(info, 0, blockInput, previous.Length, info.Length);
      blockInput[^1] = counter++;
      previous = expand.ComputeHash(blockInput);
      var copyLength = Math.Min(previous.Length, length - written);
      Buffer.BlockCopy(previous, 0, output, written, copyLength);
      written += copyLength;
    }
    return output;
  }

  [GeneratedRegex("^HZ-[0-9A-F]{12}$", RegexOptions.CultureInvariant)]
  private static partial Regex TerminalIdRegex();

  [GeneratedRegex("^[0-9A-F]{32}$", RegexOptions.CultureInvariant)]
  private static partial Regex HexRegex();

  [GeneratedRegex("^[0-9A-F]{64}$", RegexOptions.CultureInvariant)]
  private static partial Regex OwnerKeyHexRegex();
}
