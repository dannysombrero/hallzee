using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;
using BathroomSync.Universal.Services;

namespace BathroomSync.Universal.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable {
  readonly ITerminalConnection connection;
  readonly TripSqliteRepository tripRepository;
  readonly RosterSqliteRepository rosterRepository;
  readonly ProfileAndPolicySqliteRepository profileRepository;
  readonly RosterService rosterService;
  readonly SyncSession syncSession;

  string activeView = "dashboard";
  string activeModal = "None";
  bool isConnected = false;
  bool isSyncing;
  string lastSyncTimeText = "Never (No sync yet)";
  string connectedTerminalName = "Room 204 Door Kiosk (East-204)";
  string profileDetails = "Period 3 (9:15–10:05)";
  string profileTeacher = "Teacher: Dr. Aris Thorne";
  string loadedRosterFileName = "Chemistry_Period3_Students.csv";
  string syncProgressText = "";
  ClassroomProfile activeProfile;
  string exportFolder;
  Timer? timer;

  public MainViewModel(
    ITerminalConnection connection,
    string? appDataPath = null,
    bool isPreviewMode = true
  ) {
    this.connection = connection;
    var appData = appDataPath ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Hallzee",
      isPreviewMode ? "universal-preview" : "universal"
    );
    exportFolder = Path.Combine(appData, "exports");
    Directory.CreateDirectory(exportFolder);

    var dbPath = Path.Combine(appData, "hallzee-trips.db");
    tripRepository = new TripSqliteRepository(dbPath);
    rosterRepository = new RosterSqliteRepository(dbPath);
    profileRepository = new ProfileAndPolicySqliteRepository(dbPath);
    rosterService = new RosterService(rosterRepository);
    syncSession = new SyncSession(tripRepository);

    // Load active profile
    var allProfiles = profileRepository.GetAllProfiles();
    foreach (var p in allProfiles) Profiles.Add(p);
    activeProfile = profileRepository.GetActiveProfile() ?? Profiles.FirstOrDefault() ?? new ClassroomProfile("default", "Room 204 • Chemistry AP");

    // Initialize child viewmodels
    ActivePass = new ActivePassViewModel();
    ActivePass.SetUnknown();

    Dashboard = new DashboardViewModel(tripRepository, rosterService, ActivePass);
    TripsModal = new TripsViewModel(tripRepository, rosterService);
    RosterModal = new RosterViewModel(rosterService);
    PolicyModal = new PolicyViewModel(profileRepository);
    TerminalSettingsModal = new TerminalSettingsViewModel(profileRepository, connection);
    FindTerminalsModal = new FindTerminalsViewModel(connection);

    // Connection events
    connection.TextReceived += HandleTextReceived;
    connection.ConnectionLost += HandleConnectionLost;

    // Refresh data
    RefreshActiveProfileData();

    // Start 1-second ticker for active pass elapsed timer
    timer = new Timer(_ => {
      ActivePass.Tick();
      Dashboard.TickLiveActivePasses();
    }, null, 1000, 1000);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<ClassroomProfile> Profiles { get; } = new();

  public ActivePassViewModel ActivePass { get; }
  public DashboardViewModel Dashboard { get; }
  public TripsViewModel TripsModal { get; }
  public RosterViewModel RosterModal { get; }
  public PolicyViewModel PolicyModal { get; }
  public TerminalSettingsViewModel TerminalSettingsModal { get; }
  public FindTerminalsViewModel FindTerminalsModal { get; }

  public string ActiveView {
    get => activeView;
    set {
      if (activeView != value) {
        activeView = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsTripsActive));
        OnPropertyChanged(nameof(IsRosterActive));
        OnPropertyChanged(nameof(IsPoliciesActive));
        OnPropertyChanged(nameof(IsTerminalSettingsActive));
        OnPropertyChanged(nameof(IsAppSettingsActive));
      }
    }
  }

  public bool IsDashboardActive => ActiveView == "dashboard";
  public bool IsTripsActive => ActiveView == "trips";
  public bool IsRosterActive => ActiveView == "roster";
  public bool IsPoliciesActive => ActiveView == "policies";
  public bool IsTerminalSettingsActive => ActiveView == "terminal";
  public bool IsAppSettingsActive => ActiveView == "settings";

  public string ActiveModal {
    get => activeModal;
    set {
      if (activeModal != value) {
        activeModal = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(IsModalOpen));
        OnPropertyChanged(nameof(IsTripsModalVisible));
        OnPropertyChanged(nameof(IsRosterModalVisible));
        OnPropertyChanged(nameof(IsPoliciesModalVisible));
        OnPropertyChanged(nameof(IsTerminalSettingsModalVisible));
        OnPropertyChanged(nameof(IsFindTerminalsModalVisible));
      }
    }
  }

  public bool IsModalOpen => ActiveModal != "None";
  public bool IsTripsModalVisible => ActiveModal == "Trips";
  public bool IsRosterModalVisible => ActiveModal == "Roster";
  public bool IsPoliciesModalVisible => ActiveModal == "Policies";
  public bool IsTerminalSettingsModalVisible => ActiveModal == "TerminalSettings";
  public bool IsFindTerminalsModalVisible => ActiveModal == "FindTerminals";

  public bool IsConnected {
    get => isConnected;
    private set {
      if (isConnected != value) {
        isConnected = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(ConnectionStatusColor));
        OnPropertyChanged(nameof(ConnectionBadgeBackground));
        OnPropertyChanged(nameof(ConnectionBadgeForeground));
        OnPropertyChanged(nameof(TopStatusBadgeText));
        OnPropertyChanged(nameof(TopStatusBadgeBackground));
        OnPropertyChanged(nameof(TopStatusBadgeForeground));
        OnPropertyChanged(nameof(TerminalAvatarBackground));
        OnPropertyChanged(nameof(ConnectedTerminalName));
      }
    }
  }

  public bool IsSyncing {
    get => isSyncing;
    private set {
      if (isSyncing != value) {
        isSyncing = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(TopStatusBadgeText));
        OnPropertyChanged(nameof(TopStatusBadgeBackground));
        OnPropertyChanged(nameof(TopStatusBadgeForeground));
        OnPropertyChanged(nameof(SyncButtonText));
      }
    }
  }

  public string SyncButtonText => IsSyncing ? "Syncing…" : "Sync Now";
  
  public string SyncProgressText {
    get => syncProgressText;
    private set { syncProgressText = value; OnPropertyChanged(); }
  }

  public string ConnectedTerminalName {
    get => isConnected ? connectedTerminalName : "No Terminal Connected";
    set { connectedTerminalName = value; OnPropertyChanged(); }
  }

  public string ProfileDetails => profileDetails;
  public string ProfileTeacher => profileTeacher;
  public string LoadedRosterFileName => loadedRosterFileName;

  public string ConnectionStatusText =>
    IsSyncing ? "SYNCING" : IsConnected ? "BLE CONNECTED" : "OFFLINE";

  public string ConnectionStatusColor =>
    IsSyncing ? "#0284C7" : IsConnected ? "#10B981" : "#94A3B8";

  public string ConnectionBadgeBackground =>
    IsSyncing ? "#E0F2FE" : IsConnected ? "#DCFCE7" : "#F1F5F9";

  public string ConnectionBadgeForeground =>
    IsSyncing ? "#0369A1" : IsConnected ? "#166534" : "#475569";

  public string TopStatusBadgeText =>
    !IsConnected ? "OFFLINE" : IsSyncing ? "SYNCING" : "BLE CONNECTED";

  public string TopStatusBadgeBackground =>
    !IsConnected ? "#E2E8F0" : IsSyncing ? "#E0F2FE" : "#DCFCE7";

  public string TopStatusBadgeForeground =>
    !IsConnected ? "#475569" : IsSyncing ? "#0369A1" : "#166534";

  public string TerminalAvatarBackground =>
    !IsConnected ? "#94A3B8" : "#10B981";

  public string LastSyncTimeText {
    get => lastSyncTimeText;
    private set { lastSyncTimeText = value; OnPropertyChanged(); }
  }

  public ClassroomProfile ActiveProfile {
    get => activeProfile;
    set {
      if (activeProfile != value && value != null) {
        activeProfile = value;
        profileRepository.SetActiveProfile(value.ProfileId);
        OnPropertyChanged();
        Dashboard.ClearLiveCheckouts();
        RefreshActiveProfileData();
      }
    }
  }

  public void OpenModal(string modalName) {
    ActiveModal = modalName;
    if (modalName == "Trips") TripsModal.Refresh(ActiveProfile.ProfileId);
    else if (modalName == "Roster") RosterModal.Refresh(ActiveProfile.ProfileId);
    else if (modalName == "Policies") PolicyModal.Refresh(ActiveProfile.ProfileId);
    else if (modalName == "FindTerminals") _ = FindTerminalsModal.ScanAsync();
  }

  public void CloseModal() {
    ActiveModal = "None";
    RosterModal.CancelImport();
    Dashboard.Refresh(ActiveProfile.ProfileId);
  }

  public void ExportTrips(string exportPath) {
    TripsModal.ExportCsv(ActiveProfile.ProfileId, exportPath);
    OpenModal("Trips");
  }

  public void SwitchProfile(ClassroomProfile profile) {
    ActiveProfile = profile;
  }

  public void CreateProfileFromPolicy() {
    var name = PolicyModal.NewProfileName.Trim();
    if (string.IsNullOrWhiteSpace(name)) return;

    var idBase = string.Concat(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
    if (string.IsNullOrWhiteSpace(idBase)) idBase = "classroom";
    var id = idBase;
    var suffix = 2;
    while (Profiles.Any(profile => string.Equals(profile.ProfileId, id, StringComparison.OrdinalIgnoreCase))) {
      id = $"{idBase}-{suffix++}";
    }

    var profile = new ClassroomProfile(id, name);
    profileRepository.SaveProfile(profile);
    PolicyModal.Save(profile.ProfileId);
    Profiles.Add(profile);
    ActiveProfile = profile;
    PolicyModal.NewProfileName = "";
  }

  public async Task ConnectAndSyncAsync() {
    var device = FindTerminalsModal.SelectedDevice;
    if (device != null) {
      connectedTerminalName = device.Name;
    }

    var connected = await FindTerminalsModal.ConnectAsync();
    if (connected) {
      IsConnected = true;
      CloseModal();
      await SyncNowAsync();
    }
  }

  public async Task SyncNowAsync() {
    if (!IsConnected) {
      OpenModal("FindTerminals");
      return;
    }

    IsSyncing = true;
    SyncProgressText = "Preparing sync...";
    try {
      syncSession.Start();
      await connection.SendAsync("HELLO,1");
      await connection.SendAsync(ActivePassProtocol.BuildGetActivePassCommand());
      await connection.SendAsync(KioskSettingsProtocol.QueryCommand + "\n");
      await connection.SendAsync($"SET,MAX_ACTIVE_PASSES,{PolicyModal.MaxSimultaneousPasses}");

      // The kiosk stores its classroom wall-clock time as a UTC-shaped epoch.
      // Send the Mac's local time so the terminal display and its local records
      // match the classroom clock.
      var now = DateTime.Now;
      var lastTripId = tripRepository.GetRecentTrips(1).FirstOrDefault()?.TripId ?? 0;
      var command = $"TIME_CURSOR,{now:yyyy-MM-dd},{now:HH:mm:ss},{lastTripId}\n";
      await connection.SendAsync(command);

      LastSyncTimeText = $"Today, {DateTime.Now:h:mm tt} (just now)";
    } catch {
      IsConnected = false;
      LastSyncTimeText = "Sync failed (Terminal offline)";
      IsSyncing = false;
    }
  }

  public async Task DisconnectAsync() {
    await connection.DisconnectAsync();
    IsConnected = false;
    ActivePass.SetUnknown();
    Dashboard.ClearLiveCheckouts();
    Dashboard.Refresh(ActiveProfile.ProfileId);
  }

  public async Task CheckInActivePassAsync() {
    if (ActivePass.IsOccupied && !string.IsNullOrEmpty(ActivePass.StudentId)) {
      var studentId = ActivePass.StudentId;
      if (connection is PreviewTerminalConnection preview) {
        preview.SimulateCheckin(studentId);
      } else {
        await connection.SendAsync("MANUAL_CHECKIN");
      }
    }
  }

  public async Task ApplyPolicyCapacityAsync() {
    if (IsConnected) {
      await connection.SendAsync($"SET,MAX_ACTIVE_PASSES,{PolicyModal.MaxSimultaneousPasses}");
    }
  }

  public void RefreshActiveProfileData() {
    Dashboard.Refresh(ActiveProfile.ProfileId);
    PolicyModal.Refresh(ActiveProfile.ProfileId);
  }

  void HandleTextReceived(object? sender, string text) {
    var update = syncSession.ProcessReceivedData(text);

    if (update.ActivePass != null) {
      if (update.ActivePass.Status == ActivePassStatus.Occupied && !string.IsNullOrEmpty(update.ActivePass.StudentId)) {
        var student = rosterService.LookupStudent(ActiveProfile.ProfileId, update.ActivePass.StudentId);
        ActivePass.SetOccupied(update.ActivePass.StudentId, student?.FullName, update.ActivePass.CheckedOutAt);
        Dashboard.RegisterLiveCheckout(update.ActivePass.StudentId, student?.FullName, update.ActivePass.CheckedOutAt);
        Dashboard.RefreshAdditionalActiveTrips();
      } else {
        ActivePass.SetAvailable();
        Dashboard.ClearLiveCheckouts();
      }
    }

    if (update.LiveEvent != null) {
      if (update.LiveEvent.EventType == LivePassEventType.Checkout) {
        var student = rosterService.LookupStudent(ActiveProfile.ProfileId, update.LiveEvent.StudentId);
        Dashboard.RegisterLiveCheckout(update.LiveEvent.StudentId, student?.FullName, update.LiveEvent.CheckoutTime);
        if (!ActivePass.IsOccupied) {
          ActivePass.SetOccupied(update.LiveEvent.StudentId, student?.FullName, update.LiveEvent.CheckoutTime);
        }
        Dashboard.RefreshAdditionalActiveTrips();
        Dashboard.Refresh(ActiveProfile.ProfileId);
      } else if (update.LiveEvent.EventType is LivePassEventType.Checkin or LivePassEventType.Reset) {
        Dashboard.ResolveLiveCheckout(update.LiveEvent.StudentId);
        if (ActivePass.StudentId == update.LiveEvent.StudentId) ActivePass.SetAvailable();
        Dashboard.RefreshAdditionalActiveTrips();
        Dashboard.Refresh(ActiveProfile.ProfileId);
      }
    }

    // A LIVE_TRIP follows the real-time check-in event and is stored immediately.
    // Refresh only after that durable record arrives so the dashboard gains the
    // completed activity without requiring a separate Sync Now action.
    if (update.LiveTripStored) {
      Dashboard.Refresh(ActiveProfile.ProfileId);
    }
    
    if (update.TransferTotal is not null || update.TransferredTripCount is not null) {
      SyncProgressText = $"Syncing {update.TransferredTripCount ?? 0} of {update.TransferTotal ?? 0} trips...";
    }

    if (update.Status == SyncStatus.Complete) {
      IsSyncing = false;
      LastSyncTimeText = $"Today, {DateTime.Now:h:mm tt} (just now)";
      Dashboard.Refresh(ActiveProfile.ProfileId);
    }
    
    if (update.OutboundCommands.Count > 0) {
      // Send ACKs and any other outbound commands
      _ = Task.Run(async () => {
        foreach (var command in update.OutboundCommands) {
          try { await connection.SendAsync(command); } catch { }
        }
      });
    }
  }

  void HandleConnectionLost(object? sender, string detail) {
    IsConnected = false;
    IsSyncing = false;
    ActivePass.SetUnknown();
    Dashboard.ClearLiveCheckouts();
  }

  public void Dispose() {
    timer?.Dispose();
    connection.TextReceived -= HandleTextReceived;
    connection.ConnectionLost -= HandleConnectionLost;
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
