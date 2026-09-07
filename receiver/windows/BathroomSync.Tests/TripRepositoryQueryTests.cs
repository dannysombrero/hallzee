using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class TripRepositoryQueryTests {
  [Fact]
  public void QueryTripsAndRecentTripsReturnCorrectRecordsAndEnrichment() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "query_test.db");

    try {
      Directory.CreateDirectory(folder);
      var tripRepo = new TripSqliteRepository(dbPath);
      var rosterRepo = new RosterSqliteRepository(dbPath);

      // Save student roster
      rosterRepo.SaveStudents("default", new[] {
        new RosterStudent("1001", "default", "Alex", "Rivera", "10", "Period 1"),
        new RosterStudent("1002", "default", "Jordan", "Lee", "10", "Period 2")
      });

      // Insert trips
      tripRepo.Store("1,1001,2026-09-01,08:30:00,08:35:00,300,COMPLETE,0,ROOM204");
      tripRepo.Store("2,1002,2026-09-01,09:10:00,09:18:00,480,COMPLETE,0,ROOM204");
      tripRepo.Store("3,9999,2026-09-01,10:00:00,10:04:00,240,MANUAL_RESET,0,ROOM204"); // Not on roster
      tripRepo.Store("4,1001,2026-09-02,08:40:00,08:46:00,360,COMPLETE,0,ROOM204");

      // Test GetRecentTrips
      var recent = tripRepo.GetRecentTrips(2);
      Assert.Equal(2, recent.Count);
      Assert.Equal(4, recent[0].TripId);
      Assert.Equal("Alex Rivera", recent[0].FullName);
      Assert.Equal("Alex Rivera", recent[0].DisplayName);
      Assert.Equal("Period 1", recent[0].ClassPeriod);
      Assert.Equal(3, recent[1].TripId);
      Assert.Null(recent[1].FullName);
      Assert.Equal("#9999", recent[1].DisplayName);

      // Test Search by Name
      var searchByName = tripRepo.QueryTrips(new TripQueryFilter(SearchText: "Jordan"));
      Assert.Single(searchByName);
      Assert.Equal(2, searchByName[0].TripId);
      Assert.Equal("Jordan Lee", searchByName[0].FullName);

      // Test Search by Student ID
      var searchById = tripRepo.QueryTrips(new TripQueryFilter(SearchText: "9999"));
      Assert.Single(searchById);
      Assert.Equal(3, searchById[0].TripId);

      // Test Status Filter
      var resetsOnly = tripRepo.QueryTrips(new TripQueryFilter(Status: "MANUAL_RESET"));
      Assert.Single(resetsOnly);
      Assert.Equal(3, resetsOnly[0].TripId);

      var completedOnly = tripRepo.QueryTrips(new TripQueryFilter(Status: "COMPLETED"));
      Assert.Equal(3, completedOnly.Count);

      // Test Date Range Filter
      var day1Only = tripRepo.QueryTrips(new TripQueryFilter(StartDate: "2026-09-01", EndDate: "2026-09-01"));
      Assert.Equal(3, day1Only.Count);

      // Test Pagination
      var paged = tripRepo.QueryTrips(new TripQueryFilter(Limit: 2, Offset: 1, OrderBy: "trip_id ASC"));
      Assert.Equal(2, paged.Count);
      Assert.Equal(2, paged[0].TripId);
      Assert.Equal(3, paged[1].TripId);

      // Test CountTrips
      Assert.Equal(4, tripRepo.CountTrips(new TripQueryFilter()));
      Assert.Equal(1, tripRepo.CountTrips(new TripQueryFilter(Status: "MANUAL_RESET")));

      // Test Summary Calculation
      var summary = tripRepo.GetTripSummary();
      Assert.Equal(4, summary.TotalTrips);
      Assert.Equal(3, summary.CompletedTrips);
      Assert.Equal(1, summary.ManualResetTrips);
      Assert.Equal(3, summary.UniqueStudents);
      Assert.Equal((300 + 480 + 240 + 360) / 4.0, summary.AverageDurationSeconds);

      // Test Enriched CSV Export
      var exportPath = Path.Combine(folder, "enriched.csv");
      tripRepo.ExportEnrichedCsv(exportPath);
      Assert.True(File.Exists(exportPath));
      var lines = File.ReadAllLines(exportPath);
      Assert.Equal(5, lines.Length);
      Assert.Equal(TripSqliteRepository.EnrichedCsvHeader, lines[0]);
      Assert.Contains("1,1001,Alex Rivera,Period 1,,10,2026-09-01,08:30:00,08:35:00,300,COMPLETE,ROOM204", lines[1]);
      Assert.Contains("3,9999,,,,,", lines[3]); // Unrostered student has blank name/section/schedule/grade
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }

  [Fact]
  public void HandlesTripStatusExtensionsAndSpecialCharactersInCsv() {
    Assert.Equal(TripStatus.Completed, TripStatusExtensions.ParseStatus("COMPLETE"));
    Assert.Equal(TripStatus.Completed, TripStatusExtensions.ParseStatus("completed"));
    Assert.Equal(TripStatus.ManualReset, TripStatusExtensions.ParseStatus("MANUAL_RESET"));
    Assert.Equal(TripStatus.Unknown, TripStatusExtensions.ParseStatus("OTHER"));

    Assert.Equal("COMPLETED", TripStatus.Completed.ToStorageString());
    Assert.Equal("MANUAL_RESET", TripStatus.ManualReset.ToStorageString());
    Assert.Equal("UNKNOWN", TripStatus.Unknown.ToStorageString());

    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "csv_special.db");

    try {
      Directory.CreateDirectory(folder);
      var tripRepo = new TripSqliteRepository(dbPath);
      var rosterRepo = new RosterSqliteRepository(dbPath);

      rosterRepo.SaveStudents("default", new[] {
        new RosterStudent("101", "default", "Jane, \"JJ\"", "O'Connor\nJr", "11", "Period 3, AP")
      });

      tripRepo.Store("1,101,2026-09-01,10:00:00,10:05:00,300,COMPLETE");

      var exportPath = Path.Combine(folder, "special.csv");
      tripRepo.ExportEnrichedCsv(exportPath);
      var text = File.ReadAllText(exportPath);
      Assert.Contains("\"Jane, \"\"JJ\"\" O'Connor\nJr\"", text);
      Assert.Contains("\"Period 3, AP\"", text);
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }
}
