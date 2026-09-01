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
| `ClockService.*` | System time setting, formatting, and date validation |
| `BluetoothSync.*` | BLE GATT connection and incremental record-sync protocol |
| `KeypadController.*` | Keypad events and the `* + #` reset gesture |
| `TerminalDisplay.*` | TFT rendering only |
| `TripStorage.*` | ESP32 Preferences and LittleFS trip-log persistence |
| `receiver/windows/` | Current Windows sync application |
| `receiver/universal/` | Shared Avalonia desktop UI preview for the next Windows/macOS client |
| `receiver/` | Legacy/native macOS receiver |
| `docs/` | Architecture and Bluetooth protocol references |

## Contributor guides & design documentation

- [Installation and testing guide](docs/testing-and-installation.md) — Contributor setup, Mac vs. Windows verification rules, and one-command bootstraps.
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

## Hardware

- ESP32 with Bluetooth Low Energy support
- ST7735 TFT display
- 3×4 matrix keypad

The configured pins are documented in `Config.h` and in [WOKWI.md](WOKWI.md).

## Firmware build

The firmware targets the original ESP32 family with Bluetooth Low Energy
available. Install the ESP32 Arduino core and these libraries:

- Adafruit GFX Library
- Adafruit ST7735 and ST7789 Library
- Keypad

Compile from the repository root:

```sh
arduino-cli compile --fqbn esp32:esp32:esp32 .
```

Upload only after selecting the correct board and serial port for the physical
terminal. The terminal starts by asking for the local date and time; this is
also set automatically when the receiver sends a valid `TIME` command.

## Using the terminal

- Enter a student ID and press `#` to check out.
- Enter the same ID and press `#` to check back in.
- Use the Windows client's **Max student ID digits** control to configure a
  persisted 4–16 digit limit; the firmware default is 10.
- Press `*` to clear the in-progress ID.
- Hold `*` and `#` together for two seconds to record a manual reset of an
  occupied pass.
- Enter `1234#` to set the terminal clock manually.
- Enter `9999#` to show the local trip-log summary.
- Send `p` or `P` over USB Serial Monitor to print the stored trip log.

## Desktop sync

The desktop transport is Bluetooth Low Energy GATT. On Windows, the published .NET 8
WinForms receiver scans for Hallzee's service UUID, subscribes to notifications,
and synchronizes directly without a COM port or pairing PIN. Normal syncs use
the latest trip ID durably stored in the local SQLite database, so only newer
records are transferred. Full history remains an explicit recovery operation.

See [Bluetooth protocol](docs/bluetooth-protocol.md) and
[architecture](docs/architecture.md).

## Validation

For each firmware change, run:

```sh
arduino-cli compile --fqbn esp32:esp32:esp32 .
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
