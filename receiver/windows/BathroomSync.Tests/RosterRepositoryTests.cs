using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class RosterRepositoryTests {
  [Fact]
  public void RosterPerformsCrudAndIsolatesProfiles() {
    var folder = Path.Combine(Path.GetTempPath(), "BathroomSyncTests", Guid.NewGuid().ToString("N"));
    var dbPath = Path.Combine(folder, "roster_test.db");

    try {
      Directory.CreateDirectory(folder);
      var rosterRepo = new RosterSqliteRepository(dbPath);
      var profileRepo = new ProfileAndPolicySqliteRepository(dbPath);

      // Create extra profile
      profileRepo.SaveProfile(new ClassroomProfile("period2", "Period 2 Science"));

      // Add students to default profile
      rosterRepo.SaveStudents("default", new[] {
        new RosterStudent("101", "default", "Charlie", "Brown", "9", "Period 1"),
        new RosterStudent("102", "default", "Lucy", "Van Pelt", "9", "Period 1")
      });

      // Add students to period2 profile
      rosterRepo.SaveStudents("period2", new[] {
        new RosterStudent("201", "period2", "Linus", "Van Pelt", "10", "Period 2"),
        new RosterStudent("101", "period2", "Charlie", "Brown", "10", "Period 2") // Same student ID in different class
      });

      // Check counts
      Assert.Equal(2, rosterRepo.CountStudents("default"));
      Assert.Equal(2, rosterRepo.CountStudents("period2"));

      // Check roster listing order (by last_name, first_name)
      var defaultRoster = rosterRepo.GetRoster("default");
      Assert.Equal("Brown", defaultRoster[0].LastName);
      Assert.Equal("Van Pelt", defaultRoster[1].LastName);

      // Find Student
      var student = rosterRepo.FindStudent("default", "101");
      Assert.NotNull(student);
      Assert.Equal("Charlie Brown", student.FullName);

      // Upsert update
      rosterRepo.SaveStudents("default", new[] {
        new RosterStudent("101", "default", "Charles", "Brown", "9", "Period 1 Honors")
      });
      var updated = rosterRepo.FindStudent("default", "101");
      Assert.NotNull(updated);
      Assert.Equal("Charles", updated.FirstName);
      Assert.Equal("Period 1 Honors", updated.ClassPeriod);

      // Delete student
      rosterRepo.DeleteStudent("default", "102");
      Assert.Equal(1, rosterRepo.CountStudents("default"));
      Assert.Null(rosterRepo.FindStudent("default", "102"));

      // Clear roster
      rosterRepo.ClearRoster("default");
      Assert.Equal(0, rosterRepo.CountStudents("default"));
      // period2 roster untouched
      Assert.Equal(2, rosterRepo.CountStudents("period2"));
    } finally {
      if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
  }
}
