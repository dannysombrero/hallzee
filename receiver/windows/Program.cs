using BathroomSync.Core;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

class SyncForm : Form {
  readonly ComboBox terminals = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
  readonly Button refresh = new() { Text = "Find Terminal" };
  readonly Button sync = new() { Text = "Sync Now", Enabled = false };
  readonly NumericUpDown maxStudentIdLength = new() {
    Minimum = KioskSettingsProtocol.MinimumStudentIdLength,
    Maximum = KioskSettingsProtocol.MaximumStudentIdLength,
    Value = KioskSettingsProtocol.DefaultStudentIdLength,
    Width = 55
  };
  readonly Button applyIdLimit = new() { Text = "Apply ID Limit", Enabled = false };
  readonly Button openCsv = new() { Text = "Open CSV" };
  readonly Button saveCsv = new() { Text = "Save CSV As..." };
  readonly Label status = new() { AutoSize = true };
  readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
  readonly BluetoothConnectionManager connection = new();
  readonly TripSqliteRepository tripStorage;
  readonly SyncSession session;
  readonly string exportFolder;
  TaskCompletionSource<bool>? settingsCompletion;

  public SyncForm() {
    Text = "Hallzee Sync";
    Width = 700;
    Height = 500;

    var documentsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hallzee");
    exportFolder = Path.Combine(documentsFolder, "exports");
    var databaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hallzee");
    var legacyCsv = Path.Combine(documentsFolder, "hallzee_trips.csv");
    if (!File.Exists(legacyCsv)) {
      legacyCsv = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Bathroom Terminal",
        "bathroom_trips.csv"
      );
    }
    var databasePath = Path.Combine(databaseFolder, "hallzee-trips.db");
    MigrateLegacyDatabase(databasePath);
    tripStorage = new TripSqliteRepository(databasePath, legacyCsv);
    session = new SyncSession(tripStorage);
    connection.TextReceived += (_, text) => BeginInvoke(() => Process(text));
    connection.ConnectionLost += (_, message) => BeginInvoke(() => {
      SetStatus($"Connection lost: {message} Find the terminal and try again.", Color.Firebrick);
      sync.Enabled = terminals.SelectedItem is TerminalDevice;
      applyIdLimit.Enabled = terminals.SelectedItem is TerminalDevice;
      refresh.Enabled = true;
      settingsCompletion?.TrySetResult(false);
    });

    var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(12) };
    top.Controls.AddRange(new Control[] {
      new Label { Text = "Hallzee device:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
      terminals,
      refresh,
      sync,
      new Label { Text = "Max student ID digits:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) },
      maxStudentIdLength,
      applyIdLimit,
      openCsv,
      saveCsv
    });
    Controls.Add(top);

    refresh.Click += async (_, _) => await DiscoverAsync();
    sync.Click += async (_, _) => await ConnectAndSyncAsync();
    applyIdLimit.Click += async (_, _) => await ApplyIdLimitAsync();
    openCsv.Click += (_, _) => OpenCsv();
    saveCsv.Click += (_, _) => SaveCsvAs();

    var panel = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(12) };
    status.Text = "Turn on Hallzee, then choose Find Terminal.";
    panel.Controls.Add(status);
    Controls.Add(log);
    Controls.Add(panel);
    Controls.Add(top);
    FormClosing += (_, _) => connection.Dispose();
    Shown += async (_, _) => await DiscoverAsync();
  }

  async Task DiscoverAsync() {
    refresh.Enabled = false;
    sync.Enabled = false;
    applyIdLimit.Enabled = false;
    terminals.Items.Clear();
    SetStatus("Looking for Hallzee nearby...", Color.RoyalBlue);
    try {
      var found = await connection.DiscoverAsync();
      foreach (var terminal in found) terminals.Items.Add(terminal);
      if (terminals.Items.Count > 0) {
        terminals.SelectedIndex = 0;
        sync.Enabled = true;
        applyIdLimit.Enabled = true;
        SetStatus("Select Hallzee and choose Sync Now.", Color.ForestGreen);
      } else {
        SetStatus("Hallzee was not found. Confirm it is powered on and nearby, then try again.", Color.Firebrick);
      }
    } catch (Exception exception) {
      Log(DescribeException(exception) + "\n");
      SetStatus("Bluetooth discovery failed. Confirm Bluetooth is turned on and try again.", Color.Firebrick);
    } finally {
      refresh.Enabled = true;
    }
  }

  async Task ConnectAndSyncAsync() {
    if (terminals.SelectedItem is not TerminalDevice terminal) return;

    sync.Enabled = false;
    refresh.Enabled = false;
    SetStatus("Connecting to Hallzee...", Color.RoyalBlue);
    try {
      session.Start();
      await connection.ConnectAsync(terminal);
      await connection.SendAsync("HELLO,1");
      await connection.SendAsync(KioskSettingsProtocol.QueryCommand);
      var latestTripId = Math.Clamp(tripStorage.GetLatestTripId(), 0L, (long)uint.MaxValue);
      await connection.SendAsync($"TIME_CURSOR,{DateTime.Now:yyyy-MM-dd,HH:mm:ss},{latestTripId}");
      Log($"Connected directly to Hallzee; requesting trips after durable ID {latestTripId}.\n");
      SetStatus("Connected to Hallzee. Synchronizing...", Color.ForestGreen);
    } catch (Exception exception) {
      Log(DescribeException(exception) + "\n");
      SetStatus("Could not connect. Find the terminal and try again.", Color.Firebrick);
      await connection.DisconnectAsync();
      sync.Enabled = true;
    } finally {
      refresh.Enabled = true;
    }
  }

  async Task ApplyIdLimitAsync() {
    if (terminals.SelectedItem is not TerminalDevice terminal) return;

    sync.Enabled = false;
    refresh.Enabled = false;
    applyIdLimit.Enabled = false;
    settingsCompletion = new TaskCompletionSource<bool>(
      TaskCreationOptions.RunContinuationsAsynchronously
    );
    SetStatus("Connecting to update Hallzee settings...", Color.RoyalBlue);

    try {
      session.Start();
      await connection.ConnectAsync(terminal);
      await connection.SendAsync("HELLO,1");
      await connection.SendAsync(
        KioskSettingsProtocol.BuildStudentIdLengthCommand(
          Decimal.ToInt32(maxStudentIdLength.Value)
        )
      );

      var applied = await settingsCompletion.Task.WaitAsync(TimeSpan.FromSeconds(8));
      if (!applied && settingsCompletion.Task.IsCompletedSuccessfully) {
        // Process() already displayed the terminal's specific rejection.
      }
    } catch (TimeoutException) {
      SetStatus(
        "Hallzee did not confirm the setting. Update its firmware and try again.",
        Color.Firebrick
      );
    } catch (Exception exception) {
      Log(DescribeException(exception) + "\n");
      SetStatus("Could not update Hallzee settings.", Color.Firebrick);
    } finally {
      settingsCompletion = null;
      await connection.DisconnectAsync();
      sync.Enabled = terminals.SelectedItem is TerminalDevice;
      applyIdLimit.Enabled = terminals.SelectedItem is TerminalDevice;
      refresh.Enabled = true;
    }
  }

  static string DescribeException(Exception exception) {
    var message = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
    return exception.HResult == 0 ? message : $"{message} (0x{exception.HResult:X8})";
  }

  static void MigrateLegacyDatabase(string destinationPath) {
    if (File.Exists(destinationPath)) return;

    var legacyPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Bathroom Terminal",
      "bathroom-trips.db"
    );
    if (!File.Exists(legacyPath)) return;

    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
    File.Copy(legacyPath, destinationPath);
  }

  async void Process(string text) {
    var update = session.ProcessReceivedData(text);
    foreach (var line in update.Logs) Log(line + "\n");

    if (update.MaxStudentIdLength is int configuredLength) {
      maxStudentIdLength.Value = Math.Clamp(
        configuredLength,
        KioskSettingsProtocol.MinimumStudentIdLength,
        KioskSettingsProtocol.MaximumStudentIdLength
      );
    }

    if (update.SettingsApplied) {
      SetStatus(
        $"Hallzee ID limit updated to {update.MaxStudentIdLength} digits.",
        Color.ForestGreen
      );
      settingsCompletion?.TrySetResult(true);
    } else if (update.SettingsError is not null) {
      var detail = update.SettingsError switch {
        "ACTIVE_ID_TOO_LONG" => "Check in or reset the current student before lowering the ID limit.",
        "INVALID_VALUE" => "Choose an ID limit from 4 through 16.",
        "STORAGE_UNAVAILABLE" => "Hallzee could not save the setting.",
        _ => "Hallzee rejected the setting."
      };
      SetStatus(detail, Color.Firebrick);
      settingsCompletion?.TrySetResult(false);
    }

    try {
      foreach (var command in update.OutboundCommands) await connection.SendAsync(command);
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Connection was interrupted. Find the terminal and sync again.", Color.Firebrick);
      await connection.DisconnectAsync();
      sync.Enabled = true;
      return;
    }

    if (update.Status == SyncStatus.Complete) {
      SetStatus($"Sync complete. New trips: {update.SavedTripCount}", Color.ForestGreen);
      await connection.DisconnectAsync();
      sync.Enabled = true;
    }

    if (update.StorageUnavailable) {
      SetStatus("Local trip storage is unavailable. Try Sync Now again.", Color.Firebrick);
      await connection.DisconnectAsync();
      sync.Enabled = true;
    }
  }

  void SaveCsvAs() {
    using var dialog = new SaveFileDialog {
      Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
      FileName = "hallzee_trips.csv",
      OverwritePrompt = true
    };
    if (dialog.ShowDialog(this) != DialogResult.OK) return;

    try {
      tripStorage.ExportCsv(dialog.FileName);
      Log($"Saved CSV copy to {dialog.FileName}\n");
      SetStatus("CSV copy saved in numeric trip ID order.", Color.ForestGreen);
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Could not save the CSV copy. Choose another location and try again.", Color.Firebrick);
    }
  }

  void OpenCsv() {
    try {
      Directory.CreateDirectory(exportFolder);
      var exportPath = Path.Combine(exportFolder, $"hallzee_trips_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
      tripStorage.ExportCsv(exportPath);
      System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exportPath) { UseShellExecute = true });
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Could not create a CSV export. Choose Save CSV As... and try another location.", Color.Firebrick);
    }
  }

  void Log(string message) => log.AppendText(message);
  void SetStatus(string message, Color color) { status.Text = message; status.ForeColor = color; }
}

static class Program {
  [STAThread]
  static void Main() {
    Application.ThreadException += (_, eventArgs) => ReportStartupFailure(eventArgs.Exception);
    AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => {
      if (eventArgs.ExceptionObject is Exception exception) ReportStartupFailure(exception);
    };

    try {
      Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
      ApplicationConfiguration.Initialize();
      Application.Run(new SyncForm());
    } catch (Exception exception) {
      ReportStartupFailure(exception);
    }
  }

  static void ReportStartupFailure(Exception exception) {
    const string title = "Hallzee Sync could not start";
    var logPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Hallzee",
      "startup-errors.log"
    );

    try {
      Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
      File.AppendAllText(
        logPath,
        $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}"
      );
    } catch {
      // A visible error remains useful even if Windows blocks access to the log folder.
    }

    MessageBox.Show(
      $"{exception.Message}{Environment.NewLine}{Environment.NewLine}Details were saved to:{Environment.NewLine}{logPath}",
      title,
      MessageBoxButtons.OK,
      MessageBoxIcon.Error
    );
  }
}
