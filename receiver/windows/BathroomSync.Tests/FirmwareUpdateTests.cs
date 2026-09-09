using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BathroomSync.Core;
using Xunit;

namespace BathroomSync.Tests;

public sealed class FirmwareUpdateTests {
  static readonly FirmwareInfo Device = new("1.0.0","old","esp32-ili9341-r1","ota-v1",1572864,true,"CONFIRMED",1);
  static (byte[] Archive,string Key) Package(string variant="esp32-ili9341-r1",string version="1.1.0",bool corruptImage=false,bool duplicate=false) {
    using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
    byte[] image=new byte[1100]; image[0]=0xe9;
    Encoding.ASCII.GetBytes($"HZFW1|{version}|test-build|{variant}|ota-v1").CopyTo(image,128);
    byte[] notes=Encoding.UTF8.GetBytes("Test release");
    byte[] manifest=Encoding.ASCII.GetBytes($"HALLZEE-FW|1|{version}|test-build|1.0.0|1|1|{FirmwarePackage.Hash(notes)}|hallzee-1\n{variant}|ota-v1|{image.Length}|{FirmwarePackage.Hash(image)}|{new string('0',64)}|{new string('0',64)}|{new string('0',64)}\n");
    byte[] signature=key.SignData(manifest,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
    if(corruptImage) image[100]^=1;
    using var memory=new MemoryStream();
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
      void Add(string n,byte[] b) { using var stream=zip.CreateEntry(n).Open(); stream.Write(b); }
      Add("manifest.txt",manifest); Add("manifest.sig",signature); Add("release-notes.md",notes); Add($"images/{variant}.bin",image);
      if(duplicate) Add("manifest.txt",manifest);
    }
    return (memory.ToArray(),key.ExportSubjectPublicKeyInfoPem());
  }
  static FirmwarePackage Load((byte[] Archive,string Key) p) => FirmwarePackage.Load(new MemoryStream(p.Archive),p.Key);
  [Fact] public void PackageRequiresSignatureAndUnmodifiedImage() {
    var good=Package(); var p=Load(good);
    Assert.Equal(1100,p.Select(Device).Bytes.Length);
    Assert.Throws<InvalidDataException>(()=>FirmwarePackage.Load(new MemoryStream(good.Archive)));
    Assert.Throws<InvalidDataException>(()=>Load(Package(corruptImage:true)));
    Assert.Throws<InvalidDataException>(()=>Load(Package(duplicate:true)));
  }
  [Fact] public void PackageRejectsWrongVariantLayoutAndDowngrades() {
    var package=Load(Package());
    Assert.Throws<InvalidDataException>(()=>package.Select(Device with {Variant="esp32-st7735-r1"}));
    Assert.Throws<InvalidDataException>(()=>package.Select(Device with {Layout="legacy"}));
    Assert.Throws<InvalidDataException>(()=>package.Select(Device with {Version="1.1.0"}));
    Assert.Throws<InvalidDataException>(()=>package.Select(Device with {CanUpdate=false}));
    Assert.Throws<InvalidDataException>(()=>package.Select(Device with {SlotSize=512}));
    Assert.Throws<InvalidDataException>(()=>package.Select(Device with {BootState="PENDING"}));
  }
  [Theory]
  [InlineData("1.2.3",true)] [InlineData("1.10.0",true)] [InlineData("1.2",false)]
  [InlineData("01.2.3",false)] [InlineData("1.2.3-beta",false)] [InlineData("1000.0.0",false)]
  public void StableVersionsAreBounded(string value,bool valid) => Assert.Equal(valid,FirmwareVersion.TryParse(value,out _));
  [Fact] public void FirmwareInfoComesFromStrictVersionedResponse() {
    var info=FirmwareInfo.Parse("FIRMWARE_INFO,1,1.2.3,build,esp32-ili9341-r1,ota-v1,1572864,1,CONFIRMED,1");
    Assert.Equal("1.2.3",info.Version); Assert.True(info.CanUpdate);
    Assert.Throws<InvalidDataException>(()=>FirmwareInfo.Parse("FIRMWARE_INFO,2,1.2.3,build,esp32-ili9341-r1,ota-v1,1572864,1,CONFIRMED,1"));
  }
  [Theory]
  [InlineData(false)] [InlineData(true)]
  public async Task BinaryTransferSequencesChunksAndAbortsCancellation(bool cancel) {
    var connection=new OtaConnection(); var credentials=new InMemoryTerminalCredentialStore();
    credentials.SaveOwnerKey("HZ-A1B2C3D4E5F6",new byte[32]);
    using var session=new TerminalSession(connection,credentials,Guid.NewGuid().ToString());
    await session.OpenAsync(new TerminalDevice("ota-device","Hallzee",true)); await session.AuthenticateAsync();
    var package=Load(Package()); var token=new CancellationTokenSource();
    var transfer=new FirmwareTransfer(session);
    async Task Send() => await transfer.SendAsync(package,(percent,_)=> { if(cancel && percent>0) token.Cancel(); },token.Token);
    if(cancel) {
      await Assert.ThrowsAnyAsync<OperationCanceledException>(Send);
      Assert.True(connection.Aborted); Assert.False(connection.Committed);
    } else {
      await Send(); Assert.True(connection.Committed); Assert.False(connection.Aborted);
      Assert.Equal(package.Images[0].Bytes,connection.Image.ToArray());
      Assert.Equal(package.Manifest,connection.Metadata.ToArray());
    }
    Assert.True(credentials.TryGetOwnerKey("HZ-A1B2C3D4E5F6",out _));
  }
  sealed class OtaConnection : ITerminalConnection,ITerminalBinaryConnection {
    public event EventHandler<string>? TextReceived;
    public event EventHandler<string>? ConnectionLost { add{} remove{} }
    public MemoryStream Image {get;}=new(); public MemoryStream Metadata {get;}=new();
    public bool Committed,Aborted; uint session;
    public Task ConnectAsync(TerminalDevice device)=>Task.CompletedTask;
    public Task DisconnectAsync()=>Task.CompletedTask;
    public void Dispose() { Image.Dispose(); Metadata.Dispose(); }
    public Task<IReadOnlyList<TerminalDevice>> DiscoverAsync()=>Task.FromResult<IReadOnlyList<TerminalDevice>>([]);
    void Emit(string text)=>TextReceived?.Invoke(this,text+"\n");
    public Task SendAsync(string command) {
      if(command.StartsWith("HELLO,")) Emit("IDENTITY,2,HZ-A1B2C3D4E5F6,E5F6,CLAIMED,AVAILABLE,00112233445566778899AABBCCDDEEFF");
      else if(command.StartsWith("AUTH,")) Emit("AUTH_OK,2,HZ-A1B2C3D4E5F6,Hallzee");
      else if(command=="GET_FIRMWARE_INFO") Emit("FIRMWARE_INFO,1,1.0.0,old,esp32-ili9341-r1,ota-v1,1572864,1,CONFIRMED,1");
      else if(command.StartsWith("FW_BEGIN,")) { var id=command.Split(',')[1]; session=Convert.ToUInt32(id,16); Emit("FW_READY,"+id); }
      else if(command.StartsWith("FW_COMMIT,")) { Committed=true; Emit("FW_COMMITTED"); }
      else if(command.StartsWith("FW_ABORT,")) { Aborted=true; Emit("FW_ABORTED"); }
      return Task.CompletedTask;
    }
    public Task SendBinaryAsync(byte[] frame) {
      Assert.Equal(new byte[]{0,72,90,1},frame.Take(4).ToArray());
      Assert.Equal(session,BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(5)));
      int offset=BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(9)); int size=BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(13));
      Assert.InRange(size,1,512); Assert.Equal(size+15,frame.Length);
      var target=frame[4]==1 ? Metadata : Image; Assert.Equal(target.Length,offset); target.Write(frame,15,size);
      Emit($"FW_ACK,{session:x8},{frame[4]},{offset+size}"); return Task.CompletedTask;
    }
  }
}
