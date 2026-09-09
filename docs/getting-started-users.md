# Getting started: normal users

Use this page if you received a Hallzee terminal and need to run the finished
sync application. Developers should use [Getting started: developers](getting-started-developers.md).

## What you need

- A powered Hallzee terminal with its ESP32, TFT, and keypad already wired.
- A Windows PC for the current ready-to-run Universal download.
- Bluetooth enabled on the PC.

The downloadable Universal artifact is a self-contained Windows x64 app. A
Mac build is checked by the workflow but is not currently published as a
ready-to-run download; Mac users should follow the developer build instructions.

## Download the latest Universal app

1. Open the repository's **Actions** tab.
2. Choose **Build Universal Sync Client**.
3. Open the latest successful workflow run. If needed, choose **Run workflow**
   first and wait for it to finish.
4. Under **Artifacts**, download `HallzeeSync-Universal-Windows`.
5. Extract the entire downloaded ZIP to a normal folder. Do not run the EXE
   from inside the ZIP.
6. Start `HallzeeSync.Universal.exe`.

No .NET installation is required for this self-contained Windows artifact.

## Connect the terminal the first time

1. Power on the terminal. You can pair directly from any date/time setup
   step, or enter the date and time on the keypad first.
2. Make sure no student pass is active.
3. Hold `*` and `#` together for five seconds, then release both keys.
4. The terminal displays its full unique ID, friendly name, and six-digit
   Bluetooth passkey. If pairing expires during clock setup, that setup step returns.
5. In the app, choose **Find Terminal** or **Find Terminals**.
6. Select the matching `Hallzee-XXXX` terminal. Each row shows **Signal Strength:**,
   a four-bar indicator, the quality label, and RSSI in dBm. More filled bars
   mean a stronger signal; unavailable readings leave all bars gray.
   Enter the six-digit passkey when prompted, then choose **Connect** again if
   the dialog asks.
7. Choose **Sync Now**.

The terminal can have one active owner connection. A claimed terminal or one
with an active student checkout is shown as **IN USE**, even when its pass is
available. Unclaimed, unoccupied terminals show **READY**. The selected row has
a blue background and border. The remembered owner can reconnect
without the pairing passkey so the checkout can be completed; other clients
cannot claim or connect to it.

## Everyday operation

- Students enter their ID and press `#` to check out.
- They enter the same ID and press `#` to check back in.
- Press `*` to clear an ID that is being entered.
- Leave the app connected: completed trips stream automatically and a
  five-minute reconciliation sync recovers missed notifications. **Sync Now**
  remains available for an immediate check.
- Use **View All** or **History** to inspect the full **Hall Pass Trip History** table, sorted and filterable by Date, Student ID, Name, Departed, Returned, Duration, and Status. Use **Export** to create a CSV report.
- In **Settings**, save the teacher name and school to update the classroom
  profile card immediately. The OS title bar shows **Hallzee Desktop Client ·
  terminal name**. The **Device** tab shows the friendly name, selectable unique
  device ID, and connection status (including connecting, syncing, and offline).
- In **Settings → Device**, click the pencil beside the terminal name, edit it,
  and choose **Save** or **Cancel**. Saving requires a connection and confirmation
  from the terminal; **Apply ID Limit** changes only the student-ID limit.
  The name is persisted by the kiosk and reused in discovery, reconnect UI, and
  the terminal header as **Terminal: [name]**. Long names are abbreviated in the
  narrow header; pairing displays the full name.
- **Settings → Device** also shows the terminal's firmware version. After the
  one-time OTA USB setup, use **Install firmware from file…** with a signed
  `.hallzee-fw` package, or **Check for software updates → Download & Install
  firmware**. Check in all passes first and keep the terminal powered nearby.
  See [Firmware updates](firmware-updates.md) for the rollout/verification status.
- To release a terminal, connect to it, check in all active passes, and choose
  **Settings → Device → Disconnect & Unpair**. The notice explains that the
  next connection requires physical pairing mode and a new passkey. This removes
  the terminal owner, its BLE bonds, this computer’s saved owner credential, and
  its workspace assignments; trip history, terminal name, and settings remain.
  Install the matching current firmware first. If release is not confirmed, the
  app keeps the saved pairing and reports the failure. An OS Bluetooth entry may
  remain; use **Forget/Remove device** there if a fresh pairing reports a stale bond.
- In **Policies & Bell Times**, keep one teacher workspace, create `Regular`
  or alternate schedule templates, assign a class section to each period, and
  add date exceptions for early-release or no-school days. Bell-window actions
  are desktop guidance unless **Enforce bell-time lockouts on the terminal** is
  selected; it is off by default. When enabled, the next 14 days are copied to
  the kiosk and refreshed by sync.
- In the **Exceeded Time** dashboard card, filter by duration threshold (e.g. `> 7m`)
  and timeframe (e.g. **Last 2 Weeks**, **Today**, **This Week**, or **All Time**)
  to spot students with high hallway time.

The app remembers the terminal owner credential on supported production builds,
so normal reconnects should not require the passkey again. After the first
successful connection, the app remembers the terminal's Bluetooth transport and
automatically reconnects to it when the app starts or the link drops. The app
then syncs without requiring the date/time or pairing screens. The reconnect
message counts down for up to 45 seconds; after that, use **Find Terminal** to return
to manual recovery. The dashboard also leaves a **Reconnect** action for the
remembered claimed terminal; that action does not require a pairing key. If the terminal was replaced, reset, or paired to a
different computer, contact the project owner before clearing its owner state.

## Put the Mini Window board on display

Choose **Mini Window** in the app's top bar to open a compact, always-on-top
window. It remains visible while the main Hallzee app is minimized and displays
the current class period, a live digital clock, and pass status (e.g.
**WINDOW CLOSED** during locked windows or outside a scheduled period, green
**WINDOW OPEN** when passes are allowed (including Warn windows), or orange
**PASS IN USE** whenever a student is out) with live period countdowns.
The window includes close and minimize controls at the top left (and supports the
`Esc` key to dismiss). Set each period's start and end time in **Policies & Bell Times**
for this display to be accurate.

`Warn` windows show a warning but allow checkout. `Lock` windows reject only
new checkouts; an already-out student can always check back in. The current
kiosk has no speaker, so selected alert sounds play on the desktop only.

## If something does not work

- Confirm the terminal is powered on and showing **AVAILABLE**.
- Close other Bluetooth apps that may be connected to it.
- Use **Scan Again**, then retry the connection.
- If Windows asks for Bluetooth permission, allow the app to use Bluetooth.
- If a valid terminal name produces `SETTINGS_ERROR,TERMINAL_NAME,INVALID_VALUE`,
  update the terminal firmware and retry **Settings → Device → pencil → Save**.
  Older firmware used an overlength storage namespace, preventing every name
  save and incorrectly reporting a storage failure as an invalid name. The fix
  does not require erasing the terminal or resetting its owner.
- If reconnect reports that the terminal is **UNCLAIMED**, flash the current
  terminal firmware and claim it again with the six-digit pairing key.
- Do not factory-reset or send `OWNER_RESET` unless you are intentionally
  reassigning the terminal; that is a developer/test recovery action.

Windows is required for this ready-to-run packaged app and Windows-specific BLE
behavior. Mac testing is sufficient for the shared app behavior and macOS BLE
path, but the current Windows artifact has not been verified on macOS.

## Add students and share workspace settings

In **Classroom Roster & Students**, choose the teacher workspace, enter the
student ID and name, optionally add grade and class/period, then click
**Add Student**. Numeric IDs keep leading zeros. An existing ID is rejected
without replacing that student's details. CSV import remains available.

In **Policies & Bell Times**, choose **Export Workspace** and save the
`.hallzee.json` file. Export saves the current workspace settings and includes
pass rules, bell schedule templates, class sections, and date exceptions.
It excludes student rosters, trip history, terminal claims, and credentials.
The other teacher chooses **Import Workspace** and selects that file. Import
creates and selects a separate workspace with new identifiers; existing
workspaces are preserved. Use **Save Workspace Settings** while connected to
apply the imported rules to that teacher's terminal.

For a terminal pass, desktop **Check In** asks the terminal to complete and
record the trip. Recent Activity receives one terminal record even if the
notification is replayed by a later sync. Reconnect before checking in a
terminal pass while offline. Teacher-started passes are recorded locally.
The fix prevents new duplicate records; it does not delete older history.
