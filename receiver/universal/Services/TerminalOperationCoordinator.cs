namespace BathroomSync.Universal.Services;

public sealed class TerminalOperationCoordinator : IDisposable {
  readonly SemaphoreSlim gate = new(1, 1);

  public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default) {
    await gate.WaitAsync(cancellationToken);
    try {
      await operation();
    } finally {
      gate.Release();
    }
  }

  public void Dispose() => gate.Dispose();
}
