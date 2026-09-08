using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BathroomSync.Core;
using BathroomSync.Universal.Services;

namespace BathroomSync.Universal.ViewModels;

public sealed class BellPeriodItemViewModel : INotifyPropertyChanged {
  readonly string scheduleId;
  readonly string profileId;
  string scheduleName;
  string periodName;
  string startTime;
  string endTime;
  bool isMonday;
  bool isTuesday;
  bool isWednesday;
  bool isThursday;
  bool isFriday;
  bool isSaturday;
  bool isSunday;
  bool isEditing;
  string editName;
  string editStartTime;
  string editEndTime;
  string editScheduleName;
  string editClassSection;
  string classSection;
  readonly Action? onSaved;

  public BellPeriodItemViewModel(BellSchedulePeriod period, bool isEditing = false, Action? onSaved = null) {
    scheduleId = period.ScheduleId;
    profileId = period.ProfileId;
    periodName = period.PeriodName;
    startTime = period.StartTime;
    endTime = period.EndTime;
    scheduleName = string.IsNullOrWhiteSpace(period.ScheduleName) ? "Regular" : period.ScheduleName;
    this.isEditing = isEditing;
    this.onSaved = onSaved;

    editName = periodName;
    editStartTime = startTime;
    editEndTime = endTime;
    editScheduleName = scheduleName;
    classSection = period.ClassSection;
    editClassSection = classSection;

    var days = period.DaysOfWeek ?? "";
    isMonday = days.Contains("Mon", StringComparison.OrdinalIgnoreCase);
    isTuesday = days.Contains("Tue", StringComparison.OrdinalIgnoreCase);
    isWednesday = days.Contains("Wed", StringComparison.OrdinalIgnoreCase);
    isThursday = days.Contains("Thu", StringComparison.OrdinalIgnoreCase);
    isFriday = days.Contains("Fri", StringComparison.OrdinalIgnoreCase);
    isSaturday = days.Contains("Sat", StringComparison.OrdinalIgnoreCase);
    isSunday = days.Contains("Sun", StringComparison.OrdinalIgnoreCase);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public string ScheduleId => scheduleId;
  public string ProfileId => profileId;

  public string EditName {
    get => editName;
    set {
      if (editName != value) {
        editName = value;
        OnPropertyChanged();
      }
    }
  }

  public string EditStartTime {
    get => editStartTime;
    set {
      if (editStartTime != value) {
        editStartTime = value;
        OnPropertyChanged();
      }
    }
  }

  public string EditEndTime {
    get => editEndTime;
    set {
      if (editEndTime != value) {
        editEndTime = value;
        OnPropertyChanged();
      }
    }
  }

  public string EditScheduleName {
    get => editScheduleName;
    set { if (editScheduleName != value) { editScheduleName = value; OnPropertyChanged(); } }
  }

  public string EditClassSection {
    get => editClassSection;
    set { if (editClassSection != value) { editClassSection = value; OnPropertyChanged(); } }
  }

  public string ScheduleName {
    get => scheduleName;
    set {
      if (scheduleName != value) {
        scheduleName = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DisplayTitle));
      }
    }
  }

  public string PeriodName {
    get => periodName;
    set {
      if (periodName != value) {
        periodName = value;
        editName = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(EditName));
      }
    }
  }

  public string StartTime {
    get => startTime;
    set {
      if (startTime != value) {
        startTime = value;
        editStartTime = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DisplayTimeRange));
        OnPropertyChanged(nameof(EditStartTime));
      }
    }
  }

  public string EndTime {
    get => endTime;
    set {
      if (endTime != value) {
        endTime = value;
        editEndTime = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DisplayTimeRange));
        OnPropertyChanged(nameof(EditEndTime));
      }
    }
  }

  public bool IsMonday {
    get => isMonday;
    set {
      if (isMonday != value) {
        isMonday = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(FormattedDaysSummary));
      }
    }
  }

  public bool IsTuesday {
    get => isTuesday;
    set {
      if (isTuesday != value) {
        isTuesday = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(FormattedDaysSummary));
      }
    }
  }

  public bool IsWednesday {
    get => isWednesday;
    set {
      if (isWednesday != value) {
        isWednesday = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(FormattedDaysSummary));
      }
    }
  }

  public bool IsThursday {
    get => isThursday;
    set {
      if (isThursday != value) {
        isThursday = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(FormattedDaysSummary));
      }
    }
  }

  public bool IsFriday {
    get => isFriday;
    set {
      if (isFriday != value) {
        isFriday = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(FormattedDaysSummary));
      }
    }
  }

  public bool IsSaturday { get => isSaturday; set { if (isSaturday != value) { isSaturday = value; OnPropertyChanged(); OnPropertyChanged(nameof(DaysOfWeek)); } } }
  public bool IsSunday { get => isSunday; set { if (isSunday != value) { isSunday = value; OnPropertyChanged(); OnPropertyChanged(nameof(DaysOfWeek)); } } }

  public bool IsEditing {
    get => isEditing;
    set {
      if (isEditing != value) {
        isEditing = value;
        OnPropertyChanged();
      }
    }
  }

  public string DaysOfWeek {
    get {
      var days = new List<string>(5);
      if (IsMonday) days.Add("Mon");
      if (IsTuesday) days.Add("Tue");
      if (IsWednesday) days.Add("Wed");
      if (IsThursday) days.Add("Thu");
      if (IsFriday) days.Add("Fri");
      if (IsSaturday) days.Add("Sat");
      if (IsSunday) days.Add("Sun");
      return string.Join(",", days);
    }
  }

  public string FormattedDaysSummary {
    get {
      var days = new List<string>(5);
      if (IsMonday) days.Add("Mon");
      if (IsTuesday) days.Add("Tue");
      if (IsWednesday) days.Add("Wed");
      if (IsThursday) days.Add("Thu");
      if (IsFriday) days.Add("Fri");
      if (days.Count == 5) return "Mon – Fri (All days)";
      if (days.Count == 0) return "No days selected";
      return string.Join(", ", days);
    }
  }

  public string DisplayTimeRange => $"{StartTime} – {EndTime}";

  public string DisplayTitle => string.IsNullOrWhiteSpace(ScheduleName) || ScheduleName == "Regular"
    ? AppendSection(PeriodName)
    : AppendSection($"{ScheduleName} • {PeriodName}");

  string AppendSection(string value) => string.IsNullOrWhiteSpace(classSection) ? value : $"{value} · {classSection}";

  public void StartEdit() {
    EditName = PeriodName;
    EditStartTime = StartTime;
    EditEndTime = EndTime;
    EditScheduleName = ScheduleName;
    EditClassSection = classSection;
    IsEditing = true;
  }

  public void SaveEdit() {
    if (!string.IsNullOrWhiteSpace(EditName)) PeriodName = EditName.Trim();
    if (!string.IsNullOrWhiteSpace(EditStartTime)) StartTime = EditStartTime.Trim();
    if (!string.IsNullOrWhiteSpace(EditEndTime)) EndTime = EditEndTime.Trim();
    ScheduleName = string.IsNullOrWhiteSpace(EditScheduleName) ? "Regular" : EditScheduleName.Trim();
    classSection = EditClassSection.Trim();
    OnPropertyChanged(nameof(DisplayTitle));
    IsEditing = false;
    onSaved?.Invoke();
  }

  public void CancelEdit() {
    EditName = PeriodName;
    EditStartTime = StartTime;
    EditEndTime = EndTime;
    EditScheduleName = ScheduleName;
    EditClassSection = classSection;
    IsEditing = false;
  }

  public BellSchedulePeriod ToModel() => new(
    ScheduleId: scheduleId,
    ProfileId: profileId,
    PeriodName: PeriodName,
    StartTime: StartTime,
    EndTime: EndTime,
    DaysOfWeek: DaysOfWeek,
    ScheduleName: ScheduleName,
    ClassSection: classSection
  );

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}

public sealed class ScheduleExceptionItemViewModel : INotifyPropertyChanged {
  string exceptionDate;
  string scheduleName;
  bool isNoSchool;

  public ScheduleExceptionItemViewModel(ScheduleException item) {
    ExceptionId = item.ExceptionId;
    ProfileId = item.ProfileId;
    exceptionDate = item.ExceptionDate;
    scheduleName = item.ScheduleName;
    isNoSchool = item.IsNoSchool;
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public string ExceptionId { get; }
  public string ProfileId { get; }
  public string ExceptionDate { get => exceptionDate; set { if (exceptionDate != value) { exceptionDate = value; OnPropertyChanged(); } } }
  public string ScheduleName { get => scheduleName; set { if (scheduleName != value) { scheduleName = value; OnPropertyChanged(); } } }
  public bool IsNoSchool { get => isNoSchool; set { if (isNoSchool != value) { isNoSchool = value; OnPropertyChanged(); } } }
  public string Summary => IsNoSchool ? $"{ExceptionDate}: No school" : $"{ExceptionDate}: {ScheduleName}";
  public ScheduleException ToModel() => new(ExceptionId, ProfileId, ExceptionDate, IsNoSchool ? "" : ScheduleName.Trim(), IsNoSchool);
  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    if (propertyName != nameof(Summary)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
  }
}

public sealed class PolicyViewModel : INotifyPropertyChanged {
  readonly IPolicyRepository policyRepository;
  int maxDailyPasses = 3;
  int durationWarningMinutes = 7;
  int maxSimultaneousPasses = 1;
  int firstWindowMinutes = 10;
  int lastWindowMinutes = 10;
  string firstWindowAction = "Warn";
  string lastWindowAction = "Warn";
  string alertSound = "Chime";
  string newProfileName = "";
  string renameProfileName = "";
  string statusMessage = "";
  bool isCreatingProfile;
  bool isRenamingProfile;
  bool terminalEnforcementEnabled;
  string newExceptionDate = DateTime.Today.ToString("yyyy-MM-dd");
  string newExceptionScheduleName = "Regular";
  bool newExceptionIsNoSchool;

  public bool IsCreatingProfile {
    get => isCreatingProfile;
    set {
      if (isCreatingProfile != value) {
        isCreatingProfile = value;
        OnPropertyChanged();
      }
    }
  }

  public bool IsRenamingProfile {
    get => isRenamingProfile;
    set {
      if (isRenamingProfile != value) {
        isRenamingProfile = value;
        OnPropertyChanged();
      }
    }
  }

  public string RenameProfileName {
    get => renameProfileName;
    set {
      if (renameProfileName != value) {
        renameProfileName = value;
        OnPropertyChanged();
      }
    }
  }

  public void ToggleCreateProfile() {
    IsCreatingProfile = !IsCreatingProfile;
    if (IsCreatingProfile) {
      IsRenamingProfile = false;
      RenameProfileName = "";
      NewProfileName = "";
    } else {
      NewProfileName = "";
    }
  }

  public void CancelCreateProfile() {
    IsCreatingProfile = false;
    NewProfileName = "";
  }

  public void ToggleRenameProfile(string currentName = "") {
    IsRenamingProfile = !IsRenamingProfile;
    if (IsRenamingProfile) {
      IsCreatingProfile = false;
      NewProfileName = "";
      RenameProfileName = currentName;
    } else {
      RenameProfileName = "";
    }
  }

  public void CancelRenameProfile() {
    IsRenamingProfile = false;
    RenameProfileName = "";
  }

  public PolicyViewModel(IPolicyRepository policyRepository) {
    this.policyRepository = policyRepository;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<BellPeriodItemViewModel> Periods { get; } = new();
  public ObservableCollection<ScheduleExceptionItemViewModel> Exceptions { get; } = new();
  public IReadOnlyList<string> BellActions { get; } = new[] { "Allow", "Warn", "Lock" };
  public IReadOnlyList<string> AlertSounds { get; } = new[] { "No sound", "Chime", "Bell", "Soft alert" };

  public int MaxDailyPasses {
    get => maxDailyPasses;
    set { if (maxDailyPasses != value) { maxDailyPasses = value; OnPropertyChanged(); } }
  }

  public int DurationWarningMinutes {
    get => durationWarningMinutes;
    set { if (durationWarningMinutes != value) { durationWarningMinutes = value; OnPropertyChanged(); } }
  }

  public int MaxSimultaneousPasses {
    get => maxSimultaneousPasses;
    set { if (maxSimultaneousPasses != value) { maxSimultaneousPasses = value; OnPropertyChanged(); } }
  }

  public int FirstWindowMinutes {
    get => firstWindowMinutes;
    set { if (firstWindowMinutes != value) { firstWindowMinutes = value; OnPropertyChanged(); } }
  }

  public int LastWindowMinutes {
    get => lastWindowMinutes;
    set { if (lastWindowMinutes != value) { lastWindowMinutes = value; OnPropertyChanged(); } }
  }

  public string FirstWindowAction {
    get => firstWindowAction;
    set { if (firstWindowAction != value) { firstWindowAction = value; OnPropertyChanged(); } }
  }

  public string LastWindowAction {
    get => lastWindowAction;
    set { if (lastWindowAction != value) { lastWindowAction = value; OnPropertyChanged(); } }
  }

  public string AlertSound {
    get => alertSound;
    set { if (alertSound != value) { alertSound = value; OnPropertyChanged(); } }
  }

  public bool TerminalEnforcementEnabled {
    get => terminalEnforcementEnabled;
    set { if (terminalEnforcementEnabled != value) { terminalEnforcementEnabled = value; OnPropertyChanged(); } }
  }

  public string NewExceptionDate { get => newExceptionDate; set { if (newExceptionDate != value) { newExceptionDate = value; OnPropertyChanged(); } } }
  public string NewExceptionScheduleName { get => newExceptionScheduleName; set { if (newExceptionScheduleName != value) { newExceptionScheduleName = value; OnPropertyChanged(); } } }
  public bool NewExceptionIsNoSchool { get => newExceptionIsNoSchool; set { if (newExceptionIsNoSchool != value) { newExceptionIsNoSchool = value; OnPropertyChanged(); } } }

  public string NewProfileName {
    get => newProfileName;
    set { if (newProfileName != value) { newProfileName = value; OnPropertyChanged(); } }
  }

  public string StatusMessage {
    get => statusMessage;
    set {
      statusMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasStatusMessage));
    }
  }

  public bool HasStatusMessage => !string.IsNullOrWhiteSpace(statusMessage);

  bool isEarliestFirst = true;

  public bool IsEarliestFirst {
    get => isEarliestFirst;
    set {
      if (isEarliestFirst != value) {
        isEarliestFirst = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(SortOrderButtonText));
        SortPeriods();
      }
    }
  }

  public string SortOrderButtonText => isEarliestFirst ? "Earliest First ▲" : "Latest First ▼";

  public void ToggleSortOrder() {
    IsEarliestFirst = !IsEarliestFirst;
  }

  public static int ParseTimeToMinutes(string time) {
    if (string.IsNullOrWhiteSpace(time)) return int.MaxValue;
    var trimmed = time.Trim();
    if (DateTime.TryParse(trimmed, out var dt)) {
      return dt.Hour * 60 + dt.Minute;
    }
    if (TimeOnly.TryParse(trimmed, out var to)) {
      return to.Hour * 60 + to.Minute;
    }
    return int.MaxValue;
  }

  static int ExtractPeriodNumber(string name) {
    if (string.IsNullOrWhiteSpace(name)) return int.MaxValue;
    var digits = new string(name.Where(char.IsDigit).ToArray());
    if (int.TryParse(digits, out var num)) return num;
    return int.MaxValue;
  }

  public void SortPeriods() {
    var list = Periods.ToList();
    var sorted = isEarliestFirst
      ? list.OrderBy(p => ParseTimeToMinutes(p.StartTime))
            .ThenBy(p => ExtractPeriodNumber(p.PeriodName))
            .ThenBy(p => p.PeriodName, StringComparer.OrdinalIgnoreCase)
            .ToList()
      : list.OrderByDescending(p => ParseTimeToMinutes(p.StartTime))
            .ThenByDescending(p => ExtractPeriodNumber(p.PeriodName))
            .ThenByDescending(p => p.PeriodName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    for (int i = 0; i < sorted.Count; i++) {
      int oldIndex = Periods.IndexOf(sorted[i]);
      if (oldIndex >= 0 && oldIndex != i) {
        Periods.Move(oldIndex, i);
      }
    }
  }

  public void PlayAlertSoundPreview() {
    SoundService.Play(AlertSound);
  }

  public void Refresh(string profileId) {
    var rule = policyRepository.GetPolicyRule(profileId);
    MaxDailyPasses = rule.MaxDailyPassesPerStudent;
    DurationWarningMinutes = rule.DurationWarningSeconds / 60;
    MaxSimultaneousPasses = rule.MaxSimultaneousPasses;
    FirstWindowMinutes = rule.LockoutStartMinutes;
    LastWindowMinutes = rule.LockoutEndMinutes;
    FirstWindowAction = rule.FirstWindowAction;
    LastWindowAction = rule.LastWindowAction;
    AlertSound = rule.AlertSound;
    TerminalEnforcementEnabled = rule.TerminalEnforcementEnabled;

    var schedule = policyRepository.GetBellSchedule(profileId);
    Periods.Clear();
    foreach (var p in schedule) {
      Periods.Add(new BellPeriodItemViewModel(p, isEditing: false, onSaved: SortPeriods));
    }
    SortPeriods();
    Exceptions.Clear();
    foreach (var item in policyRepository.GetScheduleExceptions(profileId)) Exceptions.Add(new ScheduleExceptionItemViewModel(item));
    StatusMessage = "";
  }

  public void Save(string profileId) {
    foreach (var period in Periods) {
      if (period.IsEditing) period.SaveEdit();
    }
    SortPeriods();

    var rule = new PolicyRule(
      RuleId: $"rule-{profileId}",
      ProfileId: profileId,
      MaxSimultaneousPasses: MaxSimultaneousPasses,
      DurationWarningSeconds: DurationWarningMinutes * 60,
      MaxDailyPassesPerStudent: MaxDailyPasses,
      LockoutStartMinutes: FirstWindowMinutes,
      LockoutEndMinutes: LastWindowMinutes,
      FirstWindowAction: FirstWindowAction,
      LastWindowAction: LastWindowAction,
      AlertSound: AlertSound,
      TerminalEnforcementEnabled: TerminalEnforcementEnabled
    );
    policyRepository.SavePolicyRule(rule);
    policyRepository.SaveBellSchedule(profileId, Periods.Select(p => p.ToModel()).ToList());
    policyRepository.SaveScheduleExceptions(profileId, Exceptions.Select(item => item.ToModel()));
    StatusMessage = "Teacher workspace settings saved.";
  }

  public void AddPeriod(string profileId, string name, string start, string end) {
    var period = new BellPeriodItemViewModel(new BellSchedulePeriod(
      ScheduleId: $"sched-{Guid.NewGuid():N}",
      ProfileId: profileId,
      PeriodName: name,
      StartTime: start,
      EndTime: end,
      DaysOfWeek: "Mon,Tue,Wed,Thu,Fri",
      ScheduleName: "Regular"
    ), isEditing: true, onSaved: SortPeriods);
    Periods.Add(period);
    SortPeriods();
  }

  public void RemovePeriod(BellPeriodItemViewModel period) {
    Periods.Remove(period);
  }

  public void RemovePeriod(BellSchedulePeriod period) {
    var item = Periods.FirstOrDefault(p => p.ScheduleId == period.ScheduleId);
    if (item != null) Periods.Remove(item);
  }

  public void AddException(string profileId) {
    if (!DateOnly.TryParseExact(NewExceptionDate, "yyyy-MM-dd", out _)) {
      StatusMessage = "Use YYYY-MM-DD for the exception date.";
      return;
    }
    if (!NewExceptionIsNoSchool && string.IsNullOrWhiteSpace(NewExceptionScheduleName)) {
      StatusMessage = "Choose the schedule template for this date.";
      return;
    }
    var existing = Exceptions.FirstOrDefault(item => item.ExceptionDate == NewExceptionDate);
    if (existing != null) Exceptions.Remove(existing);
    Exceptions.Add(new ScheduleExceptionItemViewModel(new ScheduleException(
      $"exception-{Guid.NewGuid():N}", profileId, NewExceptionDate,
      NewExceptionIsNoSchool ? "" : NewExceptionScheduleName.Trim(), NewExceptionIsNoSchool)));
    StatusMessage = "Date exception added. Save workspace settings to apply it.";
  }

  public void RemoveException(ScheduleExceptionItemViewModel item) => Exceptions.Remove(item);

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
