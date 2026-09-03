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
  readonly TerminalSession? terminalSession;

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
    if (!isPreviewMode) {
      terminalSession = new TerminalSession(
        connection,
        new InMemoryTerminalCredentialStore(),
        profileRepository.GetOrCreateClientId()
      );
    }

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
    ManualCheckInModal = new ManualCheckInViewModel(rosterService);

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
    else if (modalName == "ManualCheckIn") ManualCheckInModal.Reset(ActiveProfile.ProfileId);
  }

  public void CloseModal() {
    ActiveModal = "None";
    RosterModal.CancelImport();
    ManualCheckInModal.Reset(ActiveProfile.ProfileId);
    Dashboard.Refresh(ActiveProfile.ProfileId);
  }

  public void SubmitManualCheckIn() {
    if (!ManualCheckInModal.CanSubmit) return;
    var (id, name, reason, location) = ManualCheckInModal.ResolvePassDetails();
    ActivePass.SetOccupied(id, name, DateTime.Now, reason, location);
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

    FindTerminalsModal.SetStatus($"Connecting to {device.Name} and verifying identity…");
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
    } catch (Exception exception) {
      FindTerminalsModal.SetStatus($"Secure connection failed: {exception.Message}");
      IsConnected = false;
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
      var payload = $"{tripId},{studentId},{date},{timeOut},{timeIn},{duration},COMPLETED";
      tripRepository.Store(payload);

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
    _ = ReconnectLastTerminalAsync(
      lastAuthenticatedDevice,
      lastAuthenticatedTerminalId,
      reconnectCancellation.Token);
  }

  async Task ReconnectLastTerminalAsync(
    TerminalDevice device,
    string terminalId,
    CancellationToken cancellationToken) {
    reconnectInProgress = true;
    OnPropertyChanged(nameof(IsReconnecting));
    OnPropertyChanged(nameof(ConnectionStatusText));
    OnPropertyChanged(nameof(ConnectionStatusColor));
    OnPropertyChanged(nameof(ConnectionBadgeBackground));
    OnPropertyChanged(nameof(TopStatusBadgeText));
    OnPropertyChanged(nameof(TopStatusBadgeBackground));
    OnPropertyChanged(nameof(TerminalAvatarBackground));
    try {
      var attempt = 0;
      while (!cancellationToken.IsCancellationRequested) {
        attempt++;
        var delay = TimeSpan.FromSeconds(Math.Min(attempt, 15));
        try {
          await Task.Delay(delay, cancellationToken);
          FindTerminalsModal.SetStatus(
            $"Connection lost. Reconnecting to {device.Name} (attempt {attempt})…");

          var identity = await terminalSession!.OpenAsync(
            device, terminalId, cancellationToken);
          if (!identity.IsClaimed) {
            FindTerminalsModal.SetStatus(
              "The last terminal is no longer claimed. Hold * and # on it to start pairing.");
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
        } catch (TerminalIdentityMismatchException exception) {
          FindTerminalsModal.SetStatus(
            $"Reconnect stopped: {exception.Message} Select the intended terminal and scan again.");
          return;
        } catch {
          // The terminal may be powered off or temporarily out of range. Keep
          // retrying with the same verified identity until the user cancels.
        }
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
    }
  }

  void CancelAutomaticReconnect() {
    reconnectCancellation?.Cancel();
    reconnectCancellation = null;
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
