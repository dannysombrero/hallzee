using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

class SyncForm : Form {
  ComboBox ports=new(){DropDownStyle=ComboBoxStyle.DropDownList}; Button sync=new(){Text="Sync Now"}; Label status=new(){AutoSize=true}; TextBox log=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill}; SerialPort? port; StringBuilder input=new(); HashSet<string> ids=new(); string csv; bool full; int saved;
  public SyncForm() {
    Text="Bathroom Sync"; Width=700; Height=500;
    var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Bathroom Terminal"); Directory.CreateDirectory(folder); csv=Path.Combine(folder,"bathroom_trips.csv");
    if(!File.Exists(csv)) File.WriteAllText(csv,"trip_id,student_id,date,time_out,time_in,duration_seconds,status\n");
    foreach(var line in File.ReadLines(csv).Skip(1)){var id=line.Split(',')[0];if(long.TryParse(id,out _))ids.Add(id);}
    var top=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(12)}; top.Controls.AddRange(new Control[]{new Label{Text="Bluetooth COM port:",AutoSize=true,Padding=new Padding(0,6,0,0)},ports,new Button{Text="Refresh Ports"},sync,new Button{Text="Open CSV"}}); Controls.Add(top);
    var refresh=(Button)top.Controls[2]; refresh.Click+=(_,_)=>Refresh(); sync.Click+=(_,_)=>Toggle(); ((Button)top.Controls[4]).Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(csv){UseShellExecute=true});
    var panel=new Panel{Dock=DockStyle.Top,Height=55,Padding=new Padding(12)}; status.Text="Pair Bathroom-Terminal in Windows, select its outgoing COM port, then sync."; panel.Controls.Add(status); Controls.Add(panel); Controls.Add(log); FormClosing+=(_,_)=>Stop(); Refresh();
  }
  void Refresh(){var selected=ports.Text;ports.Items.Clear();ports.Items.AddRange(SerialPort.GetPortNames().Order().Cast<object>().ToArray());ports.SelectedItem=selected;if(ports.SelectedIndex<0&&ports.Items.Count>0)ports.SelectedIndex=0;}
  void Toggle(){if(port?.IsOpen==true)Stop();else Start();}
  void Start(){if(ports.SelectedItem is not string name){Set("Select the outgoing Bluetooth COM port.",Color.Firebrick);return;}try{saved=0;full=false;input.Clear();port=new SerialPort(name,115200){NewLine="\n",DtrEnable=true,RtsEnable=true};port.DataReceived+=Read;port.Open();sync.Text="Stop";Set("Port opened. Waiting for Bathroom-Terminal...",Color.RoyalBlue);Send($"TIME,{DateTime.Now:yyyy-MM-dd,HH:mm:ss}");Log($"Opened {name}; waiting for Bathroom-Terminal response.\n");}catch(Exception e){Log(e.Message+"\n");Set("Could not open this COM port.",Color.Firebrick);Stop();}}
  void Stop(){if(port!=null){try{port.Close();port.Dispose();}catch{}port=null;}sync.Text="Sync Now";}
  void Read(object? s,SerialDataReceivedEventArgs e){try{var text=port?.ReadExisting()??"";BeginInvoke(()=>Process(text));}catch{}}
  void Process(string text){input.Append(text);while(true){var i=input.ToString().IndexOf('\n');if(i<0)return;var line=input.ToString(0,i).Trim();input.Remove(0,i+1);Log("ESP32: "+line+"\n");if(line=="BATHROOM_TERMINAL_READY")Set("Connected to Bathroom-Terminal. Synchronizing...",Color.ForestGreen);else if(line.StartsWith("TRIP,"))Trip(line[5..]);else if(line=="SYNC_END"&&!full){full=true;Send("SYNC_ALL");Log("Requesting full history...\n");}else if(line=="SYNC_END")Set($"Sync complete. New trips: {saved}",Color.ForestGreen);}}
  void Trip(string row){var c=row.Split(',');if(c.Length!=8||!long.TryParse(c[0],out _))return;if(ids.Add(c[0])){File.AppendAllText(csv,string.Join(",",c.Take(7))+"\n");saved++;Log($"Saved trip {c[0]}\n");}Send("ACK,"+c[0]);}
  void Send(string s){try{port?.Write(s+"\n");}catch{}}
  void Log(string s)=>log.AppendText(s); void Set(string s,Color c){status.Text=s;status.ForeColor=c;}
}
static class Program {[STAThread] static void Main(){ApplicationConfiguration.Initialize();Application.Run(new SyncForm());}}
