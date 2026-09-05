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
  string period = "";
  string destination = "";
  string purpose = "";
  string? statusMessage;
  string currentProfileId = "default";

  public ManualCheckInViewModel(IRosterService rosterService) {
    this.rosterService = rosterService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

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

  public string Period {
    get => period;
    set {
      if (period != value) {
        period = value;
        OnPropertyChanged();
      }
    }
  }

  public string Destination {
    get => destination;
    set {
      if (destination != value) {
        destination = value;
        OnPropertyChanged();
      }
    }
  }

  public string Purpose {
    get => purpose;
    set {
      if (purpose != value) {
        purpose = value;
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
    Period = "";
    Destination = "";
    Purpose = "";
    StatusMessage = null;
  }

  public (string resolvedId, string resolvedName, string? resolvedPeriod, string? resolvedDestination, string? resolvedPurpose) ResolvePassDetails() {
    var rawId = StudentId.Trim();
    var rawName = StudentName.Trim();
    var resolvedPeriod = string.IsNullOrWhiteSpace(Period) ? null : Period.Trim();
    var resolvedDestination = string.IsNullOrWhiteSpace(Destination) ? null : Destination.Trim();
    var resolvedPurpose = string.IsNullOrWhiteSpace(Purpose) ? null : Purpose.Trim();

    if (!string.IsNullOrWhiteSpace(rawId) && !string.IsNullOrWhiteSpace(rawName)) {
      return (rawId, rawName, resolvedPeriod, resolvedDestination, resolvedPurpose);
    }

    if (!string.IsNullOrWhiteSpace(rawId)) {
      var match = rosterService.LookupStudent(currentProfileId, rawId);
      var name = match?.FullName ?? $"#{rawId}";
      return (rawId, name, resolvedPeriod, resolvedDestination, resolvedPurpose);
    }

    if (!string.IsNullOrWhiteSpace(rawName)) {
      var roster = rosterService.GetRoster(currentProfileId);
      var match = roster.FirstOrDefault(s => string.Equals(s.FullName, rawName, StringComparison.OrdinalIgnoreCase));
      var id = match?.StudentId ?? $"M-{DateTime.Now:HHmmss}";
      return (id, rawName, resolvedPeriod, resolvedDestination, resolvedPurpose);
    }

    return ("MANUAL", "Manual Pass", resolvedPeriod, resolvedDestination, resolvedPurpose);
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
