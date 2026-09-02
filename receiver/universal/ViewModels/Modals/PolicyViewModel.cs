using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

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
  string statusMessage = "";

  public PolicyViewModel(IPolicyRepository policyRepository) {
    this.policyRepository = policyRepository;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<BellSchedulePeriod> Periods { get; } = new();
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

  public string NewProfileName {
    get => newProfileName;
    set { if (newProfileName != value) { newProfileName = value; OnPropertyChanged(); } }
  }

  public string StatusMessage {
    get => statusMessage;
    private set { statusMessage = value; OnPropertyChanged(); }
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

    var schedule = policyRepository.GetBellSchedule(profileId);
    Periods.Clear();
    foreach (var p in schedule) Periods.Add(p);
    StatusMessage = "";
  }

  public void Save(string profileId) {
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
      AlertSound: AlertSound
    );
    policyRepository.SavePolicyRule(rule);
    policyRepository.SaveBellSchedule(profileId, Periods);
    StatusMessage = "Profile settings saved.";
  }

  public void AddPeriod(string profileId, string name, string start, string end) {
    Periods.Add(new BellSchedulePeriod(
      ScheduleId: $"sched-{Guid.NewGuid():N}",
      ProfileId: profileId,
      PeriodName: name,
      StartTime: start,
      EndTime: end,
      DaysOfWeek: "Mon,Tue,Wed,Thu,Fri",
      ScheduleName: "Regular"
    ));
  }

  public void RemovePeriod(BellSchedulePeriod period) {
    Periods.Remove(period);
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
