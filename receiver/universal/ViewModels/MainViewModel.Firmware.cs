using System.Diagnostics;
using System.Text.Json;
using BathroomSync.Core;

namespace BathroomSync.Universal.ViewModels;

public sealed partial class MainViewModel {
  readonly Dictionary<string,FirmwareInfo> firmwareCache = new();
  FirmwarePackage? firmwarePackage;
  string? firmwareTargetId;
  string? firmwareInfoVerifiedId;
  FirmwareRelease? firmwareRelease;
  Uri? desktopRelease;
  CancellationTokenSource? firmwareCancellation;
  static readonly HttpClient firmwareHttp = new() { Timeout=TimeSpan.FromMinutes(3) };
  bool firmwareInstalling, firmwareChecking, firmwareCanCancel;
  string firmwareStatus = "", softwareStatus = "";
  int firmwareProgress;
  string FirmwareCachePath => Path.Combine(Path.GetDirectoryName(exportFolder)!,"firmware-versions.json");
  FirmwareInfo? KnownFirmware => firmwareCache.GetValueOrDefault(DeviceUniqueId);
  public string FirmwareVersionDisplay => KnownFirmware is {} info
    ? $"{(IsConnected && firmwareInfoVerifiedId==DeviceUniqueId ? "" : "Last seen: ")}{info.Version} ({info.Build})"
    : "Unknown — refresh while connected";
  public string FirmwareCapabilityDisplay => KnownFirmware is {} info
    ? info.BootState=="FAILED" ? "Terminal startup checks failed. USB recovery may be needed." : info.BootState=="PENDING" ? "Terminal startup checks are still running." : info.CanUpdate ? "Bluetooth firmware updates supported" : "One-time USB setup required for wireless updates"
    : "Older firmware may need USB setup before it can report its version.";
  public string FirmwareStatus { get=>firmwareStatus; private set { firmwareStatus=value; OnPropertyChanged(); } }
  public string SoftwareUpdateStatus { get=>softwareStatus; private set { softwareStatus=value; OnPropertyChanged(); } }
  public int FirmwareProgress { get=>firmwareProgress; private set { firmwareProgress=value; OnPropertyChanged(); } }
  public bool IsFirmwareInstalling => firmwareInstalling;
  public bool CanCancelFirmware => firmwareCanCancel;
  public bool CanLoadFirmware => CanEditDevice && !firmwareChecking && firmwareInfoVerifiedId==DeviceUniqueId;
  public bool HasFirmwarePackage => firmwarePackage!=null;
  public bool CanInstallFirmware => HasFirmwarePackage && CanUnpairDevice && firmwareTargetId==DeviceUniqueId && firmwareInfoVerifiedId==DeviceUniqueId && !firmwareChecking;
  public bool CanCheckUpdates => !IsDeviceBusy && !firmwareChecking;
  public bool CanDownloadFirmware => CanUnpairDevice && firmwareRelease!=null && firmwareInfoVerifiedId==DeviceUniqueId && !firmwareChecking;
  public bool HasDesktopRelease => desktopRelease!=null;
  public string FirmwarePackageSummary => firmwarePackage==null ? "" : $"{ConnectedTerminalName} ({firmwareTargetId})\nInstall {firmwarePackage.Version} ({firmwarePackage.Build})\n{firmwarePackage.ReleaseNotes}";
  void InitializeFirmware() {
    try { foreach(var item in JsonSerializer.Deserialize<Dictionary<string,FirmwareInfo>>(File.ReadAllText(FirmwareCachePath)) ?? []) firmwareCache[item.Key]=item.Value; } catch { }
  }
  void NotifyFirmwareState() {
    if(!isConnected) firmwareInfoVerifiedId=null;
    foreach(var name in new[]{nameof(FirmwareVersionDisplay),nameof(FirmwareCapabilityDisplay),nameof(CanLoadFirmware),nameof(HasFirmwarePackage),nameof(CanInstallFirmware),nameof(CanCheckUpdates),nameof(CanDownloadFirmware),nameof(FirmwarePackageSummary),nameof(IsFirmwareInstalling),nameof(CanCancelFirmware),nameof(HasDesktopRelease)}) OnPropertyChanged(name);
  }
  void SaveFirmwareInfo(FirmwareInfo info) {
    firmwareCache[DeviceUniqueId]=info;
    firmwareInfoVerifiedId=DeviceUniqueId;
    try { File.WriteAllText(FirmwareCachePath,JsonSerializer.Serialize(firmwareCache)); } catch { }
    NotifyFirmwareState();
  }
  public async Task RefreshFirmwareInfoAsync() {
    if(!CanEditDevice || terminalSession==null) return;
    firmwareInfoVerifiedId=null;
    IsDeviceBusy=true;
    try { await operationCoordinator.RunAsync(async()=> {
      SaveFirmwareInfo(await new FirmwareTransfer(terminalSession).QueryAsync()); FirmwareStatus="Firmware information read from terminal.";
    }); } catch(Exception ex) { FirmwareStatus=$"Firmware version unavailable. Older terminals need the initial USB update. {ex.Message}"; }
    finally { IsDeviceBusy=false; }
  }
  public void LoadFirmwarePackage(Stream input) {
    if(!CanLoadFirmware) return;
    try {
      var package=FirmwarePackage.Load(input);
      if(KnownFirmware is not {} info) throw new InvalidOperationException("Refresh the connected terminal's firmware information first.");
      package.Select(info); firmwarePackage=package; firmwareTargetId=DeviceUniqueId;
      FirmwareStatus="Ready. Keep the terminal powered and the computer nearby. Check in every pass before installing.";
    } catch(Exception ex) { firmwarePackage=null; FirmwareStatus=ex.Message; }
    NotifyFirmwareState();
  }
  public void CancelFirmwareUpdate() {
    if(firmwareCanCancel) firmwareCancellation?.Cancel();
    else if(!firmwareInstalling) { firmwarePackage=null; FirmwareStatus=""; NotifyFirmwareState(); }
  }
  public async Task InstallFirmwareAsync() {
    if(!CanInstallFirmware || terminalSession==null || lastAuthenticatedDevice==null || firmwarePackage==null) return;
    var package=firmwarePackage; var device=lastAuthenticatedDevice; var terminalId=DeviceUniqueId;
    FirmwareProgress=0; firmwareInstalling=true; firmwareCanCancel=true; IsDeviceBusy=true; intentionalDisconnect=true; CancelAutomaticReconnect();
    firmwareCancellation=new CancellationTokenSource(); var token=firmwareCancellation.Token;
    bool commitStarted=false;
    try {
      await operationCoordinator.RunAsync(async()=> {
        FirmwareStatus="Synchronizing trips before the update…";
        await SyncNowCoreAsync();
        if(!IsConnected || HasStudentsOut) throw new InvalidOperationException("Sync must finish and all passes must be checked in first.");
        await new FirmwareTransfer(terminalSession).SendAsync(package,(percent,status)=> {
          FirmwareProgress=percent; FirmwareStatus=status;
          if(status=="Verifying and restarting…") { commitStarted=true; firmwareCanCancel=false; NotifyFirmwareState(); }
        },token);
        firmwareCanCancel=false; NotifyFirmwareState();
        FirmwareStatus="Restarting and reconnecting…";
        await Task.Delay(1500);
        await terminalSession.DisconnectAsync(); IsConnected=false;
        var watch=Stopwatch.StartNew(); Exception? lastError=null;
        while(watch.Elapsed<TimeSpan.FromSeconds(45)) {
          try {
            await terminalSession.OpenAsync(device,terminalId);
            var auth=await terminalSession.AuthenticateAsync();
            IsConnected=true; ConnectedTerminalName=auth.CustomName;
            var current=await new FirmwareTransfer(terminalSession).QueryAsync(); SaveFirmwareInfo(current);
            if(current.Version!=package.Version || current.Build!=package.Build) throw new FirmwareRollbackException("The terminal is running its previous firmware. The update may have rolled back.");
            if(current.BootState!="CONFIRMED") throw new IOException("Terminal startup verification is pending.");
            await SyncNowCoreAsync();
            if(!IsConnected) throw new IOException("Post-update sync did not finish.");
            FirmwareStatus=$"Update complete — terminal confirmed {current.Version} ({current.Build}).";
            firmwarePackage=null; return;
          } catch(FirmwareRollbackException) { throw; }
          catch(Exception ex) { lastError=ex; await Task.Delay(1500); }
        }
        throw new IOException("Update result not yet verified. Reconnect and refresh firmware information. "+lastError?.Message);
      });
    } catch(OperationCanceledException) { FirmwareStatus="Update cancelled. The previous firmware remains selected."; }
    catch(Exception ex) { FirmwareStatus=(commitStarted ? "Update result not confirmed. " : "Update stopped. ")+ex.Message; }
    finally { firmwareCancellation.Dispose(); firmwareCancellation=null; firmwareInstalling=false; firmwareCanCancel=false; intentionalDisconnect=false; IsDeviceBusy=false; NotifyFirmwareState(); }
  }
  sealed class FirmwareRollbackException(string message) : Exception(message);
  public async Task CheckSoftwareUpdatesAsync() {
    if(!CanCheckUpdates) return;
    firmwareChecking=true; firmwareRelease=null; desktopRelease=null; NotifyFirmwareState(); SoftwareUpdateStatus="Checking GitHub releases…";
    try {
      var service=new FirmwareReleases(firmwareHttp); var check=await service.CheckAsync(); desktopRelease=check.DesktopDownload;
      string firmwareResult=IsConnected ? "No compatible newer firmware found." : "Connect a terminal to check firmware compatibility.";
      if(IsConnected && firmwareInfoVerifiedId==DeviceUniqueId && KnownFirmware is {} info) {
        foreach(var release in check.Firmware.Where(r=>FirmwareVersion.Parse(r.Version)>FirmwareVersion.Parse(info.Version)).Take(10)) {
          var candidate=await service.DownloadAsync(release);
          try { candidate.Select(info); firmwareRelease=release; firmwareResult=$"Terminal firmware {release.Version} is available."; break; }
          catch(InvalidDataException) { }
        }
      } else if(IsConnected) firmwareResult="Refresh terminal firmware information to check compatibility.";
      SoftwareUpdateStatus=check.DesktopStatus+" "+firmwareResult;
    } catch(Exception ex) { SoftwareUpdateStatus="Could not check for updates. Local firmware files still work. "+ex.Message; }
    finally { firmwareChecking=false; NotifyFirmwareState(); }
  }
  public async Task DownloadAndInstallFirmwareAsync() {
    if(!CanDownloadFirmware || firmwareRelease==null) return;
    var targetId=DeviceUniqueId;
    firmwareChecking=true; NotifyFirmwareState(); FirmwareStatus="Downloading and verifying firmware…";
    try {
      var package=await new FirmwareReleases(firmwareHttp).DownloadAsync(firmwareRelease);
      if(!IsConnected || DeviceUniqueId!=targetId || firmwareInfoVerifiedId!=targetId || KnownFirmware is not {} info) throw new IOException("The connected terminal changed. Select the update again for the intended terminal.");
      package.Select(info); firmwarePackage=package; firmwareTargetId=DeviceUniqueId;
    } catch(Exception ex) { FirmwareStatus=ex.Message; firmwarePackage=null; }
    finally { firmwareChecking=false; NotifyFirmwareState(); }
    if(firmwarePackage!=null) await InstallFirmwareAsync();
  }
  public void OpenDesktopRelease() { if(desktopRelease!=null) Process.Start(new ProcessStartInfo(desktopRelease.AbsoluteUri) { UseShellExecute=true }); }
}
