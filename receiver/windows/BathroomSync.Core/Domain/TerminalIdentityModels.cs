namespace BathroomSync.Core;

public enum TerminalSecurityState {
  Unknown,
  Nearby,
  Unclaimed,
  Claimed,
  OwnedByThisInstallation,
  Authenticated,
  IdentityMismatch,
  LegacyUnsecured,
  AuthenticationFailed
}

public sealed record TerminalIdentity(
  string TerminalId,
  string TerminalSuffix,
  bool IsClaimed,
  string Nonce
);

public sealed record AuthenticatedTerminalSession(
  string TerminalId,
  string ClientId,
  string CustomName,
  DateTime AuthenticatedAtUtc
);

public sealed class TerminalIdentityMismatchException : InvalidOperationException {
  public TerminalIdentityMismatchException(string expectedTerminalId, string observedTerminalId)
    : base($"Connected hardware does not match the saved terminal. Expected {expectedTerminalId}; observed {observedTerminalId}.") {
    ExpectedTerminalId = expectedTerminalId;
    ObservedTerminalId = observedTerminalId;
  }

  public string ExpectedTerminalId { get; }
  public string ObservedTerminalId { get; }
}
