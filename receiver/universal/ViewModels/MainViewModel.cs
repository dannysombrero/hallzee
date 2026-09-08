using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
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
  readonly TerminalSession? terminalSession;
  readonly ITerminalCredentialStore? ownerCredentialStore;
  readonly TerminalOperationCoordinator operationCoordinator = new();
  readonly PolicyScheduleService scheduleService = new();
  TaskCompletionSource<bool>? syncCompletion;

  string activeView = "dashboard";
  string activeModal = "None";
  bool isConnected = false;
  readonly bool isPreviewMode;
  bool isSyncing;
  string lastSyncTimeText = "Never (No sync yet)";
  string connectedTerminalName = "";
  string profileDetails = "Period 3 (9:15–10:05)";
  string profileTeacher = "Teacher: Dr. Aris Thorne";
  string loadedRosterFileName = "Chemistry_Period3_Students.csv";
  string syncProgressText = "";
  ClassroomProfile activeProfile;
  string exportFolder;
  Timer? timer;
  TerminalDevice? lastAuthenticatedDevice;
  string? lastAuthenticatedTerminalId;
  CancellationTokenSource? reconnectCancellation;
  bool reconnectInProgress;
  bool intentionalDisconnect;
  DateTime nextBackgroundSyncAt = DateTime.Now.AddMinutes(5);

  public MainViewModel(
    ITerminalConnection connection,
    string? appDataPath = null,
    bool isPreviewMode = true,
    ITerminalCredentialStore? credentialStore = null
  ) {
    this.connection = connection;
    this.isPreviewMode = isPreviewMode;
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
    if (!isPreviewMode) {
      ownerCredentialStore = credentialStore ?? new InMemoryTerminalCredentialStore();
      terminalSession = new TerminalSession(
        connection,
        ownerCredentialStore,
        profileRepository.GetOrCreateClientId()
      );
    }

    // Load active profile
    var allProfiles = profileRepository.GetAllProfiles();
    foreach (var p in allProfiles) Profiles.Add(p);
    activeProfile = profileRepository.GetActiveProfile() ?? Profiles.FirstOrDefault() ?? new ClassroomProfile("default", "Room 204 • Chemistry AP");
    RestoreReconnectTarget();

    // Initialize child viewmodels
    ActivePass = new ActivePassViewModel();
    ActivePass.SetUnknown();

    Dashboard = new DashboardViewModel(tripRepository, rosterService, ActivePass);
    TripsModal = new TripsViewModel(tripRepository, rosterService);
    RosterModal = new RosterViewModel(rosterService);
    PolicyModal = new PolicyViewModel(profileRepository);
    PolicyModal.PropertyChanged += (s, e) => {
      if (e.PropertyName == nameof(PolicyModal.DurationWarningMinutes)) {
        Dashboard.ThresholdMinutes = PolicyModal.DurationWarningMinutes;
      }
      if (e.PropertyName == nameof(PolicyModal.MaxDailyPasses)) Dashboard.MaxDailyPasses = PolicyModal.MaxDailyPasses;
    };
    TerminalSettingsModal = new TerminalSettingsViewModel(
      profileRepository,
      SendProtocolAsync,
      appData,
      HandleClassroomInfoChanged);
    if (!string.IsNullOrWhiteSpace(LastPairedDeviceName)) {
      connectedTerminalName = LastPairedDeviceName;
      TerminalSettingsModal.TerminalName = LastPairedDeviceName;
    }
    FindTerminalsModal = new FindTerminalsViewModel(connection);
    ManualCheckInModal = new ManualCheckInViewModel(rosterService);

    // Connection events
    connection.TextReceived += HandleTextReceived;
    connection.ConnectionLost += HandleConnectionLost;

    // Refresh data
    RefreshActiveProfileData();

    // A claimed terminal has already completed the physical pairing flow. Try
    // its remembered transport as soon as the desktop app starts so the user
    // does not have to open the date/time or pairing screens first.
    if (!isPreviewMode) StartAutomaticReconnect();

    ActivePass.PropertyChanged += HandleActivePassPropertyChanged;
    Dashboard.AdditionalActiveTrips.CollectionChanged += HandleAdditionalActiveTripsChanged;
    PolicyModal.Periods.CollectionChanged += (_, _) => UpdatePeriodWindow();
    UpdatePeriodWindow();

    // Start a UI-thread ticker so both the dashboard and the companion window
    // keep their pass timers and bell-window countdowns in sync.
    timer = new Timer(_ => Dispatcher.UIThread.Post(() => {
      ActivePass.Tick();
      Dashboard.TickLiveActivePasses();
      UpdatePeriodWindow();
      if (IsConnected && !IsSyncing && DateTime.Now >= nextBackgroundSyncAt) {
        nextBackgroundSyncAt = DateTime.Now.AddMinutes(5);
        _ = SyncNowAsync();
      }
    }), null, 1000, 1000);
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
  public ManualCheckInViewModel ManualCheckInModal { get; }

  public bool HasStudentsOut => ActivePass.IsOccupied || Dashboard.AdditionalActiveTrips.Count > 0;

  public string CurrentPeriodName { get; private set; } = "No current period";
  public string CurrentPeriodRange { get; private set; } = "Set bell times in Policies & Bell Times";
  public string FormattedPeriodRange { get; private set; } = "";
  public string CurrentTimeDisplay { get; private set; } = DateTime.Now.ToString("h:mm  tt");
  public string PeriodWindowTitle { get; private set; } = "Bell schedule needed";
  public string PeriodWindowDetail { get; private set; } = "Add the current class period to show first and last ten-minute windows.";
  public string PeriodWindowAccent { get; private set; } = "#64748B";
  public double CurrentPeriodProgress { get; private set; }
  public string PopupPillText { get; private set; } = "WINDOW CLOSED";
  public string PopupPillBackground { get; private set; } = "#F43F5E";
  public string PopupPillForeground { get; private set; } = "#FFFFFF";
  public string PopupPillToolTip { get; private set; } = "Passes closed";
  public string PopupStatusPrefix { get; private set; } = "Bathroom Window Closed · Period ends in: ";
  public string PopupStatusTimer { get; private set; } = "00:00";

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
        OnPropertyChanged(nameof(IsManualCheckInModalVisible));
        OnPropertyChanged(nameof(IsReconnectPromptVisible));
      }
    }
  }

  public bool IsModalOpen => ActiveModal != "None";
  public bool IsTripsModalVisible => ActiveModal == "Trips";
  public bool IsRosterModalVisible => ActiveModal == "Roster";
  public bool IsPoliciesModalVisible => ActiveModal == "Policies";
  public bool IsTerminalSettingsModalVisible => ActiveModal == "TerminalSettings";
  public bool IsFindTerminalsModalVisible => ActiveModal == "FindTerminals";
  public bool IsManualCheckInModalVisible => ActiveModal == "ManualCheckIn";

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
        OnPropertyChanged(nameof(TerminalFriendlyNameDisplay));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(TerminalTransportInfoDisplay));
        OnPropertyChanged(nameof(IsReconnectPromptVisible));
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
  
  string checkInError = "";
  public string CheckInError {
    get => checkInError;
    private set { checkInError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCheckInError)); }
  }
  public bool HasCheckInError => !string.IsNullOrEmpty(CheckInError);

  public string SyncProgressText {
    get => syncProgressText;
    private set { syncProgressText = value; OnPropertyChanged(); }
  }

  public string? LastPairedDeviceName {
    get {
      if (lastAuthenticatedDevice != null && !string.IsNullOrWhiteSpace(lastAuthenticatedDevice.Name)) {
        return lastAuthenticatedDevice.Name;
      }
      var terminalId = profileRepository.GetAssignedTerminalId(ActiveProfile.ProfileId);
      if (!string.IsNullOrWhiteSpace(terminalId) && !terminalId.StartsWith("LEGACY", StringComparison.OrdinalIgnoreCase)) {
        var saved = profileRepository.GetTerminal(terminalId);
        if (saved != null && !string.IsNullOrWhiteSpace(saved.CustomName)) {
          return saved.CustomName;
        }
      }
      var all = profileRepository.GetAllTerminals()
        .Where(t => !t.TerminalId.StartsWith("LEGACY", StringComparison.OrdinalIgnoreCase));
      var latest = all.OrderByDescending(t => t.LastSeenAt).FirstOrDefault();
      if (latest != null && !string.IsNullOrWhiteSpace(latest.CustomName)) {
        return latest.CustomName;
      }
      return null;
    }
  }

  public string CurrentTerminalId => lastAuthenticatedTerminalId ?? "preview-hallzee";

  public string WindowTitle => $"Hallzee Desktop Client · {ConnectedTerminalName}";

  public string ConnectedTerminalName {
    get => isConnected ? connectedTerminalName : (!string.IsNullOrWhiteSpace(LastPairedDeviceName) ? LastPairedDeviceName : "No Terminal Connected");
    set {
      connectedTerminalName = value ?? "";
      if (TerminalSettingsModal != null && !string.IsNullOrWhiteSpace(value)) {
        TerminalSettingsModal.TerminalName = value;
      }
      OnPropertyChanged();
      OnPropertyChanged(nameof(TerminalFriendlyNameDisplay));
      OnPropertyChanged(nameof(WindowTitle));
    }
  }

  public string TerminalFriendlyNameDisplay {
    get {
      if (IsConnected) {
        return !string.IsNullOrWhiteSpace(connectedTerminalName) && connectedTerminalName != "No Terminal Connected"
          ? connectedTerminalName
          : (!string.IsNullOrWhiteSpace(LastPairedDeviceName) ? LastPairedDeviceName : "Connected Device");
      }
      if (!string.IsNullOrWhiteSpace(LastPairedDeviceName)) {
        return $"Last paired: {LastPairedDeviceName}";
      }
      return "No Device Paired";
    }
  }

  public string TerminalTransportInfoDisplay =>
    IsConnected
      ? "BLE 4.2+ GATT • Active sync"
      : (!string.IsNullOrWhiteSpace(LastPairedDeviceName) ? "Standby • Ready to reconnect" : "Standby • Ready to discover");

  public string ProfileDetails {
    get {
      var school = TerminalSettingsModal?.School?.Trim();
      return string.IsNullOrWhiteSpace(school)
        ? profileDetails
        : $"{profileDetails} • {school}";
    }
  }

  public string ProfileTeacher {
    get {
      var teacher = TerminalSettingsModal?.TeacherName?.Trim();
      return string.IsNullOrWhiteSpace(teacher)
        ? profileTeacher
        : $"Teacher: {teacher}";
    }
  }
  public string LoadedRosterFileName => loadedRosterFileName;

  public string ConnectionStatusText =>
    IsReconnecting ? "RECONNECTING" : IsSyncing ? "SYNCING" : IsConnected ? "CONNECTED" : "OFFLINE";

  public string ConnectionStatusColor =>
    IsReconnecting ? "#F59E0B" : IsSyncing ? "#0284C7" : IsConnected ? "#10B981" : "#94A3B8";

  public string ConnectionBadgeBackground =>
    IsReconnecting ? "#F59E0B" : IsSyncing ? "#0284C7" : IsConnected ? "#10B981" : "#94A3B8";

  public string ConnectionBadgeForeground => "White";

  public string TopStatusBadgeText =>
    IsReconnecting ? "RECONNECTING" : !IsConnected ? "OFFLINE" : IsSyncing ? "SYNCING" : "CONNECTED";

  public string TopStatusBadgeBackground =>
    IsReconnecting ? "#F59E0B" : !IsConnected ? "#94A3B8" : IsSyncing ? "#0284C7" : "#10B981";

  public string TopStatusBadgeForeground => "White";

  public string TerminalAvatarBackground =>
    IsReconnecting ? "#F59E0B" : !IsConnected ? "#94A3B8" : "#10B981";

  public bool IsReconnecting => reconnectInProgress;

  public bool HasReconnectCandidate => lastAuthenticatedDevice != null && lastAuthenticatedTerminalId != null;

  public string ReconnectCandidateName => lastAuthenticatedDevice?.Name ?? "";

  public bool IsReconnectPromptVisible => HasReconnectCandidate && !IsConnected && ActiveModal == "None";

  public string ReconnectPromptTitle => IsReconnecting
    ? "Attempting to reconnect to last known device…"
    : "Previously connected terminal found";

  public string ReconnectPromptDetail => IsReconnecting
    ? FindTerminalsModal.StatusText
    : "Reconnect without entering a Bluetooth pairing key.";

  public async Task ReconnectCandidateAsync() {
    if (!HasReconnectCandidate) return;
    await ReconnectLastTerminalAsync(lastAuthenticatedDevice!, lastAuthenticatedTerminalId!, CancellationToken.None);
    OnPropertyChanged(nameof(IsReconnectPromptVisible));
  }

  public void DismissReconnectPrompt() {
    lastAuthenticatedDevice = null;
    lastAuthenticatedTerminalId = null;
    CancelAutomaticReconnect();
    OnPropertyChanged(nameof(HasReconnectCandidate));
    OnPropertyChanged(nameof(ReconnectCandidateName));
    OnPropertyChanged(nameof(IsReconnectPromptVisible));
  }

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
        OnPropertyChanged(nameof(HeaderLocationText));
        Dashboard.ClearLiveCheckouts();
        RefreshActiveProfileData();
        UpdatePeriodWindow();
      }
    }
  }

  public string HeaderLocationText {
    get {
      var parts = new List<string>(3);
      if (!string.IsNullOrWhiteSpace(TerminalSettingsModal?.Room)) {
        var r = TerminalSettingsModal.Room.Trim();
        if (!r.StartsWith("Room", StringComparison.OrdinalIgnoreCase)) {
          r = "Room " + r;
        }
        parts.Add(r);
      }
      if (!string.IsNullOrWhiteSpace(TerminalSettingsModal?.TeacherName)) {
        parts.Add(TerminalSettingsModal.TeacherName.Trim());
      }
      if (!string.IsNullOrWhiteSpace(TerminalSettingsModal?.School)) {
        parts.Add(TerminalSettingsModal.School.Trim());
      }

      if (parts.Count > 0) {
        return string.Join(" - ", parts);
      }

      return ActiveProfile?.Name ?? "Default Classroom";
    }
  }

  void HandleClassroomInfoChanged() {
    // The location in the title bar and the classroom-profile card both read
    // these values. Refresh all affected bindings as soon as Settings saves.
    OnPropertyChanged(nameof(HeaderLocationText));
    OnPropertyChanged(nameof(ProfileDetails));
    OnPropertyChanged(nameof(ProfileTeacher));
  }

  public void OpenModal(string modalName) {
    ActiveModal = modalName;
    if (modalName == "Trips") TripsModal.Refresh(ActiveProfile.ProfileId);
    else if (modalName == "Roster") RosterModal.Refresh(ActiveProfile.ProfileId);
    else if (modalName == "Policies") PolicyModal.Refresh(ActiveProfile.ProfileId);
    else if (modalName == "FindTerminals") _ = FindTerminalsModal.ScanAsync();
    else if (modalName == "ManualCheckIn") ManualCheckInModal.Reset(ActiveProfile.ProfileId);
  }

  public void CloseModal() {
    ActiveModal = "None";
    RosterModal.CancelImport();
    ManualCheckInModal.Reset(ActiveProfile.ProfileId);
    Dashboard.Refresh(ActiveProfile.ProfileId);
  }

  public void SubmitManualCheckIn() {
    if (!ManualCheckInModal.CanSubmit) {
      ManualCheckInModal.StatusMessage = "Enter a student name or student ID to continue.";
      return;
    }
    var (id, name, period, destination, purpose) = ManualCheckInModal.ResolvePassDetails();
    ActivePass.SetOccupied(id, name, DateTime.Now, period, destination, purpose, isManual: true);
    Dashboard.RegisterLiveCheckout(id, name, DateTime.Now);
    Dashboard.RefreshAdditionalActiveTrips();
    Dashboard.Refresh(ActiveProfile.ProfileId);
    CloseModal();
  }

  public void ExportTrips(string exportPath, bool openModal = true) {
    TripsModal.ExportCsv(ActiveProfile.ProfileId, exportPath);
    if (openModal) OpenModal("Trips");
  }

  public string ExportWorkspace() {
    PolicyModal.Save(ActiveProfile.ProfileId);
    return WorkspaceTransfer.Export(ActiveProfile, profileRepository);
  }

  public void ImportWorkspace(string json) {
    var profile = profileRepository.ImportWorkspace(WorkspaceTransfer.Parse(json));
    Profiles.Add(profile);
    ActiveProfile = profile;
    PolicyModal.StatusMessage = "Workspace imported. Rules and bell schedules are ready.";
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
    PolicyModal.IsCreatingProfile = false;
  }

  public async Task ConnectAndSyncAsync() {
    var device = FindTerminalsModal.SelectedDevice;
    if (device == null) return;
    CancelAutomaticReconnect();

    if (terminalSession == null) {
      lastAuthenticatedDevice = device;
      ConnectedTerminalName = device.Name;
      if (TerminalSettingsModal != null) {
        TerminalSettingsModal.TerminalName = device.Name;
      }
      var connected = await FindTerminalsModal.ConnectAsync();
      if (connected) {
        IsConnected = true;
        CloseModal();
        await SyncNowAsync();
      }
      return;
    }

    var pairingPromptHint = string.IsNullOrWhiteSpace(FindTerminalsModal.PairingPasskey)
      ? ""
      : OperatingSystem.IsWindows()
        ? " Windows pairing will use the six digits entered above."
        : " If the operating system asks for a Bluetooth passkey, enter the same six digits shown on the kiosk.";
    FindTerminalsModal.SetStatus($"Connecting to {device.Name} and verifying identity…{pairingPromptHint}");
    var pairingPasskeySink = connection as ITerminalPairingPasskeySink;
    pairingPasskeySink?.SetPairingPasskey(FindTerminalsModal.PairingPasskey);
    try {
      var identity = await terminalSession.OpenAsync(device);
      if (!identity.IsClaimed && string.IsNullOrWhiteSpace(FindTerminalsModal.PairingPasskey)) {
        FindTerminalsModal.SetStatus(
          $"Unclaimed terminal {identity.TerminalId}. Hold * and # on the kiosk for five seconds, then enter its six-digit passkey above."
        );
        return;
      }

      var authenticated = await terminalSession.AuthenticateAsync(
        identity.IsClaimed ? null : FindTerminalsModal.PairingPasskey
      );
      profileRepository.SaveTerminal(new TerminalDeviceConfig(
        authenticated.TerminalId,
        authenticated.CustomName,
        device.Id,
        DateTime.UtcNow,
        ProtocolVersion: TerminalIdentityProtocol.ProtocolVersion,
        ClaimStatus: "CLAIMED",
        TransportId: device.Id
      ));
      profileRepository.AssignTerminalToProfile(ActiveProfile.ProfileId, authenticated.TerminalId);
      ConnectedTerminalName = authenticated.CustomName;
      lastAuthenticatedDevice = device;
      lastAuthenticatedTerminalId = authenticated.TerminalId;
      FindTerminalsModal.PairingPasskey = "";
      IsConnected = true;
      CloseModal();
      await SyncNowAsync();
    } catch (TerminalInUseException exception) {
      FindTerminalsModal.SetStatus($"{exception.Message} Scan Again to refresh its status.");
      IsConnected = false;
    } catch (TerminalBondRepairRequiredException exception) {
      FindTerminalsModal.SetStatus($"{exception.Message} Ownership was retained; repair Bluetooth and reconnect.");
      IsConnected = false;
    } catch (Exception exception) {
      FindTerminalsModal.SetStatus($"Secure connection failed: {exception.Message}");
      IsConnected = false;
    } finally {
      pairingPasskeySink?.SetPairingPasskey(null);
    }
  }

  public Task SyncNowAsync() => operationCoordinator.RunAsync(SyncNowCoreAsync);

  async Task SyncNowCoreAsync() {
    if (!IsConnected) {
      OpenModal("FindTerminals");
      return;
    }

    IsSyncing = true;
    SyncProgressText = "Preparing sync...";
    try {
      var authenticatedTerminalId = terminalSession?.AuthenticatedTerminal?.TerminalId;
      if (terminalSession != null && authenticatedTerminalId == null) {
        throw new InvalidOperationException("The terminal session is not authenticated.");
      }
      if (authenticatedTerminalId == null) {
        syncSession.Start();
        await connection.SendAsync("HELLO,1");
      } else {
        syncSession.Start(authenticatedTerminalId);
      }
      syncCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      await SendProtocolAsync(ActivePassProtocol.BuildGetActivePassesCommand());
      await SendProtocolAsync(terminalSession == null
        ? KioskSettingsProtocol.QueryCommand + "\n"
        : KioskSettingsProtocol.QueryCommand);
      await SendProtocolAsync($"SET,MAX_ACTIVE_PASSES,{PolicyModal.MaxSimultaneousPasses}");
      await SendBellPolicyCoreAsync();

      // The kiosk stores its classroom wall-clock time as a UTC-shaped epoch.
      // Send the Mac's local time so the terminal display and its local records
      // match the classroom clock.
      var now = DateTime.Now;
      var lastTripId = authenticatedTerminalId == null
        ? tripRepository.GetRecentTrips(1).FirstOrDefault()?.TripId ?? 0
        : tripRepository.GetRecentTrips(authenticatedTerminalId).FirstOrDefault()?.TripId ?? 0;
      var command = $"TIME_CURSOR,{now:yyyy-MM-dd},{now:HH:mm:ss},{lastTripId}\n";
      await SendProtocolAsync(command);
      var completed = await Task.WhenAny(syncCompletion.Task, Task.Delay(TimeSpan.FromSeconds(20)));
      if (completed != syncCompletion.Task) throw new TimeoutException("The terminal did not finish synchronization.");
    } catch {
      IsConnected = false;
      LastSyncTimeText = "Sync failed (Terminal offline)";
      IsSyncing = false;
    } finally {
      syncCompletion = null;
    }
  }

  public async Task DisconnectAsync() {
    intentionalDisconnect = true;
    CancelAutomaticReconnect();
    try {
      if (terminalSession != null) await terminalSession.DisconnectAsync();
      else await connection.DisconnectAsync();
      IsConnected = false;
      ActivePass.SetUnknown();
      Dashboard.ClearLiveCheckouts();
      Dashboard.Refresh(ActiveProfile.ProfileId);
    } finally {
      intentionalDisconnect = false;
    }
  }

  public async Task CheckInActivePassAsync() {
    CheckInError = "";
    if (ActivePass.IsOccupied && !string.IsNullOrEmpty(ActivePass.StudentId)) {
      var studentId = ActivePass.StudentId;
      if (!ActivePass.IsManual) {
        if (!IsConnected) {
          CheckInError = "Reconnect to the terminal to check this student in.";
          return;
        }
        try { await CheckInStudentAsync(studentId); }
        catch (Exception ex) { CheckInError = $"Check-in failed: {ex.Message}"; }
        // The terminal's LIVE_TRIP/sync supplies the durable record and its ID.
        return;
      }
      var checkoutTime = ActivePass.CheckoutTime ?? DateTime.Now;
      var checkinTime = DateTime.Now;
      var duration = (int)Math.Max(0, (checkinTime - checkoutTime).TotalSeconds);
      var date = checkoutTime.ToString("yyyy-MM-dd");
      var timeOut = checkoutTime.ToString("HH:mm:ss");
      var timeIn = checkinTime.ToString("HH:mm:ss");

      var tripId = tripRepository.GetLatestTripId() + 1;
      const string status = "MANUAL";
      var payload = $"{tripId},{studentId},{date},{timeOut},{timeIn},{duration},{status}";
      if (tripRepository.StoreManual("DESKTOP", payload, ActivePass.StudentName) != TripStoreResult.Saved) {
        CheckInError = "Could not save the check-in. Try again.";
        return;
      }
      AssignTripContext("DESKTOP", tripId, date, timeOut);

      ActivePass.SetAvailable();
      Dashboard.ResolveLiveCheckout(studentId);
      Dashboard.RefreshAdditionalActiveTrips();
      Dashboard.Refresh(ActiveProfile.ProfileId);
    }
  }

  public async Task CheckInStudentAsync(string studentId) {
    if (string.IsNullOrWhiteSpace(studentId)) return;
    if (connection is PreviewTerminalConnection preview) {
      preview.SimulateCheckin(studentId);
      } else {
        await operationCoordinator.RunAsync(() => SendProtocolAsync($"MANUAL_CHECKIN,{studentId}"));
    }
  }

  public Task ApplyPolicyCapacityAsync() => ApplyPolicySettingsAsync();

  public Task ApplyPolicySettingsAsync() => operationCoordinator.RunAsync(async () => {
    if (!IsConnected) return;
    await SendProtocolAsync($"SET,MAX_ACTIVE_PASSES,{PolicyModal.MaxSimultaneousPasses}");
    await SendBellPolicyCoreAsync();
  });

  async Task SendBellPolicyCoreAsync() {
    var rule = profileRepository.GetPolicyRule(ActiveProfile.ProfileId);
    foreach (var command in BellPolicyProtocol.BuildTransfer(
      DateTime.Today,
      rule,
      profileRepository.GetBellSchedule(ActiveProfile.ProfileId),
      profileRepository.GetScheduleExceptions(ActiveProfile.ProfileId))) {
      await SendProtocolAsync(command);
    }
  }

  public async Task ApplyTerminalSettingsAsync() {
    if (!IsConnected) {
      TerminalSettingsModal.SetFailure("Connect to the kiosk before applying device settings.");
      return;
    }
    await operationCoordinator.RunAsync(async () => {
      if (await TerminalSettingsModal.ApplySettingsAsync(CurrentTerminalId)) {
        ConnectedTerminalName = TerminalSettingsModal.TerminalName;
        if (lastAuthenticatedDevice != null) {
          lastAuthenticatedDevice = lastAuthenticatedDevice with { Name = ConnectedTerminalName };
        }
      }
    });
  }

  public void RefreshActiveProfileData() {
    PolicyModal.Refresh(ActiveProfile.ProfileId);
    RosterModal.Refresh(ActiveProfile.ProfileId);
    Dashboard.ThresholdMinutes = PolicyModal.DurationWarningMinutes;
    Dashboard.MaxDailyPasses = PolicyModal.MaxDailyPasses;
    Dashboard.Refresh(ActiveProfile.ProfileId);
    var lastSync = profileRepository.GetLastSuccessfulSync(ActiveProfile.ProfileId);
    LastSyncTimeText = lastSync.HasValue ? lastSync.Value.ToLocalTime().ToString("MMM d, h:mm tt") : "Never (No sync yet)";
    UpdatePeriodWindow();
  }

  void HandleActivePassPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) {
    if (eventArgs.PropertyName is nameof(ActivePassViewModel.IsOccupied) or nameof(ActivePassViewModel.IsStatusUnknown) or nameof(ActivePassViewModel.DurationDisplay) or nameof(ActivePassViewModel.DisplayName)) {
      OnPropertyChanged(nameof(HasStudentsOut));
      UpdatePeriodWindow();
    }
  }

  void HandleAdditionalActiveTripsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) {
    OnPropertyChanged(nameof(HasStudentsOut));
    UpdatePeriodWindow();
  }

  public void UpdatePeriodWindow() {
    var now = DateTime.Now;
    CurrentTimeDisplay = now.ToString("h:mm  tt");
    OnPropertyChanged(nameof(CurrentTimeDisplay));

    var resolved = scheduleService.ResolvePeriod(
      now,
      PolicyModal.Periods.Select(item => item.ToModel()),
      PolicyModal.Exceptions.Select(item => item.ToModel()));
    var current = resolved == null ? null : PolicyModal.Periods.FirstOrDefault(item => item.ScheduleId == resolved.Period.ScheduleId);

    if (current is null || resolved is null) {
      SetPeriodWindow(
        "No current period",
        "Set bell times in Policies & Bell Times",
        "Bell schedule needed",
        "Add the current class period to show first and last ten-minute windows.",
        "#64748B",
        0);
      FormattedPeriodRange = "";

      if (HasStudentsOut) {
        PopupPillText = "PASS IN USE";
        PopupPillBackground = "#F59E0B";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = "A student is currently out";
        PopupStatusPrefix = "Pass In Use · ";
        PopupStatusTimer = ActivePass.DurationDisplay;
      } else {
        PopupPillText = "WINDOW CLOSED";
        PopupPillBackground = "#64748B";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = "No class period is currently in session";
        PopupStatusPrefix = "No Active Period · Schedule needed";
        PopupStatusTimer = "";
      }
      NotifyPopupProperties();
      return;
    }

    var periodStart = resolved.StartsAt;
    var periodEnd = resolved.EndsAt;
    var totalSeconds = Math.Max(1, (periodEnd - periodStart).TotalSeconds);
    var elapsedSeconds = Math.Clamp((now - periodStart).TotalSeconds, 0, totalSeconds);
    var remaining = periodEnd - now;
    var firstMinutes = Math.Max(0, PolicyModal.FirstWindowMinutes);
    var lastMinutes = Math.Max(0, PolicyModal.LastWindowMinutes);
    var firstWindowEnds = periodStart.AddMinutes(firstMinutes);
    var lastWindowStarts = periodEnd.AddMinutes(-lastMinutes);
    var range = $"{periodStart:h:mm tt} – {periodEnd:h:mm tt}";
    FormattedPeriodRange = FormatPeriodRange(periodStart, periodEnd);

    if (now < firstWindowEnds) {
      SetPeriodWindow(
        current.DisplayTitle,
        range,
        $"First {firstMinutes} minutes · {PolicyModal.FirstWindowAction}",
        $"Window active · {FormatCountdown(firstWindowEnds - now)} remaining",
        "#F59E0B",
        elapsedSeconds / totalSeconds * 100);

      if (HasStudentsOut) {
        PopupPillText = "PASS IN USE";
        PopupPillBackground = "#F59E0B";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = "A student is currently out";
        PopupStatusPrefix = "Pass In Use · Passes open in: ";
        PopupStatusTimer = FormatCountdown(firstWindowEnds - now);
      } else {
        var locked = string.Equals(PolicyModal.FirstWindowAction, "Lock", StringComparison.OrdinalIgnoreCase);
        var warned = string.Equals(PolicyModal.FirstWindowAction, "Warn", StringComparison.OrdinalIgnoreCase);
        PopupPillText = locked ? "WINDOW CLOSED" : "WINDOW OPEN";
        PopupPillBackground = locked ? "#F43F5E" : "#059669";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = locked ? "Passes are locked during the beginning-of-class window" : warned ? "Passes show a warning during this window" : "Passes are allowed during this window";
        PopupStatusPrefix = locked ? "Bathroom Window Closed · Passes open in: " : warned ? "Bell Window Warning · Window ends in: " : "Bathroom Window Open · Window ends in: ";
        PopupStatusTimer = FormatCountdown(firstWindowEnds - now);
      }
    } else if (now >= lastWindowStarts) {
      SetPeriodWindow(
        current.DisplayTitle,
        range,
        $"Last {lastMinutes} minutes · {PolicyModal.LastWindowAction}",
        $"Window active · period ends in {FormatCountdown(remaining)}",
        "#E11D48",
        elapsedSeconds / totalSeconds * 100);

      if (HasStudentsOut) {
        PopupPillText = "PASS IN USE";
        PopupPillBackground = "#F59E0B";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = "A student is currently out";
        PopupStatusPrefix = "Pass In Use · Period ends in: ";
        PopupStatusTimer = FormatCountdown(remaining);
      } else {
        var locked = string.Equals(PolicyModal.LastWindowAction, "Lock", StringComparison.OrdinalIgnoreCase);
        var warned = string.Equals(PolicyModal.LastWindowAction, "Warn", StringComparison.OrdinalIgnoreCase);
        PopupPillText = locked ? "WINDOW CLOSED" : "WINDOW OPEN";
        PopupPillBackground = locked ? "#F43F5E" : "#059669";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = locked ? "Passes are locked during the end-of-class window" : warned ? "Passes show a warning during this window" : "Passes are allowed during this window";
        PopupStatusPrefix = locked ? "Bathroom Window Closed · Period ends in: " : warned ? "Bell Window Warning · Period ends in: " : "Bathroom Window Open · Period ends in: ";
        PopupStatusTimer = FormatCountdown(remaining);
      }
    } else {
      SetPeriodWindow(
        current.DisplayTitle,
        range,
        "Open pass window",
        $"Last {lastMinutes}-minute window begins in {FormatCountdown(lastWindowStarts - now)}",
        "#059669",
        elapsedSeconds / totalSeconds * 100);

      if (HasStudentsOut) {
        PopupPillText = "PASS IN USE";
        PopupPillBackground = "#F59E0B";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = "A student is currently out";
        PopupStatusPrefix = "Pass In Use · Window closes in: ";
        PopupStatusTimer = FormatCountdown(lastWindowStarts - now);
      } else {
        PopupPillText = "WINDOW OPEN";
        PopupPillBackground = "#059669";
        PopupPillForeground = "#FFFFFF";
        PopupPillToolTip = "Pass is currently available";
        PopupStatusPrefix = "Bathroom Window Open · Window closes in: ";
        PopupStatusTimer = FormatCountdown(lastWindowStarts - now);
      }
    }

    // Overlapping first/last windows use the same strictest action as the kiosk.
    var windowRule = new PolicyRule("popup", ActiveProfile.ProfileId,
      LockoutStartMinutes: firstMinutes, LockoutEndMinutes: lastMinutes,
      FirstWindowAction: PolicyModal.FirstWindowAction, LastWindowAction: PolicyModal.LastWindowAction);
    if (!HasStudentsOut && scheduleService.Evaluate(now, resolved, windowRule) == BellWindowDecision.Lock) {
      PopupPillText = "WINDOW CLOSED";
      PopupPillBackground = "#F43F5E";
      PopupPillToolTip = "Passes are locked during this bell window";
    }
    NotifyPopupProperties();
  }

  void AssignTripContext(string terminalId, long tripId, string tripDate, string timeOut) {
    if (!DateTime.TryParse($"{tripDate} {timeOut}", out var checkout)) return;
    var resolved = scheduleService.ResolvePeriod(
      checkout,
      profileRepository.GetBellSchedule(ActiveProfile.ProfileId),
      profileRepository.GetScheduleExceptions(ActiveProfile.ProfileId));
    tripRepository.AssignTripContext(
      terminalId,
      tripId,
      ActiveProfile.ProfileId,
      resolved?.ScheduleName,
      resolved?.ClassSection);
  }

  void NotifyPopupProperties() {
    OnPropertyChanged(nameof(FormattedPeriodRange));
    OnPropertyChanged(nameof(PopupPillText));
    OnPropertyChanged(nameof(PopupPillBackground));
    OnPropertyChanged(nameof(PopupPillForeground));
    OnPropertyChanged(nameof(PopupPillToolTip));
    OnPropertyChanged(nameof(PopupStatusPrefix));
    OnPropertyChanged(nameof(PopupStatusTimer));
  }

  static string FormatPeriodRange(DateTime start, DateTime end) {
    var range = start.ToString("tt") == end.ToString("tt")
      ? $"{start:h:mm} – {end:h:mm tt}"
      : $"{start:h:mm tt} – {end:h:mm tt}";
    return $"({range})";
  }

  bool IsScheduledOn(BellPeriodItemViewModel period, DayOfWeek day) => day switch {
    DayOfWeek.Monday => period.IsMonday,
    DayOfWeek.Tuesday => period.IsTuesday,
    DayOfWeek.Wednesday => period.IsWednesday,
    DayOfWeek.Thursday => period.IsThursday,
    DayOfWeek.Friday => period.IsFriday,
    DayOfWeek.Saturday or DayOfWeek.Sunday => isPreviewMode,
    _ => false
  };

  static bool TryGetPeriodBounds(
    BellPeriodItemViewModel period,
    DateTime date,
    out DateTime start,
    out DateTime end) {
    start = default;
    end = default;
    if (!DateTime.TryParse(period.StartTime, out var startTime) ||
        !DateTime.TryParse(period.EndTime, out var endTime)) return false;
    start = date.Add(startTime.TimeOfDay);
    end = date.Add(endTime.TimeOfDay);
    if (end <= start) end = end.AddDays(1);
    return true;
  }

  void SetPeriodWindow(
    string periodName,
    string range,
    string title,
    string detail,
    string accent,
    double progress) {
    CurrentPeriodName = periodName;
    CurrentPeriodRange = range;
    PeriodWindowTitle = title;
    PeriodWindowDetail = detail;
    PeriodWindowAccent = accent;
    CurrentPeriodProgress = progress;
    OnPropertyChanged(nameof(CurrentPeriodName));
    OnPropertyChanged(nameof(CurrentPeriodRange));
    OnPropertyChanged(nameof(PeriodWindowTitle));
    OnPropertyChanged(nameof(PeriodWindowDetail));
    OnPropertyChanged(nameof(PeriodWindowAccent));
    OnPropertyChanged(nameof(CurrentPeriodProgress));
  }

  static string FormatCountdown(TimeSpan duration) {
    if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
    return duration.TotalHours >= 1
      ? $"{(int)duration.TotalHours}h {duration.Minutes:D2}m"
      : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
  }

  void RestoreReconnectTarget() {
    if (terminalSession == null || ownerCredentialStore == null) return;
    var terminalId = profileRepository.GetAssignedTerminalId(ActiveProfile.ProfileId);
    if (string.IsNullOrWhiteSpace(terminalId) ||
        !ownerCredentialStore.TryGetOwnerKey(terminalId, out var ownerKey)) return;
    Array.Clear(ownerKey);

    var saved = profileRepository.GetTerminal(terminalId);
    var transportId = saved?.TransportId ?? saved?.BleAddress;
    if (string.IsNullOrWhiteSpace(transportId)) return;
    lastAuthenticatedTerminalId = terminalId;
    lastAuthenticatedDevice = new TerminalDevice(
      transportId,
      string.IsNullOrWhiteSpace(saved?.CustomName) ? $"Hallzee ({terminalId})" : saved!.CustomName,
      true,
      false);
    ConnectedTerminalName = lastAuthenticatedDevice.Name;
    OnPropertyChanged(nameof(HasReconnectCandidate));
    OnPropertyChanged(nameof(ReconnectCandidateName));
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

    if (update.ActivePasses != null) {
      Dashboard.ClearLiveCheckouts();
      foreach (var pass in update.ActivePasses) {
        var student = rosterService.LookupStudent(ActiveProfile.ProfileId, pass.StudentId!);
        Dashboard.RegisterLiveCheckout(pass.StudentId!, student?.FullName, pass.CheckedOutAt);
      }
      var oldest = update.ActivePasses.FirstOrDefault();
      if (oldest != null) {
        var student = rosterService.LookupStudent(ActiveProfile.ProfileId, oldest.StudentId!);
        ActivePass.SetOccupied(oldest.StudentId!, student?.FullName, oldest.CheckedOutAt);
      } else {
        ActivePass.SetAvailable();
      }
      Dashboard.RefreshAdditionalActiveTrips();
      Dashboard.Refresh(ActiveProfile.ProfileId);
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

    foreach (var storedTrip in update.StoredTrips) {
      AssignTripContext(storedTrip.TerminalId, storedTrip.TripId, storedTrip.TripDate, storedTrip.TimeOut);
    }

    // A LIVE_TRIP follows the real-time check-in event and is stored immediately.
    // Refresh after its schedule/class context is attached.
    if (update.LiveTripStored) Dashboard.Refresh(ActiveProfile.ProfileId);
    
    if (update.TransferTotal is not null || update.TransferredTripCount is not null) {
      SyncProgressText = $"Syncing {update.TransferredTripCount ?? 0} of {update.TransferTotal ?? 0} trips...";
    }

    if (update.Status == SyncStatus.Complete) {
      IsSyncing = false;
      var completedAt = DateTime.UtcNow;
      profileRepository.SaveLastSuccessfulSync(ActiveProfile.ProfileId, completedAt);
      LastSyncTimeText = $"Today, {completedAt.ToLocalTime():h:mm tt} (just now)";
      nextBackgroundSyncAt = DateTime.Now.AddMinutes(5);
      Dashboard.Refresh(ActiveProfile.ProfileId);
      syncCompletion?.TrySetResult(true);
    }
    
    if (update.OutboundCommands.Count > 0) {
      // Send ACKs and any other outbound commands
      _ = Task.Run(async () => {
        foreach (var command in update.OutboundCommands) {
          try { await SendProtocolAsync(command); } catch { }
        }
      });
    }
  }

  void HandleConnectionLost(object? sender, string detail) {
    IsConnected = false;
    IsSyncing = false;
    ActivePass.SetUnknown();
    Dashboard.ClearLiveCheckouts();
    if (!intentionalDisconnect && terminalSession != null &&
        lastAuthenticatedDevice != null && lastAuthenticatedTerminalId != null) {
      StartAutomaticReconnect();
    }
  }

  public void Dispose() {
    intentionalDisconnect = true;
    CancelAutomaticReconnect();
    timer?.Dispose();
    ActivePass.PropertyChanged -= HandleActivePassPropertyChanged;
    Dashboard.AdditionalActiveTrips.CollectionChanged -= HandleAdditionalActiveTripsChanged;
    connection.TextReceived -= HandleTextReceived;
    connection.ConnectionLost -= HandleConnectionLost;
    terminalSession?.Dispose();
    operationCoordinator.Dispose();
  }

  void StartAutomaticReconnect() {
    if (reconnectInProgress || reconnectCancellation != null ||
        lastAuthenticatedDevice == null || lastAuthenticatedTerminalId == null) return;

    reconnectCancellation = new CancellationTokenSource();
    OnPropertyChanged(nameof(IsReconnectPromptVisible));
    OnPropertyChanged(nameof(ReconnectPromptTitle));
    OnPropertyChanged(nameof(ReconnectPromptDetail));
    _ = ReconnectLastTerminalAsync(
      lastAuthenticatedDevice,
      lastAuthenticatedTerminalId,
      reconnectCancellation.Token);
  }

  async Task ReconnectLastTerminalAsync(
    TerminalDevice device,
    string terminalId,
    CancellationToken cancellationToken) {
    // A computer waking from sleep can report the link loss before its
    // Bluetooth stack is ready to accept a new CoreBluetooth/WinRT request.
    // Keep trying long enough for that subsystem to resume instead of making
    // the user press Reconnect after the original short countdown expires.
    const int reconnectWindowSeconds = 45;
    var reconnectDeadline = Stopwatch.GetTimestamp() +
      (long)(Stopwatch.Frequency * reconnectWindowSeconds);
    reconnectInProgress = true;
    _ = UpdateReconnectCountdownAsync(cancellationToken, device.Name, reconnectWindowSeconds);
    OnPropertyChanged(nameof(IsReconnecting));
    OnPropertyChanged(nameof(ReconnectPromptTitle));
    OnPropertyChanged(nameof(ConnectionStatusText));
    OnPropertyChanged(nameof(ConnectionStatusColor));
    OnPropertyChanged(nameof(ConnectionBadgeBackground));
    OnPropertyChanged(nameof(TopStatusBadgeText));
    OnPropertyChanged(nameof(TopStatusBadgeBackground));
    OnPropertyChanged(nameof(TerminalAvatarBackground));
    try {
      var attempt = 0;
      while (!cancellationToken.IsCancellationRequested &&
             Stopwatch.GetTimestamp() < reconnectDeadline) {
        attempt++;
        var remainingSeconds = Math.Max(1, (int)Math.Ceiling(
          (reconnectDeadline - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency));
        var delay = attempt == 1 ? TimeSpan.Zero : TimeSpan.FromSeconds(1);
        try {
          await Task.Delay(delay, cancellationToken);
          if (Stopwatch.GetTimestamp() >= reconnectDeadline) break;
          remainingSeconds = Math.Max(1, (int)Math.Ceiling(
            (reconnectDeadline - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency));
          FindTerminalsModal.SetStatus(
            $"Attempting to reconnect to last known device {device.Name}… " +
            $"{remainingSeconds}s remaining (attempt {attempt})");
          OnPropertyChanged(nameof(ReconnectPromptDetail));

          TerminalIdentity identity;
          try {
            // The saved transport is sufficient for a bonded device and does
            // not require an advertisement scan. This is the fast path used
            // at startup and immediately after a transient disconnect.
            identity = await terminalSession!.OpenAsync(
              device, terminalId, cancellationToken);
          } catch {
            // If the peripheral rotated its BLE address, rediscover it and
            // continue using the verified terminal identity as the selector.
            var candidates = await connection.DiscoverAsync();
            var suffix = terminalId.Length >= 4 ? terminalId[^4..] : terminalId;
            var candidate = candidates.FirstOrDefault(item =>
                string.Equals(item.Id, device.Id, StringComparison.OrdinalIgnoreCase))
              ?? candidates.FirstOrDefault(item =>
                item.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
              // CoreBluetooth can assign a new identifier after a reset. If
              // only one Hallzee is visible, let the full identity handshake
              // below verify it instead of requiring the old identifier/name.
              ?? (candidates.Count == 1 ? candidates[0] : null);
            if (candidate == null) continue;
            device = candidate;
            identity = await terminalSession!.OpenAsync(
              device, terminalId, cancellationToken);
          }
          if (!identity.IsClaimed) {
            FindTerminalsModal.SetStatus(
              "This terminal reports UNCLAIMED after reconnect. Reflash the current terminal firmware, then claim it once with the six-digit pairing key.");
            return;
          }

          var authenticated = await terminalSession.AuthenticateAsync(
            cancellationToken: cancellationToken);
          profileRepository.SaveTerminal(new TerminalDeviceConfig(
            authenticated.TerminalId,
            authenticated.CustomName,
            device.Id,
            DateTime.UtcNow,
            ProtocolVersion: TerminalIdentityProtocol.ProtocolVersion,
            ClaimStatus: "CLAIMED",
            TransportId: device.Id));
          profileRepository.AssignTerminalToProfile(ActiveProfile.ProfileId, authenticated.TerminalId);
          ConnectedTerminalName = authenticated.CustomName;
          IsConnected = true;
          OnPropertyChanged(nameof(IsReconnectPromptVisible));
          await SyncNowAsync();
          if (IsConnected) return;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
          return;
        } catch (TerminalInUseException) {
          FindTerminalsModal.SetStatus(
            "The last terminal is In Use. Automatic reconnect is paused; use Scan Again when it is available.");
          return;
        } catch (TerminalCredentialMissingException) {
          FindTerminalsModal.SetStatus(
            "The last terminal is known, but this app has no owner credential. Connect again to pair it.");
          return;
        } catch (TerminalBondRepairRequiredException exception) {
          FindTerminalsModal.SetStatus($"{exception.Message} Automatic reconnect is paused until Bluetooth is repaired.");
          return;
        } catch (TerminalIdentityMismatchException exception) {
          FindTerminalsModal.SetStatus(
            $"Reconnect stopped: {exception.Message} Select the intended terminal and scan again.");
          return;
        } catch {
          // The terminal may be powered off or temporarily out of range. Keep
          // retrying with the same verified identity until the user cancels.
        }
      }
      if (!cancellationToken.IsCancellationRequested) {
        FindTerminalsModal.SetStatus(
          "Reconnect is still unavailable after 45 seconds. Check that Bluetooth is on, then choose Find Terminal.");
        OnPropertyChanged(nameof(ReconnectPromptDetail));
      }
    } finally {
      reconnectInProgress = false;
      OnPropertyChanged(nameof(IsReconnecting));
      OnPropertyChanged(nameof(ConnectionStatusText));
      OnPropertyChanged(nameof(ConnectionStatusColor));
      OnPropertyChanged(nameof(ConnectionBadgeBackground));
      OnPropertyChanged(nameof(TopStatusBadgeText));
      OnPropertyChanged(nameof(TopStatusBadgeBackground));
      OnPropertyChanged(nameof(TerminalAvatarBackground));
      reconnectCancellation?.Dispose();
      reconnectCancellation = null;
      OnPropertyChanged(nameof(IsReconnectPromptVisible));
      OnPropertyChanged(nameof(ReconnectPromptTitle));
      OnPropertyChanged(nameof(ReconnectPromptDetail));
    }
  }

  async Task UpdateReconnectCountdownAsync(
    CancellationToken cancellationToken,
    string deviceName,
    int seconds) {
    try {
      for (var remaining = seconds; remaining > 0; remaining--) {
        FindTerminalsModal.SetStatus(
          $"Attempting to reconnect to last known device {deviceName}… " +
          $"{remaining}s remaining");
        OnPropertyChanged(nameof(ReconnectPromptDetail));
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
      }
    } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
      // The reconnect succeeded, was cancelled, or reached its timeout.
    }
  }

  void CancelAutomaticReconnect() {
    var cancellation = reconnectCancellation;
    reconnectCancellation = null;
    cancellation?.Cancel();
    cancellation?.Dispose();
  }

  Task SendProtocolAsync(string command) {
    return terminalSession == null
      ? connection.SendAsync(command)
      : terminalSession.SendAuthorizedAsync(command);
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
