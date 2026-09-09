using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BathroomSync.Core;

public sealed record FirmwareInfo(string Version, string Build, string Variant, string Layout, int SlotSize, bool CanUpdate, string BootState, int Bootstrap) {
  public static FirmwareInfo Parse(string line) {
    var f = line.Split(',');
    if (f.Length != 10 || f[0] != "FIRMWARE_INFO" || f[1] != "1" || !FirmwareVersion.TryParse(f[2], out _) ||
        !int.TryParse(f[6], out var size) || size < 0 || size > 4 * 1024 * 1024 ||
        f[7] is not ("0" or "1") || f[8] is not ("CONFIRMED" or "PENDING" or "FAILED") || !int.TryParse(f[9], out var bootstrap))
      throw new InvalidDataException("Invalid terminal firmware information.");
    return new(f[2], f[3], f[4], f[5], size, f[7] == "1", f[8], bootstrap);
  }
}
public static class FirmwareVersion {
  public static bool TryParse(string text, out Version version) {
    version = new Version(0,0,0);
    if (!Regex.IsMatch(text, @"^(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,2})$")) return false;
    return Version.TryParse(text, out version!);
  }
  public static Version Parse(string text) => TryParse(text, out var v) ? v : throw new InvalidDataException("Invalid firmware version.");
}
public sealed record FirmwareImage(string Variant, string Layout, byte[] Bytes, string Sha256, string BootloaderHash, string PartitionsHash, string BootAppHash);

public sealed class FirmwarePackage {
  public const int MaximumArchiveBytes = 16 * 1024 * 1024;
  public static string ClientVersion {
    get { var v=typeof(FirmwarePackage).Assembly.GetName().Version!; return $"{v.Major}.{v.Minor}.{Math.Max(0,v.Build)}"; }
  }
  public const string KeyId = "hallzee-1";
  public byte[] Manifest { get; private init; } = [];
  public byte[] Signature { get; private init; } = [];
  public string Version { get; private init; } = "";
  public string Build { get; private init; } = "";
  public string MinimumClient { get; private init; } = "";
  public int Bootstrap { get; private init; }
  public string ReleaseNotes { get; private init; } = "";
  public IReadOnlyList<FirmwareImage> Images { get; private init; } = [];
  public static string PublicKey {
    get {
      using var stream = typeof(FirmwarePackage).Assembly.GetManifestResourceStream("Hallzee.FirmwarePublicKey")!;
      using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
  }
  public static FirmwarePackage Load(Stream stream, string? trustedKey = null) {
    if (!stream.CanSeek || stream.Length > MaximumArchiveBytes) throw new InvalidDataException("Firmware file is too large or unreadable.");
    using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen:true);
    if (zip.Entries.Count is < 4 or > 11 || zip.Entries.Sum(e => e.Length) > MaximumArchiveBytes)
      throw new InvalidDataException("Invalid firmware archive size.");
    var entries = new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
    foreach (var e in zip.Entries) {
      if (e.FullName.Contains("..") || e.FullName.Contains('\\') || e.FullName.StartsWith('/') || !entries.TryAdd(e.FullName,e))
        throw new InvalidDataException("Unsafe or duplicate package entry.");
    }
    byte[] Read(string name, int max) {
      if (!entries.TryGetValue(name, out var e) || e.FullName != name || e.Length > max) throw new InvalidDataException($"Missing or oversized {name}.");
      using var input = e.Open(); using var output = new MemoryStream();
      var buffer = new byte[8192]; int count;
      while ((count = input.Read(buffer)) > 0) { if (output.Length + count > max) throw new InvalidDataException("Expanded firmware entry is too large."); output.Write(buffer,0,count); }
      if (output.Length != e.Length) throw new InvalidDataException("Truncated firmware entry.");
      return output.ToArray();
    }
    var manifest = Read("manifest.txt",2048); var signature = Read("manifest.sig",72);
    using var key = ECDsa.Create(); key.ImportFromPem(trustedKey ?? PublicKey);
    if (!key.VerifyData(manifest,signature,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence))
      throw new InvalidDataException("Firmware signature is not trusted.");
    if (manifest.Any(b => b != 10 && (b < 32 || b > 126)) || manifest.Length == 0 || manifest[^1] != 10)
      throw new InvalidDataException("Invalid firmware manifest encoding.");
    var lines = Encoding.ASCII.GetString(manifest).TrimEnd('\n').Split('\n');
    var h = lines[0].Split('|');
    if (h.Length != 9 || h[0] != "HALLZEE-FW" || h[1] != "1" || h[5] != "1" || h[6] != "1" || h[8] != KeyId ||
        !Regex.IsMatch(h[3], @"^[a-zA-Z0-9._-]{1,40}$")) throw new InvalidDataException("Unsupported firmware package format.");
    FirmwareVersion.Parse(h[2]); FirmwareVersion.Parse(h[4]);
    var notes = Read("release-notes.md",32768);
    if (Hash(notes) != h[7]) throw new InvalidDataException("Release notes failed verification.");
    var images = new List<FirmwareImage>();
    foreach (var line in lines.Skip(1)) {
      var f = line.Split('|');
      if (f.Length != 7 || f.Skip(3).Any(h => !Regex.IsMatch(h, "^[0-9a-f]{64}$")) || !Regex.IsMatch(f[0], @"^esp32-(st7735-r1|ili9341-r[0-3])$") || f[1] != "ota-v1" ||
          !int.TryParse(f[2],NumberStyles.None,CultureInfo.InvariantCulture,out var length) || length is < 256 or > 1572864 ||
          images.Any(i => i.Variant == f[0])) throw new InvalidDataException("Unsupported or ambiguous firmware variant.");
      var bytes = Read($"images/{f[0]}.bin",1572864);
      if (bytes.Length != length || bytes[0] != 0xe9 || bytes[12] != 0 || bytes[13] != 0 || Hash(bytes) != f[3])
        throw new InvalidDataException("Firmware image failed validation.");
      var marker = Encoding.ASCII.GetBytes($"HZFW1|{h[2]}|{h[3]}|{f[0]}|ota-v1");
      if (bytes.AsSpan().IndexOf(marker) < 0) throw new InvalidDataException("Image build metadata does not match the package.");
      images.Add(new(f[0],f[1],bytes,f[3],f[4],f[5],f[6]));
    }
    if (images.Count == 0 || entries.Count != images.Count + 3) throw new InvalidDataException("Unexpected package contents.");
    return new() { Manifest=manifest, Signature=signature, Version=h[2], Build=h[3], MinimumClient=h[4], Bootstrap=1,
      ReleaseNotes=new UTF8Encoding(false,true).GetString(notes), Images=images };
  }
  public FirmwareImage Select(FirmwareInfo info) {
    if (!info.CanUpdate || info.Bootstrap < Bootstrap || info.BootState != "CONFIRMED") throw new InvalidDataException("USB setup or boot confirmation is required before updating.");
    if (FirmwareVersion.Parse(MinimumClient) > FirmwareVersion.Parse(ClientVersion)) throw new InvalidDataException("Update the desktop client first.");
    if (FirmwareVersion.Parse(Version) <= FirmwareVersion.Parse(info.Version)) throw new InvalidDataException("This version is already installed or older. Downgrades are not supported.");
    return Images.SingleOrDefault(i => i.Variant == info.Variant && i.Layout == info.Layout && i.Bytes.Length <= info.SlotSize)
      ?? throw new InvalidDataException("This package does not match the terminal hardware or flash layout.");
  }
  public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
