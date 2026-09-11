# Bluetooth repair and unexpected keypad input

## Reconnect a saved terminal after the OS forgets pairing

Hallzee ownership and the operating system's Bluetooth bond are separate. A
saved owner key can still exist when macOS has forgotten its bond. The browser
must first establish encrypted Bluetooth before it can prove that owner key.
A `pairing / NotSupportedError` identifies a failed encrypted read; it does not
identify its exact native cause or prove that ownership was lost.

Use **Disconnect** in Mac Bluetooth settings if another connection is holding
the terminal. Avoid **Forget** for routine reconnects; forgetting may require a
new OS code. Keep the original Chrome profile, URL, and Hallzee site data.

The following repair gesture requires firmware containing this change. Update
using the existing [firmware installation instructions](Testing-and-Installation.md#assemble-or-flash-a-terminal),
with the correct display/rotation options. A web app update alone cannot add a
physical terminal gesture. No hardware has been flashed as part of this fix.

1. Finish any active passes. Exit clock setup or the experimental touch screen.
2. Hold **`*` alone for five seconds**, then release. Do not hold `#` too:
   the existing `*`+`#` ten-second gesture resets ownership.
3. Wait for **BT REPAIR** and a fresh six-digit code. The firmware first waits
   for the old connection to close, then removes terminal-side OS bonds. If
   **REPAIR FAILED** appears, close other connected clients and retry.
4. In the original Hallzee browser profile, open **Connect terminal** and click
   **Choose saved terminal**. Enter the BT REPAIR code only in the macOS/Windows
   Bluetooth prompt. Leave Hallzee's claim-code field empty. Do not connect
   separately in Mac Bluetooth settings before using the browser chooser.
5. Wait for authenticated connection and sync. If the OS still holds an obsolete
   bond and rejects this code, forget only that terminal in OS Bluetooth settings
   and repeat from step 2. Do not clear Hallzee site data or reset ownership.

The displayed code lasts two minutes. Completion requires authentication with
the existing owner key; OS pairing alone is insufficient. Expiry disconnects an
unauthenticated client and invalidates the displayed code. Ownership, trips,
roster, settings, and active-pass storage are not reset. This procedure cannot
recover a deleted browser owner key or transfer ownership to another client.

## Unexpected digits on the physical keypad

In the reported incident, supporting the terminal on a hard surface stopped
unrequested digits. That is consistent with keypad flex, mounting pressure, or
an intermittent connection, but does not prove the physical cause.

The physical digit-entry path is the matrix keypad. BLE does not inject keypad
characters. The Keypad library's scan interval allowed a single sampled press
to reach the app. The adapter now requires each press and release to stay
unchanged for 40 ms. Short glitches and release bounce are rejected; held keys
do not repeat and deliberate overlapping digit presses remain supported. A
sustained false contact still looks like a real press to software.

With power disconnected, seat all seven keypad leads and check against the
[pin table](Build-Your-Own-Terminal.md). Make sure the keypad lies
flat, has support behind it, and is not squeezed by the cover, screws, or wires.
Keep bare contacts off conductive surfaces. Re-test with fictional digits on a
firm, nonconductive surface. Do not collect real student IDs in diagnostic logs;
the firmware no longer prints each typed digit to Serial.

## Verification and physical checks

Native regressions cover short glitches, stable edges, release bounce, held keys,
repeated/overlapping digits, clock rollover, output capacity, repair eligibility,
`*` versus `*`+`#`, one action per hold, disconnect timeout, failed bond removal,
repair expiry, and completion only after owner authentication. Web regressions
verify sanitized repair guidance and saved-key authentication without re-claiming.

A Mac plus the physical terminal is sufficient for these Mac recovery and keypad
checks; a Windows PC is not required. Windows-specific OS code prompts, bond
replacement, encrypted GATT, reconnect/sleep and PWA behavior remain unverified.

After installing the new firmware, verify:

- Leave the idle keypad untouched for two minutes, then enter repeated and mixed
  fictional digits at normal speed. Repeat with the enclosure supported in its
  normal mounting position. No extra digits, repeats, or missed normal presses.
- Pair a fictional classroom, disconnect/reconnect normally, then intentionally
  forget only its OS bond. Use BT REPAIR and the original browser profile to
  reconnect. Confirm roster/history counts and the assigned terminal are unchanged.
- Cancel the OS prompt and let repair expire; repeat the gesture and succeed with
  a fresh code. A new browser profile without the owner key must not claim the
  owned terminal using the repair code.
- Confirm short `*` still clears, `#` still submits, and repair is unavailable
  during an active pass or clock setup. Do not test ownership reset on live data.

These physical checks are pending. Passing automated tests does not establish
that the reported intermittent wiring issue or macOS bond-repair failure is gone.
