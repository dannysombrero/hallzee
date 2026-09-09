# Optional 2.8-inch touchscreen experiment

This experiment targets the red ILI9341 module with a separate SPI resistive
touch controller. The labels `T_CLK`, `T_CS`, `T_DIN`, `T_DO`, and `T_IRQ` suggest
an XPT2046-compatible controller; the chip model and actual finger sensitivity
still need verification on the physical module. A resistive panel responds to
pressure, so try a fingertip and a blunt plastic stylus as well as a finger pad.
Do not use a sharp object.

## Connect four touch wires

Disconnect USB power first. Keep the existing display and keypad wiring from
[Installation and testing](testing-and-installation.md).

| Screen pin | ESP32 connection | Purpose |
| --- | --- | --- |
| `T_CLK` | GPIO **16** | Touch SPI clock |
| `T_DIN` | GPIO **17** | Commands into touch controller |
| `T_DO` | GPIO **19** | Touch readings back to ESP32 |
| `T_CS` | GPIO **4** | Separate touch chip select; TFT `CS` stays on GPIO 5 |
| `T_IRQ` | Leave unconnected | Firmware polls; no interrupt wire needed |
| Display `SDO (MISO)` | Leave unconnected | This is separate from touch `T_DO` |

Each touch signal now has its own direct jumper wire: **no breadboard or
splitter is needed**. The display keeps GPIO 18 and 23; touch uses a separate
hardware SPI bus on GPIO 16, 17, 19, and 4. Existing 3.3 V power and common
ground serve the module; no extra power wire is needed.

This replaces the earlier shared-wire instructions. If already wired that way,
move only `T_CLK` from 18 to **16** and `T_DIN` from 23 to **17**, then reflash
with the command below. Moving the wires without updating firmware will not
work. Keep the display's clock/MOSI wires on 18/23.

This pin assignment is for the project's **ESP-WROOM-32 DevKit**. GPIO 16 and
17 are available on that module; do not assume they are free on a WROVER or
other PSRAM-equipped variant. Leave `EN` and the flash pins
`SD0`, `SD1`, `SD2`, `SD3`, `CMD` (possibly read as `CND`), and `CLK` alone.
Use the numbered GPIO **16** header pin for `T_CLK`, not the board pin `CLK`.
The proposed wiring also avoids the boot-strapping pins 0, 2, 12, and 15,
and the USB serial RX/TX pins. Do not substitute another pin without updating
and checking the firmware wiring.

## Build and run

From the project root, use the existing bootstrap; it installs Arduino CLI,
ESP32 core, required libraries (including `XPT2046_Touchscreen@1.4`), and the
USB migration tools when needed. No existing Arduino or .NET installation is
assumed. For obtaining the project on a clean computer, start with
[Installation and testing](testing-and-installation.md).

```sh
bash scripts/flash-terminal-macos.sh --display ili9341 --touch-test
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/flash-terminal-windows.ps1 -Display ili9341 -TouchTest
```

If needed, append the Mac serial port or `-Port COM5` on Windows. Add
`--rotation 3` / `-Rotation 3` if the image is upside down. Touch supports only
landscape rotations 1 and 3, including the firmware's existing mounting rotation
mapping. `--compile-only` / `-CompileOnly` builds without changing a board.
The bootstrap preserves the existing verified USB backup/migration workflow.

1. Set the clock with the physical keypad, or sync time from the desktop.
   Calibration automatically opens once the clock is set and no pass is active.
2. Tap and release each of the four crosshairs. The first three map the axes;
   the fourth checks alignment. Poor calibration restarts the sequence.
   Physical `*` skips/exits even if touch is not working.
3. Successful calibration opens the typing sandbox. **MODE** cycles through
   number keys at 88×34 pixels, smaller number keys at 28×26 pixels, and letter
   keys at 28×32 pixels. **CALIBRATE** repeats calibration. **EXIT** or physical
   `*` returns to sign-in. `<` is Backspace; `CLR`/Clear empties the sandbox.
4. On sign-in, enter an ID with the physical keypad. Tap **Clear** to clear it,
   or **Submit** to run the normal sign-in/check-in action. These two controls
   operate on real passes. A green outline indicates an accepted press; actions
   fire after release. Holding does not repeat, and sliding off cancels the tap.
5. When no pass is active, tap **TOUCH TEST** beside the Student ID heading to
   reopen the sandbox. Its text never becomes a student ID or trip. Physical
   number keys and `#` are ignored inside the sandbox; `*` exits. Exit before
   starting pairing, owner-reset chords, or a Bluetooth firmware update.

Calibration is kept in RAM for this experiment and must be repeated after
restart. Skipping or cancelling incomplete calibration leaves normal touch
controls disabled. Reboot to retry, or send `TOUCH_TEST` followed by a newline
in a USB serial monitor at 115200 baud after clock setup with no active pass.
This command reopens the test without rebooting; it replies `TOUCH_TEST,BUSY`
when setup, pairing, or a pass prevents entry. It is available only in touch
experiment builds. Bluetooth polling continues while the sandbox is open.

## Judge finger usability

Copy `1234567890` several times on each numeric layout and `hello hallzee` on
the letter layout. Compare a finger pad, fingertip, and blunt stylus. Check the
entered text and count corrections; the counters show registered key taps and
Backspace taps, not automatic accuracy or undetected touches. Clear and Mode
reset those counters. The readout holds up to 48 characters. The last sampled
contact's pressure value is a controller reading, not a calibrated force or a
sensitivity setting. A finger press that never registers cannot appear in it.

Check the corners as well as the center, quick taps, a long hold on Submit,
sliding between buttons, gaps between small keys, and idle operation without
phantom input. Confirm Clear and Submit still work through the physical keypad,
and that entering/exiting the sandbox changes neither active passes nor trip
records. Repeat in the other landscape orientation after reflashing if needed.
The large numeric layout still has limited key height; the test is intended to
measure whether this particular panel is comfortable enough for fingers.

A **Mac plus the physical ESP32/display is sufficient** for these touch checks.
A Windows PC is **not required** for touch; no Windows-specific touch capability
is involved. A Windows PC is needed to verify the new PowerShell `-TouchTest`
bootstrap installation/USB flash path, which has not been verified on Windows.
Physical sensitivity, calibration, separate-bus wiring, and touch behavior
have not yet been verified. Native tests cover calibration math, tap timing, and sandbox/control routing;
firmware compilation is not hardware validation. The browser emulator and
Wokwi do not simulate this controller or finger sensitivity.

## Return to ordinary firmware

Re-run the usual ILI9341 flash command without `--touch-test` / `-TouchTest`.
Ordinary signed releases also omit touch. With this separate-bus wiring, the
four touch wires may remain connected when disabling the feature. If removing
them, disconnect USB power first and leave the original display wiring intact.
The experiment keeps the existing display/orientation firmware identity, so a
normal OTA package can replace it but will not retain touch support.

Hardware references: [ESP32 multiple SPI buses](https://docs.espressif.com/projects/arduino-esp32/en/latest/api/spi.html),
[ESP-WROOM-32 pin definitions](https://documentation.espressif.com/esp32-wroom-32_datasheet_en.html),
[XPT2046 library and alternate SPI support](https://github.com/PaulStoffregen/XPT2046_Touchscreen),
[ESP32 GPIO restrictions](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/gpio.html),
and [resistive touch operation](https://learn.adafruit.com/adafruit-2-8-tft-touch-shield-v2/resistive-touchscreen-paint-demo).
