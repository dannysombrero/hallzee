# Hallzee in Chrome and Edge

The web client testing site is [web.hallzee.com](https://web.hallzee.com); a local development
build is also available through the [contributor setup](testing-and-installation.md).
Physical Bluetooth acceptance remains pending on Chromebook, Windows, and Mac
for the updated pairing flow. Use fictional records during evaluation.

## Prepare your classroom

Use a regular, persistent browser profile (Google Chrome or Microsoft Edge) on the same computer. Guest/Incognito
profiles are unsupported. Open **Classroom & data** and save the classroom name,
teacher, room and IANA time zone (for example `America/New_York`). The computer
must use that time zone before terminal clock synchronization. Import a roster
CSV under **Student roster**, map Student ID and either full name or first/last
name, inspect the preview, then confirm merge or replacement. IDs retain leading
zeros. Roster deletion does not delete completed trips.

## Pair and reconnect

Update both the web app and terminal firmware for this flow; updating the app
alone cannot change the terminal's Bluetooth pairing method. Use the
[firmware installer](testing-and-installation.md#assemble-or-flash-a-terminal).

1. With no active pass and no existing owner, hold `*` and `#` for five seconds
   and release. The terminal displays its name and a six-digit pairing code.
2. Click **Find Nearby Terminals**, then select the matching name, such as
   `Hallzee-2A58`, in the browser's device chooser.
3. Enter the terminal's code in Hallzee's pairing dialog and click **Pair & Connect**.
   The code is entered once in the app; the Bluetooth chooser does not require those digits.
   The operating system may still ask permission to pair. Confirm this is the
   same classroom when requested, then wait for authenticated connection and sync.

**Unknown** pass status means the app has no fresh occupancy snapshot; it does
not mean nobody is out. Keypad operation continues when the browser disconnects.

This browser profile saves the owner key. Reopening the same app/profile or
losing a connection starts a remembered-device reconnect attempt, with a
45-second budget. When the browser cannot restore its saved device handle,
click **Reconnect** and select the saved terminal in the chooser. Returning
owners need neither pairing mode nor a code, including after terminal reboot.
Automatic retries never open a chooser; they need the saved key, browser device
permission, Bluetooth enabled, and a terminal that is awake and in range.

Hallzee shows **Currently Paired**, **Not Paired**, or **Paired to other device**
when ownership is known. These describe Hallzee ownership for this app/profile,
not OS Bluetooth pairing or whether a student is out. The browser owns its
nearby-device chooser and does not expose every nearby terminal to the page.
Until Hallzee checks the live terminal, its row shows **Status unknown**, including
saved pairings; a saved row alone does not prove the terminal is nearby or still
has the same owner. Select a saved row to reconnect without re-entering a code.
Bluetooth names remain stable, without Hallzee-added `Paired` or `In Use` text.
Chrome's chooser can add **Paired** because this site was previously allowed to
access the device, even after a failed connection. Hallzee cannot remove that
browser badge; it does not confirm Hallzee ownership, a connection, or a working
OS bond. See the [Chromium permission check](https://chromium.googlesource.com/chromium/src/+/main/content/browser/bluetooth/web_bluetooth_service_impl.cc#493).
See [Chrome's device chooser](https://developer.chrome.com/docs/capabilities/bluetooth)
and [previously granted devices](https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetooth-getdevices).

**Disconnect** preserves ownership. **Terminal settings → Disconnect & Unpair**
first syncs, requires no active passes, and releases the owner. Local keys are
removed only after the terminal confirms release. A failed/ambiguous release
retains the key. Clearing browser Bluetooth permissions does not release the
terminal. Losing this profile/site storage loses ownership; a data backup cannot
restore it. Use the previous client to release or physical owner recovery.
Unpairing does not sanitize records for a different teacher; cross-teacher
handoff is outside this version.

If **connect / TimeoutError** is followed by **previous Bluetooth operation in
this tab**, Chrome still has the original operation pending; another tab is not
required. Close Hallzee completely and reopen the same URL/profile. If needed,
restart the Chromebook and power-cycle the terminal. Keep site data and
ownership. This happens before encryption and does not call for another flash
or pairing code. See the [single-tab recovery steps](bluetooth-repair-and-keypad.md#connect-timeout-followed-by-a-pending-operation-in-one-tab).
The native cause and hardware recovery remain unverified. A Chromebook is
required to retest; Mac-only checks are insufficient and no Windows PC is needed.
Windows native connection cancellation, encrypted GATT and reconnect remain
separately unverified.

A Chromebook retry after flashing and applying the web update reported
`pairing-write / Disconnected` through **Find nearby terminals**, then succeeded
through **Last Paired** and Chrome's chooser. Both use the same connection flow;
Chrome can require selection again even with a saved Hallzee credential. The
latest web correction asks the updated terminal to restore encryption **before**
the protected-write fallback. With the previously flashed Security Request
firmware, this needs only a web update. See the
[chooser-disconnect retest](bluetooth-repair-and-keypad.md#chooser-disconnect-during-the-encrypted-write).
One successful connection does not verify repeated reconnects or the new order.

For a saved terminal that fails with `pairing-write / NotSupportedError` and
`GATT_UNKNOWN_ERROR` after losing power, install the latest **terminal firmware
and web client**. The new fallback asks the terminal to restore encryption with
the existing bond, then requires protected access before authenticating with the
saved owner key. Reconnect in the original profile without opening pairing mode,
entering a code, forgetting Bluetooth, or clearing site data. See the
[power-cycle retest](bluetooth-repair-and-keypad.md#saved-owner-reconnect-after-a-terminal-power-cycle).
ChromeOS recovery still needs verification on the Chromebook and terminal;
Mac-only testing is insufficient. No Windows PC is required for this retest;
Windows encrypted GATT, notifications and reconnect remain unverified.

If `pairing / NotSupportedError` occurs **before** any code prompt, apply the
new web update with encrypted-write fallback. If that also fails, Hallzee now
reports `pairing-write` and a GATT category when available. Follow the
[pre-code Chromebook retry steps](bluetooth-repair-and-keypad.md#chromebook-encrypted-read-failure-before-the-code-prompt).
This needs Chromebook hardware verification; Mac checks are insufficient and a
Windows PC is not required. Already updated firmware needs no additional flash.

If Chrome reaches the code dialog but reports `pairing / NotSupportedError`
after submission, apply the updated **web client**. It keeps the first encrypted
connection open while you enter the code. Already updated firmware needs no new
flash for this correction. Follow the [Chromebook retry steps](bluetooth-repair-and-keypad.md#chromebook-encrypted-read-failure-after-entering-the-code).
Verification requires the Chromebook and terminal; Mac checks are insufficient,
and no Windows PC is needed for this retry. ChromeOS success is still unverified.

For the reported Windows Chrome/Edge disconnect before code entry, install the
current firmware and web client, then follow the [Windows retry steps](bluetooth-repair-and-keypad.md#windows-chromeedge-disconnect-before-the-code-prompt).
The firmware correction defers security until protected access after discovery;
its Windows hardware result remains unverified. Mac-only testing is insufficient:
a Windows BLE PC must verify discovery, encryption, notifications and reconnect.

### Switching browsers (Chrome ↔ Edge)

Google Chrome and Microsoft Edge maintain separate local storage, IndexedDB databases,
and nonextractable Web Crypto keys. Switching browsers requires transferring your classroom
records and explicitly re-authenticating the terminal:

1. In your current browser, open **Classroom & data → Download backup** to save an unencrypted
   backup JSON to district-approved local storage.
2. In **Terminal settings → Disconnect & Unpair**, release the terminal owner.
3. Open Hallzee in the target browser (Chrome or Edge).
4. In **Classroom & data → Restore backup**, import your backup JSON to restore roster and history.
5. Open pairing mode on the terminal (`*` and `#` for five seconds), click
   **Find Nearby Terminals**, select it, then enter the code in Hallzee.

> [!NOTE]
> Bluetooth repair alone (`*` for five seconds) restores a lost OS Bluetooth pairing, but it
> does not transfer terminal ownership between browsers because the cryptographic owner key
> lives in the originating browser profile.

### Mozilla Firefox

Mozilla Firefox supports local classroom tools, roster importing, trip history, reports, and
policies. However, Firefox currently lacks Web Bluetooth, so direct terminal connection is
not possible within Firefox. Direct terminal connection requires Chrome or Edge until a planned
local Bluetooth companion daemon is released.

## Daily use and local data

Current passes have individually targeted **Check in** controls. Teachers can also start
a pass directly from the computer by clicking **+ Start Pass**. Teacher-started passes are
saved in local browser storage, resume automatically after a restart, start a live timer on
the dashboard, and record a completed trip with status `MANUAL` upon clicking **Check in**.
Trip history supports search/date/status/section filters and exports every filtered row, not
only the visible page. CSV and JSON downloads contain unencrypted student data;
choose a district-approved local destination. Nothing is uploaded by Hallzee.

Policies and bell schedules save locally. The UI distinguishes pending changes
from terminal-acknowledged settings. The terminal receives pass capacity and up
to 96 dated bell windows covering the next 14 days. Missing dates allow passes;
returning students can always check in. Overdue/daily guidelines are advisory.
This version uses first/last bell-window rules; newer desktop-only policy modes
are not included. New overnight or overlapping periods are rejected.

Under **Classroom & data**, download a backup regularly and before clearing
browser data. Backups include classroom, roster, policy and completed trips,
but exclude owner keys, device hints, live occupancy and sync cursors. Review
counts before confirming a restore. Restore replaces classroom data atomically,
retains this profile's existing keys, disables automatic connection and resets
history replay to zero. Desktop SQLite backups are a different format.

**Recover terminal history** replays retained terminal records and deduplicates
identical rows. Conflicting trip IDs stop sync. Export/review records before
**Start a new terminal history**, which deliberately deletes this terminal's
local rows and is only for a known factory reset/flash rollback.

## Virtual terminals

Use **Start Virtual Terminal** in the header. The open status and code remain
there; select it to copy a join link, show the QR or display the code to the class.
See [Virtual terminals and student joining](virtual-terminal.md) for student entry at
`pass.hallzee.com`, explicit Bluetooth switching and session limits.

## Offline use, updates and projection

Wait for **Ready offline** under **Classroom & data → Storage & Backup** before disconnecting internet. Initial setup and
application updates download static files; roster, trips and keys remain local.
Browser/profile clearing can remove both the offline app and classroom data.
Storage persistence requests are best effort; keep a backup even when granted.

For this local development build, bookmark `http://localhost:4190/` in the same
browser profile. After **Ready offline** appears under **Classroom & data → Storage & Backup**, you can stop the local server and reopen
that exact URL; no hosted site is needed. Do not switch to `127.0.0.1`, another
port or another profile, because that has different storage and permissions.
The development server on port 5173 does not install an offline worker. To get
new code after an edit, run `bash scripts/web-client-macos.sh preview` again
(Windows: the matching PowerShell bootstrap with `preview`), then use **Check
updates → Apply update**. Refresh alone intentionally keeps the installed build.

If connection fails, report the displayed step/category, such as
`pairing / NetworkError` or `service / NotFoundError`; do not send the pairing
code. Check Bluetooth is on and close other terminal clients. Open physical
pairing mode only for a new claim; saved-owner reconnect needs neither that mode
nor its code. Complete any OS permission prompt if shown. A
missing service points to device selection or firmware. Browser-native error
payloads, device identifiers and pairing secrets are not included in diagnostics.

A Mac is sufficient to retest this Mac connection fix; a Windows PC is not
required for that retest. Windows-specific OS pairing, encrypted GATT,
reconnect/sleep and PWA behavior remain unverified.

Installing the app from Chrome or Edge is optional. **Check updates** downloads an update
without replacing a running lesson. Save your work, close other Hallzee windows,
then **Apply update**; this disconnects Bluetooth, activates and reloads once.
An interrupted update leaves the existing offline app intact.

**Mini window** opens a compact Document Picture-in-Picture view where supported.
**Projection view** provides a fullscreen-capable fallback. Both show counts,
elapsed times and period status without student names or IDs. Tab-only screen
sharing may omit PiP; verify the actual projector/sharing mode before class.

### Dashboard interface and layout parity

The web client dashboard matches the Hallzee Universal desktop client layout:

- **Top Window Header Bar**: Canonical blue gradient header with Hallzee brand mark, classroom and room breadcrumb, Mini Window / Picture-in-Picture trigger, offline readiness status, and build version.
- **Left Sidebar**: Canonical 42×42 brand icon, inset Classroom Profile box, primary navigation menu (Dashboard, Trip History Log, Student Roster with live count badge, Policies & Bell Times, Settings), and bottom Terminal Node status card with transport state and quick sync/pair actions.
- **Hero Active Pass Card**: Prominent status indicator (`PASS READY`, `PASS OCCUPIED`, or `STATUS UNKNOWN`), 56×56 status avatar, large heading, and floating quick-action card with live monospace elapsed timer and teacher check-in.
- **2-Column Dashboard Grid**:
  - **Recent Activity**: Today's completed trips with avatar icons, departure/return timestamps, elapsed duration, status badges, and one-click CSV export.
  - **Roster & Pass Policy**: Live policy parameters, active roster size, and warning thresholds.
  - **Exceeded Time**: Trips exceeding the classroom warning threshold with quick duration presets (5m, 7m, 10m, 15m), search filter, and student breakdown.

A Mac is sufficient for shared software testing and establishing Edge-on-Mac
support. It does not verify managed Chromebook policy, storage eviction, BLE
pairing or sleep/wake. A Windows BLE PC is required to verify Windows Bluetooth
permission/Just Works pairing, encrypted GATT, bond reuse, reconnect/sleep and installed-PWA behavior in
Chrome and Edge. **Windows behavior is unverified.**
See [acceptance evidence](testing/web-client-acceptance.md).

If the connection error says **Bluetooth permission blocked** on a Mac, enable
your browser (Chrome or Edge) under **System Settings → Privacy & Security → Bluetooth**,
then quit and reopen the browser. The browser's chooser showing **Paired** does not
establish that GATT connection or Hallzee ownership succeeded. If the error instead says
**Bluetooth connection failed**, close other connected apps, power-cycle the
terminal (do not factory reset), and retry nearby. Open physical pairing mode
only for a new claim; a saved owner reconnects without it.

On macOS, if the terminal is already connected in **System Settings → Bluetooth**
and the browser fails at `connect / NetworkError`, disconnect it there and retry from
Hallzee's chooser. Disconnecting is sufficient; do not forget the device, clear
Hallzee data, or reset ownership. This recovered the reported local Mac connection.

If an old OS bond prevents reconnecting an owned terminal, see
[Bluetooth repair and keypad troubleshooting](bluetooth-repair-and-keypad.md). Updated firmware
adds **hold `*` alone for five seconds** to repair the OS bond while retaining
ownership. The browser claim-code field is for unclaimed terminals only.
