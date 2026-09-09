using BathroomSync.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BathroomSync.Tests;
public sealed class DesktopPassStorageTests : IDisposable {
  readonly string folder = Path.Combine(Path.GetTempPath(), "HallzeeDesktopStorage", Guid.NewGuid().ToString("N"));
  string Db => Path.Combine(folder, "trips.db");
  DesktopPass Pass(string profile = "default") => new(Guid.NewGuid().ToString("N"), profile, "001234", "Avery, Chen",
    new DateTime(2026, 9, 8, 23, 58, 0, DateTimeKind.Local), "Period 3", "Nurse", "Visit", "Regular");
  public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }

  [Fact] public void DuplicateStartCannotReplaceActivePass() {
    var repo = new TripSqliteRepository(Db);
    var pass = Pass(); repo.StartDesktopPass(pass);
    Assert.Throws<SqliteException>(() => repo.StartDesktopPass(Pass()));
    Assert.Equal(pass, new TripSqliteRepository(Db).GetActiveDesktopPass("default"));
    Assert.False(repo.CompleteDesktopPass("another-workspace", pass.PassId, DateTime.Now));
    Assert.NotNull(repo.GetActiveDesktopPass("default"));
  }

  [Fact] public void CompletionPreservesExistingHistoryAndUsesStartDateAcrossMidnight() {
    var repo = new TripSqliteRepository(Db);
    Assert.Equal(TripStoreResult.Saved, repo.StoreManual("DESKTOP", "15,old,2026-09-08,10:00:00,10:01:00,60,MANUAL", "Previous"));
    var pass = Pass(); repo.StartDesktopPass(pass);
    var returned = pass.CheckoutAt.AddMinutes(5);
    Assert.True(repo.CompleteDesktopPass(pass.ProfileId, pass.PassId, returned));
    Assert.False(new TripSqliteRepository(Db).CompleteDesktopPass(pass.ProfileId, pass.PassId, returned));
    var trip = repo.GetRecentTrips().Single(t => t.TripId == 16);
    Assert.Equal(300, trip.DurationSeconds);
    Assert.Equal("2026-09-08", trip.TripDate);
    Assert.Equal("00:03:00", trip.TimeIn);
    Assert.Equal("Avery, Chen", trip.DisplayName);
    Assert.Equal("Regular", trip.ScheduleName);
    Assert.Equal(2, repo.CountTrips(new()));
    Assert.Equal(0, repo.GetTripSummary(profileId: "another-workspace").TotalTrips);
    var next = Pass(); repo.StartDesktopPass(next);
    Assert.False(repo.CompleteDesktopPass(pass.ProfileId, pass.PassId, returned));
    Assert.Equal(next, repo.GetActiveDesktopPass("default"));
  }

  [Fact] public async Task ConcurrentCompletionCreatesOneTrip() {
    var repo = new TripSqliteRepository(Db);
    var pass = Pass(); repo.StartDesktopPass(pass);
    var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
      repo.CompleteDesktopPass(pass.ProfileId, pass.PassId, pass.CheckoutAt.AddMinutes(5)))));
    Assert.Single(results, completed => completed);
    Assert.Single(repo.GetRecentTrips());
    Assert.Null(repo.GetActiveDesktopPass("default"));
  }

  [Fact] public void FailureAfterTripInsertionRollsBackEntireCompletion() {
    var repo = new TripSqliteRepository(Db);
    var pass = Pass(); repo.StartDesktopPass(pass);
    using var db = new SqliteConnection($"Data Source={Db}"); db.Open();
    using var cmd = db.CreateCommand();
    cmd.CommandText = "CREATE TRIGGER block_close BEFORE UPDATE ON desktop_passes BEGIN SELECT RAISE(ABORT, 'test failure'); END;";
    cmd.ExecuteNonQuery();
    Assert.Throws<SqliteException>(() => repo.CompleteDesktopPass(pass.ProfileId, pass.PassId, DateTime.Now));
    Assert.Empty(repo.GetRecentTrips());
    Assert.Equal(pass, repo.GetActiveDesktopPass("default"));
    cmd.CommandText = "DROP TRIGGER block_close;"; cmd.ExecuteNonQuery();
    Assert.True(repo.CompleteDesktopPass(pass.ProfileId, pass.PassId, DateTime.Now));
    Assert.Single(repo.GetRecentTrips());
  }
}
