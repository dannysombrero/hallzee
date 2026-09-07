using BathroomSync.Core;
using BathroomSync.Universal.Services;
using BathroomSync.Universal.ViewModels;
using Xunit;

namespace BathroomSync.Universal.Tests;

public sealed class MainViewModelTests : IDisposable {
  readonly string tempFolder;
  readonly PreviewTerminalConnection connection;
  readonly MainViewModel viewModel;

  public MainViewModelTests() {
    tempFolder = Path.Combine(Path.GetTempPath(), "HallzeeMainViewModelTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempFolder);
    connection = new PreviewTerminalConnection();
    viewModel = new MainViewModel(connection, tempFolder, isPreviewMode: true);
  }

  public void Dispose() {
    viewModel.Dispose();
    try {
      if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
    } catch { }
  }

  [Fact]
  public void InitializesWithDefaultProfileAndClosedModals() {
    Assert.NotNull(viewModel.ActiveProfile);
    Assert.NotEmpty(viewModel.Profiles);
    Assert.False(viewModel.IsModalOpen);
    Assert.Equal("None", viewModel.ActiveModal);
    Assert.False(viewModel.IsConnected);
    Assert.Equal("OFFLINE", viewModel.ConnectionStatusText);
  }

  [Fact]
  public void OpensAndClosesModalsCorrectly() {
    viewModel.OpenModal("Trips");
    Assert.True(viewModel.IsModalOpen);
    Assert.True(viewModel.IsTripsModalVisible);
    Assert.False(viewModel.IsRosterModalVisible);

    viewModel.OpenModal("Roster");
    Assert.True(viewModel.IsRosterModalVisible);
    Assert.False(viewModel.IsTripsModalVisible);

    viewModel.OpenModal("Policies");
    Assert.True(viewModel.IsPoliciesModalVisible);

    viewModel.OpenModal("TerminalSettings");
    Assert.True(viewModel.IsTerminalSettingsModalVisible);

    viewModel.OpenModal("FindTerminals");
    Assert.True(viewModel.IsFindTerminalsModalVisible);

    viewModel.OpenModal("ManualCheckIn");
    Assert.True(viewModel.IsManualCheckInModalVisible);
    Assert.False(viewModel.IsFindTerminalsModalVisible);

    viewModel.CloseModal();
    Assert.False(viewModel.IsModalOpen);
    Assert.Equal("None", viewModel.ActiveModal);
  }

  [Fact]
  public void ExportTripsOpensTheHistoryPanel() {
    var exportPath = Path.Combine(tempFolder, "hallzee_trips.csv");

    viewModel.ExportTrips(exportPath);

    Assert.True(File.Exists(exportPath));
    Assert.True(viewModel.IsTripsModalVisible);
    Assert.Equal("Trips", viewModel.ActiveModal);
  }

  [Fact]
  public void ManualCheckInRequiresIdentityAndStartsActivePass() {
    viewModel.OpenModal("ManualCheckIn");

    viewModel.SubmitManualCheckIn();

    Assert.False(viewModel.ActivePass.IsOccupied);
    Assert.Equal("Enter a student name or student ID to continue.", viewModel.ManualCheckInModal.StatusMessage);

    viewModel.ManualCheckInModal.StudentName = "Avery Chen";
    viewModel.ManualCheckInModal.Period = "Period 3";
    viewModel.ManualCheckInModal.Destination = "Room 102";
    viewModel.ManualCheckInModal.Purpose = "Nurse / Clinic";
    viewModel.SubmitManualCheckIn();

    Assert.True(viewModel.ActivePass.IsOccupied);
    Assert.Equal("Avery Chen", viewModel.ActivePass.StudentName);
    Assert.Equal("Period 3", viewModel.ActivePass.Period);
    Assert.Equal("Room 102", viewModel.ActivePass.Destination);
    Assert.Equal("Nurse / Clinic", viewModel.ActivePass.Purpose);
    Assert.False(viewModel.IsModalOpen);
  }

  [Fact]
  public void ClassroomProfileCardReflectsTeacherAndSchoolSettings() {
    viewModel.TerminalSettingsModal.TeacherName = "Mr. Herrero";
    viewModel.TerminalSettingsModal.School = "Hallzee Middle School";

    Assert.Equal("Teacher: Mr. Herrero", viewModel.ProfileTeacher);
    Assert.Contains("Hallzee Middle School", viewModel.ProfileDetails);
    Assert.Contains("Mr. Herrero", viewModel.HeaderLocationText);
    Assert.Contains("Hallzee Middle School", viewModel.HeaderLocationText);
  }

  [Fact]
  public async Task StudentsOutBoardTracksAnActivePassAndCanCheckItIn() {
    Assert.False(viewModel.HasStudentsOut);

    viewModel.ManualCheckInModal.StudentName = "Avery Chen";
    viewModel.SubmitManualCheckIn();

    Assert.True(viewModel.HasStudentsOut);

    await viewModel.CheckInActivePassAsync();

    Assert.False(viewModel.HasStudentsOut);
  }

  [Fact]
  public async Task ConnectAndSyncWorkflowUpdatesConnectionAndStatus() {
    Assert.Equal("No Device Paired", viewModel.TerminalFriendlyNameDisplay);
    Assert.Equal("Standby • Ready to discover", viewModel.TerminalTransportInfoDisplay);

    await viewModel.FindTerminalsModal.ScanAsync();
    Assert.NotEmpty(viewModel.FindTerminalsModal.Devices);

    await viewModel.ConnectAndSyncAsync();

    Assert.True(viewModel.IsConnected);
    Assert.Equal("CONNECTED", viewModel.ConnectionStatusText);
    Assert.Equal("Room 204 Door Kiosk (East-204)", viewModel.TerminalFriendlyNameDisplay);
    Assert.Equal("BLE 4.2+ GATT • Active sync", viewModel.TerminalTransportInfoDisplay);
    Assert.Equal("Room 204 Door Kiosk (East-204)", viewModel.TerminalSettingsModal.TerminalName);
    Assert.Contains("HELLO,1", connection.SentCommands);
    Assert.Contains("GET_ACTIVE_PASSES\n", connection.SentCommands);
    Assert.Contains("GET_SETTINGS\n", connection.SentCommands);

    await viewModel.DisconnectAsync();
    Assert.False(viewModel.IsConnected);
    Assert.Equal("OFFLINE", viewModel.ConnectionStatusText);
    Assert.Equal("Last paired: Room 204 Door Kiosk (East-204)", viewModel.TerminalFriendlyNameDisplay);
    Assert.Equal("Standby • Ready to reconnect", viewModel.TerminalTransportInfoDisplay);
    Assert.Equal("Room 204 Door Kiosk (East-204)", viewModel.TerminalSettingsModal.TerminalName);
  }

  [Fact]
  public void HeaderLocationTextReflectsClassroomInfoAndFallsBackToActiveProfile() {
    // Initial fallback
    Assert.Equal(viewModel.ActiveProfile.Name, viewModel.HeaderLocationText);

    // Set Classroom Info
    viewModel.TerminalSettingsModal.Room = "Room 2-207";
    viewModel.TerminalSettingsModal.TeacherName = "Herrero";
    viewModel.TerminalSettingsModal.School = "HRMS";

    Assert.Equal("Room 2-207 - Herrero - HRMS", viewModel.HeaderLocationText);

    // Test Room without prefix
    viewModel.TerminalSettingsModal.Room = "2-207";
    Assert.Equal("Room 2-207 - Herrero - HRMS", viewModel.HeaderLocationText);
  }

  [Fact]
  public void PopupWindowPropertiesReflectLiveClockAndDefaultState() {
    Assert.False(string.IsNullOrWhiteSpace(viewModel.CurrentTimeDisplay));
    Assert.Contains(DateTime.Now.ToString("tt"), viewModel.CurrentTimeDisplay);
    Assert.Equal("PASSES CLOSED", viewModel.PopupPillText);
  }

  [Fact]
  public void PopupWindowTracksActiveStudentCheckout() {
    viewModel.ManualCheckInModal.StudentName = "Avery Chen";
    viewModel.SubmitManualCheckIn();

    Assert.Equal("PASS UNAVAILABLE", viewModel.PopupPillText);
    Assert.Contains("Pass In Use", viewModel.PopupStatusPrefix);
  }

  [Fact]
  public void PopupWindowReflectsCurrentPeriodAndStatus() {
    var now = DateTime.Now;
    var start = now.AddMinutes(-5).ToString("h:mm tt");
    var end = now.AddMinutes(45).ToString("h:mm tt");
    var period = new BellSchedulePeriod("p-1", viewModel.ActiveProfile.ProfileId, "Period 3", start, end, "Mon,Tue,Wed,Thu,Fri,Sat,Sun");
    viewModel.PolicyModal.Periods.Add(new BellPeriodItemViewModel(period));

    Assert.Equal("Period 3", viewModel.CurrentPeriodName);
    Assert.StartsWith("(", viewModel.FormattedPeriodRange);
    Assert.EndsWith(")", viewModel.FormattedPeriodRange);
    Assert.Equal("PASS WARNING", viewModel.PopupPillText);
    Assert.Equal("Bell Window Warning · Window ends in: ", viewModel.PopupStatusPrefix);
  }
}
