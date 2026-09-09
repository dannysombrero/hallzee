using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BathroomSync.Core;

static string Required(string[] args,string key) {
  int index=Array.IndexOf(args,key);
  return index>=0 && index+1<args.Length ? args[index+1] : throw new ArgumentException($"Missing {key}");
}
try {
  switch(args.FirstOrDefault()) {
    case "pack": {
      var version=Required(args,"--version"); FirmwareVersion.Parse(version);
      var build=Required(args,"--build"); var folder=Required(args,"--input"); var output=Required(args,"--output");
      var keyFile=Required(args,"--key"); var notes=File.ReadAllBytes(Required(args,"--notes"));
      using var key=ECDsa.Create(); key.ImportFromPem(File.ReadAllText(keyFile));
      if(key.ExportSubjectPublicKeyInfoPem().Trim()!=FirmwarePackage.PublicKey.Trim()) throw new InvalidOperationException("Signing key does not match the pinned Hallzee release key.");
      var images=Directory.GetFiles(folder,"esp32-*.bin").Order().ToArray();
      if(images.Length==0) throw new InvalidOperationException("No firmware images found.");
      string manifest=$"HALLZEE-FW|1|{version}|{build}|1.0.0|1|1|{FirmwarePackage.Hash(notes)}|hallzee-1\n";
      foreach(var file in images) {
        var bytes=File.ReadAllBytes(file);
        if(bytes.Length>1572864-131072) throw new InvalidOperationException("Firmware must leave 128 KiB of app-slot headroom.");
        var variant=Path.GetFileNameWithoutExtension(file); var usb=Path.Combine(folder,"USB-"+variant);
        string Digest(string pattern) { var files=Directory.GetFiles(usb,pattern); if(files.Length!=1) throw new IOException("USB bundle entry missing: "+pattern); return FirmwarePackage.Hash(File.ReadAllBytes(files[0])); }
        manifest+=$"{variant}|ota-v1|{bytes.Length}|{FirmwarePackage.Hash(bytes)}|{Digest("*.bootloader.bin")}|{Digest("*.partitions.bin")}|{Digest("boot_app0.bin")}\n";
      }
      var data=Encoding.ASCII.GetBytes(manifest);
      var signature=key.SignData(data,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
      using var memory=new MemoryStream();
      using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
        void Add(string name,byte[] bytes) { using var target=zip.CreateEntry(name,CompressionLevel.Optimal).Open(); target.Write(bytes); }
        Add("manifest.txt",data); Add("manifest.sig",signature); Add("release-notes.md",notes);
        foreach(var file in images) Add("images/"+Path.GetFileName(file),File.ReadAllBytes(file));
      }
      memory.Position=0; var package=FirmwarePackage.Load(memory);
      using var destination=new FileStream(output,FileMode.CreateNew); memory.Position=0; memory.CopyTo(destination);
      Console.WriteLine($"Created signed firmware {package.Version} with {package.Images.Count} variants."); break;
    }
    case "verify": {
      using var stream=File.OpenRead(Required(args,"--package")); var p=FirmwarePackage.Load(stream);
      Console.WriteLine($"Verified {p.Version} ({p.Build}): {string.Join(", ",p.Images.Select(i=>i.Variant))}"); break;
    }
    case "recover": {
      await UsbBootstrap.RecoverAsync(Required(args,"--esptool"),Required(args,"--port"),Required(args,"--backup")); break;
    }
    case "usb": {
      await UsbBootstrap.InstallAsync(Required(args,"--esptool"),Required(args,"--mklittlefs"),Required(args,"--port"),Required(args,"--build"),Required(args,"--backup")); break;
    }
    default: throw new ArgumentException("Commands: pack --version V --build ID --input DIR --output FILE --key PEM --notes FILE; verify --package FILE; usb --esptool EXE --mklittlefs EXE --port PORT --build DIR --backup DIR");
  }
} catch(Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode=1; }
