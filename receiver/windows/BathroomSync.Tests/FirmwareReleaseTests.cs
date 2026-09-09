using System.Net;
using System.Net.Http;
using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;
public sealed class FirmwareReleaseTests {
  [Fact] public async Task FiltersProductsAndPrereleasesAndSortsSemantically() {
    using var http=new HttpClient(new FakeHttp(_=>new(HttpStatusCode.OK) { Content=new StringContent("""
      [
        {"draft":false,"prerelease":false,"tag_name":"client-v1.1.0","html_url":"https://github.com/dannysombrero/hallzee-mono/releases/tag/client-v1.1.0","assets":[]},
        {"draft":false,"prerelease":false,"tag_name":"firmware-v1.10.0","html_url":"https://github.com/dannysombrero/hallzee-mono/releases/tag/firmware-v1.10.0","assets":[{"name":"Hallzee.hallzee-fw","browser_download_url":"https://github.com/dannysombrero/hallzee-mono/releases/download/firmware-v1.10.0/Hallzee.hallzee-fw"}]},
        {"draft":false,"prerelease":true,"tag_name":"firmware-v9.0.0","assets":[]},
        {"draft":true,"prerelease":false,"tag_name":"firmware-v8.0.0","assets":[]}
      ]
      """) }));
    var result=await new FirmwareReleases(http).CheckAsync();
    Assert.Equal("1.10.0",Assert.Single(result.Firmware).Version);
    Assert.Contains("1.1.0 available",result.DesktopStatus);
  }
  [Fact] public async Task NetworkFailureIsNotReportedAsUpToDate() {
    using var http=new HttpClient(new FakeHttp(_=>new(HttpStatusCode.Forbidden)));
    await Assert.ThrowsAsync<HttpRequestException>(()=>new FirmwareReleases(http).CheckAsync());
  }
  [Fact] public async Task BoundedDownloadStopsBeforeAllocatingOversizedArchive() {
    using var input=new MemoryStream(new byte[1025]); using var output=new MemoryStream();
    await Assert.ThrowsAsync<InvalidDataException>(()=>FirmwareReleases.CopyBoundedAsync(input,output,1024));
  }
  sealed class FakeHttp(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => Task.FromResult(respond(request));
  }
}
