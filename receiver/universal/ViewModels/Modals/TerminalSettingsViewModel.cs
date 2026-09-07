using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class TerminalSettingsViewModel : INotifyPropertyChanged {
  readonly ITerminalRepository terminalRepository;
  readonly ITerminalConnection connection;
  readonly string classroomInfoFilePath;
  readonly Action? onClassroomInfoChanged;
  int maxStudentIdLength = 10;
  string terminalName = "No Device Paired";
  string statusMessage = "";
  string statusColor = "#64748B";

  string teacherName = "";
  string school = "";
  string room = "";

  public TerminalSettingsViewModel(
    ITerminalRepository terminalRepository,
    ITerminalConnection connection,
    string? appDataPath = null,
    Action? onClassroomInfoChanged = null
  ) {
    this.terminalRepository = terminalRepository;
    this.connection = connection;
    this.onClassroomInfoChanged = onClassroomInfoChanged;

    classroomInfoFilePath = string.IsNullOrEmpty(appDataPath)
      ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hallzee", "classroom-info.json")
      : Path.Combine(appDataPath, "classroom-info.json");

    LoadClassroomInfo();
    LoadLastPairedTerminal();
  }

  void LoadLastPairedTerminal() {
    try {
      var all = terminalRepository.GetAllTerminals()
        .Where(t => !t.TerminalId.StartsWith("LEGACY", StringComparison.OrdinalIgnoreCase));
      var latest = all.OrderByDescending(t => t.LastSeenAt).FirstOrDefault();
      if (latest != null && !string.IsNullOrWhiteSpace(latest.CustomName)) {
        terminalName = latest.CustomName;
      }
    } catch {
      // Best effort load
    }
  }

  public string TeacherName {
    get => teacherName;
    set {
      if (teacherName != value) {
        teacherName = value;
        OnPropertyChanged();
        SaveClassroomInfo();
      }
    }
  }

  public string School {
    get => school;
    set {
      if (school != value) {
        school = value;
        OnPropertyChanged();
        SaveClassroomInfo();
      }
    }
  }

  public string Room {
    get => room;
    set {
      if (room != value) {
        room = value;
        OnPropertyChanged();
        SaveClassroomInfo();
      }
    }
  }

  void LoadClassroomInfo() {
    try {
      if (File.Exists(classroomInfoFilePath)) {
        var json = File.ReadAllText(classroomInfoFilePath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("TeacherName", out var t)) teacherName = t.GetString() ?? "";
        if (root.TryGetProperty("School", out var s)) school = s.GetString() ?? "";
        if (root.TryGetProperty("Room", out var r)) room = r.GetString() ?? "";
      }
    } catch {
      // Best effort load
    }
  }

  public void SaveClassroomInfo() {
    try {
      var dir = Path.GetDirectoryName(classroomInfoFilePath);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
      var payload = new {
        TeacherName = TeacherName,
        School = School,
        Room = Room
      };
      File.WriteAllText(classroomInfoFilePath, System.Text.Json.JsonSerializer.Serialize(payload));
      StatusMessage = "Classroom details saved.";
      StatusColor = "#10B981";
      onClassroomInfoChanged?.Invoke();
    } catch (Exception ex) {
      StatusMessage = $"Failed to save: {ex.Message}";
      StatusColor = "#EF4444";
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  string selectedSubmenu = "Profile";

  public string SelectedSubmenu {
    get => selectedSubmenu;
    set {
      if (selectedSubmenu != value) {
        selectedSubmenu = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(IsProfileSelected));
        OnPropertyChanged(nameof(IsDeviceSelected));
        OnPropertyChanged(nameof(ProfileTabBackground));
        OnPropertyChanged(nameof(ProfileTabForeground));
        OnPropertyChanged(nameof(DeviceTabBackground));
        OnPropertyChanged(nameof(DeviceTabForeground));
      }
    }
  }

  public bool IsProfileSelected => SelectedSubmenu == "Profile";
  public bool IsDeviceSelected => SelectedSubmenu == "Device";

  public string ProfileTabBackground => IsProfileSelected ? "#0284C7" : "Transparent";
  public string ProfileTabForeground => IsProfileSelected ? "White" : "#475569";

  public string DeviceTabBackground => IsDeviceSelected ? "#0284C7" : "Transparent";
  public string DeviceTabForeground => IsDeviceSelected ? "White" : "#475569";

  public void SelectProfileSubmenu() => SelectedSubmenu = "Profile";
  public void SelectDeviceSubmenu() => SelectedSubmenu = "Device";

  public int MaxStudentIdLength {
    get => maxStudentIdLength;
    set {
      if (maxStudentIdLength != value) {
        maxStudentIdLength = value;
        OnPropertyChanged();
      }
    }
  }

  public string TerminalName {
    get => terminalName;
    set {
      if (terminalName != value) {
        terminalName = value;
        OnPropertyChanged();
      }
    }
  }

  public string StatusMessage {
    get => statusMessage;
    private set { statusMessage = value; OnPropertyChanged(); }
  }

  public string StatusColor {
    get => statusColor;
    private set { statusColor = value; OnPropertyChanged(); }
  }

  public async Task ApplySettingsAsync(string terminalId = "preview-hallzee") {
    StatusMessage = "Applying settings to terminal…";
    StatusColor = "#0284C7";

    try {
      var command = KioskSettingsProtocol.BuildStudentIdLengthCommand(MaxStudentIdLength) + "\n";
      await connection.SendAsync(command);

      terminalRepository.SaveTerminal(new TerminalDeviceConfig(
        TerminalId: terminalId,
        CustomName: TerminalName,
        LastSeenAt: DateTime.UtcNow,
        MaxIdLength: MaxStudentIdLength
      ));

      StatusMessage = $"Applied maximum student ID length: {MaxStudentIdLength}";
      StatusColor = "#10B981";
    } catch (Exception ex) {
      StatusMessage = $"Failed to apply settings: {ex.Message}";
      StatusColor = "#EF4444";
    }
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
