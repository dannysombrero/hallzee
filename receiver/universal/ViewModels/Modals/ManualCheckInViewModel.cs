using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class ManualCheckInViewModel : INotifyPropertyChanged {
  readonly IRosterService rosterService;
  string studentName = "";
  string studentId = "";
  string reason = "Restroom";
  string location = "";
  string? statusMessage;
  string currentProfileId = "default";

  public ManualCheckInViewModel(IRosterService rosterService) {
    this.rosterService = rosterService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public IReadOnlyList<string> ReasonOptions { get; } = new[] {
    "Restroom",
    "Water Fountain",
    "Nurse / Clinic",
    "Main Office",
    "Counselor",
    "Library",
    "Lockers",
    "Other"
  };

  public string StudentName {
    get => studentName;
    set {
      if (studentName != value) {
        studentName = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(CanSubmit));
        TryAutoLookupId();
      }
    }
  }

  public string StudentId {
    get => studentId;
    set {
      if (studentId != value) {
        studentId = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(CanSubmit));
        TryAutoLookupName();
      }
    }
  }

  public string Reason {
    get => reason;
    set {
      if (reason != value) {
        reason = value;
        OnPropertyChanged();
      }
    }
  }

  public string Location {
    get => location;
    set {
      if (location != value) {
        location = value;
        OnPropertyChanged();
      }
    }
  }

  public string? StatusMessage {
    get => statusMessage;
    set {
      if (statusMessage != value) {
        statusMessage = value;
        OnPropertyChanged();
      }
    }
  }

  public bool CanSubmit =>
    !string.IsNullOrWhiteSpace(StudentName) || !string.IsNullOrWhiteSpace(StudentId);

  public void Reset(string profileId) {
    currentProfileId = profileId;
    StudentName = "";
    StudentId = "";
    Reason = "Restroom";
    Location = "";
    StatusMessage = null;
  }

  public (string resolvedId, string resolvedName, string resolvedReason, string? resolvedLocation) ResolvePassDetails() {
    var rawId = StudentId.Trim();
    var rawName = StudentName.Trim();
    var resolvedReason = string.IsNullOrWhiteSpace(Reason) ? "Restroom" : Reason.Trim();
    var resolvedLocation = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();

    if (!string.IsNullOrWhiteSpace(rawId) && !string.IsNullOrWhiteSpace(rawName)) {
      return (rawId, rawName, resolvedReason, resolvedLocation);
    }

    if (!string.IsNullOrWhiteSpace(rawId)) {
      var match = rosterService.LookupStudent(currentProfileId, rawId);
      var name = match?.FullName ?? $"#{rawId}";
      return (rawId, name, resolvedReason, resolvedLocation);
    }

    if (!string.IsNullOrWhiteSpace(rawName)) {
      var roster = rosterService.GetRoster(currentProfileId);
      var match = roster.FirstOrDefault(s => string.Equals(s.FullName, rawName, StringComparison.OrdinalIgnoreCase));
      var id = match?.StudentId ?? $"M-{DateTime.Now:HHmmss}";
      return (id, rawName, resolvedReason, resolvedLocation);
    }

    return ("MANUAL", "Manual Pass", resolvedReason, resolvedLocation);
  }

  void TryAutoLookupName() {
    if (string.IsNullOrWhiteSpace(StudentId) || !string.IsNullOrWhiteSpace(StudentName)) return;
    var match = rosterService.LookupStudent(currentProfileId, StudentId.Trim());
    if (match != null && !string.IsNullOrWhiteSpace(match.FullName)) {
      studentName = match.FullName;
      OnPropertyChanged(nameof(StudentName));
    }
  }

  void TryAutoLookupId() {
    if (string.IsNullOrWhiteSpace(StudentName) || !string.IsNullOrWhiteSpace(StudentId)) return;
    var roster = rosterService.GetRoster(currentProfileId);
    var match = roster.FirstOrDefault(s => string.Equals(s.FullName, StudentName.Trim(), StringComparison.OrdinalIgnoreCase));
    if (match != null && !string.IsNullOrWhiteSpace(match.StudentId)) {
      studentId = match.StudentId;
      OnPropertyChanged(nameof(StudentId));
    }
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
