using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
  readonly TerminalSession? terminalSession;
  readonly ITerminalCredentialStore? ownerCredentialStore;

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
  TerminalDevice? lastAuthenticatedDevice;
  string? lastAuthenticatedTerminalId;
  CancellationTokenSource? reconnectCancellation;
  bool reconnectInProgress;
  bool intentionalDisconnect;

  public MainViewModel(
    ITerminalConnection connection,
    string? appDataPath = null,
    bool isPreviewMode = true,
    ITerminalCredentialStore? credentialStore = null
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
    };
    TerminalSettingsModal = new TerminalSettingsViewModel(profileRepository, connection, appData, () => OnPropertyChanged(nameof(HeaderLocationText)));
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
  public ManualCheckInViewModel ManualCheckInModal { get; }

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
      connectedTerminalName = device.Name;
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
      connectedTerminalName = authenticated.CustomName;
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

  public async Task SyncNowAsync() {
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
      await SendProtocolAsync(ActivePassProtocol.BuildGetActivePassesCommand());
      await SendProtocolAsync(terminalSession == null
        ? KioskSettingsProtocol.QueryCommand + "\n"
        : KioskSettingsProtocol.QueryCommand);
      await SendProtocolAsync($"SET,MAX_ACTIVE_PASSES,{PolicyModal.MaxSimultaneousPasses}");

      // The kiosk stores its classroom wall-clock time as a UTC-shaped epoch.
      // Send the Mac's local time so the terminal display and its local records
      // match the classroom clock.
      var now = DateTime.Now;
      var lastTripId = authenticatedTerminalId == null
        ? tripRepository.GetRecentTrips(1).FirstOrDefault()?.TripId ?? 0
        : tripRepository.GetRecentTrips(authenticatedTerminalId).FirstOrDefault()?.TripId ?? 0;
      var command = $"TIME_CURSOR,{now:yyyy-MM-dd},{now:HH:mm:ss},{lastTripId}\n";
      await SendProtocolAsync(command);

      LastSyncTimeText = $"Today, {DateTime.Now:h:mm tt} (just now)";
    } catch {
      IsConnected = false;
      LastSyncTimeText = "Sync failed (Terminal offline)";
      IsSyncing = false;
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
    if (ActivePass.IsOccupied && !string.IsNullOrEmpty(ActivePass.StudentId)) {
      var studentId = ActivePass.StudentId;
      var checkoutTime = ActivePass.CheckoutTime ?? DateTime.Now;
      var checkinTime = DateTime.Now;
      var duration = (int)Math.Max(0, (checkinTime - checkoutTime).TotalSeconds);
      var date = checkoutTime.ToString("yyyy-MM-dd");
      var timeOut = checkoutTime.ToString("HH:mm:ss");
      var timeIn = checkinTime.ToString("HH:mm:ss");

      var tripId = tripRepository.GetLatestTripId() + 1;
      var status = ActivePass.IsManual ? "MANUAL" : "COMPLETED";
      var payload = $"{tripId},{studentId},{date},{timeOut},{timeIn},{duration},{status}";
      if (ActivePass.IsManual) {
        tripRepository.StoreManual("LEGACY-DEFAULT", payload, ActivePass.StudentName);
      } else {
        tripRepository.Store(payload);
      }

      if (IsConnected) {
        try {
          await CheckInStudentAsync(studentId);
        } catch { }
      }

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
        await SendProtocolAsync($"MANUAL_CHECKIN,{studentId}");
    }
  }

  public async Task ApplyPolicyCapacityAsync() {
    if (IsConnected) {
      await SendProtocolAsync($"SET,MAX_ACTIVE_PASSES,{PolicyModal.MaxSimultaneousPasses}");
    }
  }

  public void RefreshActiveProfileData() {
    PolicyModal.Refresh(ActiveProfile.ProfileId);
    Dashboard.ThresholdMinutes = PolicyModal.DurationWarningMinutes;
    Dashboard.Refresh(ActiveProfile.ProfileId);
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
    connectedTerminalName = lastAuthenticatedDevice.Name;
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
    connection.TextReceived -= HandleTextReceived;
    connection.ConnectionLost -= HandleConnectionLost;
    terminalSession?.Dispose();
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
          connectedTerminalName = authenticated.CustomName;
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
