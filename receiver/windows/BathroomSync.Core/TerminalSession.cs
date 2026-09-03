namespace BathroomSync.Core;

public enum TerminalSessionState {
  Disconnected,
  Connecting,
  AwaitingIdentity,
  ClaimRequired,
  AwaitingAuthentication,
  Authenticated,
  Failed
}

public sealed class TerminalClaimRequiredException : InvalidOperationException {
  public TerminalClaimRequiredException(string terminalId)
    : base($"Terminal {terminalId} is unclaimed and requires the physical Bluetooth passkey.") {
    TerminalId = terminalId;
  }

  public string TerminalId { get; }
}

public sealed class TerminalCredentialMissingException : InvalidOperationException {
  public TerminalCredentialMissingException(string terminalId)
    : base($"No owner credential is available for terminal {terminalId}.") {
    TerminalId = terminalId;
  }

  public string TerminalId { get; }
}

public sealed class TerminalSession : IAsyncDisposable, IDisposable {
  static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

  readonly ITerminalConnection connection;
  readonly ITerminalCredentialStore credentialStore;
  readonly string clientId;
  readonly SemaphoreSlim operationLock = new(1, 1);
  readonly object inputGate = new();
  readonly System.Text.StringBuilder input = new();

  TaskCompletionSource<TerminalIdentity>? identityCompletion;
  TaskCompletionSource<(string TerminalId, string CommitNonce)>? claimCompletion;
  TaskCompletionSource<AuthenticatedTerminalSession>? authCompletion;
  TerminalDevice? selectedDevice;
  TerminalIdentity? identity;
  string? expectedTerminalId;
  bool disposed;

  public TerminalSession(
    ITerminalConnection connection,
    ITerminalCredentialStore credentialStore,
    string clientId) {
    this.connection = connection;
    this.credentialStore = credentialStore;
    this.clientId = TerminalIdentityProtocol.NormalizeClientId(clientId);
    connection.TextReceived += HandleTextReceived;
    connection.ConnectionLost += HandleConnectionLost;
    State = TerminalSessionState.Disconnected;
  }

  public event EventHandler<TerminalSessionState>? StateChanged;
  public event EventHandler<TerminalIdentity>? IdentityReceived;
  public event EventHandler<string>? ApplicationDataReceived;
  public event EventHandler<string>? SessionError;

  public TerminalSessionState State { get; private set; }
  public TerminalIdentity? Identity => identity;
  public AuthenticatedTerminalSession? AuthenticatedTerminal { get; private set; }
  public TerminalDevice? SelectedDevice => selectedDevice;

  public async Task<TerminalIdentity> OpenAsync(
    TerminalDevice device,
    string? expectedTerminalId = null,
    CancellationToken cancellationToken = default) {
    await operationLock.WaitAsync(cancellationToken);
    try {
      await DisconnectCoreAsync();
      selectedDevice = device;
      this.expectedTerminalId = string.IsNullOrWhiteSpace(expectedTerminalId)
        ? null
        : TerminalIdentityProtocol.NormalizeTerminalId(expectedTerminalId);
      identity = null;
      AuthenticatedTerminal = null;
      SetState(TerminalSessionState.Connecting);

      identityCompletion = NewCompletion<TerminalIdentity>();
      try {
        await connection.ConnectAsync(device);
        SetState(TerminalSessionState.AwaitingIdentity);
        await connection.SendAsync(TerminalIdentityProtocol.BuildHello(clientId));
        var observed = await WaitAsync(identityCompletion.Task, cancellationToken);
        if (this.expectedTerminalId is not null &&
            !string.Equals(this.expectedTerminalId, observed.TerminalId, StringComparison.OrdinalIgnoreCase)) {
          throw new TerminalIdentityMismatchException(this.expectedTerminalId, observed.TerminalId);
        }
        if (observed.IsInUse) {
          throw new TerminalInUseException(observed.TerminalId);
        }
        identity = observed;
        SetState(observed.IsClaimed
          ? TerminalSessionState.AwaitingAuthentication
          : TerminalSessionState.ClaimRequired);
        IdentityReceived?.Invoke(this, observed);
        return observed;
      } catch {
        await DisconnectCoreAsync();
        SetState(TerminalSessionState.Failed);
        throw;
      } finally {
        identityCompletion = null;
      }
    } finally {
      operationLock.Release();
    }
  }

  public async Task<AuthenticatedTerminalSession> AuthenticateAsync(
    string? pairingPasskey = null,
    CancellationToken cancellationToken = default) {
    await operationLock.WaitAsync(cancellationToken);
    try {
      if (identity is null || State is not (TerminalSessionState.ClaimRequired or TerminalSessionState.AwaitingAuthentication)) {
        throw new InvalidOperationException("The terminal must be opened before authentication.");
      }
      if (!identity.IsClaimed && string.IsNullOrWhiteSpace(pairingPasskey)) {
        throw new TerminalClaimRequiredException(identity.TerminalId);
      }

      authCompletion = NewCompletion<AuthenticatedTerminalSession>();
      try {
        if (!identity.IsClaimed) {
          claimCompletion = NewCompletion<(string TerminalId, string CommitNonce)>();
          await connection.SendAsync(TerminalIdentityProtocol.BuildClaim(clientId, pairingPasskey!, identity));
          var claimResult = await WaitAsync(claimCompletion.Task, cancellationToken);
          if (!string.Equals(claimResult.TerminalId, identity.TerminalId, StringComparison.OrdinalIgnoreCase)) {
            throw new TerminalIdentityMismatchException(identity.TerminalId, claimResult.TerminalId);
          }

          var ownerKey = TerminalIdentityProtocol.DeriveOwnerKey(
            pairingPasskey!, identity.TerminalId, clientId);
          try {
            credentialStore.SaveOwnerKey(identity.TerminalId, ownerKey);
            await connection.SendAsync(TerminalIdentityProtocol.BuildClaimCommit(
              clientId,
              Convert.ToHexString(ownerKey),
              identity.TerminalId,
              claimResult.CommitNonce));
          } catch {
            try { await connection.SendAsync(TerminalIdentityProtocol.BuildClaimAbort(clientId)); } catch { }
            credentialStore.DeleteOwnerKey(identity.TerminalId);
            throw;
          } finally {
            Array.Clear(ownerKey);
          }
        } else {
          if (!credentialStore.TryGetOwnerKey(identity.TerminalId, out var ownerKey)) {
            throw new TerminalCredentialMissingException(identity.TerminalId);
          }
          try {
            await connection.SendAsync(TerminalIdentityProtocol.BuildAuth(
              clientId, Convert.ToHexString(ownerKey), identity));
          } finally {
            Array.Clear(ownerKey);
          }
        }

        var authenticated = await WaitAsync(authCompletion.Task, cancellationToken);
        AuthenticatedTerminal = authenticated;
        SetState(TerminalSessionState.Authenticated);
        return authenticated;
      } catch {
        authCompletion?.TrySetCanceled();
        await DisconnectCoreAsync();
        SetState(TerminalSessionState.Failed);
        throw;
      } finally {
        claimCompletion = null;
        authCompletion = null;
      }
    } finally {
      operationLock.Release();
    }
  }

  public async Task SendAuthorizedAsync(string command, CancellationToken cancellationToken = default) {
    await operationLock.WaitAsync(cancellationToken);
    try {
      if (State != TerminalSessionState.Authenticated) {
        throw new InvalidOperationException("The terminal session is not authenticated.");
      }
      await connection.SendAsync(command);
    } finally {
      operationLock.Release();
    }
  }

  public async Task DisconnectAsync() {
    await operationLock.WaitAsync();
    try {
      await DisconnectCoreAsync();
      SetState(TerminalSessionState.Disconnected);
    } finally {
      operationLock.Release();
    }
  }

  async Task DisconnectCoreAsync() {
    identityCompletion?.TrySetCanceled();
    claimCompletion?.TrySetCanceled();
    authCompletion?.TrySetCanceled();
    identityCompletion = null;
    claimCompletion = null;
    authCompletion = null;
    identity = null;
    AuthenticatedTerminal = null;
    selectedDevice = null;
    await connection.DisconnectAsync();
  }

  void HandleTextReceived(object? sender, string text) {
    List<string> lines = new();
    lock (inputGate) {
      input.Append(text);
      while (true) {
        var newlineIndex = input.ToString().IndexOf('\n');
        if (newlineIndex < 0) break;
        lines.Add(input.ToString(0, newlineIndex).Trim());
        input.Remove(0, newlineIndex + 1);
      }
    }

    foreach (var line in lines) HandleLine(line);
  }

  void HandleLine(string line) {
    if (TerminalIdentityProtocol.TryParseIdentity(line, out var parsedIdentity) && parsedIdentity is not null) {
      identityCompletion?.TrySetResult(parsedIdentity);
      return;
    }

    if (TerminalIdentityProtocol.TryParseClaimOk(line, out var terminalId, out var commitNonce) &&
        terminalId is not null && commitNonce is not null) {
      claimCompletion?.TrySetResult((terminalId, commitNonce));
      return;
    }

    if (TerminalIdentityProtocol.TryParseAuthOk(line, out terminalId, out var customName) &&
        terminalId is not null && customName is not null) {
      authCompletion?.TrySetResult(new AuthenticatedTerminalSession(
        terminalId, clientId, customName, DateTime.UtcNow));
      return;
    }

    if (line.StartsWith("ERROR,", StringComparison.Ordinal)) {
      var error = new InvalidOperationException($"Terminal rejected the session: {line}");
      identityCompletion?.TrySetException(error);
      claimCompletion?.TrySetException(error);
      authCompletion?.TrySetException(error);
      SessionError?.Invoke(this, line);
      return;
    }

    if (State == TerminalSessionState.Authenticated) {
      ApplicationDataReceived?.Invoke(this, line + "\n");
    }
  }

  void HandleConnectionLost(object? sender, string detail) {
    identityCompletion?.TrySetException(new IOException(detail));
    claimCompletion?.TrySetException(new IOException(detail));
    authCompletion?.TrySetException(new IOException(detail));
    AuthenticatedTerminal = null;
    if (State != TerminalSessionState.Disconnected) SetState(TerminalSessionState.Failed);
    SessionError?.Invoke(this, detail);
  }

  void SetState(TerminalSessionState state) {
    if (State == state) return;
    State = state;
    StateChanged?.Invoke(this, state);
  }

  static TaskCompletionSource<T> NewCompletion<T>() =>
    new(TaskCreationOptions.RunContinuationsAsynchronously);

  static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken) {
    return await task.WaitAsync(HandshakeTimeout, cancellationToken);
  }

  public void Dispose() {
    if (disposed) return;
    disposed = true;
    connection.TextReceived -= HandleTextReceived;
    connection.ConnectionLost -= HandleConnectionLost;
    operationLock.Dispose();
    connection.Dispose();
  }

  public ValueTask DisposeAsync() {
    Dispose();
    return ValueTask.CompletedTask;
  }
}
