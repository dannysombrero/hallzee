using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BathroomSync.Universal.ViewModels;

public sealed class ActivePassViewModel : INotifyPropertyChanged {
  bool isOccupied;
  string? studentId;
  string? studentName;
  string? departTime;
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
      }
    }
  }

  public string? StudentId {
    get => studentId;
    private set { studentId = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
  }

  public string? StudentName {
    get => studentName;
    private set { studentName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
  }

  public string DisplayName =>
    !string.IsNullOrWhiteSpace(StudentName)
      ? StudentName
      : !string.IsNullOrWhiteSpace(StudentId)
        ? $"#{StudentId}"
        : "No active pass";

  public string? DepartTime {
    get => departTime;
    private set { departTime = value; OnPropertyChanged(); }
  }

  public int ElapsedSeconds {
    get => elapsedSeconds;
    private set {
      elapsedSeconds = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ElapsedFormatted));
    }
  }

  public string ElapsedFormatted {
    get {
      var minutes = ElapsedSeconds / 60;
      var seconds = ElapsedSeconds % 60;
      return $"{minutes:D2}:{seconds:D2}";
    }
  }

  public string StatusText => IsOccupied ? "Student Out of Class" : "Hall Pass Available";
  public string StatusColor => IsOccupied ? "#F43F5E" : "#10B981";
  public string BadgeColor => IsOccupied ? "#FFE4E6" : "#DCFCE7";
  public string BadgeText => IsOccupied ? "OCCUPIED" : "AVAILABLE";

  public void SetOccupied(string id, string? name, DateTime? timestamp = null) {
    StudentId = id;
    StudentName = name;
    checkoutTimestamp = timestamp ?? DateTime.Now;
    DepartTime = checkoutTimestamp.Value.ToString("hh:mm tt");
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
