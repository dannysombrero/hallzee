# Hallzee in Chrome and Edge

The web client is available as a **local development build**. Chromebook,
Windows and Mac physical Bluetooth acceptance is still pending; there is no
published classroom URL yet. Use the [contributor setup](testing-and-installation.md)
to preview it. Use fictional records during evaluation.

## Prepare your classroom

Use a regular, persistent browser profile (Google Chrome or Microsoft Edge) on the same computer. Guest/Incognito
profiles are unsupported. Open **Classroom & data** and save the classroom name,
teacher, room and IANA time zone (for example `America/New_York`). The computer
must use that time zone before terminal clock synchronization. Import a roster
CSV under **Student roster**, map Student ID and either full name or first/last
name, inspect the preview, then confirm merge or replacement. IDs retain leading
zeros. Roster deletion does not delete completed trips.

## Pair and reconnect

1. Release the terminal from any previous desktop/browser owner first. With no
   active pass, hold `*` and `#` for five seconds and release to open pairing.
2. Click **Connect terminal**, select the kiosk in **Find Nearby Terminals**, enter the terminal's six-digit passkey if unclaimed, confirm this
   is the same classroom, then click **Connect & Sync**. Choose the Hallzee
   device in your browser's picker. Chrome, Edge, or the operating system may separately
   ask for the same physical code. Enter it in that OS prompt too; typing it in
   Hallzee alone does not pair the OS. Hallzee first reads the encrypted terminal
   channel, allowing up to one minute for OS pairing, then starts the short
   application-authentication handshake. Hallzee does not save the code.
3. Wait for authenticated connection, completed sync and current pass status.
   **Unknown** means the app has no fresh occupancy snapshot; it is not proof
   that nobody is out. Keypad operation continues when the browser disconnects.

This profile saves an owner key. After restart, Hallzee tries remembered devices
for up to 45 seconds. If the browser does not provide a remembered device handle,
click **Connect terminal → Choose saved terminal**. A chooser is never opened
by an automatic retry. Keep Bluetooth enabled even when internet is disconnected.

**Disconnect** preserves ownership. **Terminal settings → Disconnect & Unpair**
first syncs, requires no active passes, and releases the owner. Local keys are
removed only after the terminal confirms release. A failed/ambiguous release
retains the key. Clearing browser Bluetooth permissions does not release the
terminal. Losing this profile/site storage loses ownership; a data backup cannot
restore it. Use the previous client to release or physical owner recovery.
Unpairing does not sanitize records for a different teacher; cross-teacher
handoff is outside this version.

### Switching browsers (Chrome ↔ Edge)

Google Chrome and Microsoft Edge maintain separate local storage, IndexedDB databases,
and nonextractable Web Crypto keys. Switching browsers requires transferring your classroom
records and explicitly re-authenticating the terminal:

1. In your current browser, open **Classroom & data → Download backup** to save an unencrypted
   backup JSON to district-approved local storage.
2. In **Terminal settings → Disconnect & Unpair**, release the terminal owner.
3. Open Hallzee in the target browser (Chrome or Edge).
4. In **Classroom & data → Restore backup**, import your backup JSON to restore roster and history.
5. Click **Connect terminal**, enter a new pairing code from the terminal (`*` and `#` for five
   seconds), and pair.

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

Current passes have individually targeted **Check in** controls. Trip history
supports search/date/status/section filters and exports every filtered row, not
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

## Offline use, updates and projection

Wait for **Ready offline** before disconnecting internet. Initial setup and
application updates download static files; roster, trips and keys remain local.
Browser/profile clearing can remove both the offline app and classroom data.
Storage persistence requests are best effort; keep a backup even when granted.

For this local development build, bookmark `http://localhost:4190/` in the same
browser profile. After **Ready offline**, you can stop the local server and reopen
that exact URL; no hosted site is needed. Do not switch to `127.0.0.1`, another
port or another profile, because that has different storage and permissions.
The development server on port 5173 does not install an offline worker. To get
new code after an edit, run `bash scripts/web-client-macos.sh preview` again
(Windows: the matching PowerShell bootstrap with `preview`), then use **Check
updates → Apply update**. Refresh alone intentionally keeps the installed build.

If connection fails, report the displayed step/category, such as
`pairing / NetworkError` or `service / NotFoundError`; do not send the pairing
code. Check Bluetooth is on and close other terminal clients. Pairing failures
may require reopening physical pairing mode and completing the OS prompt. A
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

A Mac is sufficient for shared software testing and establishing Edge-on-Mac
support. It does not verify managed Chromebook policy, storage eviction, BLE
pairing or sleep/wake. A Windows BLE PC is required to verify Windows OS passkey
flow, encrypted GATT, bond reuse, reconnect/sleep and installed-PWA behavior in
Chrome and Edge. **Windows behavior is unverified.**
See [acceptance evidence](testing/web-client-acceptance.md).

If the connection error says **Bluetooth permission blocked** on a Mac, enable
your browser (Chrome or Edge) under **System Settings → Privacy & Security → Bluetooth**,
then quit and reopen the browser. The browser's chooser showing **Paired** does not
establish that GATT connection or Hallzee ownership succeeded. If the error instead says
**Bluetooth connection failed**, close other connected apps, power-cycle the
terminal (do not factory reset), reopen physical pairing mode, and retry nearby.

On macOS, if the terminal is already connected in **System Settings → Bluetooth**
and the browser fails at `connect / NetworkError`, disconnect it there and retry from
Hallzee's chooser. Disconnecting is sufficient; do not forget the device, clear
Hallzee data, or reset ownership. This recovered the reported local Mac connection.

If the OS asks for a new code when reconnecting an owned terminal, see
[Bluetooth repair and keypad troubleshooting](bluetooth-repair-and-keypad.md). Updated firmware
adds **hold `*` alone for five seconds** to repair the OS bond while retaining
ownership. The browser claim-code field is for unclaimed terminals only.
