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
    await viewModel.FindTerminalsModal.ScanAsync();
    Assert.NotEmpty(viewModel.FindTerminalsModal.Devices);

    await viewModel.ConnectAndSyncAsync();

    Assert.True(viewModel.IsConnected);
    Assert.Equal("CONNECTED", viewModel.ConnectionStatusText);
    Assert.Contains("HELLO,1", connection.SentCommands);
    Assert.Contains("GET_ACTIVE_PASSES\n", connection.SentCommands);
    Assert.Contains("GET_SETTINGS\n", connection.SentCommands);

    await viewModel.DisconnectAsync();
    Assert.False(viewModel.IsConnected);
    Assert.Equal("OFFLINE", viewModel.ConnectionStatusText);
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
}
