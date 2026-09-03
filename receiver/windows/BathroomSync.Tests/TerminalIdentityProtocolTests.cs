using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class TerminalIdentityProtocolTests {
  const string ClientId = "12345678-1234-1234-1234-1234567890ab";
  const string TerminalId = "HZ-A1B2C3D4E5F6";
  const string Nonce = "00112233445566778899AABBCCDDEEFF";
  const string PairingPasskey = "807481";

  static TerminalIdentity Identity(bool claimed = false) =>
    new(TerminalId, "E5F6", claimed, Nonce, false);

  [Fact]
  public void BuildsAndParsesHelloAndIdentity() {
    var hello = TerminalIdentityProtocol.BuildHello(ClientId);
    Assert.Equal("HELLO,2,12345678-1234-1234-1234-1234567890AB", hello);

    Assert.True(TerminalIdentityProtocol.TryParseIdentity(
      $"IDENTITY,2,{TerminalId},E5F6,UNCLAIMED,AVAILABLE,{Nonce}", out var identity));
    Assert.NotNull(identity);
    Assert.Equal(TerminalId, identity!.TerminalId);
    Assert.Equal("E5F6", identity.TerminalSuffix);
    Assert.False(identity.IsClaimed);
    Assert.False(identity.IsInUse);
  }

  [Theory]
  [InlineData("HZ-1234")]
  [InlineData("AA-A1B2C3D4E5F6")]
  [InlineData("HZ-A1B2C3D4E5FZ")]
  public void RejectsInvalidTerminalIds(string terminalId) {
    Assert.Throws<FormatException>(() => TerminalIdentityProtocol.NormalizeTerminalId(terminalId));
  }

  [Fact]
  public void RejectsIdentityWhenSuffixOrNonceDoesNotMatch() {
    Assert.False(TerminalIdentityProtocol.TryParseIdentity(
      $"IDENTITY,2,{TerminalId},0000,CLAIMED,AVAILABLE,{Nonce}", out _));
    Assert.False(TerminalIdentityProtocol.TryParseIdentity(
      $"IDENTITY,2,{TerminalId},E5F6,CLAIMED,AVAILABLE,not-a-nonce", out _));
  }

  [Fact]
  public void ParsesInUseIdentity() {
    Assert.True(TerminalIdentityProtocol.TryParseIdentity(
      $"IDENTITY,2,{TerminalId},E5F6,CLAIMED,IN_USE,{Nonce}", out var identity));
    Assert.True(identity!.IsInUse);
  }

  [Theory]
  [InlineData("")]
  [InlineData("12345")]
  [InlineData("1234567")]
  [InlineData("12A456")]
  public void RejectsInvalidPairingPasskeys(string passkey) {
    Assert.Throws<FormatException>(() =>
      TerminalIdentityProtocol.NormalizePairingPasskey(passkey));
  }

  [Fact]
  public void ClaimAndAuthProofsAreDeterministicAndUseExpectedLengths() {
    var claimProof = TerminalIdentityProtocol.ComputeClaimProof(PairingPasskey, ClientId, Identity());
    var ownerKey = TerminalIdentityProtocol.DeriveOwnerKey(PairingPasskey, TerminalId, ClientId);
    var ownerKeyHex = Convert.ToHexString(ownerKey);
    var authProof = TerminalIdentityProtocol.ComputeAuthProof(ownerKeyHex, TerminalId, ClientId, Nonce);

    Assert.Equal(64, claimProof.Length);
    Assert.Equal(64, authProof.Length);
    Assert.Equal(claimProof, TerminalIdentityProtocol.ComputeClaimProof(PairingPasskey, ClientId, Identity()));
    Assert.Equal(authProof, TerminalIdentityProtocol.ComputeAuthProof(ownerKeyHex, TerminalId, ClientId, Nonce));
    Assert.NotEqual(claimProof, authProof);
  }

  [Fact]
  public void ClaimAndAuthCommandsContainNormalizedProtocolFields() {
    var claim = TerminalIdentityProtocol.BuildClaim(ClientId.ToLowerInvariant(), PairingPasskey, Identity());
    Assert.StartsWith("CLAIM,2,12345678-1234-1234-1234-1234567890AB,", claim);

    var ownerKey = Convert.ToHexString(
      TerminalIdentityProtocol.DeriveOwnerKey(PairingPasskey, TerminalId, ClientId));
    var auth = TerminalIdentityProtocol.BuildAuth(ClientId, ownerKey.ToLowerInvariant(), Identity(true));
    Assert.StartsWith("AUTH,2,12345678-1234-1234-1234-1234567890AB,", auth);
    Assert.Equal(64, auth.Split(',')[3].Length);
  }

  [Fact]
  public void ClaimOkAndAuthOkAreParsedOnlyWhenStrictlyValid() {
    Assert.True(TerminalIdentityProtocol.TryParseClaimOk(
      $"CLAIM_OK,2,{TerminalId},{Nonce}", out var claimTerminalId, out var commitNonce));
    Assert.Equal(TerminalId, claimTerminalId);
    Assert.Equal(Nonce, commitNonce);

    Assert.True(TerminalIdentityProtocol.TryParseAuthOk(
      $"AUTH_OK,2,{TerminalId},Room 204", out var authTerminalId, out var name));
    Assert.Equal(TerminalId, authTerminalId);
    Assert.Equal("Room 204", name);

    Assert.False(TerminalIdentityProtocol.TryParseAuthOk(
      $"AUTH_OK,1,{TerminalId},Room 204", out _, out _));
    Assert.False(TerminalIdentityProtocol.TryParseAuthOk(
      $"AUTH_OK,2,{TerminalId},Room,204", out _, out _));
  }

  [Theory]
  [InlineData("")]
  [InlineData("Room,204")]
  [InlineData("Room\n204")]
  [InlineData("  ")]
  [InlineData("This terminal name is much too long")]
  public void RejectsUnsafeTerminalNames(string name) {
    Assert.False(TerminalIdentityProtocol.TryNormalizeTerminalName(name, out _));
  }

  [Fact]
  public void InMemoryCredentialStoreCopiesAndDeletesSecrets() {
    var store = new InMemoryTerminalCredentialStore();
    var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    store.SaveOwnerKey(TerminalId, key);
    key[0] = 255;

    Assert.True(store.TryGetOwnerKey(TerminalId, out var saved));
    Assert.Equal(0, saved[0]);

    store.DeleteOwnerKey(TerminalId);
    Assert.False(store.TryGetOwnerKey(TerminalId, out _));
  }
}
