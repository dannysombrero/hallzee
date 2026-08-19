# Bathroom Terminal Mac Receiver

Build the receiver from this directory:

```sh
zsh build.sh
```

Start the receiver while `Bathroom-Terminal` is powered and no Android serial
app is connected. It uses an existing macOS pairing when available, otherwise
searches nearby devices and opens the ESP32 serial service directly. If macOS
asks for a pairing code, enter `1234`. The receiver sends the Mac's local time
and writes acknowledged records to an open CSV file:

```sh
./bathroom-receiver
```

Optional arguments are the paired Bluetooth device name and CSV output path:

```sh
./bathroom-receiver "Bathroom-Terminal" "/Users/danny/Documents/bathroom_trips.csv"
```

The receiver appends a row only once per trip ID. It acknowledges duplicate
retransmissions safely, so a disconnect after CSV writing cannot create a
duplicate row. Each connection requests the terminal's full trip history after
the normal sync; the CSV adds only records it does not already contain.

## Desktop Sync App

For everyday use, build and open the native Mac app:

    cd gui
    zsh build-gui.sh
    open "../Bathroom Sync.app"

It provides a **Sync Now** button, live connection/sync status, a session trip
count, a diagnostic log, and **Open CSV**. Its CSV is stored in the user's
Documents/Bathroom Terminal folder.

## Windows receiver development

The Windows WinForms app is in `windows/`. Its Bluetooth serial transport and
UI stay in the app project, while `windows/BathroomSync.Core` contains the
deterministic line protocol and CSV persistence logic. Run its tests from the
repository root on a machine with .NET 8 installed:

```sh
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
```

For normal use, turn on the terminal, choose **Find Terminal**, select
`Bathroom-Terminal`, and choose **Sync Now**. The app handles pairing (PIN
`1234` when requested) and connects directly to the Bluetooth SPP service;
there is no COM-port selection step.

When downloading a Windows build artifact, extract the entire ZIP to a normal
folder before running `BathroomSync.Windows.exe`. If the app cannot start, it
shows the error and saves diagnostics to
`%LOCALAPPDATA%\\Bathroom Terminal\\startup-errors.log`.
