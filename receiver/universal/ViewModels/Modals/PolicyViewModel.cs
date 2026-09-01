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
  string statusMessage = "";

  public PolicyViewModel(IPolicyRepository policyRepository) {
    this.policyRepository = policyRepository;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<BellSchedulePeriod> Periods { get; } = new();

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

  public string StatusMessage {
    get => statusMessage;
    private set { statusMessage = value; OnPropertyChanged(); }
  }

  public void Refresh(string profileId) {
    var rule = policyRepository.GetPolicyRule(profileId);
    MaxDailyPasses = rule.MaxDailyPassesPerStudent;
    DurationWarningMinutes = rule.DurationWarningSeconds / 60;
    MaxSimultaneousPasses = rule.MaxSimultaneousPasses;

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
      MaxDailyPassesPerStudent: MaxDailyPasses
    );
    policyRepository.SavePolicyRule(rule);
    policyRepository.SaveBellSchedule(profileId, Periods);
    StatusMessage = "Policy and schedule saved.";
  }

  public void AddPeriod(string profileId, string name, string start, string end) {
    Periods.Add(new BellSchedulePeriod(
      ScheduleId: $"sched-{Guid.NewGuid():N}",
      ProfileId: profileId,
      PeriodName: name,
      StartTime: start,
      EndTime: end
    ));
  }

  public void RemovePeriod(BellSchedulePeriod period) {
    Periods.Remove(period);
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
