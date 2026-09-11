# Web client IT guide

**Status:** Development artifact only; no production origin or approved district
Chrome or Edge version is selected. Managed Chromebook and physical Windows/Mac BLE
acceptance are pending. Do not interpret a successful headless test as deployment
approval. [Acceptance checklist](Web-Client-Acceptance.md).

## Hosting contract

Host `web-client/dist/` as static files at the root of a dedicated, stable HTTPS
origin, separate from the marketing/waitlist website. No API, cloud database,
analytics, remote error reporting, third-party scripts or remote fonts belong
on this origin. Apply `dist/_headers` (or translate it to the host's header
syntax); verify the actual HTTPS responses before a pilot. Development preview
uses the same headers on localhost, which is a browser secure-context exception.
Use revalidation for HTML/worker/build metadata and immutable caching for hashed
assets. Retain prior hashed files while old workers may still be installed.

Changing hostname, scheme, port, browser (Chrome vs. Edge), or browser profile creates a
different storage and permission boundary. Switching between Chrome and Edge does not
transfer local IndexedDB data or cryptographic owner keys. Plan owner release/reclaim and
data backup explicitly; restoring data does not restore ownership. Publish exact matching AGPL source
and the artifact's notices. `build.json` records version, build hash, source
commit, dirty-source status and pending acceptance. An artifact with uncommitted
changes is for local review, not release provenance.

## Browser and district settings

The bundle targets Chrome 116 and Edge 116 syntax, but feature detection governs operation;
116 is not an approved production minimum. Require secure contexts, Web Crypto,
IndexedDB and Web Locks for the local app; Web Bluetooth for terminal operation;
service workers/cache storage for offline use. Mozilla Firefox supports local classroom
data and offline storage, but lacks Web Bluetooth for direct terminal connection (deferred
to a future local helper daemon). Document PiP is optional. Saved
Bluetooth `getDevices()` support varies; an explicit picker remains available.
Do not require experimental browser flags in a classroom deployment.

Allow the approved site and Bluetooth device access in district policy. Review
browser policies for Chrome and Microsoft Edge such as `DefaultWebBluetoothGuardSetting`,
`WebBluetoothAskForUrls` and `WebBluetoothBlockedForUrls` for the deployed browser
version. A blocked chooser is an IT configuration issue; the app cannot bypass
it. Test the actual managed student/teacher OU, OS Bluetooth permissions,
encrypted pairing and notifications before enabling a pilot.

Use persistent individual teacher profiles. Guest/Incognito, forced profile
reset, scheduled site-data clearing and browser cleanup tools can erase roster,
trips and the nonexportable key. IndexedDB CryptoKeys are origin-bound browser
storage, not a hardware-backed vault. Persistence grants do not protect against
explicit clearing, account removal or device reset. District backups must include
an explicit data export workflow; owner keys are deliberately excluded.

Optional managed PWA installation must use the same approved origin and scope
`/`. Verify offline restart both in a tab and installed app. Only one Hallzee
window can own the runtime. Updates wait for teacher action and closed competing
windows; they do not automatically restart an active classroom.

## Approval evidence

Record OS/browser/build/firmware/tester/date and results for secure first/return
pairing, permission revocation, lid sleep, profile persistence/eviction, offline
PWA restart, update/rollback and actual smartboard projection. Run the documented
six-hour/100-trip soak and lowest-powered Chromebook performance checks.
A Mac covers shared automated checks and establishes Edge-on-Mac support. Windows requires
a Windows BLE PC for OS passkey, encrypted GATT, bond reuse, reconnect/sleep and installed-PWA
checks in Chrome and Edge; **Windows behavior is unverified**. Managed ChromeOS acceptance remains separate.
