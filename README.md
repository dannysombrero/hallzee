# Bathroom Terminal

Bathroom Terminal is an ESP32-based school bathroom sign-in terminal. Students
enter an ID on a 3×4 keypad to check out and back in. The terminal stores trip
records locally and can synchronize them to a desktop receiver over Bluetooth
Classic Serial Port Profile (SPP).

## Project layout

| Path | Purpose |
| --- | --- |
| `bathroom-signin.ino` | Firmware composition, UI/controller callbacks, `setup()`, and `loop()` |
| `Config.h` | Product constants, pin assignments, and display palette |
| `TerminalController.*` | Active-pass state, check-in/out, reset, and persistence decisions |
| `ClockService.*` | System time setting, formatting, and date validation |
| `BluetoothSync.*` | Bluetooth Classic SPP connection and record-sync protocol |
| `KeypadController.*` | Keypad events and the `* + #` reset gesture |
| `TerminalDisplay.*` | TFT rendering only |
| `TripStorage.*` | ESP32 Preferences and LittleFS trip-log persistence |
| `receiver/windows/` | Current Windows sync application |
| `receiver/` | Legacy/native macOS receiver |
| `docs/` | Architecture and Bluetooth protocol references |

## Hardware

- ESP32 with Bluetooth Classic support
- ST7735 TFT display
- 3×4 matrix keypad

The configured pins are documented in `Config.h` and in [WOKWI.md](WOKWI.md).

## Firmware build

The firmware targets the original ESP32 family with Bluetooth Classic SPP
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
- Press `*` to clear the in-progress ID.
- Hold `*` and `#` together for two seconds to record a manual reset of an
  occupied pass.
- Enter `1234#` to set the terminal clock manually.
- Enter `9999#` to show the local trip-log summary.
- Send `p` or `P` over USB Serial Monitor to print the stored trip log.

## Desktop sync

The current desktop transport is Bluetooth Classic SPP/RFCOMM. On Windows, the
existing receiver is a .NET 8 WinForms application that expects the terminal
to be paired in Windows and exposes the available Bluetooth COM ports.

The planned Windows improvement is to keep the same ESP32 SPP protocol while
replacing the manual pairing/COM-port flow with in-app discovery, pairing,
direct RFCOMM connection, remembered-terminal reconnect, and clear recovery
actions. See [Bluetooth protocol](docs/bluetooth-protocol.md) and
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

Use the Wokwi setup for keypad-flow checks and perform a physical-board test
for TFT rendering, Bluetooth pairing/reconnection, and flash persistence.
