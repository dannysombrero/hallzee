using BathroomSync.Universal.Services;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class TerminalOperationCoordinatorTests {
  [Fact]
  public async Task OperationsRunInOrderWithoutInterleaving() {
    using var coordinator = new TerminalOperationCoordinator();
    var events = new List<string>();
    var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    var first = coordinator.RunAsync(async () => {
      events.Add("first-start");
      firstStarted.SetResult(true);
      await releaseFirst.Task;
      events.Add("first-end");
    });
    await firstStarted.Task;
    var second = coordinator.RunAsync(() => {
      events.Add("second");
      return Task.CompletedTask;
    });

    await Task.Delay(20);
    Assert.DoesNotContain("second", events);
    releaseFirst.SetResult(true);
    await Task.WhenAll(first, second);
    Assert.Equal(new[] { "first-start", "first-end", "second" }, events);
  }
}
