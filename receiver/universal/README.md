# Bathroom Sync Universal UI

This is the shared Avalonia desktop shell for the next receiver client. It
runs on macOS and Windows from the same XAML/C# UI. At this stage it uses a
simulated terminal so UI behavior can be developed without Bluetooth hardware.

Run it on a machine with the .NET 8 SDK:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The shared protocol and SQLite storage remain in `receiver/windows/BathroomSync.Core`.
The production Windows RFCOMM transport will be attached after UI parity is
established; the existing WinForms receiver remains the production client in
the meantime.
