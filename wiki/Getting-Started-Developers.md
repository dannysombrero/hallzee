# Getting started: developers

Use this page if you are changing Hallzee, flashing a terminal, assembling a
fresh hardware batch, or running tests. If you only need to operate the
finished app, use [Getting started: normal users](Getting-Started-Users.md).

## Start from a clean computer

Download the repository from GitHub with **Code → Download ZIP**, then unzip
it anywhere. Open Terminal (macOS) or PowerShell (Windows) in the **project
root**, the extracted folder containing `README.md`, `firmware/`, and
`scripts/`. All commands below use paths relative to that folder; no
particular download location or folder name is required. Git, Arduino CLI,
the ESP32 board package, libraries, and .NET do not need to be installed first
for the scripted workflows.

## Assemble and flash a terminal

The supported hardware is an original ESP32 DevKit V1/WROOM-32, a 3×4
membrane keypad, and either an ST7735 160×128 TFT or the red ILI9341 240×320
TFT. Use the complete [wiring table and bring-up checklist](Testing-and-Installation.md#physical-wiring).

The ESP32 must provide BLE peripheral mode through the original ESP32
Bluedroid stack, at least 12 usable signal GPIOs, 4 MB or more of flash,
3.3 V logic, and a USB-UART bootloader path. The known-good board is an
ESP32 DevKit V1 with an ESP-WROOM-32 module. See the full [ESP32 board
requirements](Testing-and-Installation.md#esp32-board-requirements) before
ordering substitutes; ESP32-C3/S2/S3/H2 boards are not supported by this
firmware profile.

From the repository root, connect one ESP32 with a USB **data** cable and run
the matching command.

### macOS

```sh
bash scripts/flash-terminal-macos.sh
```

For the red ILI9341 display:

```sh
bash scripts/flash-terminal-macos.sh --display ili9341
```

If macOS finds more than one serial device, unplug the others or pass the
matching `/dev/cu.*` path as the final argument. Add `--rotation 3` if the
ILI9341 is mounted upside down.

### Windows

Open PowerShell in the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1
```

For ILI9341, use `-Display ili9341`; if needed, specify the port with
`-Port COM5`. Add `-Rotation 3` for an upside-down ILI9341. The script installs
Arduino CLI, ESP32 core `3.3.11`, and the required libraries automatically.

To compile without uploading or connecting hardware:

```sh
bash scripts/flash-terminal-macos.sh --compile-only
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1 -CompileOnly
```

## Faster repeat USB flashes

Run the regular flash command above once to install tools and migrate the
terminal to the OTA partition layout. For subsequent development flashes:

```sh
bash scripts/flash-terminal-macos.sh --fast --display ili9341
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1 -Fast -Display ili9341
```

Omit the display option for ST7735 and retain the installed rotation. Fast mode
compiles current source using installed dependencies, checks the existing
bootloader and partition table, then writes/verifies the application and
boot-selection metadata. It skips dependency downloads, full-flash backups,
and filesystem migration; NVS and LittleFS are not written. Same-version
source builds are allowed. Compilation and serial transfer still take time.

Use the regular command for first setup or a compatibility-check failure.
Fast mode creates no fresh backup and does not provide the Bluetooth updater's
rollback protection. Sync important trips and finish active passes first;
after an interrupted write, retry USB before using the terminal. See
[firmware updates and fast USB limits](Firmware-Updates.md#local-testing-update-checks-and-repeat-usb-flashes).

Mac compilation and simulated fast-USB/migration tests passed. A Mac is
sufficient to check physical boot and data retention, including a fast flash
after a Bluetooth update. A Windows PC is required to verify PowerShell
`-Fast` forwarding, COM-port selection, and Windows esptool execution.
Physical fast-USB speed/retention and Windows behavior remain unverified.

## Designing for terminals moving between teachers

Desktop trips already store the authenticated terminal ID, and enriched CSV
exports include it. That identifies hardware, not the teacher who used it
when the trip began. Sync currently assigns classroom context from the active
workspace, and roster lookups use that workspace's students. Old trips can
therefore acquire the wrong classroom context or student name after a move.
Owner reset preserves trips and settings; it is not a data-handoff workflow.

The recommended next design is to capture an immutable assignment ID when each
pass starts, keep hardware identity separate from workspace ownership, and use
a storage epoch to prevent trip-number collisions after destructive resets.
A deliberate transfer flow should finish passes, verify archived history,
remove the previous classroom's terminal data, then provision the new teacher.
Legacy records must remain unassigned until explicitly reconciled; a new owner
must not receive another teacher's records merely because they claimed the
hardware.

These assignment-aware changes are **proposed, not implemented**. Read the
[terminal reassignment and record provenance design](Design-Terminal-Reassignment.md)
for the identity model, migration, handoff failure cases, future school-wide
sync considerations, and acceptance checks before changing storage or sync.

## Run the desktop client locally

Install the .NET 8 SDK. On macOS, install Xcode and enable the macOS workload:

```sh
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo dotnet workload install macos
```

Run the Universal client from the repository root:

```sh
dotnet run --project receiver/universal/BathroomSync.Universal.csproj
```

The physical client uses Bluetooth LE on macOS and Windows. On a new terminal,
hold `*` and `#` for five seconds and release. Choose **Find Nearby Terminals**,
select the terminal, then enter the six-digit code in Hallzee's pairing dialog.
Current firmware uses encrypted Just Works Bluetooth pairing without an OS
passkey. Returning owners reconnect without a code or pairing mode; see the
[main setup guide](Testing-and-Installation.md#pair-once-then-reconnect).

## Run tests

These checks do not require a Windows PC or connected ESP32:

```sh
make -C test coverage
dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
```

Mac testing is sufficient for firmware compilation, rendering, keypad flow,
shared logic, and the macOS BLE path. A Windows PC is required for the
Windows-specific packaged app, Windows BLE adapter behavior, and Windows
installer/OS behavior. Windows behavior has not been verified by Mac-only
testing.

For the full hardware map, troubleshooting, Bluetooth test matrix, and CI
details, see [Installation and testing](Testing-and-Installation.md).
