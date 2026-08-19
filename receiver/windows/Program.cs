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
  readonly Label status = new() { AutoSize = true };
  readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
  readonly BluetoothConnectionManager connection = new();
  readonly SyncSession session;
  readonly string csv;

  public SyncForm() {
    Text = "Bathroom Sync";
    Width = 700;
    Height = 500;

    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Bathroom Terminal");
    csv = Path.Combine(folder, "bathroom_trips.csv");
    session = new SyncSession(new TripCsvRepository(csv));
    connection.TextReceived += (_, text) => BeginInvoke(() => Process(text));
    connection.ConnectionLost += (_, message) => BeginInvoke(() => SetStatus($"Connection lost: {message} Find the terminal and try again.", Color.Firebrick));

    var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(12) };
    top.Controls.AddRange(new Control[] {
      new Label { Text = "Bathroom terminal:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
      terminals,
      refresh,
      sync,
      new Button { Text = "Open CSV" }
    });
    Controls.Add(top);

    refresh.Click += async (_, _) => await DiscoverAsync();
    sync.Click += async (_, _) => await ConnectAndSyncAsync();
    ((Button)top.Controls[4]).Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(csv) { UseShellExecute = true });

    var panel = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(12) };
    status.Text = "Turn on Bathroom-Terminal, then choose Find Terminal.";
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
    terminals.Items.Clear();
    SetStatus("Looking for Bathroom-Terminal nearby...", Color.RoyalBlue);
    try {
      var found = await connection.DiscoverAsync();
      foreach (var terminal in found) terminals.Items.Add(terminal);
      if (terminals.Items.Count > 0) {
        terminals.SelectedIndex = 0;
        sync.Enabled = true;
        SetStatus("Select Bathroom-Terminal and choose Sync Now.", Color.ForestGreen);
      } else {
        SetStatus("Bathroom-Terminal was not found. Confirm it is powered on and nearby, then try again.", Color.Firebrick);
      }
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Bluetooth discovery failed. Confirm Bluetooth is turned on and try again.", Color.Firebrick);
    } finally {
      refresh.Enabled = true;
    }
  }

  async Task ConnectAndSyncAsync() {
    if (terminals.SelectedItem is not TerminalDevice terminal) return;

    sync.Enabled = false;
    refresh.Enabled = false;
    SetStatus(terminal.IsPaired ? "Connecting to Bathroom-Terminal..." : "Pairing with Bathroom-Terminal...", Color.RoyalBlue);
    try {
      await connection.ConnectAsync(terminal);
      session.Start();
      await connection.SendAsync($"TIME,{DateTime.Now:yyyy-MM-dd,HH:mm:ss}");
      Log("Connected directly to Bathroom-Terminal; waiting for sync response.\n");
      SetStatus("Connected to Bathroom-Terminal. Synchronizing...", Color.ForestGreen);
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Could not connect. Find the terminal and try again.", Color.Firebrick);
      await connection.DisconnectAsync();
      sync.Enabled = true;
    } finally {
      refresh.Enabled = true;
    }
  }

  async void Process(string text) {
    var update = session.ProcessReceivedData(text);
    foreach (var line in update.Logs) Log(line + "\n");

    try {
      foreach (var command in update.OutboundCommands) await connection.SendAsync(command);
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Connection was interrupted. Find the terminal and sync again.", Color.Firebrick);
      return;
    }

    if (update.Status == SyncStatus.Complete) {
      SetStatus($"Sync complete. New trips: {update.SavedTripCount}", Color.ForestGreen);
      sync.Enabled = true;
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
    const string title = "Bathroom Sync could not start";
    var logPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Bathroom Terminal",
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
