# <img src="docs/images/hallzee-logo.png" alt="Hallzee Logo" width="48" style="vertical-align: middle; margin-right: 8px;" /> Hallzee

Hallzee is an ESP32-based school bathroom sign-in terminal. Students
enter an ID on a 3×4 keypad to check out and back in. The terminal stores trip
records locally and can synchronize them to a desktop receiver over Bluetooth Low Energy (BLE).

## Project layout

| Path | Purpose |
| --- | --- |
| `bathroom-signin.ino` | Firmware composition, UI/controller callbacks, `setup()`, and `loop()` |
| `Config.h` | Product constants, pin assignments, and display palette |
| `TerminalController.*` | Active-pass state, check-in/out, reset, and persistence decisions |
| `BellPolicy.*` | Atomic offline bell-window cache and checkout evaluation |
| `ClockService.*` | System time setting, formatting, and date validation |
| `BluetoothSync.*` | BLE GATT connection and incremental record-sync protocol |
| `KeypadController.*` | Keypad events and the `* + #` reset gesture |
| `TerminalDisplay.*` | TFT rendering only |
| `TripStorage.*` | ESP32 Preferences and LittleFS trip-log persistence |
| `receiver/windows/` | Current Windows sync application |
| `receiver/universal/` | Current shared Avalonia desktop UI for Windows and macOS |
| `receiver/` | Legacy/native macOS receiver |
| `models/3d/` | Enclosure, mounting, and other Hallzee fabrication files |
| `docs/` | Architecture and Bluetooth protocol references |

## Getting started, contributor guides & design documentation

- [Getting started: normal users](docs/getting-started-users.md) — Download the app, pair a terminal, add students, share workspace settings, and sync trips.
- [Getting started: developers](docs/getting-started-developers.md) — Set up a clean computer, wire/flash hardware, run the app locally, and test changes.
- [Installation and testing guide](docs/testing-and-installation.md) — Detailed hardware wiring, Mac vs. Windows verification rules, and one-command bootstraps.
- **Level 1 (PRD):** [Product & Requirements Document](docs/product-requirements.md) — Product vision, personas, classroom workflows, and FERPA privacy boundaries.
- **Level 2 (Architecture):**
  - [System Technical Architecture](docs/architecture.md) — Topology, state ownership decision matrix, SQLite schema, and layer boundaries.
  - [Client UI Architecture](docs/client-ui-architecture.md) — Desktop presentation contracts, ViewModel state, and refactor roadmap.
  - [Bluetooth Sync Protocol](docs/bluetooth-protocol.md) — BLE GATT services, command framing, and incremental sync session flow.
- **Level 3 (Feature Design Specifications):**
  - [Roster Import & Enrichment](docs/design/roster-import.md) — CSV parser, student ID mapping, and profile-scoped storage.
  - [Live Active-Pass Protocol](docs/design/active-pass-protocol.md) — Real-time BLE query/event stream for live pass monitoring.
  - [Classroom Policies & Bell Schedules](docs/design/policy-and-schedules.md) — Pass limits, 10/10 lockout rules, and timetable engine.
  - [Background Auto-Sync](docs/design/auto-sync.md) — Autonomous connection management and channel arbitration.
  - [Terminal Identity & Exclusive Claim](docs/design/terminal-identity-and-exclusive-claim.md) — Stable kiosk IDs, one-owner authorization, safe multi-terminal storage, and implementation plan.
  - [Bluetooth Firmware Updates](docs/design/bluetooth-firmware-updates.md) — Planned version reporting, firmware packages on GitHub, desktop-to-terminal installation, and future update checks.

## Hardware

- Original ESP32/WROOM-32 board with BLE peripheral support, the Bluedroid
  stack, 4 MB or more of flash, at least 12 usable signal GPIOs, 3.3 V logic,
  and a USB-UART bootloader path. The known-good board is an ESP32 DevKit V1.
- ST7735 TFT display (the default firmware profile)
- Optional ILI9341 TFT display at 240×320 (the `ili9341` firmware profile)
- 3×4 matrix keypad

The complete physical wiring and fresh-machine flashing checklist is in the
[installation and testing guide](docs/testing-and-installation.md). The
configured pins, the 12 required GPIOs, and unsupported board variants are
also documented there and mirrored in `Config.h` and [WOKWI.md](WOKWI.md).

## Firmware build

Extract or clone the project anywhere, then open Terminal (macOS) or PowerShell
(Windows) in the **project root**: the folder containing this README,
`bathroom-signin.ino`, and `scripts/`. All commands use paths relative to that
folder. See the [installation guide](docs/testing-and-installation.md#first-day-start-from-nothing)
for opening a terminal in the right folder.

The firmware targets the original ESP32 family with Bluetooth Low Energy.
The platform scripts install Arduino CLI, the ESP32 board package, and these
libraries automatically:

- Adafruit GFX Library
- Adafruit ST7735 and ST7789 Library
- Adafruit ILI9341
- Keypad

Use the repository flasher so the Arduino sketch is staged with the required
matching folder and `.ino` names:

```sh
bash scripts/flash-terminal-macos.sh /dev/cu.usbserial-XXXX
```

The original 160×128 display remains the default. For the 240×320 ILI9341
module, use the alternate profile:

```sh
bash scripts/flash-terminal-macos.sh --display ili9341 /dev/cu.usbserial-XXXX
```

The ILI9341 profile uses the same keypad, Bluetooth, storage, and terminal
behavior. Its landscape UI is rendered natively at 320×240 with a large,
non-touch readout and physical-key legend. Use `--rotation 3` (macOS) or `-Rotation 3` (Windows) if the
panel is mounted upside down. On Windows, use
`powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1 -Display ili9341 -Port COM5`.
The ILI9341 profile uses a conservative 20 MHz SPI clock for reliable updates
on jumper-wired modules and embeds only the bitmap font sizes used by the
native terminal UI.

The supported firmware build uses the `esp32:esp32` Arduino core version
3.3.11. The Windows equivalent is
`scripts/flash-terminal-windows.ps1 -Port COM5`; both scripts install the
board package and libraries when needed.

Upload only after selecting the correct board and serial port for the physical
terminal. The terminal starts by asking for the local date and time; on the
ILI9341 profile this setup screen also uses the full native 320×240 layout.
The clock is also set automatically when the receiver sends a valid `TIME`
command.

## Using the terminal

- Enter a student ID and press `#` to check out.
- Enter the same ID and press `#` to check back in.
- Use the Windows client's **Max student ID digits** control to configure a
  persisted 4–16 digit limit; the firmware default is 10.
- Press `*` to clear the in-progress ID.
- Hold `*` and `#` together for two seconds to record a manual reset of an
  occupied pass.
- When the terminal is unoccupied and unclaimed, hold `*` and `#` for five
  seconds to show the six-digit Bluetooth passkey. This is the
  only way to start ownership claim.
- Enter `1234#` to set the terminal clock manually.
- Enter `9999#` to show the local trip-log summary.
- Send `p` or `P` over USB Serial Monitor to print the stored trip log.
- Send `OWNER_RESET` over USB Serial Monitor, or hold `*` and `#` for 10 seconds
  while the terminal is unoccupied, to clear terminal ownership after a test;
  this preserves trips, settings, and the stable terminal ID.

## Desktop sync

The desktop transport is Bluetooth Low Energy GATT. The terminal advertises a
stable suffix, requires Secure Connections/MITM pairing, and allows one active
central. The receiver identifies the terminal through the v2 handshake and
authorizes it with the installation's owner credential. Normal syncs use
the latest trip ID durably stored in the local SQLite database, so only newer
records are transferred. The assigned terminal reconnects automatically, live
events stream continuously, and a serialized five-minute reconciliation sync
recovers missed notifications. Full history remains an explicit recovery
operation.

See [Bluetooth protocol](docs/bluetooth-protocol.md) and
[architecture](docs/architecture.md).

## Validation

For each firmware change, compile using the staged flasher or the CI workflow.
The repository folder is not itself a valid Arduino sketch directory; use:

```sh
bash scripts/flash-terminal-macos.sh
```

Run the deterministic native test suite and its coverage report with:

```sh
make -C test coverage
```

The test runner uses the system Apple Clang toolchain and `llvm-cov`; it does
not require a connected ESP32. Rendering tests record the exact display
instructions sent by `TerminalDisplay`, including coordinates, colors, text,
and pause durations.

The Windows receiver's protocol and CSV persistence tests are in
`receiver/windows/BathroomSync.Tests`. On a machine with .NET 8 installed,
run them with:

```sh
dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
```

The Windows CI workflow enforces at least 80% line coverage for that
hardware-independent core before publishing the app.

Use the Wokwi setup for keypad-flow checks and perform a physical-board test
for TFT rendering, Bluetooth discovery/reconnection, and flash persistence.
For the ILI9341 profile, verify the 320×240 landscape layout, large student-ID
readout, occupied/available cards, and that no display element appears to be a
touchscreen control. Mac testing is sufficient for rendering and keypad
behavior; a Windows PC is not required for those checks. A Windows PC is only
required for Windows-specific desktop packaging/BLE adapter behavior, which is
not verified by the firmware rendering tests.
