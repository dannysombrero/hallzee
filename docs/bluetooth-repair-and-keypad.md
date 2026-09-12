# Bluetooth repair and unexpected keypad input

## Reconnect a saved terminal after the OS forgets pairing

Hallzee ownership and the operating system's Bluetooth bond are separate. A
saved owner key can still exist when macOS has forgotten its bond. The browser
must first establish encrypted Bluetooth before it can prove that owner key.
A `pairing / NotSupportedError` identifies a failed encrypted read; it does not
identify its exact native cause or prove that ownership was lost.

Use Hallzee's **Reconnect** first, in the original browser profile and URL.
A saved owner normally reconnects without pairing mode or any code. Close other
clients holding a connection. Avoid resetting ownership or clearing Hallzee data.

The no-passkey flow and repair gesture require updated terminal firmware. Use
the [firmware installation instructions](testing-and-installation.md#assemble-or-flash-a-terminal)
with the correct display/rotation options. An app update alone cannot change
firmware pairing. Physical verification of this update remains pending.

If reconnect still reports a lost or stale OS bond:

1. Finish active passes. Exit clock setup or the experimental touch screen.
2. Hold **`*` alone for five seconds**, then release. Do not hold `#` too:
   the existing `*`+`#` ten-second gesture resets ownership.
3. Wait for **BT REPAIR** and **Reconnect in Hallzee**. Firmware closes the old
   connection and removes terminal-side OS bonds. If **REPAIR FAILED** appears,
   close other connected clients and retry.
4. In the original Hallzee app/profile, click **Reconnect** and choose the saved
   terminal if the browser asks. No pairing code is needed; the OS may request
   permission to pair, but Hallzee does not supply or ask for an OS passkey.
5. If the OS retains an obsolete bond, forget only that terminal in OS Bluetooth
   settings and repeat from step 2. Keep Hallzee site data and the original owner.

The repair window lasts two minutes. Completion requires authentication with
the existing owner key; OS pairing alone is insufficient. Expiry disconnects an
unauthenticated client. Ownership, trips, roster, settings, and active-pass
storage are not reset. Repair cannot recover a deleted browser owner key or
transfer ownership to another client.

## Authentication failure after a fast firmware update

`bash scripts/flash-terminal-macos.sh --fast --display ili9341` is sufficient
for application/Bluetooth firmware changes after the initial USB setup, provided
it completes with **Fast USB write verified**. It compiles the current source,
verifies bootloader/layout compatibility, and writes/verifies the application and
boot selection. Both fast and regular USB flashing preserve NVS, including
Hallzee ownership and Bluetooth bonds; a regular flash is not a bond reset.

A `pairing / NetworkError` with **Bluetooth authentication incomplete** means
the protected Bluetooth read failed before HELLO or Hallzee code verification.
A stale OS bond is one possible cause, not a confirmed diagnosis. Before that
read succeeds, **Currently Paired** can reflect a browser-stored credential
without confirming the terminal's current ownership. If the terminal displays
a six-digit pairing code, it is in the unclaimed pairing flow; do not infer that
BT REPAIR is available just from the browser's saved label.

For a Chromebook retry after this update:

1. Close Hallzee tabs/PWA and other connected Hallzee clients. In ChromeOS
   **Settings → Bluetooth**, select only this terminal and choose **Forget**, if
   it is listed. This removes the OS bond; keep Hallzee site data, owner keys,
   and classroom records. See [Google's device-forgetting instructions](https://support.google.com/chromebook/answer/2587653?hl=en).
2. Restart the terminal. If it is unclaimed, open its six-digit pairing screen
   with `*` + `#` held for five seconds, then release. If it still has an owner,
   finish clock setup and active passes, then hold `*` alone for five seconds
   until **BT REPAIR** appears. That path retains the owner credential.
3. Reopen the same Hallzee URL/browser profile, click **Find nearby terminals**,
   and choose the terminal. An unclaimed terminal should ask for its code in
   Hallzee; a saved owner should reconnect without one. Do not pair it separately
   through the ChromeOS Bluetooth settings screen.

If the same encrypted-read error persists, the cause is unresolved; repeating
full flashes or clearing classroom data is not a supported diagnosis. Record
the exact terminal screen and sanitized error category, not pairing codes or
raw Bluetooth logs. This retry needs the Chromebook; a Mac is sufficient to
flash and run shared software checks but cannot verify ChromeOS behavior. A
Windows PC is not required for this Chromebook retry. Windows BLE authentication,
GATT notifications, and reconnect require a Windows BLE PC and remain unverified.

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
[pin table](build-your-own-terminal.md). Make sure the keypad lies
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
checks; a Windows PC is not required for those Mac checks. A Chromebook is
required to verify ChromeOS pairing, managed permissions, and sleep/wake. A
Windows BLE PC is required for Windows Just Works pairing, bond replacement,
encrypted GATT, reconnect/sleep and PWA behavior; **Windows behavior remains
unverified**.

After installing the new firmware, verify:

- Leave the idle keypad untouched for two minutes, then enter repeated and mixed
  fictional digits at normal speed. Repeat with the enclosure supported in its
  normal mounting position. No extra digits, repeats, or missed normal presses.
- Pair a fictional classroom, disconnect/reconnect normally, then intentionally
  forget only its OS bond. Use BT REPAIR and the original browser profile to
  reconnect. Confirm roster/history counts and the assigned terminal are unchanged.
- Cancel any OS permission prompt and let repair expire; repeat the gesture and
  reconnect without a code. A new browser profile without the owner key must
  remain unable to claim the owned terminal during repair.
- Confirm short `*` still clears, `#` still submits, and repair is unavailable
  during an active pass or clock setup. Do not test ownership reset on live data.

These physical checks are pending. Passing automated tests does not establish
that the reported intermittent wiring issue or macOS bond-repair failure is gone.
