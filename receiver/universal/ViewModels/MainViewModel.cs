using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable {
  readonly ITerminalConnection connection;
  readonly TripSqliteRepository tripRepository;
  readonly RosterSqliteRepository rosterRepository;
  readonly ProfileAndPolicySqliteRepository profileRepository;
  readonly RosterService rosterService;
  readonly SyncSession syncSession;

  string activeModal = "None";
  bool isConnected;
  bool isSyncing;
  string lastSyncTimeText = "Never synced";
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
    activeProfile = profileRepository.GetActiveProfile() ?? Profiles.FirstOrDefault() ?? new ClassroomProfile("default", "Room 204");

    // Initialize child viewmodels
    ActivePass = new ActivePassViewModel();
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
      }
    }
  }

  public string ConnectionStatusText =>
    IsSyncing ? "Syncing…" : IsConnected ? "Connected" : "Disconnected";

  public string ConnectionStatusColor =>
    IsSyncing ? "#0284C7" : IsConnected ? "#10B981" : "#94A3B8";

  public string ConnectionBadgeBackground =>
    IsConnected ? "#DCFCE7" : "#F1F5F9";

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

  public void SwitchProfile(ClassroomProfile profile) {
    ActiveProfile = profile;
  }

  public async Task ConnectAndSyncAsync() {
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
    try {
      syncSession.Start();
      await connection.SendAsync("HELLO,1");
      await connection.SendAsync(ActivePassProtocol.BuildGetActivePassCommand());
      await connection.SendAsync(KioskSettingsProtocol.QueryCommand + "\n");

      var now = DateTime.Now;
      var lastTripId = tripRepository.GetRecentTrips(1).FirstOrDefault()?.TripId ?? 0;
      var command = $"TIME_CURSOR,{now:yyyy-MM-dd},{now:HH:mm:ss},{lastTripId}\n";
      await connection.SendAsync(command);

      LastSyncTimeText = $"Today, {DateTime.Now:hh:mm tt}";
    } catch {
      IsConnected = false;
    } finally {
      IsSyncing = false;
      Dashboard.Refresh(ActiveProfile.ProfileId);
    }
  }

  public async Task DisconnectAsync() {
    await connection.DisconnectAsync();
    IsConnected = false;
    ActivePass.SetAvailable();
  }

  public void RefreshActiveProfileData() {
    Dashboard.Refresh(ActiveProfile.ProfileId);
  }

  void HandleTextReceived(object? sender, string text) {
    var update = syncSession.ProcessReceivedData(text);

    if (update.ActivePass != null) {
      if (update.ActivePass.Status == ActivePassStatus.Occupied && !string.IsNullOrEmpty(update.ActivePass.StudentId)) {
        var student = rosterService.LookupStudent(ActiveProfile.ProfileId, update.ActivePass.StudentId);
        ActivePass.SetOccupied(update.ActivePass.StudentId, student?.FullName, update.ActivePass.CheckedOutAt);
      } else {
        ActivePass.SetAvailable();
      }
    }

    if (update.LiveEvent != null) {
      if (update.LiveEvent.EventType == LivePassEventType.Checkout) {
        var student = rosterService.LookupStudent(ActiveProfile.ProfileId, update.LiveEvent.StudentId);
        ActivePass.SetOccupied(update.LiveEvent.StudentId, student?.FullName, update.LiveEvent.CheckoutTime);
      } else if (update.LiveEvent.EventType is LivePassEventType.Checkin or LivePassEventType.Reset) {
        ActivePass.SetAvailable();
        Dashboard.Refresh(ActiveProfile.ProfileId);
      }
    }

    if (update.Status == SyncStatus.Complete) {
      Dashboard.Refresh(ActiveProfile.ProfileId);
    }
  }

  void HandleConnectionLost(object? sender, string detail) {
    IsConnected = false;
    IsSyncing = false;
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
