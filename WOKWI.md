# Virtual hardware test setup

This project includes a Wokwi circuit for the ESP32 and its 3x4 keypad. It
uses the same GPIO assignments as the physical terminal:

| Part | ESP32 pins |
| --- | --- |
| Keypad rows R1--R4 | 32, 33, 25, 26 |
| Keypad columns C1--C3 | 27, 14, 13 |
| ST7735 TFT | CS 5, RST 22, DC 21, MOSI 23, SCLK 18 |

## Run it in Wokwi

1. Create a new **ESP32 Arduino** project at [Wokwi](https://wokwi.com/).
2. Copy these files into that project without changing their names:
   `bathroom-signin.ino`, `TripStorage.h`, `TripStorage.cpp`, `diagram.json`,
   and `libraries.txt`.
3. Start the simulation and open its Serial Monitor (115200 baud).
4. Click the keypad, then use your physical keyboard's number, `*`, and `#`
   keys to operate it. The first interaction must set the clock:
   `1234#`, then month, day, year, hour, minute, and `1` (AM) or `2` (PM),
   pressing `#` after each value.

The virtual keypad exercises the same multi-key behavior as the board,
including holding `*` and `#` for two seconds to reset an occupied pass.
Type `p` in the Serial Monitor to print the trip log.

## Simulator scope

Wokwi currently has no ST7735 display component. The sketch still compiles
and runs with its existing Adafruit ST7735 calls, but Wokwi cannot render that
screen. Use the Serial Monitor to verify the checkout/check-in flow, clock
setup, reset, and CSV log behavior. Keep one short physical-board check for
the display layout, color order, and real flash persistence.
