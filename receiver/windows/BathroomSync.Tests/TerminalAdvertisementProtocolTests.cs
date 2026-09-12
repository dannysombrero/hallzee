using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class TerminalAdvertisementProtocolTests {
  [Theory]
  [InlineData("485A0100E5F6", false)]
  [InlineData("485A0101E5F6", true)]
  public void ParsesFirmwareStatusWithoutInferringOwnership(string wire, bool expectedClaimed) {
    Assert.True(TerminalAdvertisementProtocol.TryParse(Convert.FromHexString(wire), out var claimed, out var suffix));
    Assert.Equal(expectedClaimed, claimed);
    Assert.Equal("E5F6", suffix);
  }

  [Theory]
  [InlineData("")]
  [InlineData("485A0100E5")]
  [InlineData("485A0100E5F600")]
  [InlineData("005A0100E5F6")]
  [InlineData("485A0200E5F6")]
  [InlineData("485A0102E5F6")]
  public void RejectsMalformedOrUnknownStatusInsteadOfCallingItUnclaimed(string wire) {
    Assert.False(TerminalAdvertisementProtocol.TryParse(Convert.FromHexString(wire), out _, out var suffix));
    Assert.Null(suffix);
  }
}
