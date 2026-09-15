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
A stale OS bond is one possible cause, not a confirmed diagnosis. The web client
now shows **Status unknown** for saved rows until it can check the live terminal;
a saved credential alone does not confirm current ownership. If the terminal displays
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

## Chromebook encrypted-read failure before the code prompt

The latest retry on a personal Chromebook running ChromeOS/Chrome
**152.0.7977.113 (64-bit)** fails at `pairing / NotSupportedError` before Hallzee
asks for a code. Retaining the discovery connection cannot address a failure
that happens before identity inspection succeeds. Chrome's Paired chooser badge
still does not prove Hallzee ownership or a working encrypted connection.

The web client now tries the existing encrypted RX channel if the protected TX
read returns NotSupportedError. It sends an acknowledged blank line, which the
firmware ignores, before enabling notifications or sending HELLO. Successful
protected access is still required; this does not disable encryption or send
a pairing code prematurely. If the write also fails, the error identifies
`pairing-write` and, when recognized, a fixed GATT category. Chromium maps
several different failures to NotSupportedError; see its [error mapping](https://chromium.googlesource.com/chromium/src/+/main/third_party/blink/renderer/modules/bluetooth/bluetooth_error.cc).
Neither that error nor the fallback establishes the exact native cause.

Deploy the new **web client**, then use **Check updates → Apply update** in the
original Chromebook profile and URL. Already updated firmware needs no new
flash for this change. Retry selection with the unowned terminal displaying its
code; then verify reconnect after reopening Hallzee without another code. If it
fails, record the complete sanitized stage and GATT category shown by Hallzee.
Keep site data, owner credentials and classroom records. No repeated reset is
required by this correction.

A Mac is sufficient for shared automated checks but cannot verify this failure;
the Chromebook and ESP32 are required. A Windows PC is not needed for this
Chromebook retry. Windows Chrome/Edge protected reads/writes, notifications and
saved-owner reconnect require separate Windows BLE hardware testing and remain
unverified. Physical success on the Chromebook is also still unverified.

## Chromebook encrypted-read failure after entering the code

On September 15, Chrome on a Chromebook reached Hallzee's code-entry dialog,
then reported `pairing / NotSupportedError` on submission. That stage is the
protected Bluetooth read, before the submitted Hallzee code is checked. The
error alone cannot identify ChromeOS's native cause.

The web client previously closed its successful identity-inspection link and
opened another connection on submission. It now keeps that encrypted link open,
clears the temporary challenge using the firmware's existing CLAIM_ABORT
acknowledgement, and requests a fresh HELLO challenge when the code is submitted.
Human input can take longer than the ten-second handshake deadline. Application
data remains unauthorized until the normal code proof and durable owner-key
commit complete. Back/close/cancel releases the link; abandoned input expires
after two minutes. A real Bluetooth drop still requires a new connection.

This correction changes the **web client only**. With the previously updated
firmware already installed, no additional fast or full flash is required.

1. Deploy the updated web client, then use **Check updates → Apply update** in
   the same Chromebook browser profile and Hallzee URL.
2. With an unclaimed test terminal already displaying its pairing code, choose
   **Find nearby terminals**, select it, wait about 15 seconds, then enter the
   code in Hallzee and choose **Pair & Connect**. It should authenticate and sync.
3. Close/reopen Hallzee or restart the terminal and verify saved-owner reconnect
   without another code. Record only the sanitized error category if it fails;
   preserve Hallzee site data and credentials.

The Chromebook and terminal are required to verify this reported failure; a Mac
is sufficient for shared automated checks, not ChromeOS Bluetooth verification.
A Windows PC is **not required for this Chromebook retry**. Separate Windows BLE
PC testing is required for Chrome/Edge encrypted reads, notifications, first
claim and saved-owner reconnect. ChromeOS success after this correction and the
previously reported Windows failures remain physically unverified.

## Operation already in progress with only one tab

This error does not prove another app or tab is connected. A Hallzee timeout or
cancelled discovery can finish before Chrome's native Bluetooth operation does.
The web transport now waits for that operation to settle before reconnecting,
including any late connection cleanup. It gives the OS a short disconnect
cooldown and retries briefly busy setup operations on the same connection.
Command writes are never automatically replayed. If Chrome does not settle the
old operation within ten seconds of a new attempt, Hallzee pauses attempts and
explains the pending operation instead of starting overlapping work.

The September 12 pending-operation fix changes the **web client** only. The
September 14 Windows pre-code disconnect correction below also changes firmware.
Retry **Find nearby terminals**. Pairing mode affects the later Hallzee code
check; switching it on/off cannot resolve a pending native Bluetooth operation.
A full flash or repeated Chromebook restarts are not a fix for this app race.
Physical confirmation of the reported Chromebook failure remains pending.

Chrome's chooser can display `Hallzee-XXXX — Paired` when the site already has
permission to access that device, even if the connection then fails. This is
Chrome's own badge, not part of the advertised name, a live-connection indicator,
or proof of Hallzee ownership or a working OS bond. Hallzee cannot suppress it.
See the [Chromium permission check](https://chromium.googlesource.com/chromium/src/+/main/content/browser/bluetooth/web_bluetooth_service_impl.cc#493)
and [chooser display inputs](https://chromium.googlesource.com/chromium/src/+/main/content/browser/bluetooth/bluetooth_device_chooser_controller.cc#338).
Do not clear site data to remove the badge; that would delete the owner key and
classroom records.

A Mac is sufficient for automated regression checks, but cannot verify this
ChromeOS failure. Retesting this issue requires the Chromebook and terminal;
a Windows PC is not required. Windows BLE authentication, notifications and
reconnect remain unverified and require separate Windows BLE hardware testing.

## Windows Chrome/Edge disconnect before the code prompt

On September 14, the user reported a disconnect before Hallzee's pairing-code
prompt in both Windows Chrome and Edge, while the website worked in Mac Chrome.
The older disconnect message omitted the active stage, so it cannot establish
whether Windows failed during connection, discovery, the encrypted read, or
notification setup. The physical failure cause is still unconfirmed.

Firmware now defers security negotiation until protected characteristic access.
The pinned ESP32 library otherwise requests security immediately on connection,
which can overlap Windows service discovery. The web client already performs
an encrypted TX read after discovery and before notifications/HELLO. This
correction keeps encryption, bonding and the Hallzee owner proof enabled. The
[ESP32 API contract](https://github.com/espressif/arduino-esp32/blob/3.3.11/libraries/BLE/src/BLESecurity.cpp#L217-L224)
documents the on-demand behavior. Chromium also documents that macOS handles
protected-access pairing transparently, while Windows requires an explicit
pairing attempt after an authentication error; see [secure characteristics](https://chromium.googlesource.com/chromium/src/+/main/content/browser/bluetooth/README.md#secure-characteristics).
That difference supports investigating initiation timing, but does not prove it
caused this hardware failure. A new web diagnostic also preserves the exact
stage, such as `connect / Disconnected` or `pairing / Disconnected`.

For the next check:

1. On the Mac, from the updated repository, run
   `bash scripts/flash-terminal-macos.sh --fast --display ili9341` for the already
   installed ILI9341 terminal. Wait for **Fast USB write verified**. No full flash,
   ownership reset or data deletion is required by this firmware change.
2. Deploy the updated web client, then **Check updates → Apply update** on Windows.
3. For first pairing, use an unclaimed test terminal showing its six-digit code,
   select it in Hallzee, and enter the code only when Hallzee asks. If the terminal
   still belongs to the Mac, release it from that owning Hallzee profile before
   testing a new Windows claim; merely disconnecting or forgetting OS Bluetooth
   does not transfer ownership. For an existing Windows owner, reconnect from its
   original browser profile without a code. Chrome and Edge have separate keys.
4. If it still disconnects, record the new stage, Windows/browser versions and
   whether the terminal shows a pairing screen or normal idle screen. Do not
   share codes, owner credentials or raw Bluetooth logs.

An initial **Unknown** name in the browser chooser and its later **Paired** badge
are browser discovery/permission state, not Hallzee ownership. Firmware still
advertises the stable name in its scan response; this change does not promise
to replace an unknown chooser name before the browser obtains it.

A Mac is sufficient to flash and run shared automated checks, but **is not
sufficient to verify this Windows fix**. A Windows BLE PC is required for Chrome
and Edge service discovery, protected-read security negotiation, notifications,
first-claim code entry, bond reuse and reconnect. Windows success after this
change remains unverified; Mac Chrome's reported success predates this change.

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
