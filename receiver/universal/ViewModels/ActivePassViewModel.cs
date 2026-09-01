using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BathroomSync.Universal.ViewModels;

public sealed class ActivePassViewModel : INotifyPropertyChanged {
  bool isOccupied;
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
    IsOccupied
      ? $"{DisplayName} is Out of Class"
      : "Restroom is Currently Empty";

  public string SubtitleText =>
    IsOccupied
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
    }
  }

  public string ElapsedFormatted {
    get {
      var minutes = ElapsedSeconds / 60;
      var seconds = ElapsedSeconds % 60;
      return $"{minutes:D2}:{seconds:D2}";
    }
  }

  public string DurationDisplay {
    get {
      if (!IsOccupied) return "Ready";
      var minutes = ElapsedSeconds / 60;
      var seconds = ElapsedSeconds % 60;
      return $"{minutes}m {seconds:D2}s";
    }
  }

  public string StatusText => IsOccupied ? "Student Out of Class" : "Hall Pass Available";
  public string StatusColor => IsOccupied ? "#D97706" : "#059669";
  public string BadgeColor => IsOccupied ? "#F59E0B" : "#10B981";
  public string BadgeText => IsOccupied ? "PASS OCCUPIED (DEMO STATE)" : "PASS AVAILABLE (DEMO STATE)";
  public string HeroCardBackground => IsOccupied ? "#FFFBEB" : "#ECFDF5";
  public string HeroCardBorderBrush => IsOccupied ? "#FCD34D" : "#6EE7B7";
  public string AvatarBackground => IsOccupied ? "#F59E0B" : "#10B981";
  public string AvatarIconResource => IsOccupied ? "IconUserX" : "IconUserCheck";
  public string TimerTextColor => IsOccupied ? "#D97706" : "#059669";
  public string ActionButtonText => IsOccupied ? "Check In" : "Simulate Tap";

  public void SetOccupied(string id, string? name, DateTime? timestamp = null) {
    StudentId = id;
    StudentName = name;
    checkoutTimestamp = timestamp ?? DateTime.Now;
    DepartTime = checkoutTimestamp.Value.ToString("h:mm tt");
    var diff = (int)Math.Max(0, (DateTime.Now - checkoutTimestamp.Value).TotalSeconds);
    ElapsedSeconds = diff;
    IsOccupied = true;
  }

  public void SetAvailable() {
    IsOccupied = false;
    StudentId = null;
    StudentName = null;
    DepartTime = null;
    checkoutTimestamp = null;
    ElapsedSeconds = 0;
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
