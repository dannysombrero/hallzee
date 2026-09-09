using Microsoft.Data.Sqlite;
using Xunit;

namespace BathroomSync.Tests;

public sealed class NativeSqliteTests {
  [Fact]
  public void LoadsPatchedNativeEngineWithoutManualProviderInitialization() {
    // Exercise the native library actually loaded on this runner, not just the
    // NuGet version. This also catches missing RID assets or a system fallback.
    using var connection = new SqliteConnection("Data Source=:memory:;Pooling=False");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT sqlite_version();";
    var actual = (string)command.ExecuteScalar()!;
    Assert.True(Version.Parse(actual) >= new Version(3, 53, 4),
      $"Expected the patched bundled SQLite engine (3.53.4 or newer), loaded {actual}.");
  }
}
