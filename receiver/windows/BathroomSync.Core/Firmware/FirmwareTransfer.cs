using System.Buffers.Binary;
using System.Security.Cryptography;

namespace BathroomSync.Core;

public sealed class FirmwareTransfer(TerminalSession session) {
  public async Task<FirmwareInfo> QueryAsync(CancellationToken token = default) => FirmwareInfo.Parse(
    await session.ExchangeAuthorizedAsync("GET_FIRMWARE_INFO",null,"FIRMWARE_INFO,",true,token));

  public async Task SendAsync(FirmwarePackage package, Action<int,string> progress, CancellationToken token = default) {
    var info = await QueryAsync(token); var image = package.Select(info);
    uint sid; do { sid = BinaryPrimitives.ReadUInt32LittleEndian(RandomNumberGenerator.GetBytes(4)); } while (sid == 0);
    string id = sid.ToString("x8"); bool begun = false, committed = false;
    try {
      await session.RequestAuthorizedAsync($"FW_BEGIN,{id},{package.Manifest.Length},{Convert.ToHexString(package.Signature)}",$"FW_READY,{id}",token);
      begun = true;
      async Task Send(byte kind, byte[] bytes) {
        for (int offset=0;offset<bytes.Length;offset+=512) {
          int length=Math.Min(512,bytes.Length-offset); var frame=new byte[15+length];
          frame[1]=(byte)'H'; frame[2]=(byte)'Z'; frame[3]=1; frame[4]=kind;
          BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(5),sid);
          BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(9),offset);
          BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13),(ushort)length);
          bytes.AsSpan(offset,length).CopyTo(frame.AsSpan(15));
          for (int attempt=0;;attempt++) {
            try { await session.ExchangeAuthorizedAsync(null,frame,$"FW_ACK,{id},{kind},{offset+length}",false,token); break; }
            catch (TimeoutException) when (attempt<2) { }
          }
          if (kind==2) progress((offset+length)*100/bytes.Length,"Sending firmware…");
        }
      }
      progress(0,"Verifying package on terminal…"); await Send(1,package.Manifest);
      await Send(2,image.Bytes);
      token.ThrowIfCancellationRequested();
      progress(100,"Verifying and restarting…");
      // After commit is sent, its outcome is uncertain if its ACK is lost. Never send abort then.
      committed = true;
      await session.RequestAuthorizedAsync($"FW_COMMIT,{id}","FW_COMMITTED");
    } finally {
      if (begun && !committed) {
        try { await session.RequestAuthorizedAsync($"FW_ABORT,{id}","FW_ABORTED"); } catch { }
      }
    }
  }
}
