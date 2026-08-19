using BathroomSync.Core;
using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

class SyncForm : Form {
  readonly ComboBox ports = new() { DropDownStyle = ComboBoxStyle.DropDownList };
  readonly Button sync = new() { Text = "Sync Now" };
  readonly Label status = new() { AutoSize = true };
  readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
  readonly SyncSession session;
  SerialPort? port;

  public SyncForm() {
    Text = "Bathroom Sync";
    Width = 700;
    Height = 500;

    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Bathroom Terminal");
    var csv = Path.Combine(folder, "bathroom_trips.csv");
    session = new SyncSession(new TripCsvRepository(csv));

    var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(12) };
    top.Controls.AddRange(new Control[] {
      new Label { Text = "Bluetooth COM port:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
      ports,
      new Button { Text = "Refresh Ports" },
      sync,
      new Button { Text = "Open CSV" }
    });
    Controls.Add(top);

    ((Button)top.Controls[2]).Click += (_, _) => RefreshPorts();
    sync.Click += (_, _) => Toggle();
    ((Button)top.Controls[4]).Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(csv) { UseShellExecute = true });

    var panel = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(12) };
    status.Text = "Pair Bathroom-Terminal in Windows, select its outgoing COM port, then sync.";
    panel.Controls.Add(status);
    Controls.Add(log);
    Controls.Add(panel);
    Controls.Add(top);
    FormClosing += (_, _) => Stop();
    RefreshPorts();
  }

  void RefreshPorts() {
    var selected = ports.Text;
    ports.Items.Clear();
    ports.Items.AddRange(SerialPort.GetPortNames().Order().Cast<object>().ToArray());
    ports.SelectedItem = selected;
    if (ports.SelectedIndex < 0 && ports.Items.Count > 0) ports.SelectedIndex = 0;
  }

  void Toggle() { if (port?.IsOpen == true) Stop(); else Start(); }

  void Start() {
    if (ports.SelectedItem is not string name) {
      SetStatus("Select the outgoing Bluetooth COM port.", Color.Firebrick);
      return;
    }

    try {
      session.Start();
      port = new SerialPort(name, 115200) { NewLine = "\n", DtrEnable = true, RtsEnable = true };
      port.DataReceived += Read;
      port.Open();
      sync.Text = "Stop";
      SetStatus("Port opened. Waiting for Bathroom-Terminal...", Color.RoyalBlue);
      Send($"TIME,{DateTime.Now:yyyy-MM-dd,HH:mm:ss}");
      Log($"Opened {name}; waiting for Bathroom-Terminal response.\n");
    } catch (Exception exception) {
      Log(exception.Message + "\n");
      SetStatus("Could not open this COM port.", Color.Firebrick);
      Stop();
    }
  }

  void Stop() {
    if (port != null) {
      try { port.Close(); port.Dispose(); } catch { }
      port = null;
    }
    sync.Text = "Sync Now";
  }

  void Read(object? sender, SerialDataReceivedEventArgs eventArgs) {
    try {
      var text = port?.ReadExisting() ?? "";
      BeginInvoke(() => Process(text));
    } catch { }
  }

  void Process(string text) {
    var update = session.ProcessReceivedData(text);
    foreach (var line in update.Logs) Log(line + "\n");
    foreach (var command in update.OutboundCommands) Send(command);

    if (update.Status == SyncStatus.Synchronizing) {
      SetStatus("Connected to Bathroom-Terminal. Synchronizing...", Color.ForestGreen);
    } else if (update.Status == SyncStatus.Complete) {
      SetStatus($"Sync complete. New trips: {update.SavedTripCount}", Color.ForestGreen);
    }
  }

  void Send(string command) { try { port?.Write(command + "\n"); } catch { } }
  void Log(string message) => log.AppendText(message);
  void SetStatus(string message, Color color) { status.Text = message; status.ForeColor = color; }
}

static class Program {
  [STAThread]
  static void Main() {
    ApplicationConfiguration.Initialize();
    Application.Run(new SyncForm());
  }
}
