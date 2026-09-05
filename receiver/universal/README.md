# Hallzee Sync Universal UI

This is the shared Avalonia desktop shell for the next receiver client. It
runs on macOS and Windows from the same XAML/C# UI. On macOS it uses the
CoreBluetooth helper in `receiver/MacBLEAgent`; on Windows it uses the direct
Bluetooth transport. Physical clients use the v2 terminal identity and
authorization handshake before syncing through the shared protocol and SQLite
store. Preview mode uses the simulated v1 terminal.

For an unclaimed physical terminal, hold `*` and `#` for five seconds, enter
the displayed six-digit Bluetooth passkey in the Find Terminals dialog, and
connect again. A kiosk with an active checkout is shown as **In Use**; its
remembered owner can reconnect without the passkey, while other clients must
wait until it becomes available.
After a successful connection, an unexpected Bluetooth drop automatically
retries the same terminal using its owner credential and resumes sync without
asking for the passkey. The current v2 test path keeps that credential in
memory for the running process; OS credential-vault persistence is still a
follow-up task for reconnecting after an app restart.

Run it on a machine with the .NET 8 SDK:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The shared protocol and SQLite storage remain in `receiver/windows/BathroomSync.Core`.
The existing WinForms receiver remains available while the Avalonia client is
validated in classrooms.
