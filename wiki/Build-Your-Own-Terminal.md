# Build your own Hallzee terminal

Build a Hallzee terminal with an original ESP32 DevKit V1/WROOM-32, a 3×4
membrane keypad, and a 2.8″ red ILI9341 display. The 3D-printable enclosure is
designed for no-solder Dupont connections.

The complete guide includes component sourcing, estimated costs, the free
enclosure files, the exact keypad and display pin map, safety checks, flashing,
and pairing instructions:

- [Open the Build Your Own Terminal guide](https://github.com/dannysombrero/hallzee/blob/main/docs/build-your-own-terminal.md)
- [3D models and enclosure renders](3D-Models)

Use the pin map in that guide rather than a generic ESP32 wiring diagram. The
terminal uses 3.3 V logic and the firmware supports the original ESP32/WROOM-32
board family, not ESP32-C3, S2, S3, or H2 boards.

If digits appear without a deliberate keypress, inspect keypad support and
connections using [the keypad troubleshooting checks](Bluetooth-Repair-and-Keypad.md#unexpected-digits-on-the-physical-keypad).
