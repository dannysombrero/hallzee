# Architecture

## Runtime flow

```text
KeypadController ── events ──> bathroom-signin.ino ──> TerminalController
                                      │                      │
                                      │                      └──> TripStorage
                                      │
                                      ├──> TerminalDisplay
                                      ├──> ClockService
                                      └──> BluetoothSync ──> TripStorage
```

`bathroom-signin.ino` is deliberately the composition root. It creates the
hardware-backed services, binds input callbacks to terminal actions, and keeps
the main loop non-blocking except for the existing short user-feedback screens.

## Firmware modules

### TerminalController

Owns the active-pass lifecycle:

1. Restore a saved active pass at startup.
2. Accept a first student ID as a checkout and persist it immediately.
3. Accept the same ID as a check-in, append a `COMPLETE` record, and clear the
   active pass only after the record is safely written.
4. Append a `MANUAL_RESET` record when an occupied pass is reset.
5. Classify the clock and log-summary administrator codes.

It returns an action result to the sketch. The sketch supplies the display and
diagnostic output associated with that action; therefore the controller has no
TFT dependency.

### TripStorage

Stores the current active pass in ESP32 Preferences and stores append-only trip
records in LittleFS. Records have stable numeric IDs and sync state. The
Bluetooth protocol acknowledges each record separately, which makes a retry
safe after a desktop or Bluetooth disconnect.

### ClockService

Sets the ESP32 system clock, formats the date and time for the display, and
provides calendar validation for manual clock setup.

### TerminalDisplay

Contains all ST7735 drawing operations. It is given values to display and does
not read persistent state or Bluetooth state itself.

### KeypadController

Owns multi-key keypad mechanics:

- press/release interpretation;
- standard `*` clear and `#` submit behavior;
- the two-second `* + #` reset gesture;
- suppression of clear/submit events after a successful reset.

The callback receiver determines whether the terminal is in clock setup mode
and whether a reset is allowed.

### BluetoothSync

Owns the ESP32 Bluetooth Classic SPP server, newline-delimited command parser,
and reliable trip streaming. It does not know about the display or keypad. A
successful Bluetooth `TIME` command invokes callbacks that update the clock and
leave manual clock setup when appropriate.

## Design rules

- Keep the trip-sync message format independent of the Bluetooth transport.
  A future BLE implementation should be able to use the same protocol.
- Do not let display code decide business outcomes or mutate persisted state.
- Do not clear an active pass until its checkout/check-in/reset persistence
  operation has succeeded.
- Treat Bluetooth connection loss as normal. The terminal retains unacknowledged
  records, and the receiver can request them again.
