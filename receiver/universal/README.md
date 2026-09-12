# Hallzee Sync Universal UI

This is the shared Avalonia desktop shell for the receiver client. It
runs on macOS and Windows from the same XAML/C# UI. On macOS it uses the
CoreBluetooth helper in `receiver/MacBLEAgent`; on Windows it uses the direct
Bluetooth transport. Physical clients use the v2 terminal identity and
authorization handshake before syncing through the shared protocol and SQLite
store. Preview mode uses the simulated v1 terminal.

For an unclaimed physical terminal, hold `*` and `#` for five seconds and
release. Choose **Find Nearby Terminals**, select its stable name, then enter
the displayed six-digit code in Hallzee's pairing dialog. Current firmware uses
encrypted Just Works bonding without an OS passkey. Known ownership appears
inside Hallzee as **Currently Paired**, **Not Paired**, or **Paired to other
device**. An active checkout does not prevent its saved owner from reconnecting
without a code or pairing mode. Other clients need explicit owner release/reset
before making a new claim. See [setup and testing](../../docs/testing-and-installation.md).
The **Mini Window** top-bar action opens a compact always-on-top companion
widget. It displays the current period range, live digital clock, prominent
pass status indicator (**PASSES CLOSED**, **PASS AVAILABLE**, or **PASS UNAVAILABLE**
when a pass is in use), and live period countdowns.
After a successful connection, an unexpected Bluetooth drop automatically
retries the same terminal using its owner credential and resumes sync without
asking for the passkey. Production builds persist that credential in the platform credential store
so reconnect also works after an app restart.

Run it on a machine with the .NET 8 SDK:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The shared protocol and SQLite storage remain in `receiver/windows/BathroomSync.Core`.
The existing WinForms receiver remains available while the Avalonia client is
validated in classrooms.

Versioned Windows and Mac packages, including the native Mac Bluetooth helper,
are built with **Build Desktop Release Packages** and promoted with **Publish
Tested Release**. See [release instructions](../../docs/releasing.md) and the
[teacher guide](../../docs/getting-started-users.md). **Help** and **Updates** are
available in the sidebar; the top bar displays the actual build version.
