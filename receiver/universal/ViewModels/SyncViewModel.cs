using BathroomSync.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BathroomSync.Universal.ViewModels;

public sealed class SyncViewModel : INotifyPropertyChanged, IDisposable {
  readonly ITerminalConnection connection;
  readonly TripSqliteRepository storage;
  readonly SyncSession session;
  readonly string exportFolder;
  readonly SynchronizationContext? uiContext;
  TerminalDevice? selectedDevice;
  string statusTitle = "Ready to find a terminal";
  string statusDetail = "Choose Find terminal to start the preview discovery flow.";
  string statusColor = "#2C8A50";
  int savedTrips;
  int? transferTotal;
  int transferredTrips;
  bool canFind = true;
  bool canSync;

  public SyncViewModel(ITerminalConnection connection, string? appDataPath = null, bool isPreviewMode = true) {
    this.connection = connection;
    uiContext = SynchronizationContext.Current;
    var appData = appDataPath ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Hallzee",
      isPreviewMode ? "universal-preview" : "universal"
    );
    exportFolder = Path.Combine(appData, "exports");
    if (appDataPath is null) MigrateLegacyDatabase(appData, isPreviewMode);
    storage = new TripSqliteRepository(Path.Combine(appData, "hallzee-trips.db"));
    session = new SyncSession(storage);
    connection.TextReceived += HandleTerminalText;
    connection.ConnectionLost += HandleConnectionLost;
    IsPreviewMode = isPreviewMode;
    ModeDescription = isPreviewMode
      ? "Desktop sync client · Preview mode"
      : "Desktop sync client · Windows Bluetooth";
    LogEntries.Add(isPreviewMode
      ? "Preview ready. No Bluetooth hardware is used in this shell."
      : "Windows Bluetooth transport ready.");
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public ObservableCollection<TerminalDevice> Devices { get; } = new();
  public ObservableCollection<string> LogEntries { get; } = new();

  public TerminalDevice? SelectedDevice {
    get => selectedDevice;
    set { selectedDevice = value; OnPropertyChanged(); CanSync = value is not null; }
  }
  public string StatusTitle { get => statusTitle; private set { statusTitle = value; OnPropertyChanged(); } }
  public string StatusDetail { get => statusDetail; private set { statusDetail = value; OnPropertyChanged(); } }
  public string StatusColor { get => statusColor; private set { statusColor = value; OnPropertyChanged(); } }
  public int SavedTrips { get => savedTrips; private set { savedTrips = value; OnPropertyChanged(); } }
  public int? TransferTotal { get => transferTotal; private set { transferTotal = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTransferProgress)); OnPropertyChanged(nameof(TransferProgressText)); OnPropertyChanged(nameof(TransferProgressPercent)); } }
  public int TransferredTrips { get => transferredTrips; private set { transferredTrips = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransferProgressText)); OnPropertyChanged(nameof(TransferProgressPercent)); } }
  public bool HasTransferProgress => TransferTotal is not null;
  public string TransferProgressText => TransferTotal is null ? "Preparing transfer…" : $"{TransferredTrips} / {TransferTotal} records";
  public double TransferProgressPercent => TransferTotal is > 0 ? Math.Min(100, TransferredTrips * 100.0 / TransferTotal.Value) : 100;
  public bool CanFind { get => canFind; private set { canFind = value; OnPropertyChanged(); } }
  public bool CanSync { get => canSync; private set { canSync = value; OnPropertyChanged(); } }
  public bool IsPreviewMode { get; }
  public string ModeDescription { get; }
  public string WindowTitle => IsPreviewMode ? "Hallzee Sync Preview" : "Hallzee Sync";
  public string FooterDescription => IsPreviewMode
    ? "The preview uses a simulated terminal. The Windows build uses this same UI with Bluetooth."
    : "Find, pair, and sync Hallzee directly from this Windows app.";

  public async Task FindAsync() {
    CanFind = false;
    CanSync = false;
    Devices.Clear();
    SetStatus("Finding Hallzee", "Scanning nearby devices…", "#1261A0");
    try {
      // A stale Windows GATT object can keep reporting Connected after the
      // kiosk has powered off. Always discard it before a fresh scan.
      await connection.DisconnectAsync();
      var devices = await connection.DiscoverAsync();
      foreach (var device in devices) Devices.Add(device);
      SelectedDevice = Devices.FirstOrDefault();
      SetStatus(
        SelectedDevice is null ? "Terminal not found" : "Terminal ready",
        SelectedDevice is null ? "Make sure Hallzee is powered on, then try again." : "Hallzee is ready to sync.",
        SelectedDevice is null ? "#B3443C" : "#2C8A50"
      );
      LogEntries.Add($"Discovery completed: {Devices.Count} matching terminal(s).");
    } catch (Exception exception) {
      var detail = DescribeException(exception);
      SetStatus("Discovery failed", detail, "#B3443C");
      LogEntries.Add($"Discovery error: {detail}");
    } finally {
      CanFind = true;
    }
  }

  public async Task SyncAsync() {
    if (SelectedDevice is null) return;

    CanSync = false;
    SetStatus("Connecting", "Opening a secure Bluetooth connection…", "#1261A0");
    try {
      session.Start();
      SavedTrips = 0;
      TransferTotal = null;
      TransferredTrips = 0;
      await connection.ConnectAsync(SelectedDevice);
      await connection.SendAsync("HELLO,1");
      var latestTripId = Math.Clamp(storage.GetLatestTripId(), 0L, uint.MaxValue);
      await connection.SendAsync($"TIME_CURSOR,{DateTime.Now:yyyy-MM-dd,HH:mm:ss},{latestTripId}");
      LogEntries.Add($"Connected; requesting trips after durable ID {latestTripId}.");
    } catch (Exception exception) {
      var detail = DescribeException(exception);
      SetStatus("Connection failed", detail, "#B3443C");
      LogEntries.Add($"Connection error: {detail}");
      await connection.DisconnectAsync();
      CanSync = true;
    }
  }

  static string DescribeException(Exception exception) {
    var message = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
    return exception.HResult == 0 ? message : $"{message} (0x{exception.HResult:X8})";
  }

  static void MigrateLegacyDatabase(string appData, bool isPreviewMode) {
    var destinationPath = Path.Combine(appData, "hallzee-trips.db");
    if (File.Exists(destinationPath)) return;

    var legacyPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Bathroom Terminal",
      isPreviewMode ? "universal-preview" : "universal",
      "bathroom-trips.db"
    );
    if (!File.Exists(legacyPath)) return;

    Directory.CreateDirectory(appData);
    File.Copy(legacyPath, destinationPath);
  }

  public Task OpenExportFolderAsync() {
    Directory.CreateDirectory(exportFolder);
    storage.ExportCsv(Path.Combine(exportFolder, $"hallzee_trips_{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
    Process.Start(new ProcessStartInfo(exportFolder) { UseShellExecute = true });
    return Task.CompletedTask;
  }

  public Task SaveCsvAsAsync(string path) {
    storage.ExportCsv(path);
    SetStatus("CSV saved", "The exported trip list is sorted by numeric trip ID.", "#2C8A50");
    LogEntries.Add($"Exported CSV: {path}");
    return Task.CompletedTask;
  }

  void HandleTerminalText(object? sender, string text) => RunOnUiContext(() => _ = ProcessTerminalTextAsync(text));

  async Task ProcessTerminalTextAsync(string text) {
    var update = session.ProcessReceivedData(text);
    if (update.TransferTotal is not null) TransferTotal = update.TransferTotal;
    if (update.TransferredTripCount is not null) TransferredTrips = update.TransferredTripCount.Value;
    foreach (var log in update.Logs) LogEntries.Add(log);
    try {
      foreach (var command in update.OutboundCommands) await connection.SendAsync(command);
    } catch (Exception exception) {
      SetStatus("Sync interrupted", exception.Message, "#B3443C");
      await connection.DisconnectAsync();
      CanSync = true;
      return;
    }

    if (update.Status == SyncStatus.Synchronizing) {
      SetStatus("Synchronizing", "Securely retrieving trips from Hallzee.", "#2C8A50");
    } else if (update.Status == SyncStatus.Complete) {
      SavedTrips = update.SavedTripCount ?? 0;
      SetStatus("Sync complete", $"{SavedTrips} new trip(s) saved this session.", "#2C8A50");
      await connection.DisconnectAsync();
      CanSync = true;
    }

    if (update.StorageUnavailable) {
      SetStatus("Local storage unavailable", "The terminal retained this trip. Try syncing again.", "#B3443C");
      CanSync = true;
    }
  }

  void HandleConnectionLost(object? sender, string detail) =>
    RunOnUiContext(() => _ = HandleConnectionLostAsync(detail));

  async Task HandleConnectionLostAsync(string detail) {
    await connection.DisconnectAsync();
    SetStatus("Connection lost", detail, "#B3443C");
    LogEntries.Add(detail);
    CanSync = true;
  }

  void RunOnUiContext(Action action) {
    if (uiContext is null || SynchronizationContext.Current == uiContext) action();
    else uiContext.Post(_ => action(), null);
  }

  void SetStatus(string title, string detail, string color) {
    StatusTitle = title;
    StatusDetail = detail;
    StatusColor = color;
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

  public void Dispose() {
    connection.TextReceived -= HandleTerminalText;
    connection.ConnectionLost -= HandleConnectionLost;
    connection.Dispose();
  }
}
