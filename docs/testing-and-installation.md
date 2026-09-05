# Installation and testing guide

This guide explains which checks can be done on a Mac and which require a
Windows PC.

## Automatic reconnect verification

After completing the first Bluetooth claim on a Windows PC, close and reopen
the Universal app with the terminal powered on. It should reconnect to the
remembered terminal and sync without showing the date/time or physical pairing
flow. While connected, power-cycle or move the terminal out of range, then
restore it; the app should retry the remembered device, show a 10-second
countdown, and fall back to manual recovery if it cannot reconnect. It should
fall back to a BLE scan if the transport address changed.

Mac testing is sufficient for the shared reconnect state-machine and UI tests.
A Windows PC is required for the Windows-specific WinRT BLE direct reconnect,
bond reuse, and discovery fallback. Those Windows behaviors have not yet been
verified on physical hardware.

## Owner-storage recovery

The current firmware stores the terminal owner record in LittleFS so it can
survive an unavailable ESP32 NVS namespace. After flashing, claim the terminal
once through the desktop app and confirm the Serial Monitor shows
`Terminal owner: CLAIMED` after a reset. A full-flash erase removes the
LittleFS trip log and terminal settings, so export or record any needed data
before enabling **Erase All Flash Before Sketch Upload** in Arduino IDE.

## First day: start from nothing

You do not need Git, Arduino, .NET, or any project libraries installed in
advance. First open the project on GitHub, choose **Code → Download ZIP**, and
unzip it in your Downloads folder. Plug the ESP32 terminal into your computer
before using the Mac command below. The commands download the required build
tools automatically.

## Hardware you should have

The current terminal build assumes this exact hardware profile:

- 1 original ESP32 DevKit V1/WROOM-32 development board with BLE
- 1 3×4 membrane matrix keypad with seven pins labelled `R1`–`R4` and
  `C1`–`C3`
- 1 SPI TFT module, either the default ST7735 160×128 module or the red
  ILI9341 240×320 module
- Dupont jumper wires and a USB data cable
- A USB power source or computer USB port

The firmware does not support an ESP32-C3, ESP32-S2, or ESP32-S3 profile at
this time. Confirm the board family before wiring a new batch. The TFT and
keypad use 3.3 V ESP32 logic; do not feed 5 V into a signal pin.

### ESP32 board requirements

The board must meet all of the following minimum requirements:

| Requirement | Minimum / expected value | Why it matters |
| --- | --- | --- |
| MCU | Original ESP32/WROOM-32 family using the `esp32:esp32` Arduino target | The firmware currently requires the original ESP32 Bluetooth stack and board definition |
| Bluetooth | Bluetooth Low Energy peripheral support with the ESP32 Bluedroid stack | The terminal advertises a secure BLE GATT service and supports passkey/bonding |
| Available GPIO | At least 12 freely usable GPIOs in addition to the USB-UART connection | Seven GPIOs scan the keypad and five drive the TFT |
| Flash | 4 MB or more | The firmware uses program flash plus NVS and LittleFS trip-log storage |
| RAM | Standard original ESP32 SRAM; no PSRAM is required | The current firmware does not depend on external PSRAM |
| Logic level | 3.3 V GPIO and 3.3 V-compatible peripherals | The keypad and TFT signals connect directly to ESP32 GPIOs |
| USB programming | USB data connection, USB-UART bridge, and bootloader/reset support | The platform flashers upload through the board's serial bootloader |
| 3.3 V supply | A regulated 3.3 V rail capable of powering the ESP32 and TFT backlight | An underpowered rail causes resets, upload failures, or display corruption |

The current known-good board is the common **ESP32 DevKit V1 with ESP-WROOM-32
module and 4 MB flash**. The exact pin numbers matter: the board must expose
GPIO 5, 13, 14, 18, 21, 22, 23, 25, 26, 27, 32, and 33. Do not count the
`VIN`, `3V3`, `GND`, or USB-UART pins as usable signal GPIOs. GPIO 1 and 3 are
reserved for the USB serial console/upload path.

Do not substitute an ESP32-C3, ESP32-S2, ESP32-S3, ESP32-H2, an Arduino Nano,
or a generic Bluetooth-only serial board without first adding a new board
port. Boards with fewer than 4 MB flash, no USB-UART/bootloader path, or a
3.3 V rail that cannot power the TFT are also not supported by this build.

### Physical wiring

Wire by the labels printed on the modules. The keypad has no VCC or GND wire:
it is a passive switch matrix powered and scanned by the ESP32 GPIOs.

| Part pin | Connect to ESP32 DevKit V1 | Notes |
| --- | ---: | --- |
| Keypad `R1` | GPIO 32 | Row 1 |
| Keypad `R2` | GPIO 33 | Row 2 |
| Keypad `R3` | GPIO 25 | Row 3 |
| Keypad `R4` | GPIO 26 | Row 4 |
| Keypad `C1` | GPIO 27 | Column 1 |
| Keypad `C2` | GPIO 14 | Column 2 |
| Keypad `C3` | GPIO 13 | Column 3 |
| TFT `VCC` / `VIN` | 3V3 | Use 3.3 V for this project |
| TFT `GND` | GND | Common ground is required |
| TFT `CS` | GPIO 5 | Chip select |
| TFT `RST` / `RESET` | GPIO 22 | Hardware reset |
| TFT `DC` / `A0` | GPIO 21 | Data/command |
| TFT `MOSI` / `SDA` | GPIO 23 | SPI data into display |
| TFT `SCLK` / `SCK` / `CLK` | GPIO 18 | SPI clock |
| TFT `LED` / `BL` | 3V3, if the module requires it | Backlight only; follow the module's label |
| TFT `MISO` / `SDO` | Leave unconnected | The firmware does not read display data |

The ST7735 and ILI9341 use the same ESP32 connections. Do not connect the
display's `MISO` just because it is present, and do not connect keypad wires to
the TFT header. Keep SPI and keypad wires short while bringing up a new
terminal; the ILI9341 profile is limited to a 20 MHz SPI clock for
jumper-wired modules.

Before applying power, check each wire end-to-end against the table, check for
adjacent-pin bridges, and make sure the TFT orientation does not conceal a
mislabelled header. A reversed TFT power connection can damage the module or
ESP32.

### Hardware bring-up checklist

1. Leave the TFT and keypad disconnected and connect the ESP32 to the computer
   with a known-good USB **data** cable. Confirm that a serial port appears.
2. Disconnect USB power, wire the keypad, and then wire the TFT using the table.
3. Select the firmware profile that matches the display: `st7735` for the
   160×128 module, `ili9341` for the red 240×320 module.
4. Flash the firmware using the Mac or Windows instructions below.
5. On first boot, set the clock on the keypad. The display prompts for month,
   day, year, hour, minute, and AM/PM; press `#` after each value.
6. Press a test ID of at least four digits followed by `#`. Enter the same ID
   and `#` again to check it back in. Confirm the display returns to available.
7. If the display is blank but the serial port works, disconnect power before
   checking `VCC`, `GND`, `CS`, `RST`, `DC`, `MOSI`, and `SCLK`.

Record the terminal's advertised `Hallzee-XXXX` name and physical board label
with the batch inventory. The terminal identity is generated and persisted by
the firmware; it is not derived from the USB port name.

### Flash the terminal from a Mac

Open **Terminal**, paste this one command, and press Return:

```sh
bash "$HOME/Downloads/hallzee-mono-main/scripts/flash-terminal-macos.sh"
```

The script downloads Arduino CLI, ESP32 board support, and required libraries,
then builds and flashes the terminal. It automatically selects the ESP32 when
it is the only USB serial device connected. If more than one is connected,
unplug the others and run the same command again.

For the red 240×320 ILI9341 module, run this instead:

```sh
bash "$HOME/Downloads/hallzee-mono-main/scripts/flash-terminal-macos.sh" --display ili9341
```

If the display is mounted upside down, add `--rotation 3`. Use the default
command for ST7735; do not flash the ILI9341 profile to an ST7735 module. If
macOS cannot identify the port automatically, list ports with `ls /dev/cu.*`,
then pass the matching path as the final argument, for example
`/dev/cu.usbserial-XXXX`.

### Flash the terminal from Windows

After downloading and unzipping the project, plug in the ESP32, open
**PowerShell**, and run:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\hallzee-mono-main\scripts\flash-terminal-windows.ps1"
```

The same requirements apply: use a USB **data** cable, connect only one USB
serial device, and expect the script to replace the firmware on that ESP32.
It installs the Arduino tools, board support, and libraries automatically.

For the red 240×320 ILI9341 module:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\hallzee-mono-main\scripts\flash-terminal-windows.ps1" -Display ili9341
```

If more than one serial device is listed, open **Device Manager → Ports
(COM & LPT)**, identify the ESP32's `COM` number, and supply it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\hallzee-mono-main\scripts\flash-terminal-windows.ps1" -Port COM5
```

Combine `-Port COM5 -Display ili9341` when needed. Add `-Rotation 3` when the
ILI9341 is mounted upside down. Windows may install a USB-UART driver after
the board is first connected; unplug/reconnect the board and rerun the same
command after that driver installation finishes. The flasher uses the
original ESP32 target and installs Arduino CLI, ESP32 core `3.3.11`, and all
four libraries from `libraries.txt` automatically.

### Flash without changing the board

To verify a clean-machine toolchain or a pull request without uploading,
append the compile-only option:

```sh
bash scripts/flash-terminal-macos.sh --compile-only
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1 -CompileOnly
```

This still needs no connected terminal and does not erase or replace firmware.

### Build the Windows desktop app from a Windows PC

Open **PowerShell**, paste this one command, and press Enter:

```powershell
powershell -ExecutionPolicy Bypass -File "$HOME\Downloads\hallzee-mono-main\scripts\build-windows-client.ps1"
```

The script downloads the .NET 8 build tools, then creates the Windows app in
the downloaded project’s `artifacts\BathroomSync-Windows` folder. Open that
folder and run `HallzeeSync.Universal.exe`.

### Download the latest ready-to-run Windows app

The latest ready-to-run Windows build is published as an artifact by the
**Build Universal Sync Client** GitHub Action:

1. Open the repository’s **Actions** tab and choose **Build Universal Sync
   Client**.
2. Open the latest successful run, or choose **Run workflow** and wait for it
   to finish.
3. Download the `HallzeeSync-Universal-Windows` artifact.
4. Extract the entire ZIP to a normal folder, then run
   `HallzeeSync.Universal.exe`.

No .NET installation or local build is needed. The artifact is a self-contained
Windows x64 app. The workflow validates a macOS build but does not currently
publish a ready-to-run Mac package. See [Getting started: normal users](getting-started-users.md)
for the user-facing pairing and operation steps.

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
starts only one pairing session. A kiosk with an active checkout advertises `INUSE`
and is shown as **In Use**. The remembered owner may reconnect and authenticate
without the pairing passkey; a different client cannot claim or connect to it.
Do not treat the advertised name or BLE address as proof of terminal identity.

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
12. With an active checkout, the terminal advertises `INUSE`; the remembered
    owner can still reconnect and check the student back in without a passkey,
    while an unrecognized client is rejected.

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
7. While a pass is available, click **Manual Check In**. Verify that the form cannot be submitted without a Student Name or Student ID, accepts either one or both identity fields, and treats Period, Destination, and Purpose as optional. Submit a student and verify the live timer starts; clicking **Check In** should store the completed trip with the entered name/ID and a **Manual** status.
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
