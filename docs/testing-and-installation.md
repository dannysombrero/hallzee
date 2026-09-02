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
bash "$HOME/Downloads/hallzee-mono-main/scripts/flash-terminal-macos.sh"
```

The script downloads Arduino CLI, ESP32 board support, and required libraries,
then builds and flashes the terminal. It automatically selects the ESP32 when
it is the only USB serial device connected. If more than one is connected,
unplug the others and run the same command again.

### Flash the terminal from Windows

After downloading and unzipping the project, plug in the ESP32, open
**PowerShell**, and run:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\hallzee-mono-main\scripts\flash-terminal-windows.ps1"
```

The same requirements apply: use a USB **data** cable, connect only one USB
serial device, and expect the script to replace the firmware on that ESP32.
It installs the Arduino tools, board support, and libraries automatically.

### Build the Windows desktop app from a Windows PC

Open **PowerShell**, paste this one command, and press Enter:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\hallzee-mono-main\scripts\build-windows-client.ps1"
```

The script downloads the .NET 8 build tools, then creates the Windows app in
the downloaded project’s `artifacts\BathroomSync-Windows` folder. Open that
folder and run `HallzeeSync.exe`.

### Easier: download the latest ready-to-run Windows app

Once the **Publish Latest Windows Sync App** GitHub Action has run, download
the latest ready-to-run ZIP here:

```text
https://github.com/dannysombrero/hallzee-mono/releases/download/windows-client-latest/BathroomSync-Windows.zip
```

Extract the entire ZIP to a normal folder, then run `HallzeeSync.exe`.
No software installation or local build is needed. If the link has not been
published yet, open the repository’s **Actions** tab, run **Publish Latest
Windows Sync App**, then refresh this link when the run completes. The same ZIP
is also available from that workflow run’s **Artifacts** section.

> The Windows build and physical Bluetooth sync require a Windows PC. The Mac
> command flashes the terminal, but does not verify Windows Bluetooth discovery.

## Choose the right way to check a change

| What changed? | Best place to check it | Windows PC needed? |
| --- | --- | --- |
| Desktop layout, labels, simulated discovery, sync status, or export flow | Browser preview or the Avalonia app on a Mac | No |
| Shared sync logic, CSV storage, or protocol parsing | Automated .NET tests | No for the hardware-independent tests |
| Terminal display, keypad behavior, and pass lifecycle | Display emulator or Wokwi | No for the emulator; a physical terminal is recommended before release |
| Firmware build | Local development machine with the ESP32 Arduino tools | No |
| Finding, reconnecting to, or directly syncing a physical Bluetooth terminal | Shared Avalonia desktop client and the physical ESP32 terminal | No — Bluetooth LE is fully supported natively on both macOS and Windows |
| Windows installer or Windows-only operating-system behavior | Windows desktop client | **Yes** |

Mac testing using the Universal desktop client now fully supports physical Bluetooth LE discovery and data transfer to the ESP32 terminal. A Windows PC is only needed to test Windows-specific packaging or installation behaviors.

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

Mac testing is sufficient for the simulated browser flow and physical Bluetooth LE interactions via the desktop client. A Windows PC is no longer strictly required for basic Bluetooth testing.

### Shared Avalonia desktop client

Install the .NET 8 SDK. If you are developing on a Mac, you must also install Xcode from the App Store and the .NET macOS workload to support native Bluetooth LE:

```sh
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo dotnet workload install macos
```

Then run the client:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The Universal client connects to the physical ESP32 terminal via Bluetooth LE on both macOS and Windows.
On macOS, Connect & Sync waits until CoreBluetooth confirms that terminal
notifications are enabled before sending any sync commands. If this readiness
handshake does not complete within 15 seconds, the app reports a connection
failure instead of silently dropping the first commands.
On Windows, Connect & Sync records whether the terminal advertises with a Random
or Public BLE address type and applies a 15-second handshake timeout with automatic
address-type fallback before establishing GATT subscriptions.

### Firmware and display behavior

Install the ESP32 Arduino core plus the Adafruit GFX, Adafruit ST7735/ST7789,
and Keypad libraries. From the repository root, build the firmware with:

```sh
arduino-cli compile --fqbn esp32:esp32:esp32 .
```

For keypad and display-flow checks, open `display-emulator.html` in a browser
or use the Wokwi setup described in [WOKWI.md](../WOKWI.md).

## Windows / Mac Bluetooth verification

Use a Mac or Windows PC to test actual Bluetooth behavior. Install the .NET 8 SDK (along with Xcode if on Mac as documented above), make
sure the ESP32 terminal is powered on, then use the desktop app to find and
sync `Hallzee`. BLE discovery and sync do not require a pairing PIN.

Before calling a Windows change complete, check:

1. The terminal can be found within five seconds.
2. The client subscribes and receives `HALLZEE_READY,1`.
3. A second sync with no new trips completes without replaying history.
4. A sync with new trips transfers only IDs after the local durable cursor.
5. Disconnecting mid-transfer and reconnecting produces one durable copy.
6. Powering the kiosk off clears the client status; Find terminal works after it returns.

### Verify policy rules and bell schedule editing

1. In **Hall Pass Policies & Schedule**, confirm number inputs (student limits, pass counts, grace windows) only display integer numbers without decimal fractions when stepping up and down.
2. Under **Bell-Time Windows**, select an alert sound and click **Play Preview** to hear the tone.
3. Under **Bell Schedule Periods**, add a period, select day checkboxes (Mon–Fri), adjust times, and click the checkmark to save; confirm the row switches to a compact summary with edit (pencil) and delete (trashcan) controls.
4. Confirm that trip durations display in minutes and seconds (e.g., `0m 45s`, `5m 30s`) across dashboard activity and the trips history modal, and include hours if duration exceeds 60 minutes.
5. In **Classroom Roster & Students**, confirm the class selection, enrollment count badge, import button, new class input, and student list render cleanly with no overlapping controls.
6. On the dashboard, verify the top terminal strip is removed; when pass status is unknown, the active pass card displays the Sync Now / Connect action.
7. While a pass is available, click **Manual Check In** to assign a student by Name or Student ID with optional Reason and Location; verify the live timer starts, and clicking **Check In** stores the completed trip with duration.
8. In **Trip History Log**, confirm the table matches the Student Roster styling (clean column headers, student icons, stacked out/in times, duration, status pills, and friendly empty state), and the export button is labeled **Export**.
9. In the sidebar, confirm **Terminal Settings** is removed and **Settings** is present; opening **Settings** provides **Profile** and **Device** submenu tabs.
10. In **Policies & Bell Times**, confirm the Profile toolbar (active profile dropdown, status, save button, and add profile bar) is located at the top of the modal.
11. In **Trip History Log**, confirm **TIME OUT** and **TIME IN** are rendered in separate columns with a compact **DURATION** column, and clicking column headers (**STUDENT**, **DATE**, **TIME OUT**, **TIME IN**, **DURATION**, **STATUS**) toggles sorting with directional arrow indicators (`▲`/`▼`).
12. In **Classroom Roster & Students**, confirm clicking column headers (**STUDENT**, **GRADE**, **PERIOD**) sorts the roster list with directional indicators.
13. Click **Export** on the dashboard and in the Trip History modal; confirm a native file-save dialog opens to choose the file location, and exporting from the dashboard saves the file directly without opening the Trip History modal.

### Verify the student-ID limit

This feature requires both newly flashed firmware and the updated Windows
client. Mac tests and CI cover validation, persistence logic, protocol
fragmentation, and rendering, but a Windows PC is required to verify the real
BLE settings write.

1. Find Hallzee and run a normal sync; confirm the client shows the kiosk's
   current maximum ID length.
2. Choose 8 and select **Apply ID Limit**; confirm the success message.
3. On Hallzee, enter eight digits and confirm a ninth digit is ignored.
4. Restart Hallzee, sync again, and confirm the client still reports 8.
5. While an ID longer than a proposed new limit is checked out, confirm Hallzee
   rejects the shorter limit until that student is checked in or the pass is
   reset.

## Automated checks

These checks do not require a Windows PC or connected ESP32 terminal:

```sh
make -C test coverage
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj
```

## Continuous integration (CI) workflows

The repository maintains automated GitHub Actions workflows to validate pull requests and publish builds:

| Workflow | Triggers | Platform / Steps | Purpose |
| :--- | :--- | :--- | :--- |
| **Validate Firmware and BLE Protocol** (`validate-firmware.yml`) | `pull_request`, `push` (paths: `*.ino`, `*.cpp`, `*.h`, `test/**`), `workflow_dispatch` | macOS (native unit tests & coverage) + Ubuntu (ESP32 Arduino compilation) | Validates firmware builds and BLE protocol tests automatically on changes. |
| **Build Universal Sync Client** (`build-universal-client.yml`) | `pull_request`, `push` (paths: `receiver/**`), `workflow_dispatch` | macOS & Windows (runs .NET 8 unit tests, builds universal client, uploads Windows artifact) | Ensures cross-platform client builds and test suites pass on both operating systems. |
| **Publish Latest Windows Sync App** (`publish-latest-windows-client.yml`) | `push` on `main` (paths: `receiver/windows/**`), `workflow_dispatch` | Windows (tests, publishes, packages, and releases latest executable) | Automatically releases the standalone Windows client for easy contributor download. |
| **Build Windows Sync App (Legacy)** (`build-windows-sync.yml`) | `push` on `windows-sync-client`, `workflow_dispatch` | Windows (.NET 8 test and artifact generation) | Targeted validation for the standalone Windows client branch. |
| **Regenerate Preview Lockfile & Tests** (`regenerate-lock-and-test.yml`) | `push` on `refactor/react-client-shell`, `workflow_dispatch` | Ubuntu (Node.js 22 install, lint, and test) | Keeps the web preview prototype dependencies locked and tested. |

## Documentation rule

Whenever a change affects setup, installation, hardware, the testing process,
or platform support, update this page in the same change. Every request for
hands-on testing must say whether Mac testing is enough or a Windows PC is
required, and why.
