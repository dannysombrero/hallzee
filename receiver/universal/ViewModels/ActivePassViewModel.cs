using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BathroomSync.Universal.ViewModels;

public sealed class ActivePassViewModel : INotifyPropertyChanged {
  bool isOccupied;
  bool isStatusKnown;
  string? studentId;
  string? studentName;
  string? departTime;
  string destination = "Hallway Restroom (East)";
  DateTime? checkoutTimestamp;
  int elapsedSeconds;

  public event PropertyChangedEventHandler? PropertyChanged;

  public bool IsOccupied {
    get => isOccupied;
    private set {
      if (isOccupied != value) {
        isOccupied = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(BadgeColor));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(HeroCardBackground));
        OnPropertyChanged(nameof(HeroCardBorderBrush));
        OnPropertyChanged(nameof(AvatarBackground));
        OnPropertyChanged(nameof(AvatarIconResource));
        OnPropertyChanged(nameof(TimerTextColor));
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(HeadingText));
        OnPropertyChanged(nameof(SubtitleText));
      }
    }
  }

  public string? StudentId {
    get => studentId;
    private set { studentId = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(HeadingText)); }
  }

  public string? StudentName {
    get => studentName;
    private set { studentName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(HeadingText)); }
  }

  public string DisplayName =>
    !string.IsNullOrWhiteSpace(StudentName)
      ? StudentName
      : !string.IsNullOrWhiteSpace(StudentId)
        ? $"#{StudentId}"
        : "No active pass";

  public string HeadingText =>
    IsStatusUnknown
      ? "Pass Status Unknown"
      : IsOccupied
      ? $"{DisplayName} is Out of Class"
      : "Restroom is Currently Empty";

  public string SubtitleText =>
    IsStatusUnknown
      ? "Connect to the Hallzee terminal to confirm whether a student is out."
      : IsOccupied
      ? $"Departed at {DepartTime ?? "recently"} for {Destination}."
      : "The physical Hallzee terminal is ready for the next student.";

  public string? DepartTime {
    get => departTime;
    private set { departTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(SubtitleText)); }
  }

  public string Destination {
    get => destination;
    set { destination = value; OnPropertyChanged(); OnPropertyChanged(nameof(SubtitleText)); }
  }

  public int ElapsedSeconds {
    get => elapsedSeconds;
    private set {
      elapsedSeconds = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ElapsedFormatted));
        OnPropertyChanged(nameof(DurationDisplay));
        OnPropertyChanged(nameof(IsStatusUnknown));
    }
  }

  public bool IsStatusUnknown => !isStatusKnown;

  public string ElapsedFormatted {
    get {
      var minutes = ElapsedSeconds / 60;
      var seconds = ElapsedSeconds % 60;
      return $"{minutes:D2}:{seconds:D2}";
    }
  }

  public string DurationDisplay {
    get {
      if (IsStatusUnknown) return "Unknown";
      if (!IsOccupied) return "Ready";
      var minutes = ElapsedSeconds / 60;
      var seconds = ElapsedSeconds % 60;
      return $"{minutes}m {seconds:D2}s";
    }
  }

  public string StatusText => IsStatusUnknown ? "Pass Status Unknown" : IsOccupied ? "Student Out of Class" : "Hall Pass Available";
  public string StatusColor => IsStatusUnknown ? "#64748B" : IsOccupied ? "#D97706" : "#059669";
  public string BadgeColor => IsStatusUnknown ? "#64748B" : IsOccupied ? "#F59E0B" : "#10B981";
  public string BadgeText => IsStatusUnknown ? "STATUS UNKNOWN" : IsOccupied ? "PASS OCCUPIED" : "PASS AVAILABLE";
  public string HeroCardBackground => IsStatusUnknown ? "#F8FAFC" : IsOccupied ? "#FFFBEB" : "#ECFDF5";
  public string HeroCardBorderBrush => IsStatusUnknown ? "#CBD5E1" : IsOccupied ? "#FCD34D" : "#6EE7B7";
  public string AvatarBackground => IsStatusUnknown ? "#64748B" : IsOccupied ? "#F59E0B" : "#10B981";
  public string AvatarIconResource => IsOccupied ? "IconUserX" : "IconUserCheck";
  public string TimerTextColor => IsStatusUnknown ? "#64748B" : IsOccupied ? "#D97706" : "#059669";
  public string ActionButtonText => IsOccupied ? "Check In" : "Simulate Tap";

  public void SetOccupied(string id, string? name, DateTime? timestamp = null) {
    isStatusKnown = true;
    StudentId = id;
    StudentName = name;
    checkoutTimestamp = timestamp ?? DateTime.Now;
    DepartTime = checkoutTimestamp.Value.ToString("h:mm tt");
    var diff = (int)Math.Max(0, (DateTime.Now - checkoutTimestamp.Value).TotalSeconds);
    ElapsedSeconds = diff;
    IsOccupied = true;
  }

  public void SetAvailable() {
    var wasUnknown = IsStatusUnknown;
    isStatusKnown = true;
    IsOccupied = false;
    StudentId = null;
    StudentName = null;
    DepartTime = null;
    checkoutTimestamp = null;
    ElapsedSeconds = 0;
    if (wasUnknown) NotifyStatusPresentationChanged();
  }

  public void SetUnknown() {
    isStatusKnown = false;
    IsOccupied = false;
    StudentId = null;
    StudentName = null;
    DepartTime = null;
    checkoutTimestamp = null;
    ElapsedSeconds = 0;
    NotifyStatusPresentationChanged();
  }

  void NotifyStatusPresentationChanged() {
    OnPropertyChanged(nameof(IsStatusUnknown));
    OnPropertyChanged(nameof(StatusText));
    OnPropertyChanged(nameof(StatusColor));
    OnPropertyChanged(nameof(BadgeColor));
    OnPropertyChanged(nameof(BadgeText));
    OnPropertyChanged(nameof(HeroCardBackground));
    OnPropertyChanged(nameof(HeroCardBorderBrush));
    OnPropertyChanged(nameof(AvatarBackground));
    OnPropertyChanged(nameof(TimerTextColor));
    OnPropertyChanged(nameof(HeadingText));
    OnPropertyChanged(nameof(SubtitleText));
    OnPropertyChanged(nameof(DurationDisplay));
  }

  public void ToggleDemoOccupancy(string fallbackId = "9042", string fallbackName = "Marcus Sterling") {
    if (IsOccupied) {
      SetAvailable();
    } else {
      SetOccupied(fallbackId, fallbackName, DateTime.Now.AddMinutes(-57).AddSeconds(-13));
    }
  }

  public void Tick() {
    if (IsOccupied) {
      if (checkoutTimestamp.HasValue) {
        ElapsedSeconds = (int)Math.Max(0, (DateTime.Now - checkoutTimestamp.Value).TotalSeconds);
      } else {
        ElapsedSeconds++;
      }
    }
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
