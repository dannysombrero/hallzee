using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace BathroomSync.Universal.Services;

internal static class MacApplicationIcon {
  // Window.Icon is ignored on macOS. Set the process icon as well as shipping
  // the bundle's ICNS so launches through dotnet/IDE have the same Dock icon.
  public static void Apply() {
    if (!OperatingSystem.IsMacOS()) return;

    using var source = AssetLoader.Open(new Uri("avares://Hallzee/Assets/hallzee.icns"));
    using var buffer = new MemoryStream();
    source.CopyTo(buffer);
    var bytes = buffer.ToArray();
    var data = CreateData(GetClass("NSData"), Selector("dataWithBytes:length:"), bytes, (nuint)bytes.Length);
    var image = Send(Send(GetClass("NSImage"), Selector("alloc")), Selector("initWithData:"), data);
    if (image == IntPtr.Zero) return;
    try {
      var app = Send(GetClass("NSApplication"), Selector("sharedApplication"));
      SendVoid(app, Selector("setApplicationIconImage:"), image);
    } finally {
      Release(image, Selector("release"));
    }
  }

  const string ObjC = "/usr/lib/libobjc.A.dylib";

  [DllImport(ObjC, EntryPoint = "objc_getClass")]
  static extern IntPtr GetClass(string name);
  [DllImport(ObjC, EntryPoint = "sel_registerName")]
  static extern IntPtr Selector(string name);
  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  static extern IntPtr Send(IntPtr receiver, IntPtr selector);
  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr value);
  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  static extern IntPtr CreateData(IntPtr receiver, IntPtr selector, byte[] bytes, nuint length);
  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  static extern void SendVoid(IntPtr receiver, IntPtr selector, IntPtr value);
  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  static extern void Release(IntPtr receiver, IntPtr selector);
}
