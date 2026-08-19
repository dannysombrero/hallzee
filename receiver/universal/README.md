# Bathroom Sync Universal UI

This is the shared Avalonia desktop shell for the next receiver client. It
runs on macOS and Windows from the same XAML/C# UI. On macOS it uses a
simulated terminal so UI behavior can be developed without Bluetooth hardware.
On Windows, it uses the production direct-Bluetooth transport: it finds
`Bathroom-Terminal`, pairs when necessary, opens its Serial Port service, and
syncs through the shared protocol and SQLite store.

Run it on a machine with the .NET 8 SDK:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The shared protocol and SQLite storage remain in `receiver/windows/BathroomSync.Core`.
The existing WinForms receiver remains available while the Avalonia client is
validated in classrooms.
