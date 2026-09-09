using System.Net.Http;
using System.Text.Json;

namespace BathroomSync.Core;

public sealed record FirmwareRelease(string Version, Uri Download, Uri Page);
public sealed record SoftwareUpdateCheck(IReadOnlyList<FirmwareRelease> Firmware, string DesktopStatus, Uri? DesktopDownload);
public sealed class FirmwareReleases(HttpClient http) {
  public const string Repository = "dannysombrero/hallzee-mono";
  public async Task<SoftwareUpdateCheck> CheckAsync(CancellationToken token = default) {
    var firmware = new List<FirmwareRelease>(); string desktop = "No versioned desktop release found."; Uri? desktopUrl=null; Version? desktopVersion=null;
    // Bound pagination; never mistake a failed request for an empty successful result.
    for(int page=1;page<=5;page++) {
      using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{Repository}/releases?per_page=100&page={page}");
      request.Headers.UserAgent.ParseAdd($"Hallzee-Desktop/{FirmwarePackage.ClientVersion}"); request.Headers.Accept.ParseAdd("application/vnd.github+json");
      using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
      if(response.StatusCode==System.Net.HttpStatusCode.NotFound)
        throw new HttpRequestException($"The update feed ({Repository}) is not publicly accessible (HTTP 404). It may be private or unavailable. Use Install firmware from file for local testing.",null,response.StatusCode);
      response.EnsureSuccessStatusCode();
      using var stream=await response.Content.ReadAsStreamAsync(token); using var buffer=new MemoryStream();
      await CopyBoundedAsync(stream,buffer,2*1024*1024,token); buffer.Position=0;
      using var json=await JsonDocument.ParseAsync(buffer,cancellationToken:token);
      foreach(var release in json.RootElement.EnumerateArray()) {
        if(release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
        var tag=release.GetProperty("tag_name").GetString() ?? "";
        if(tag.StartsWith("firmware-v") && FirmwareVersion.TryParse(tag[10..],out _)) {
          var assets=release.GetProperty("assets").EnumerateArray().Where(a=>(a.GetProperty("name").GetString() ?? "").EndsWith(".hallzee-fw",StringComparison.Ordinal)).ToArray();
          if(assets.Length==1) firmware.Add(new(tag[10..],TrustedUri(assets[0].GetProperty("browser_download_url").GetString()!),TrustedUri(release.GetProperty("html_url").GetString()!)));
        }
        if(tag.StartsWith("client-v") && FirmwareVersion.TryParse(tag[8..],out var v) && (desktopVersion==null || v>desktopVersion)) {
          desktopVersion=v; desktopUrl=TrustedUri(release.GetProperty("html_url").GetString()!);
          desktop=v>FirmwareVersion.Parse(FirmwarePackage.ClientVersion) ? $"Desktop client {v} available." : "Desktop client is up to date.";
        }
      }
      if(json.RootElement.GetArrayLength()<100) break;
    }
    return new(firmware.OrderByDescending(f=>FirmwareVersion.Parse(f.Version)).ToArray(),desktop,desktopUrl);
  }
  public async Task<FirmwarePackage> DownloadAsync(FirmwareRelease release, CancellationToken token=default) {
    using var response=await http.GetAsync(TrustedUri(release.Download.AbsoluteUri),HttpCompletionOption.ResponseHeadersRead,token);
    response.EnsureSuccessStatusCode();
    using var input=await response.Content.ReadAsStreamAsync(token); using var output=new MemoryStream();
    await CopyBoundedAsync(input,output,FirmwarePackage.MaximumArchiveBytes,token); output.Position=0;
    var package=FirmwarePackage.Load(output);
    if(package.Version!=release.Version) throw new InvalidDataException("Release version does not match its signed package.");
    return package;
  }
  static Uri TrustedUri(string text) {
    if(!Uri.TryCreate(text,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.AbsolutePath.StartsWith($"/{Repository}/",StringComparison.Ordinal))
      throw new InvalidDataException("Unexpected release location.");
    return uri;
  }
  public static async Task CopyBoundedAsync(Stream input,Stream output,int maximum,CancellationToken token=default) {
    byte[] buffer=new byte[8192]; int count; long total=0;
    while((count=await input.ReadAsync(buffer,token))>0) { total+=count; if(total>maximum) throw new InvalidDataException("Download exceeds the permitted size."); await output.WriteAsync(buffer.AsMemory(0,count),token); }
  }
}
