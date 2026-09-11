# Build your own Hallzee terminal

This guide is for teachers and makers who want to build a Hallzee terminal from
the open-source design. You do not need prior electronics experience: the
recommended enclosure is designed around no-solder Dupont connections.

The component links below are provided as easy references. Amazon can be faster
and simpler for returns; AliExpress is often less expensive but can take longer
to arrive and have different return terms. Prices change frequently.

## Shopping list

- [Easy Reference Component List (Amazon)](https://amzn.to/4A3WNVv)
- Estimated Amazon total: **about $45**
- Estimated AliExpress total: **about $25**, excluding the ESP32 board

| Component | Buy | Why this part |
| --- | --- | --- |
| ESP32-WROOM-32D development board | [Amazon](https://link.amazon/B0iYLh3GS) | This is the board used and tested with the supplied 2.8″ case. Other original ESP32 boards can work, but may not fit the enclosure and can require model changes. Do **not** substitute an ESP32-C3, S2, S3, or H2; those are not supported by the current firmware. |
| 3×4 membrane keypad | [Amazon](https://link.amazon/B06SMyEXs) · [AliExpress](https://s.click.aliexpress.com/e/_c4o7fm1F) | You need one keypad. This pack is a low-cost option, and the tested keypad has a reliable feel. |
| 2.8″ TFT LCD screen | [Amazon](https://link.amazon/B0iAe7jEW) · [AliExpress](https://s.click.aliexpress.com/e/_c4NEynTj) | Use the red ILI9341 SPI module for the 2.8″ enclosure. A touch-capable module is fine; Hallzee does not require touch, so a non-touch version is usually cheaper. |
| Dupont jumper wires | [Amazon](https://link.amazon/B06vL5zqx) · [AliExpress](https://s.click.aliexpress.com/e/_c3Vw560Z) | Get both female-to-female and female-to-male wires. You need at least seven male-to-female connections for the keypad and nine female-to-female connections for the screen. |
| M3 × 5 mm bolts | [Amazon](https://link.amazon/B018CffbF) · [AliExpress](https://s.click.aliexpress.com/e/_c3S8bd5J) | All enclosure screws use M3 × 5 mm. The current case needs 14, although kits usually contain more. |
| Micro-USB wall charger | [Amazon](https://link.amazon/B09n9XGld) · [AliExpress](https://s.click.aliexpress.com/e/_c3iPfIwN) | The terminal needs micro-USB power. A micro-USB cable and a suitable USB wall adapter you already own also work. |

Soldering also works, but the enclosure is designed around the listed Dupont
connectors, so no soldering is required.

## 3D printer and materials

- [Bambu Lab P1S 3D printer](https://link.amazon/B0hQt6evb) — a reliable,
  approachable option for a first printer.
- [Creality Hyper PLA, blue](https://link.amazon/B0d37FG4q) — a proven option
  for the enclosure. Most PLA brands work; a dependable printer matters more
  than a particular filament brand.
- Download the free [2.8″ terminal models](../models). The current enclosure
  consists of the main case, back, LCD/keypad cover, keypad support, and ESP32
  stabilizer. The model files are licensed under CC BY-SA 4.0.

## Wire the terminal

Use an original ESP32 DevKit V1/WROOM-32 with 4 MB or more of flash and the
2.8″ red ILI9341 SPI display. Disconnect USB power before wiring. The TFT and
keypad use 3.3 V logic: **never connect 5 V to an ESP32 signal pin.**

Wire by the labels printed on each part:

| Part pin | ESP32 DevKit V1 pin | Notes |
| --- | --- | --- |
| Keypad `R1` | GPIO 32 | Row 1 |
| Keypad `R2` | GPIO 33 | Row 2 |
| Keypad `R3` | GPIO 25 | Row 3 |
| Keypad `R4` | GPIO 26 | Row 4 |
| Keypad `C1` | GPIO 27 | Column 1 |
| Keypad `C2` | GPIO 14 | Column 2 |
| Keypad `C3` | GPIO 13 | Column 3 |
| TFT `VCC` / `VIN` | `3V3` | Use the ESP32's 3.3 V pin. |
| TFT `GND` | `GND` | A common ground is required. |
| TFT `CS` | GPIO 5 | Chip select. |
| TFT `RST` / `RESET` | GPIO 22 | Hardware reset. |
| TFT `DC` / `A0` | GPIO 21 | Data/command. |
| TFT `MOSI` / `SDA` | GPIO 23 | SPI data. |
| TFT `SCLK` / `SCK` / `CLK` | GPIO 18 | SPI clock. |
| TFT `LED` / `BL` | `3V3`, if required | Follow the display module's label. |
| TFT `MISO` / `SDO` | Leave unconnected | Hallzee does not read data from the display. |

The keypad is a passive switch matrix, so it has no VCC or GND connection.
Keep the jumper wires short, check every connection before restoring power,
and do not connect the TFT's MISO pin just because it is present.

## Assemble, flash, and pair

1. Test the ESP32 first with only a known-good USB **data** cable connected.
2. Disconnect power, complete the wiring table above, then seat the display,
   keypad, ESP32, and wiring in the printed enclosure before fastening the 14
   M3 × 5 mm bolts.
3. Flash the ILI9341 firmware using the one-command setup in the
   [testing and installation guide](testing-and-installation.md#assemble-or-flash-a-terminal).
   On a Mac, pass `--display ili9341`; on Windows, pass `-Display ili9341`.
4. On the first boot, set the clock when prompted. Enter a test student ID,
   press `#` to check out, then enter it again and press `#` to check in.
5. Follow the [teacher guide](getting-started-users.md#connect-your-terminal)
   to pair the terminal with the Hallzee desktop app.

If the display stays blank but the ESP32 appears over USB, disconnect power and
check `VCC`, `GND`, `CS`, `RST`, `DC`, `MOSI`, and `SCLK` again. For the full
hardware requirements, troubleshooting, and alternate 1.8″ ST7735 profile,
see the [hardware reference](testing-reference.md#hardware-you-should-have).
