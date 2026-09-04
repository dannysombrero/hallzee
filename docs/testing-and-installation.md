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
folder and run `HallzeeSync.Universal.exe`.

### Easier: download the latest ready-to-run Windows app

Once the **Publish Latest Windows Sync App** GitHub Action has run, download
the latest ready-to-run ZIP here:

```text
https://github.com/dannysombrero/hallzee-mono/releases/download/windows-client-latest/BathroomSync-Windows.zip
```

Extract the entire ZIP to a normal folder, then run `HallzeeSync.Universal.exe`.
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

For the native ILI9341 UI, Mac testing is sufficient to inspect the 320×240
landscape rendering and physical-key instructions. A Windows PC is not
required; Windows-specific BLE adapter and packaging behavior remain
unverified by the display tests.

For this pairing/status change, Mac testing is sufficient to validate the
shared protocol and macOS BLE path, but it is not sufficient to verify the
Windows BLE adapter. A Windows PC is required to verify the Windows-specific
passkey prompt, `INUSE` discovery label, and Windows refusal to connect to an
occupied kiosk. Windows behavior has not yet been verified by automated tests
or by a second-terminal hardware run.

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
Physical clients use the v2 identity/authentication flow. For an unclaimed
terminal, hold `*` and `#` for five seconds before connecting, then enter the
displayed six-digit Bluetooth passkey in the Find Terminals dialog and choose
Connect again. On Windows, the client supplies those digits directly to the
authenticated pairing ceremony. On macOS, enter the same value if the operating
system also presents a Bluetooth passkey prompt.
Production builds store the owner credential in the platform secure store:
Windows PasswordVault and macOS Keychain. Preview builds and automated tests
use an in-memory store. Durable credentials are now available for passkey-free
reconnect after restarting the app or computer; BLE reconnect retry and startup
reconnect UI remain in progress.
On macOS, Connect & Sync waits until CoreBluetooth confirms that terminal
notifications are enabled before sending any sync commands. If this readiness
handshake does not complete within 15 seconds, the app reports a connection
failure instead of silently dropping the first commands.
Protected macOS writes are acknowledged and allow up to 60 seconds for the
operating-system passkey prompt to complete before reporting a write failure.
On Windows, Connect & Sync records whether the terminal advertises with a Random
or Public BLE address type and applies a 45-second connection/setup timeout per
address type with automatic fallback before establishing GATT subscriptions.
Owner reconnects retry transient device/service/notification failures up to
three times with fresh GATT objects and do not repeat a failed passkey ceremony.
Encrypted writes are acknowledged and allow up to 60 seconds for Windows pairing.

### Firmware and display behavior

The firmware is pinned to ESP32 Arduino core `3.3.11` and uses the Adafruit GFX,
Adafruit ST7735/ST7789, Adafruit ILI9341, and Keypad libraries. From the
repository root, compile and flash with the platform script (the script stages
the Arduino sketch under the required matching folder and `.ino` name):

```sh
bash scripts/flash-terminal-macos.sh
```

On Windows, use
`powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1`.
Pass `-Port COM5` when more than one serial device is attached. CI performs a
compile-only check without flashing.

The default profile is for the original ST7735 160×128 display. To test the
240×320 red ILI9341 module in horizontal landscape mode, select the alternate
profile:

```sh
bash scripts/flash-terminal-macos.sh --display ili9341 /dev/cu.usbserial-XXXX
```

On Windows, use `-Display ili9341 -Port COM5`. The ILI9341 profile rotates the
panel to landscape and renders its native 320×240 UI; terminal behavior and
keypad/BLE protocols are unchanged. Use `--rotation 3` on macOS or `-Rotation 3` on
Windows if the panel is mounted upside down. Rotations `0` and `2` are portrait
orientations. Do not flash this profile to an ST7735 module.

At power-on, the terminal shows the centered Hallzee logo briefly before the
date/time setup screen. The ILI9341 profile renders that setup screen at the
full native 320×240 size, including its keypad legend and input readout.
Its display bus is intentionally limited to 20 MHz to reduce corruption on
long or loosely connected SPI wiring. The native UI also uses embedded
FreeSans bitmap fonts rather than the built-in block font.

For keypad and display-flow checks, open `display-emulator.html` in a browser
or use the Wokwi setup described in [WOKWI.md](../WOKWI.md).

## Windows / Mac Bluetooth verification

Use a Mac or Windows PC to test actual Bluetooth behavior. Install the .NET 8 SDK (along with Xcode if on Mac as documented above), make
sure the ESP32 terminal is powered on, then use the desktop app to find and
sync `Hallzee-XXXX`. Initial ownership requires holding `*` and `#` on an
unclaimed, unoccupied terminal for five seconds, releasing both keys, and
entering the displayed six-digit Bluetooth passkey. One continuous key hold
starts only one pairing session. A kiosk with an active checkout advertises `INUSE`,
is shown as **In Use**, and cannot be selected for connection; use **Scan Again**
to refresh that status. Do not treat the advertised name or BLE address as
proof of terminal identity.

Before calling a Windows change complete, check:

1. The terminal can be found within five seconds.
2. The client subscribes, receives `IDENTITY,2` with `AVAILABLE` or `IN_USE`, and displays the terminal suffix and availability.
3. On a new terminal, the client cannot sync until the physical passkey flow
   completes and then reconnects using `AUTH_OK,2`.
4. A second sync with no new trips completes without replaying history.
5. A sync with new trips transfers only IDs after the local durable cursor.
6. Disconnecting mid-transfer and reconnecting produces one durable copy.
7. An unexpected drop shows **RECONNECTING**, reconnects to the last authenticated
   terminal without asking for the passkey, and resumes synchronization.
8. A second BLE central is rejected while the authorized client remains connected.
9. Powering the kiosk off shows **RECONNECTING**; restoring the kiosk lets the
   same owner reconnect, while **Scan Again** remains available.
10. After a successful claim, reboot the kiosk while it is on the clock-setup
    screen. The next `IDENTITY,2` must still report `CLAIMED`, and the same
    desktop must be able to authenticate without entering a passkey.
11. On macOS, reconnect after a brief BLE drop or reboot; the client should use
    the freshly scanned peripheral and complete owner authentication without
    the stale `Peer removed pairing information` failure.
    The terminal must not clear its BLE bond just because owner storage was
    temporarily unavailable during startup; bonds are cleared only by explicit
    pairing mode or owner reset.

If a test claim must be cleared, connect the USB serial monitor at 115200 baud
and send the line `OWNER_RESET`. Confirm `OWNER_RESET,OK`; this clears only the
stored application owner credential and preserves trips, settings, and terminal
identity. The terminal also clears its own stale BLE bonds automatically. The
operating system may still need its old Bluetooth pairing removed before
starting a fresh claim.

If a serial monitor is unavailable, the same recovery can be performed from the
keypad while the terminal is physically available. Confirm that no student is
checked out, then hold `*` and `#` together for 10 seconds, releasing both keys
when the reset screen appears. The terminal clears
only its owner state and briefly shows **OWNER RESET**. After it returns to the
idle screen, hold `*` and `#` together for five seconds, then release both keys
to enter pairing mode and display a new six-digit passkey. An active checkout deliberately blocks the
owner reset gesture. Remove the old operating-system Bluetooth pairing if the
client still cannot reconnect.

The owner-reset chord is also accepted while the terminal is on its automatic
clock-setup screen. The setup screen may not change while the keys are held;
continue holding until **OWNER RESET** appears.

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
14. On the dashboard, in the **Exceeded Time** card, click the orange duration threshold badge (e.g., `> 7m`); confirm a flyout opens with duration filter presets (`5m`, `7m`, `10m`, `15m`, `20m`, `30m`), an integer numeric stepper, and a **Reset to Policy Warning** action. Selecting a preset or entering a custom minute threshold immediately updates the badge and filters the list of students exceeding that threshold without overwriting default classroom policy rules.
15. In **Student Roster**, click **Import Roster (CSV)**; confirm the native file-open dialog appears smoothly on Windows and macOS without UI freezes. Select a CSV file (including one concurrently open in Microsoft Excel or another viewer); confirm the file is parsed asynchronously in the background, showing analysis status and opening the column mapping preview.


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

The workflows use the Node 24-compatible major versions of the standard GitHub
Actions (`checkout@v6`, `setup-dotnet@v5`, and `upload-artifact@v6`). GitHub-hosted
runners satisfy the required runner version automatically.

| Workflow | Triggers | Platform / Steps | Purpose |
| :--- | :--- | :--- | :--- |
| **Validate Firmware and BLE Protocol** (`validate-firmware.yml`) | `pull_request`, `push` (paths: `*.ino`, `*.cpp`, `*.h`, `test/**`), `workflow_dispatch` | macOS (native unit tests & coverage) + Ubuntu (ESP32 Arduino compilation) | Validates firmware builds and BLE protocol tests automatically on changes. |
| **Build Universal Sync Client** (`build-universal-client.yml`) | `pull_request`, `push` (paths: `receiver/**`), `workflow_dispatch` | macOS & Windows (runs .NET 8 unit tests, builds universal client, uploads Windows artifact) | Ensures cross-platform client builds and test suites pass on both operating systems. |
| **Publish Latest Windows Sync App** (`publish-latest-windows-client.yml`) | `push` on `main` (paths: `receiver/universal/**`, `receiver/windows/**`), `workflow_dispatch` | Windows (tests, publishes, packages, and releases the Universal v2 executable) | Automatically releases the current passkey-capable Windows client for easy contributor download. |
| **Build Windows Sync App** (`build-windows-sync.yml`) | `workflow_dispatch` | Windows (.NET 8 core/Universal tests and artifact generation) | Produces an on-demand Universal v2 Windows artifact. |
| **Regenerate Preview Lockfile & Tests** (`regenerate-lock-and-test.yml`) | `push` on `refactor/react-client-shell`, `workflow_dispatch` | Ubuntu (Node.js 22 install, lint, and test) | Keeps the web preview prototype dependencies locked and tested. |

## Documentation rule

Whenever a change affects setup, installation, hardware, the testing process,
or platform support, update this page in the same change. Every request for
hands-on testing must say whether Mac testing is enough or a Windows PC is
required, and why.
