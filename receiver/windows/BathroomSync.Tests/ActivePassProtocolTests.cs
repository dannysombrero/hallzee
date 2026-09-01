using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class ActivePassProtocolTests {
  [Fact]
  public void BuildsGetActivePassCommand() {
    Assert.Equal("GET_ACTIVE_PASS\n", ActivePassProtocol.BuildGetActivePassCommand());
  }

  [Fact]
  public void ParsesActivePassAvailableResponse() {
    Assert.True(ActivePassProtocol.TryParseActivePassResponse("ACTIVE_PASS,NONE\n", out var info));
    Assert.NotNull(info);
    Assert.Equal(ActivePassStatus.Available, info.Status);
    Assert.Null(info.StudentId);
    Assert.Null(info.CheckoutEpochSeconds);
    Assert.Null(info.CheckedOutAt);
  }

  [Fact]
  public void ParsesActivePassOccupiedResponse() {
    var epoch = 1725204120L;
    Assert.True(ActivePassProtocol.TryParseActivePassResponse($"ACTIVE_PASS,10482,{epoch}\n", out var info));
    Assert.NotNull(info);
    Assert.Equal(ActivePassStatus.Occupied, info.Status);
    Assert.Equal("10482", info.StudentId);
    Assert.Equal(epoch, info.CheckoutEpochSeconds);
    Assert.NotNull(info.CheckedOutAt);
  }

  [Fact]
  public void RejectsMalformedActivePassResponses() {
    Assert.False(ActivePassProtocol.TryParseActivePassResponse("", out _));
    Assert.False(ActivePassProtocol.TryParseActivePassResponse("UNKNOWN_LINE\n", out _));
    Assert.False(ActivePassProtocol.TryParseActivePassResponse("ACTIVE_PASS\n", out _));
    Assert.False(ActivePassProtocol.TryParseActivePassResponse("ACTIVE_PASS,10482,NOT_A_NUMBER\n", out _));
    Assert.False(ActivePassProtocol.TryParseActivePassResponse("ACTIVE_PASS,10482,-5\n", out _));
  }

  [Fact]
  public void ParsesLivePassEvents() {
    // Checkout event
    var epoch = 1725204120L;
    Assert.True(ActivePassProtocol.TryParseLiveEvent($"EVENT,CHECKOUT,10482,{epoch}\n", out var checkoutEvt));
    Assert.NotNull(checkoutEvt);
    Assert.Equal(LivePassEventType.Checkout, checkoutEvt.EventType);
    Assert.Equal("10482", checkoutEvt.StudentId);
    Assert.Equal(epoch, checkoutEvt.Value);
    Assert.NotNull(checkoutEvt.CheckoutTime);
    Assert.Equal(0, checkoutEvt.DurationSeconds);

    // Checkin event
    Assert.True(ActivePassProtocol.TryParseLiveEvent("EVENT,CHECKIN,10482,360\n", out var checkinEvt));
    Assert.NotNull(checkinEvt);
    Assert.Equal(LivePassEventType.Checkin, checkinEvt.EventType);
    Assert.Equal("10482", checkinEvt.StudentId);
    Assert.Equal(360, checkinEvt.Value);
    Assert.Equal(360, checkinEvt.DurationSeconds);
    Assert.Null(checkinEvt.CheckoutTime);

    // Reset event
    Assert.True(ActivePassProtocol.TryParseLiveEvent("EVENT,RESET,10482,0\n", out var resetEvt));
    Assert.NotNull(resetEvt);
    Assert.Equal(LivePassEventType.Reset, resetEvt.EventType);
    Assert.Equal("10482", resetEvt.StudentId);
    Assert.Equal(0, resetEvt.Value);
  }

  [Fact]
  public void RejectsMalformedLiveEvents() {
    Assert.False(ActivePassProtocol.TryParseLiveEvent("", out _));
    Assert.False(ActivePassProtocol.TryParseLiveEvent("EVENT,UNKNOWN,10482,100\n", out _));
    Assert.False(ActivePassProtocol.TryParseLiveEvent("EVENT,CHECKOUT,,100\n", out _));
    Assert.False(ActivePassProtocol.TryParseLiveEvent("EVENT,CHECKOUT,10482,ABC\n", out _));
    Assert.False(ActivePassProtocol.TryParseLiveEvent("EVENT,CHECKOUT\n", out _));
  }
}
