# Virtual terminals and student joining

The web client has two terminal actions in its header: **Start Virtual Terminal**
and **Connect Bluetooth Terminal**. A virtual terminal uses student devices or
a door tablet and requires internet. A Bluetooth terminal uses the physical kiosk.
The dashboard manages one terminal type at a time.

## Teacher workflow

1. Select **Start Virtual Terminal**, choose a recognizable code or keep the random
   suggestion, and start. Codes contain 3–16 letters, numbers or hyphens, begin with
   a letter or number, and ignore case. `ROOM-2107` is a synthetic example.
2. Wait for **Virtual terminal open**. The header then shows that status and the
   code. Opening the header button again always returns to the sharing panel.
3. Share the **join address**, **terminal code**, **direct link**, or **QR code**.
   **Copy Link** copies the direct link; **Download QR** saves a local PNG;
   **Display Join Code** enlarges the QR, code and address for the class.
4. Keep the teacher window open with internet access. Closing the sharing dialog
   leaves the terminal running. Refresh resumes the same session and active passes
   while the relay still has the room. A connection interruption pauses student
   actions until the teacher reconnects.
5. Use **End Session** when done. Check in or void active virtual passes first.
   Choosing **Connect Bluetooth Terminal** during a virtual session explains the
   switch, ends that room, then opens Bluetooth discovery. Starting a virtual
   terminal disconnects Bluetooth; active physical passes must be checked in first.

A session expires 12 hours after its initial claim. Reconnecting does not extend
it. An expired or lost session needs an explicit restart; the UI never calls a
rejected claim or an unconfirmed connection “open.” Room codes may be reused after
closure/expiry and are not permanent ownership reservations.

The optional 4–6 digit recovery PIN can reclaim a live room on a different teacher
computer. It does not transfer local roster/history. Do not share the PIN with
students. Backups exclude room credentials and do not reopen a session on restore.

## Student workflow and addresses

The intended public entry point is **https://pass.hallzee.com/**. Entering the
teacher's code opens that terminal. A direct link such as
`https://pass.hallzee.com/room-2107` skips code entry, as does its QR code.
Students choose **Check out** or **Check in**, enter their numeric student ID,
and wait for confirmation. At capacity they can join the waitlist. A closed,
expired or disconnected room disables those actions.

The same app build serves both the teacher and student origins. The root at
`web.hallzee.com` opens the teacher dashboard; the root at `pass.hallzee.com` opens
code entry. `/join` is code entry on other hosts, including localhost and previews.
`/<code>`, the earlier `/pass/<code>`, `?room=<code>` and `?pass=<code>` routes work.
The dedicated subdomain and root-level code links were not in the earlier design
notes; they are documented and implemented here. DNS/hosting still require the
[deployment setup](web-client-deployment.md#student-join-origin-and-room-relay).

## State, privacy and storage

`relay/src/RoomCoordinator.ts` is the shared room engine used by the Cloudflare
Worker and the local test relay. It handles claims, capacity, room closure,
12-hour expiry, host loss/resume, waitlists and request confirmations. The relay
keeps live IDs, pass metadata and the recovery credential in memory only. A Worker
restart or eviction may lose a room; there is no persistent cloud room database.
Completed trips and rosters remain in the teacher browser's IndexedDB database.

Public snapshots contain anonymous pass timers and counts; they exclude student
IDs, roster names, destinations and notes. Request acknowledgments return only to
the requesting station. Teacher snapshots restore active IDs and metadata after
refresh. A student sees check-in success only after the teacher saves the trip.
The public code is a room locator, not proof of a student's identity.

Virtual trips use `VIRTUAL:<code>` storage identities and locally allocated record
IDs, separate from physical terminal replay IDs. Classroom backups retain those
trips and their destination/purpose metadata without copying room credentials.
Offline readiness is under **Classroom & data → Storage & Backup**. The offline
app shell does not make an internet-based virtual terminal work offline.

## Local preview and tests

Download/extract the repository ZIP first. No Git, .NET or Arduino installation
is required. The bootstraps install pinned Node and locked npm dependencies.
Start the client in one terminal, then the relay in another after installation
finishes:

```sh
bash scripts/web-client-macos.sh preview
bash scripts/web-client-macos.sh relay
```

On Windows x64, use the corresponding commands in two PowerShell windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/web-client-windows.ps1 preview
powershell -ExecutionPolicy Bypass -File scripts/web-client-windows.ps1 relay
```

Visit `http://localhost:4190/` as teacher and `/join` as student. The relay listens
on `127.0.0.1:4192`. These loopback addresses are for the same computer, not another
student device. The `check` bootstrap starts and stops its own test relay; do not
leave a separate relay running on that port while running checks.

Automated coverage includes sharing/copying, public joins, refresh recovery,
check-in, mode switching, unavailable/conflicting rooms, expiry, private snapshots,
backups and the existing Bluetooth/offline suites. Exceeded Time uses its selected
1/7/14/30-day window including today, threshold, period/name/ID search, and
Period/Name/Count sorting. Recent Activity and its export use today's trips.

A Mac is sufficient for the virtual workflow and automated software checks. A
Windows PC is required only to verify Windows-specific physical BLE discovery,
OS pairing, encrypted GATT, reconnect/sleep and PWA behavior. Those Windows
hardware behaviors have not been verified by this change. The native Cloudflare
runtime check passed, and the relay and student hostnames were bound on
2026-09-25. Public relay verification currently receives a Cloudflare browser
challenge; live student-device acceptance remains pending until that security
configuration is resolved. See the web client deployment guide for the verified
infrastructure state and release checks.
