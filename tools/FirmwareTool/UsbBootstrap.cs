using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BathroomSync.Core;

internal static class UsbBootstrap {
  const int FlashSize=0x400000, NewFsOffset=0x310000, NewFsSize=0xe0000;
  public static async Task InstallAsync(string esptool,string mklittlefs,string port,string build,string backupRoot) {
    var app=One(build,"*.ino.bin"); var boot=One(build,"*.bootloader.bin"); var partitions=One(build,"*.partitions.bin");
    var bootApp=Path.Combine(build,"boot_app0.bin");
    if(!File.Exists(bootApp)) throw new IOException("USB bundle is missing boot_app0.bin.");
    // Downloaded USB bundles include the signed package; source-built bootstraps
    // are already under the developer's local build trust.
    var packages=Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(build))!,"*.hallzee-fw");
    if(Path.GetFileName(build).StartsWith("USB-",StringComparison.Ordinal) && packages.Length!=1)
      throw new InvalidDataException("Downloaded USB bundle must include one signed firmware package.");
    if(packages.Length==1) {
      using var input=File.OpenRead(packages[0]); var package=FirmwarePackage.Load(input);
      var image=package.Images.SingleOrDefault(i=>i.Variant==Path.GetFileName(build)[4..]);
      if(image==null || FirmwarePackage.Hash(File.ReadAllBytes(app))!=image.Sha256 || FirmwarePackage.Hash(File.ReadAllBytes(boot))!=image.BootloaderHash ||
          FirmwarePackage.Hash(File.ReadAllBytes(partitions))!=image.PartitionsHash || FirmwarePackage.Hash(File.ReadAllBytes(bootApp))!=image.BootAppHash)
        throw new InvalidDataException("USB bootstrap binaries do not match the signed release.");
    }
    var expected=File.ReadAllBytes(partitions);
    if(!HasPartition(expected,0,0x10,0x10000,0x180000) || !HasPartition(expected,0,0x11,0x190000,0x180000) || !HasPartition(expected,1,0x82,NewFsOffset,NewFsSize))
      throw new InvalidDataException("USB bundle does not use the Hallzee OTA v1 layout.");
    if(new FileInfo(app).Length>0x180000) throw new InvalidDataException("USB image exceeds the application slot.");
    string backup=Path.Combine(backupRoot,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(backup);
    File.WriteAllText(Path.Combine(backupRoot,".gitignore"),"*\n");
    if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(backup,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    else await Run("icacls",backup,"/inheritance:r","/grant:r",System.Security.Principal.WindowsIdentity.GetCurrent().Name+":(OI)(CI)F");
    string raw=Path.Combine(backup,"original-flash.bin"), verification=Path.Combine(backup,"readback.bin");
    var flashId = await Run(esptool,"--chip","esp32","--port",port,"flash-id");
    if(!System.Text.RegularExpressions.Regex.IsMatch(flashId,@"(?i)flash size:\s*(4|8|16|32|64|128)\s*MB"))
      throw new IOException("Could not verify at least 4 MB of physical flash. Terminal was not changed.");
    File.WriteAllText(Path.Combine(backup,"device-mac.txt"), DeviceMac(flashId));
    Console.WriteLine("Backing up the original ESP32 before changing any flash…");
    // --chip esp32 rejects other chip families; reading 4 MB requires at least that capacity.
    await Run(esptool,"--chip","esp32","--port",port,"read-flash","0",FlashSize.ToString(),raw);
    await Run(esptool,"--chip","esp32","--port",port,"read-flash","0",FlashSize.ToString(),verification);
    byte[] original=File.ReadAllBytes(raw);
    if(original.Length!=FlashSize || !SHA256.HashData(original).SequenceEqual(SHA256.HashData(File.ReadAllBytes(verification))))
      throw new IOException("Backup verification failed. Terminal was not changed.");
    File.Delete(verification);
    if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(raw,UnixFileMode.UserRead|UnixFileMode.UserWrite);
    File.WriteAllText(Path.Combine(backup,"SHA256.txt"),Convert.ToHexString(SHA256.HashData(original))+"  original-flash.bin\n");
    var oldPartitions=original.AsSpan(0x8000,0x1000).ToArray();
    var fs=FindPartition(oldPartitions,1,0x82);
    bool blank=oldPartitions.All(b=>b==255);
    if(!blank && (!HasPartition(oldPartitions,1,2,0x9000,0x5000) ||
        !(HasPartition(oldPartitions,0,0x10,0x10000,0x140000) && HasPartition(oldPartitions,0,0x11,0x150000,0x140000) && fs==(0x290000,0x160000)) &&
        !(HasPartition(oldPartitions,0,0x10,0x10000,0x180000) && HasPartition(oldPartitions,0,0x11,0x190000,0x180000) && fs==(NewFsOffset,NewFsSize))))
      throw new InvalidDataException("Unrecognized existing flash layout. Backup saved; no flash changes made.");
    string files=Path.Combine(backup,"filesystem"); Directory.CreateDirectory(files);
    if(!blank && fs is {} old && !original.AsSpan(old.Offset,old.Size).ToArray().All(b=>b==255)) {
      string oldImage=Path.Combine(backup,"original-littlefs.bin"); File.WriteAllBytes(oldImage,original.AsSpan(old.Offset,old.Size).ToArray());
      await Run(mklittlefs,"-u",files,"-b","4096","-p","256","-s",old.Size.ToString(),oldImage);
    }
    var contents=Inventory(files);
    string newImage=Path.Combine(backup,"migrated-littlefs.bin");
    await Run(mklittlefs,"-c",files,"-b","4096","-p","256","-s",NewFsSize.ToString(),newImage);
    string unpack=Path.Combine(backup,"verify-files"); Directory.CreateDirectory(unpack);
    await Run(mklittlefs,"-u",unpack,"-b","4096","-p","256","-s",NewFsSize.ToString(),newImage);
    if(!contents.OrderBy(p=>p.Key).SequenceEqual(Inventory(unpack).OrderBy(p=>p.Key))) throw new IOException("Filesystem migration verification failed; terminal was not changed.");
    Console.WriteLine($"Backup verified at {backup}. Installing OTA bootstrap and restoring {contents.Count} files…");
    await Run(esptool,"--chip","esp32","--port",port,"--baud","460800","write-flash","--flash-size","4MB",
      "0x1000",boot,"0x8000",partitions,"0xe000",bootApp,"0x10000",app,$"0x{NewFsOffset:x}",newImage);
    await Run(esptool,"--chip","esp32","--port",port,"verify-flash","0x8000",partitions,$"0x{NewFsOffset:x}",newImage);
    string nvs=Path.Combine(backup,"nvs-readback.bin");
    await Run(esptool,"--chip","esp32","--port",port,"read-flash","0x9000","0x5000",nvs);
    // Boot may update NVS after a reset; use --after no-reset in Run to prevent that until verification ends.
    if(!original.AsSpan(0x9000,0x5000).SequenceEqual(File.ReadAllBytes(nvs))) throw new IOException("NVS verification failed. Keep the backup for USB recovery.");
    await Run(esptool,"--chip","esp32","--port",port,"run");
    Console.WriteLine("OTA USB setup complete. Keep the private backup; pairing and files were preserved.");
  }
  public static async Task RecoverAsync(string esptool,string port,string backup) {
    string raw=Path.Combine(backup,"original-flash.bin"); var bytes=File.ReadAllBytes(raw);
    string recorded=File.ReadAllText(Path.Combine(backup,"SHA256.txt")).Split(' ')[0];
    if(bytes.Length!=FlashSize || !string.Equals(recorded,Convert.ToHexString(SHA256.HashData(bytes)),StringComparison.OrdinalIgnoreCase))
      throw new IOException("Recovery backup is incomplete or changed. Terminal was not modified.");
    string expectedMac=File.ReadAllText(Path.Combine(backup,"device-mac.txt")).Trim();
    string currentMac=DeviceMac(await Run(esptool,"--chip","esp32","--port",port,"flash-id"));
    if(!string.Equals(expectedMac,currentMac,StringComparison.OrdinalIgnoreCase)) throw new IOException("This backup belongs to a different terminal.");
    await Run(esptool,"--chip","esp32","--port",port,"--baud","460800","write-flash","0",raw);
    await Run(esptool,"--chip","esp32","--port",port,"verify-flash","0",raw);
    await Run(esptool,"--chip","esp32","--port",port,"run");
    Console.WriteLine("Original firmware and data restored. Retry the OTA USB setup when ready.");
  }
  static string DeviceMac(string output) {
    var match=System.Text.RegularExpressions.Regex.Match(output,@"(?i)MAC(?: address)?:\s*([0-9a-f]{2}(?::[0-9a-f]{2}){5})");
    return match.Success ? match.Groups[1].Value.ToLowerInvariant() : throw new IOException("Could not verify the physical terminal identity.");
  }
  static string One(string folder,string pattern) { var found=Directory.GetFiles(folder,pattern); return found.Length==1 ? found[0] : throw new IOException($"Expected one {pattern} in USB bundle."); }
  static Dictionary<string,string> Inventory(string root) => Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(root,p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
  internal static (int Offset,int Size)? FindPartition(byte[] bytes,byte type,byte subtype) {
    for(int i=0;i+32<=bytes.Length;i+=32) {
      if(bytes[i]!=0xaa || bytes[i+1]!=0x50) break;
      if(bytes[i+2]==type && bytes[i+3]==subtype) return (BitConverter.ToInt32(bytes,i+4),BitConverter.ToInt32(bytes,i+8));
    }
    return null;
  }
  static bool HasPartition(byte[] b,byte t,byte s,int o,int n) => FindPartition(b,t,s)==(o,n);
  static async Task<string> Run(string executable,params string[] args) {
    var start=new ProcessStartInfo(executable) { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true };
    // Keep application code stopped while backing up, migrating, and checking NVS.
    bool esp=args.Contains("--chip");
    if(esp && !args.Contains("run")) { start.ArgumentList.Add("--after"); start.ArgumentList.Add("no-reset"); }
    foreach(var arg in args) start.ArgumentList.Add(arg);
    using var process=Process.Start(start) ?? throw new IOException("Could not start "+executable);
    var outputTask=process.StandardOutput.ReadToEndAsync(); var errorTask=process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync(); var output=await outputTask; Console.Write(output); Console.Error.Write(await errorTask); if(process.ExitCode!=0) throw new IOException($"{Path.GetFileName(executable)} failed ({process.ExitCode}); preserve the backup and retry USB setup.");
    return output;
  }
}
