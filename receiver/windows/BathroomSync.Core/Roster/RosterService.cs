namespace BathroomSync.Core;

public interface IRosterService {
  RosterPreviewData PreviewRosterCsv(TextReader reader, int sampleSize = 3);
  RosterImportResult ImportRoster(string profileId, TextReader reader, RosterColumnMapping mapping, bool clearExisting = false);
  RosterImportResult ImportRosterAuto(string profileId, TextReader reader, bool clearExisting = false);
  RosterImportResult ImportRosterFromFile(string profileId, string filePath, RosterColumnMapping? mapping = null, bool clearExisting = false);
  RosterStudent? LookupStudent(string profileId, string studentId);
  EnrichedTripRecord EnrichTrip(string profileId, TripRecord trip);
  IReadOnlyList<EnrichedTripRecord> EnrichTrips(string profileId, IEnumerable<TripRecord> trips);
  IReadOnlyList<RosterStudent> GetRoster(string profileId);
  int CountStudents(string profileId);
  void DeleteStudent(string profileId, string studentId);
  void ClearRoster(string profileId);
}

public sealed class RosterService : IRosterService {
  readonly IRosterRepository rosterRepository;

  public RosterService(IRosterRepository rosterRepository) {
    this.rosterRepository = rosterRepository;
  }

  public RosterPreviewData PreviewRosterCsv(TextReader reader, int sampleSize = 3) {
    return RosterCsvParser.Analyze(reader, sampleSize);
  }

  public RosterImportResult ImportRoster(
    string profileId,
    TextReader reader,
    RosterColumnMapping mapping,
    bool clearExisting = false
  ) {
    var result = RosterCsvParser.ParseWithMapping(reader, mapping, profileId);
    if (result.ValidStudents.Count > 0) {
      if (clearExisting) {
        rosterRepository.ClearRoster(profileId);
      }
      rosterRepository.SaveStudents(profileId, result.ValidStudents);
    }
    return result;
  }

  public RosterImportResult ImportRosterAuto(
    string profileId,
    TextReader reader,
    bool clearExisting = false
  ) {
    var result = RosterCsvParser.ParseAuto(reader, profileId);
    if (result.ValidStudents.Count > 0) {
      if (clearExisting) {
        rosterRepository.ClearRoster(profileId);
      }
      rosterRepository.SaveStudents(profileId, result.ValidStudents);
    }
    return result;
  }

  public RosterImportResult ImportRosterFromFile(
    string profileId,
    string filePath,
    RosterColumnMapping? mapping = null,
    bool clearExisting = false
  ) {
    if (!File.Exists(filePath)) {
      return new RosterImportResult(
        TotalRows: 0,
        ImportedCount: 0,
        SkippedCount: 0,
        Errors: new[] { new RosterRowError(0, "FilePath", $"File not found: '{filePath}'") },
        ValidStudents: Array.Empty<RosterStudent>()
      );
    }

    using var reader = new StreamReader(filePath);
    return mapping != null && mapping.IsValid
      ? ImportRoster(profileId, reader, mapping, clearExisting)
      : ImportRosterAuto(profileId, reader, clearExisting);
  }

  public RosterStudent? LookupStudent(string profileId, string studentId) {
    return rosterRepository.FindStudent(profileId, studentId);
  }

  public EnrichedTripRecord EnrichTrip(string profileId, TripRecord trip) {
    var student = LookupStudent(profileId, trip.StudentId);
    return new EnrichedTripRecord(
      TripId: trip.TripId,
      StudentId: trip.StudentId,
      TripDate: trip.TripDate,
      TimeOut: trip.TimeOut,
      TimeIn: trip.TimeIn,
      DurationSeconds: trip.DurationSeconds,
      Status: trip.Status,
      SyncedAt: trip.SyncedAt,
      TerminalId: trip.TerminalId,
      FirstName: student?.FirstName,
      LastName: student?.LastName,
      Grade: student?.Grade,
      ClassPeriod: student?.ClassPeriod
    );
  }

  public IReadOnlyList<EnrichedTripRecord> EnrichTrips(string profileId, IEnumerable<TripRecord> trips) {
    var roster = rosterRepository.GetRoster(profileId);
    var studentMap = roster.ToDictionary(s => s.StudentId, s => s, StringComparer.OrdinalIgnoreCase);

    var enriched = new List<EnrichedTripRecord>();
    foreach (var trip in trips) {
      studentMap.TryGetValue(trip.StudentId, out var student);
      enriched.Add(new EnrichedTripRecord(
        TripId: trip.TripId,
        StudentId: trip.StudentId,
        TripDate: trip.TripDate,
        TimeOut: trip.TimeOut,
        TimeIn: trip.TimeIn,
        DurationSeconds: trip.DurationSeconds,
        Status: trip.Status,
        SyncedAt: trip.SyncedAt,
        TerminalId: trip.TerminalId,
        FirstName: student?.FirstName,
        LastName: student?.LastName,
        Grade: student?.Grade,
        ClassPeriod: student?.ClassPeriod
      ));
    }
    return enriched;
  }

  public IReadOnlyList<RosterStudent> GetRoster(string profileId) {
    return rosterRepository.GetRoster(profileId);
  }

  public int CountStudents(string profileId) {
    return rosterRepository.CountStudents(profileId);
  }

  public void DeleteStudent(string profileId, string studentId) {
    rosterRepository.DeleteStudent(profileId, studentId);
  }

  public void ClearRoster(string profileId) {
    rosterRepository.ClearRoster(profileId);
  }
}
