using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class TerminalSessionTests {
  const string ClientId = "12345678-1234-1234-1234-1234567890ab";
  const string TerminalId = "HZ-A1B2C3D4E5F6";
  const string Nonce = "00112233445566778899AABBCCDDEEFF";

  [Fact]
  public async Task DoesNotExposeApplicationDataBeforeAuthOk() {
    var connection = new FakeTerminalConnection(claimed: true);
    using var session = new TerminalSession(
      connection,
      new InMemoryTerminalCredentialStore(),
      ClientId);
    var received = new List<string>();
    session.ApplicationDataReceived += (_, text) => received.Add(text);

    await session.OpenAsync(new TerminalDevice("transport-1", "Hallzee-E5F6", false), TerminalId);
    connection.Emit("TRIP,1,10482,2026-09-03,09:00:00,09:05:00,300,COMPLETED\n");
    Assert.Empty(received);
  }

  [Fact]
  public async Task AuthenticatedSessionForwardsApplicationData() {
    var connection = new FakeTerminalConnection(claimed: true);
    var credentials = new InMemoryTerminalCredentialStore();
    var ownerKey = TerminalIdentityProtocol.DeriveOwnerKey("807481", TerminalId, ClientId);
    credentials.SaveOwnerKey(TerminalId, ownerKey);
    using var session = new TerminalSession(connection, credentials, ClientId);
    var received = new List<string>();
    session.ApplicationDataReceived += (_, text) => received.Add(text);

    await session.OpenAsync(new TerminalDevice("transport-1", "Hallzee-E5F6", false), TerminalId);
    await session.AuthenticateAsync();
    connection.Emit("TRIP,1,10482,2026-09-03,09:00:00,09:05:00,300,COMPLETED\n");

    Assert.Single(received);
    Assert.StartsWith("TRIP,1,", received[0]);
    Assert.Equal(TerminalSessionState.Authenticated, session.State);
  }

  [Fact]
  public async Task SameOwnerReconnectsWithoutThePhysicalPasskey() {
    var connection = new FakeTerminalConnection(claimed: true);
    var credentials = new InMemoryTerminalCredentialStore();
    var ownerKey = TerminalIdentityProtocol.DeriveOwnerKey("807481", TerminalId, ClientId);
    credentials.SaveOwnerKey(TerminalId, ownerKey);
    using var session = new TerminalSession(connection, credentials, ClientId);
    var device = new TerminalDevice("transport-1", "Hallzee-E5F6", false);

    await session.OpenAsync(device, TerminalId);
    await session.AuthenticateAsync();
    await session.DisconnectAsync();
    await session.OpenAsync(device, TerminalId);
    await session.AuthenticateAsync();

    Assert.Equal(TerminalSessionState.Authenticated, session.State);
    Assert.Equal(2, connection.SentCommands.Count(command => command.StartsWith("AUTH,2,")));
    Assert.DoesNotContain(connection.SentCommands, command => command.StartsWith("CLAIM,2,"));
  }

  [Fact]
  public async Task DisconnectsWhenObservedIdentityDiffersFromExpected() {
    var connection = new FakeTerminalConnection(claimed: true, terminalId: "HZ-112233445566");
    using var session = new TerminalSession(
      connection,
      new InMemoryTerminalCredentialStore(),
      ClientId);

    var error = await Assert.ThrowsAsync<TerminalIdentityMismatchException>(() =>
      session.OpenAsync(new TerminalDevice("transport-1", "Hallzee-5566", false), TerminalId));

    Assert.Equal(TerminalId, error.ExpectedTerminalId);
    Assert.Equal("HZ-112233445566", error.ObservedTerminalId);
    Assert.Equal(TerminalSessionState.Failed, session.State);
    Assert.Equal(2, connection.DisconnectCount);
  }

  [Fact]
  public async Task RefusesAnInUseConnectionWithoutTheOwnerCredential() {
    var connection = new FakeTerminalConnection(claimed: true, inUse: true);
    using var session = new TerminalSession(
      connection,
      new InMemoryTerminalCredentialStore(),
      ClientId);

    await Assert.ThrowsAsync<TerminalInUseException>(() =>
      session.OpenAsync(new TerminalDevice("transport-1", "Hallzee-E5F6", false), TerminalId));

    Assert.Equal(TerminalSessionState.Failed, session.State);
  }

  [Fact]
  public async Task OwnerCanReconnectWhenTheTerminalReportsInUse() {
    var connection = new FakeTerminalConnection(claimed: true, inUse: true);
    var credentials = new InMemoryTerminalCredentialStore();
    var ownerKey = TerminalIdentityProtocol.DeriveOwnerKey("807481", TerminalId, ClientId);
    credentials.SaveOwnerKey(TerminalId, ownerKey);
    using var session = new TerminalSession(connection, credentials, ClientId);

    await session.OpenAsync(new TerminalDevice("transport-1", "Hallzee-E5F6", false), TerminalId);
    await session.AuthenticateAsync();

    Assert.Equal(TerminalSessionState.Authenticated, session.State);
    Assert.Contains(connection.SentCommands, command => command.StartsWith("AUTH,2,", StringComparison.Ordinal));
  }

  [Fact]
  public async Task ClaimRequiredLeavesTheVerifiedConnectionOpenForTheClaimFlow() {
    var connection = new FakeTerminalConnection(claimed: false);
    using var session = new TerminalSession(
      connection,
      new InMemoryTerminalCredentialStore(),
      ClientId);

    await session.OpenAsync(new TerminalDevice("transport-1", "Hallzee-E5F6", false), TerminalId);
    await Assert.ThrowsAsync<TerminalClaimRequiredException>(() => session.AuthenticateAsync());
    Assert.Equal(TerminalSessionState.ClaimRequired, session.State);
    Assert.Equal(1, connection.DisconnectCount);

    await session.AuthenticateAsync("807481");
    Assert.Equal(TerminalSessionState.Authenticated, session.State);
    Assert.Contains(connection.SentCommands, command => command.StartsWith("CLAIM,2,", StringComparison.Ordinal));
    Assert.Contains(connection.SentCommands, command => command.StartsWith("CLAIM_COMMIT,2,", StringComparison.Ordinal));
  }

  [Fact]
  public async Task AuthorizedRequestRequiresExactCompleteAckAndKeepsKeyOnCancellation() {
    var connection = new FakeTerminalConnection(claimed: true);
    var credentials = new InMemoryTerminalCredentialStore();
    credentials.SaveOwnerKey(TerminalId, TerminalIdentityProtocol.DeriveOwnerKey("807481", TerminalId, ClientId));
    using var session = new TerminalSession(connection, credentials, ClientId);
    await Assert.ThrowsAsync<InvalidOperationException>(() => session.RequestAuthorizedAsync("RELEASE_OWNER", "OWNER_RELEASED"));
    await session.OpenAsync(new TerminalDevice("transport-1", "Hallzee", true));
    await session.AuthenticateAsync();
    var request = session.RequestAuthorizedAsync("RELEASE_OWNER", "OWNER_RELEASED");
    connection.Emit("SETTINGS_ACK,MAX_ID_LENGTH,10\nOWNER_RELE");
    Assert.False(request.IsCompleted);
    connection.Emit("ASED\n");
    await request;
    using var cancellation = new CancellationTokenSource();
    request = session.RequestAuthorizedAsync("RELEASE_OWNER", "OWNER_RELEASED", cancellation.Token);
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    Assert.True(credentials.TryGetOwnerKey(TerminalId, out _));
  }

  sealed class FakeTerminalConnection : ITerminalConnection {
    readonly bool claimed;
    readonly bool inUse;
    readonly string terminalId;
    readonly string nonce;

    public FakeTerminalConnection(
      bool claimed,
      string terminalId = TerminalId,
      string nonce = Nonce,
      bool inUse = false) {
      this.claimed = claimed;
      this.inUse = inUse;
      this.terminalId = terminalId;
      this.nonce = nonce;
    }

    public event EventHandler<string>? TextReceived;
    public event EventHandler<string>? ConnectionLost {
      add { }
      remove { }
    }
    public List<string> SentCommands { get; } = new();
    public int DisconnectCount { get; private set; }

    public Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() =>
      Task.FromResult<IReadOnlyList<TerminalDevice>>(Array.Empty<TerminalDevice>());

    public Task ConnectAsync(TerminalDevice terminal) => Task.CompletedTask;

    public Task SendAsync(string command) {
      SentCommands.Add(command);
      if (command.StartsWith("HELLO,2,", StringComparison.Ordinal)) {
        var state = claimed ? "CLAIMED" : "UNCLAIMED";
        Emit($"IDENTITY,2,{terminalId},{terminalId[^4..]},{state},{(inUse ? "IN_USE" : "AVAILABLE")},{nonce}\n");
      } else if (command.StartsWith("CLAIM,2,", StringComparison.Ordinal)) {
        Emit($"CLAIM_OK,2,{terminalId},FFEEDDCCBBAA99887766554433221100\n");
      } else if (command.StartsWith("CLAIM_COMMIT,2,", StringComparison.Ordinal) ||
                 command.StartsWith("AUTH,2,", StringComparison.Ordinal)) {
        Emit($"AUTH_OK,2,{terminalId},East Door\n");
      }
      return Task.CompletedTask;
    }

    public Task DisconnectAsync() {
      DisconnectCount++;
      return Task.CompletedTask;
    }

    public void Emit(string text) => TextReceived?.Invoke(this, text);

    public void Dispose() {
      TextReceived = null;
    }
  }
}
