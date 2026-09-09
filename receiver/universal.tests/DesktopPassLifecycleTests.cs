using BathroomSync.Core;
using BathroomSync.Universal.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class DesktopPassLifecycleTests : IDisposable {
  readonly string folder = Path.Combine(Path.GetTempPath(), "HallzeeDesktopPassTests", Guid.NewGuid().ToString("N"));
  string Database => Path.Combine(folder, "hallzee-trips.db");
  TripSqliteRepository Repository => new(Database);
  MainViewModel Create(TestConnection connection) => new(connection, folder, isPreviewMode: true);
  static void Start(MainViewModel vm, string id = "001234") {
    vm.OpenModal("ManualCheckIn");
    vm.ManualCheckInModal.StudentId = id;
    vm.ManualCheckInModal.StudentName = "Avery Chen";
    vm.ManualCheckInModal.Period = "Period 3";
    vm.ManualCheckInModal.Destination = "Nurse";
    vm.ManualCheckInModal.Purpose = "Visit";
    vm.SubmitManualCheckIn();
    Assert.True(vm.ActivePass.IsManual);
  }
  public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }

  [Fact] public async Task RestartRestoresDetailsAndOfflineCheckinCompletesExactlyOnce() {
    DateTime? started;
    using (var vm = Create(new())) {
      Start(vm);
      started = vm.ActivePass.CheckoutTime;
    }
    var connection = new TestConnection();
    using (var vm = Create(connection)) {
      Assert.True(vm.HasStudentsOut);
      Assert.True(vm.ActivePass.IsManual);
      Assert.Equal("001234", vm.ActivePass.StudentId);
      Assert.Equal("Avery Chen", vm.ActivePass.StudentName);
      Assert.Equal("Period 3", vm.ActivePass.Period);
      Assert.Equal("Nurse", vm.ActivePass.Destination);
      Assert.Equal("Visit", vm.ActivePass.Purpose);
      Assert.Equal(started, vm.ActivePass.CheckoutTime);
      await vm.CheckInActivePassAsync();
      await vm.CheckInActivePassAsync();
      Assert.False(vm.HasStudentsOut);
      Assert.Empty(connection.Sent);
    }
    using var reopened = Create(new());
    Assert.False(reopened.ActivePass.IsOccupied);
    var trip = Assert.Single(Repository.GetRecentTrips(10, "default"));
    Assert.Equal("DESKTOP", trip.TerminalId);
    Assert.Equal("001234", trip.StudentId);
    Assert.Equal("Period 3", trip.ClassPeriod);
    Assert.Equal("Avery Chen", trip.DisplayName);
  }

  [Theory]
  [InlineData("ACTIVE_PASS,NONE")]
  [InlineData("ACTIVE_PASSES")]
  [InlineData("ACTIVE_PASS,001234,1700000000")]
  [InlineData("ACTIVE_PASSES,001234,1700000000,9876,1700000001")]
  [InlineData("EVENT,CHECKOUT,001234,1700000000")]
  [InlineData("EVENT,CHECKIN,001234,30")]
  [InlineData("EVENT,RESET,001234,30")]
  public void TerminalMessagesCannotOverwriteTeacherPass(string message) {
    var connection = new TestConnection();
    using var vm = Create(connection);
    Start(vm);
    var started = vm.ActivePass.CheckoutTime;
    connection.Emit(message);
    Assert.True(vm.ActivePass.IsManual);
    Assert.Equal("001234", vm.ActivePass.StudentId);
    Assert.Equal(started, vm.ActivePass.CheckoutTime);
    Assert.Equal("Nurse", vm.ActivePass.Destination);
    Assert.Contains(vm.Dashboard.RecentTrips, trip => trip.CheckoutTime == started);
  }

  [Fact] public async Task DisconnectAndConnectionLossKeepLocalPass() {
    var connection = new TestConnection();
    using var vm = Create(connection);
    Start(vm);
    connection.LoseConnection();
    Assert.True(vm.ActivePass.IsManual);
    await vm.DisconnectAsync();
    Assert.True(vm.ActivePass.IsManual);
    connection.Emit("ACTIVE_PASSES");
    Assert.True(vm.HasStudentsOut);
  }

  [Fact] public async Task SameStudentOnTerminalRemainsIndependentDuringBothCheckins() {
    var connection = new TestConnection();
    using var vm = Create(connection);
    Start(vm);
    connection.Emit("ACTIVE_PASSES,001234,1700000000");
    Assert.Single(vm.Dashboard.AdditionalActiveTrips);
    await vm.CheckInStudentAsync("001234");
    Assert.Contains("MANUAL_CHECKIN,001234", connection.Sent);
    Assert.True(vm.ActivePass.IsManual);
    Assert.Empty(Repository.GetRecentTrips());
    await vm.CheckInActivePassAsync();
    Assert.False(vm.ActivePass.IsManual);
    Assert.True(vm.ActivePass.IsOccupied);
    Assert.Equal("001234", vm.ActivePass.StudentId);
    Assert.Empty(vm.Dashboard.AdditionalActiveTrips);
    Assert.Single(Repository.GetRecentTrips());
  }

  [Fact] public async Task WorkspaceSwitchRestoresItsOwnPassAndHistory() {
    using var vm = Create(new());
    var first = vm.ActiveProfile;
    Start(vm);
    vm.PolicyModal.NewProfileName = "Other teacher";
    vm.CreateProfileFromPolicy();
    var second = vm.ActiveProfile;
    Assert.False(vm.HasStudentsOut);
    Start(vm, "9876");
    vm.ActiveProfile = first;
    Assert.Equal("001234", vm.ActivePass.StudentId);
    await vm.CheckInActivePassAsync();
    Assert.Single(Repository.GetRecentTrips(10, first.ProfileId));
    Assert.Empty(Repository.GetRecentTrips(10, second.ProfileId));
    vm.ActiveProfile = second;
    Assert.True(vm.ActivePass.IsManual);
    Assert.Equal("9876", vm.ActivePass.StudentId);
  }

  [Fact] public async Task FailedCompletionKeepsPassUntilRetrySucceeds() {
    using var vm = Create(new());
    Start(vm);
    Execute("CREATE TRIGGER block_trip BEFORE INSERT ON trips BEGIN SELECT RAISE(ABORT, 'test disk failure'); END;");
    await vm.CheckInActivePassAsync();
    Assert.True(vm.ActivePass.IsManual);
    Assert.True(vm.HasCheckInError);
    Assert.NotNull(Repository.GetActiveDesktopPass("default"));
    Assert.Empty(Repository.GetRecentTrips());
    Execute("DROP TRIGGER block_trip;");
    await vm.CheckInActivePassAsync();
    Assert.False(vm.ActivePass.IsManual);
    Assert.Single(Repository.GetRecentTrips());
  }

  [Fact] public void FailedStartDoesNotCreateInMemoryPass() {
    using var vm = Create(new());
    Execute("CREATE TRIGGER block_start BEFORE INSERT ON desktop_passes BEGIN SELECT RAISE(ABORT, 'test disk failure'); END;");
    vm.ManualCheckInModal.StudentId = "001234";
    vm.SubmitManualCheckIn();
    Assert.False(vm.HasStudentsOut);
    Assert.Contains("Could not save", vm.ManualCheckInModal.StatusMessage);
    Assert.Null(Repository.GetActiveDesktopPass("default"));
  }
  void Execute(string sql) {
    using var db = new SqliteConnection($"Data Source={Database}"); db.Open();
    using var cmd = db.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
  }

  sealed class TestConnection : ITerminalConnection {
    public event EventHandler<string>? TextReceived;
    public event EventHandler<string>? ConnectionLost;
    public List<string> Sent { get; } = new();
    public void Emit(string line) => TextReceived?.Invoke(this, line + "\n");
    public void LoseConnection() => ConnectionLost?.Invoke(this, "Test disconnect");
    public Task<IReadOnlyList<TerminalDevice>> DiscoverAsync() => Task.FromResult<IReadOnlyList<TerminalDevice>>([]);
    public Task ConnectAsync(TerminalDevice terminal) => Task.CompletedTask;
    public Task SendAsync(string command) { Sent.Add(command); return Task.CompletedTask; }
    public Task DisconnectAsync() => Task.CompletedTask;
    public void Dispose() { }
  }
}
