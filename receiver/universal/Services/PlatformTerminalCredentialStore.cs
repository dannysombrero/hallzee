using System.Diagnostics;
using System.Text;
using BathroomSync.Core;

namespace BathroomSync.Universal.Services;

public static class PlatformTerminalCredentialStoreFactory {
  public static ITerminalCredentialStore Create(bool previewMode) {
    if (previewMode) return new InMemoryTerminalCredentialStore();
#if WINDOWS_BLUETOOTH
    return new WindowsTerminalCredentialStore();
#else
    if (OperatingSystem.IsMacOS()) return new MacKeychainTerminalCredentialStore();
    return new InMemoryTerminalCredentialStore();
#endif
  }
}

#if WINDOWS_BLUETOOTH
sealed class WindowsTerminalCredentialStore : ITerminalCredentialStore {
  const string Resource = "Hallzee.TerminalOwnerKey";
  readonly Windows.Security.Credentials.PasswordVault vault = new();

  public bool TryGetOwnerKey(string terminalId, out byte[] ownerKey) {
    try {
      var credential = vault.Retrieve(Resource, Normalize(terminalId));
      credential.RetrievePassword();
      ownerKey = Convert.FromBase64String(credential.Password);
      if (ownerKey.Length == 32) return true;
    } catch { }
    ownerKey = Array.Empty<byte>();
    return false;
  }

  public void SaveOwnerKey(string terminalId, ReadOnlySpan<byte> ownerKey) {
    if (ownerKey.Length != 32) throw new ArgumentException("Owner key must be 32 bytes.", nameof(ownerKey));
    DeleteOwnerKey(terminalId);
    vault.Add(new Windows.Security.Credentials.PasswordCredential(
      Resource, Normalize(terminalId), Convert.ToBase64String(ownerKey)));
  }

  public void DeleteOwnerKey(string terminalId) {
    try {
      var credential = vault.Retrieve(Resource, Normalize(terminalId));
      vault.Remove(credential);
    } catch { }
  }

  static string Normalize(string terminalId) => TerminalIdentityProtocol.NormalizeTerminalId(terminalId);
}
#endif

sealed class MacKeychainTerminalCredentialStore : ITerminalCredentialStore {
  const string Service = "com.hallzee.desktop.owner";

  public bool TryGetOwnerKey(string terminalId, out byte[] ownerKey) {
    var result = RunSecurity("find-generic-password", "-a", Normalize(terminalId), "-s", Service, "-w");
    try {
      ownerKey = Convert.FromBase64String(result.Trim());
      if (ownerKey.Length == 32) return true;
    } catch { }
    ownerKey = Array.Empty<byte>();
    return false;
  }

  public void SaveOwnerKey(string terminalId, ReadOnlySpan<byte> ownerKey) {
    if (ownerKey.Length != 32) throw new ArgumentException("Owner key must be 32 bytes.", nameof(ownerKey));
    DeleteOwnerKey(terminalId);
    RunSecurity("add-generic-password", "-a", Normalize(terminalId), "-s", Service,
      "-w", Convert.ToBase64String(ownerKey), "-U");
  }

  public void DeleteOwnerKey(string terminalId) {
    RunSecurity("delete-generic-password", "-a", Normalize(terminalId), "-s", Service);
  }

  static string Normalize(string terminalId) => TerminalIdentityProtocol.NormalizeTerminalId(terminalId);

  static string RunSecurity(params string[] arguments) {
    using var process = new Process {
      StartInfo = new ProcessStartInfo("/usr/bin/security") {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      }
    };
    foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    process.Start();
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode == 0 ? output : "";
  }
}
