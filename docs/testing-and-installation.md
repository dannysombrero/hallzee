# Installation and testing guide

This guide explains which checks can be done on a Mac and which require a
Windows PC.

## First day: start from nothing

You do not need Git, Arduino, .NET, or any project libraries installed in
advance. First open the project on GitHub, choose **Code → Download ZIP**, and
unzip it in your Downloads folder. Plug the ESP32 terminal into your computer
before using the Mac command below. The commands download the required build
tools automatically.

### Flash the terminal from a Mac

Open **Terminal**, paste this one command, and press Return:

```sh
bash "$HOME/Downloads/bathroom-signin-codex-web-preview-site/scripts/flash-terminal-macos.sh"
```

The script downloads Arduino CLI, ESP32 board support, and required libraries,
then builds and flashes the terminal. It automatically selects the ESP32 when
it is the only USB serial device connected. If more than one is connected,
unplug the others and run the same command again.

### Flash the terminal from Windows

After downloading and unzipping the project, plug in the ESP32, open
**PowerShell**, and run:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\bathroom-signin-codex-web-preview-site\scripts\flash-terminal-windows.ps1"
```

The same requirements apply: use a USB **data** cable, connect only one USB
serial device, and expect the script to replace the firmware on that ESP32.
It installs the Arduino tools, board support, and libraries automatically.

### Build the Windows desktop app from a Windows PC

Open **PowerShell**, paste this one command, and press Enter:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\bathroom-signin-codex-web-preview-site\scripts\build-windows-client.ps1"
```

The script downloads the .NET 8 build tools, then creates the Windows app in
the downloaded project’s `artifacts\BathroomSync-Windows` folder. Open that
folder and run `BathroomSync.Windows.exe`.

### Easier: download the latest ready-to-run Windows app

Once the **Publish Latest Windows Sync App** GitHub Action has run, download
the latest ready-to-run ZIP here:

```text
https://github.com/dannysombrero/bathroom-signin/releases/download/windows-client-latest/BathroomSync-Windows.zip
```

Extract the entire ZIP to a normal folder, then run `BathroomSync.Windows.exe`.
No software installation or local build is needed. If the link has not been
published yet, open the repository’s **Actions** tab, run **Publish Latest
Windows Sync App**, then refresh this link when the run completes. The same ZIP
is also available from that workflow run’s **Artifacts** section.

> The Windows build and physical Bluetooth sync require a Windows PC. The Mac
> command flashes the terminal, but does not verify Windows Bluetooth pairing.

## Choose the right way to check a change

| What changed? | Best place to check it | Windows PC needed? |
| --- | --- | --- |
| Desktop layout, labels, simulated discovery, sync status, or export flow | Browser preview or the Avalonia app on a Mac | No |
| Shared sync logic, CSV storage, or protocol parsing | Automated .NET tests | No for the hardware-independent tests |
| Terminal display, keypad behavior, and pass lifecycle | Display emulator or Wokwi | No for the emulator; a physical terminal is recommended before release |
| Firmware build | Local development machine with the ESP32 Arduino tools | No |
| Finding, reconnecting to, or directly syncing a physical Bluetooth terminal | Windows desktop client and the physical ESP32 terminal | **Yes** — this uses the Windows Bluetooth transport |
| Windows installer or Windows-only operating-system behavior | Windows desktop client | **Yes** |

Mac testing confirms the shared interface and simulated flow. It does **not**
confirm that a Windows PC can pair with or transfer data from a physical
terminal.

## Quick checks on a Mac

### Browser preview

The browser preview reproduces the desktop client’s main flow with a simulated
terminal. Use it to check the screens, device-discovery journey, sync activity,
trip count, and export interactions. Its Windows-testing notice identifies the
features that cannot be verified in a browser.

Install [Node.js 22.13 or later](https://nodejs.org/) once. From the repository
root, run this single command to install the preview's locked dependencies and
start it:

```sh
npm --prefix preview-site start
```

Then open <http://localhost:3000> in a browser. Leave the terminal window open
while using the preview; press `Ctrl+C` in that window when you are done.

After the first run, you can start the preview more quickly with:

```sh
npm --prefix preview-site run dev
```

Mac testing is sufficient for the simulated browser flow. A Windows PC is only
required to test Bluetooth pairing or a transfer with a physical terminal.

### Shared Avalonia desktop client

Install the .NET 8 SDK, then run:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

On macOS this starts in preview mode with a simulated terminal; Bluetooth
hardware is not used.

### Firmware and display behavior

Install the ESP32 Arduino core plus the Adafruit GFX, Adafruit ST7735/ST7789,
and Keypad libraries. From the repository root, build the firmware with:

```sh
arduino-cli compile --fqbn esp32:esp32:esp32 .
```

For keypad and display-flow checks, open `display-emulator.html` in a browser
or use the Wokwi setup described in [WOKWI.md](../WOKWI.md).

## Windows verification

Use a Windows PC only when the change involves actual Bluetooth behavior or a
Windows package. Install the .NET 8 SDK or use the provided Windows build, make
sure the ESP32 terminal is powered on, then use the desktop app to find and
sync `Hallzee`. Accept Windows pairing confirmation if it appears.

The terminal’s configured pairing PIN is `1234`.

Before calling a Windows change complete, check:

1. The terminal can be found within five seconds.
2. The client subscribes and receives `HALLZEE_READY,1`.
3. A second sync with no new trips completes without replaying history.
4. A sync with new trips transfers only IDs after the local durable cursor.
5. Disconnecting mid-transfer and reconnecting produces one durable copy.
6. Powering the kiosk off clears the client status; Find terminal works after it returns.

## Automated checks

These checks do not require a Windows PC or connected ESP32 terminal:

```sh
make -C test coverage
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj
```

## Documentation rule

Whenever a change affects setup, installation, hardware, the testing process,
or platform support, update this page in the same change. Every request for
hands-on testing must say whether Mac testing is enough or a Windows PC is
required, and why.
