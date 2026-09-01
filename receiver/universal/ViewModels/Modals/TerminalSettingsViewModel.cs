using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class TerminalSettingsViewModel : INotifyPropertyChanged {
  readonly ITerminalRepository terminalRepository;
  readonly ITerminalConnection connection;
  int maxStudentIdLength = 10;
  string terminalName = "Hallzee (Room 204)";
  string statusMessage = "";
  string statusColor = "#64748B";

  public TerminalSettingsViewModel(ITerminalRepository terminalRepository, ITerminalConnection connection) {
    this.terminalRepository = terminalRepository;
    this.connection = connection;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

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
