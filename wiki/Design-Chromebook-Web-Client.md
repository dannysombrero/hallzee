# Hallzee v1.2 Chromebook and web client implementation plan

Status: implemented development client in `web-client/`; physical/platform
acceptance and deployment remain pending. See [current evidence](Web-Client-Acceptance.md).
The user authorized continued software implementation without a managed Chromebook;
M0's physical gate remains open. The requirements below remain the release contract.
Reviewed against repository commit `9fbf4b5` on 2026-09-11. “Must” and acceptance
IDs below are review requirements. Paths in the delivery sections identify implementation responsibilities. This plan does not authorize publishing a site.

## 1. Review of the original proposal

The browser client is feasible enough to prototype, but secure pairing on an
actual district Chromebook must be proven before a classroom pilot. Software
implementation proceeded with the user-approved deferral recorded above. Keep the
local-first design, React interface, direct BLE connection, and optional mini
window. Correct the following before implementation:

| Original proposal | Required correction |
| --- | --- |
| Persistent connection through sleep/freeze | Recovery after execution resumes; no guarantee of background BLE, timers, or alarms during sleep, freeze, discard, or browser closure. |
| `getDevices()` always provides silent reconnection | Reuse the current in-memory device when possible. Feature-detect remembered-device enumeration after reload. A chooser is an explicit fallback, never an automatic prompt. Google's sample still documents a flag requirement; test the exact deployed Chrome build without flags. |
| `SET_TIME`, `REQ_ACTIVE`, `PASS_ACTIVE` | These are not the current protocol. Use `TIME_CURSOR`, `GET_ACTIVE_PASSES`, `ACTIVE_PASSES`, and the supported singular fallback. |
| `SYNC_ALL` after every reconnect | Incremental cursor reconciliation normally; full replay only for explicit recovery. |
| Claim proof followed by normal operation | Implement `CLAIM_OK`, durable credential save, `CLAIM_COMMIT`, and `AUTH_OK`, including interrupted-commit recovery. |
| Any `IN_USE` terminal cannot reconnect | The claimed owner can authenticate while a pass is active. An unowned/ineligible client cannot claim it. Advertising names are not authoritative. |
| Existing C# tests contain cryptographic vectors | Current identity tests mostly check determinism, lengths, and formatting. Add literal known-answer fixtures consumed by both implementations. |
| SQLite automatically gives desktop compatibility | Compatibility also requires every migration, context table, and credential store. Desktop credentials live outside SQLite. Do not promise interchangeable `.db` files. |
| All data stays strictly in the browser | IDs/trips also reside on the terminal; deliberate downloads leave the sandbox. Static hosting still receives ordinary asset requests. Specify which information the application never uploads. |
| Mac tests cover the Windows web client | Chrome uses each platform's Bluetooth stack. Physical Windows Chrome testing is required before claiming Windows support. Native WinRT tests do not validate Chrome's path. |
| Existing preview provider can become production by toggling mode | It contains seeded students and simulated operations, and shares an application with waitlist endpoints. Separate production composition and origin. |

These browser limitations are grounded in Chrome's [page lifecycle guidance](https://developer.chrome.com/docs/web-platform/page-lifecycle-api),
[BLE documentation](https://developer.chrome.com/docs/capabilities/bluetooth), and
[remembered-device sample](https://googlechrome.github.io/samples/web-bluetooth/get-devices.html).

Additional findings from the code: firmware starts a **10-second authorization
deadline after `HELLO`**; terminal epochs encode local wall-clock components;
trip IDs can have legitimate gaps after power loss; and `LIVE_TRIP` can interleave
with a cursor stream. Each has an explicit rule below.

## 2. Decisions and release boundary

### Architecture decisions

1. Create **`web-client/`**, a static React/TypeScript/Vite/Tailwind application
   with its own `package.json`, lockfile, tests, and deployment artifact. Keep
   `preview-site/` as the public website and simulated design reference. Do not
   import its provider, mock data, API routes, server runtime, or database code.
   Reuse its appearance by porting presentational markup/icons into the new
   client. Shared UI package extraction is outside this release.
2. Serve production from one stable HTTPS origin, proposed
   **`https://app.hallzee.org`**. This is a proposed hostname, not an existing
   deployment. Before pilot data is created, the maintainer must record the
   actual origin in the release configuration. Different schemes, hosts, ports,
   and browser profiles have separate data and permissions. A hostname change
   is a migration, not a redirect that transfers browser storage.
3. Use **native IndexedDB behind a small typed repository**, with one database
   named `hallzee-web` and schema version 1. Do not implement a second storage
   backend, an in-memory production fallback, SQLite WASM, or OPFS in v1.2.
   IndexedDB meets transactional local storage and structured `CryptoKey`
   persistence requirements without a WASM/worker/VFS compatibility layer.
   Choose versioned JSON for web data backup and CSV for reports/rosters.
4. Preserve protocol v2 and current terminal security. Normal connections use
   existing firmware; recovery of a forgotten OS bond now requires the firmware
   repair gesture described below. An incompatible secure-pairing result blocks release on that platform;
   do not remove encryption/MITM protection to make a browser work.
5. The C# core and firmware define existing wire/business behavior. TypeScript
   is a second implementation with shared conformance fixtures, not a second
   specification. Explicit web differences in this plan override copying known
   desktop implementation shortcuts, particularly cursor advancement and logs.

SQLite remains a possible later choice if desktop database interchange becomes
a product requirement. Its official documentation describes distinct VFS,
worker, locking, and deployment tradeoffs; simply choosing SQLite does not
resolve them. See [SQLite WASM persistence](https://sqlite.org/wasm/doc/tip/persistence.md).

### Included teacher workflows

- One teacher workspace and one assigned terminal per browser profile/origin;
  multiple class sections and named bell schedules inside that workspace.
- First claim, returning-owner authentication, manual reconnect, disconnect,
  and confirmed owner release for moving the **same classroom** to another client.
- Live occupancy for up to eight terminal passes, elapsed/overdue indicators,
  teacher check-in of a selected terminal pass, incremental and recovery sync.
- Local roster editing and CSV import, trip search/filter/pagination, enriched
  CSV export, policy/schedule editing, and optional offline terminal enforcement.
- Local data backup/restore, storage health, optional PWA installation and
  offline launch, a privacy-safe mini window, and an in-page projection fallback.

### Explicitly deferred

Direct desktop `.db` import/export; portable owner-key backup; cloud accounts,
cloud synchronization, analytics, remote logging; browser firmware flashing or
OTA; teacher-started browser-only passes; audible/background alerts; simultaneous
desktop/browser ownership; multiple connected terminals; multi-teacher handoff;
mobile/iPad/Safari/Firefox support. Do not show working-looking controls for these.
Direct users to existing supported firmware-update tools in documentation.

A terminal supports one owner client ID. A desktop-owned terminal cannot simply
be opened by a new browser installation. Same-classroom migration requires a
final sync/export and confirmed release in the old client, then a new claim.
There is no automatic desktop history/roster migration. Import the roster CSV;
retain the desktop archive separately. Terminal replay can only recover records
still on the terminal, not desktop-only records or historical class attribution.

Cross-teacher reassignment is outside this release because current firmware
retains old trips without assignment-level authorization. Do not describe owner
release as secure data sanitization. See the existing
[reassignment proposal](Design-Terminal-Reassignment.md).

## 3. Source map and implementation boundaries

Read these existing files before their corresponding work package:

| Concern | Existing reference |
| --- | --- |
| Protocol overview | `docs/bluetooth-protocol.md` |
| Identity and claim | `receiver/windows/BathroomSync.Core/Protocol/TerminalIdentityProtocol.cs`, `TerminalSession.cs`; `firmware/terminal/TerminalSecurity.cpp`, `BluetoothSync.cpp` |
| Trips and ACKs | `receiver/windows/BathroomSync.Core/SyncSession.cs`, `TripSqliteRepository.cs`; `firmware/terminal/TripStorage.cpp` |
| Active passes and time encoding | `receiver/windows/BathroomSync.Core/Protocol/ActivePassProtocol.cs` |
| Roster rules | `receiver/windows/BathroomSync.Core/Roster/RosterCsvParser.cs`, `RosterService.cs`, `Domain/RosterModels.cs` |
| Policies | `receiver/windows/BathroomSync.Core/PolicyScheduleService.cs`, `Protocol/BellPolicyProtocol.cs`, `Domain/ProfileAndPolicyModels.cs` |
| Serialization pattern | `receiver/universal/Services/TerminalOperationCoordinator.cs` |
| UI reference only | `preview-site/app/hallzee/components/`, `pages/`, and `docs/client-ui-architecture.md` |

When an older design page contradicts current code, record the discrepancy in
the PR and update that page and its Wiki mirror. For example, the old active-pass
design still contains a `HELLO,1` example; it must not become web behavior.

Proposed runtime ownership:

```text
Teacher UI / HallzeeProvider                 Projection / PiP portal
                 \                         /
                    ApplicationController
                     /       |         \
          Repositories    SyncEngine    LifecycleCoordinator
            |                |                  |
        IndexedDB    TerminalOperationQueue <----+
                             |
                    WebTerminalSession -- WebTerminalCrypto
                             |
                 WebBluetoothTerminalConnection
                             |
                  ESP32 authenticated GATT
                 /                         \
          LittleFS completed trips    Preferences active passes/settings

Service worker: static app-shell cache only; no Bluetooth/session ownership.
```

`HallzeeProvider` subscribes to immutable application snapshots and dispatches
typed actions. It contains no protocol parsing, database transactions, retry
timers, credential handling, or fabricated discovery results.

## 4. M0 — prove browser and hardware feasibility first

Deliver `web-client/README.md`, a minimal development-only BLE spike using the
production UUIDs/security, and
`docs/testing/web-client-feasibility.md` plus its Wiki mirror. Reuse spike modules
in later packages; exclude the spike screen and raw diagnostics from production.

Perform these steps with fictional records and a test terminal:

1. Record OS version, exact Chrome version/channel, managed/unmanaged profile,
   terminal firmware version/commit, board, and Bluetooth adapter. Test normal
   Stable settings, without experimental flags, extensions, or Developer Mode.
2. Prove chooser discovery by service UUID, encrypted notification subscription,
   acknowledged writes, OS passkey interaction, app claim proof and commit,
   browser restart, owner authentication, and an occupied-owner reconnect.
3. Verify the actual order and timing of OS pairing prompts versus `HELLO`.
   Collect the app passkey before `HELLO` so human typing is outside its deadline.
4. Record availability and outcomes of `getDevices`, strict IndexedDB
   transactions, stored/reloaded non-extractable HMAC keys, Web Locks, service
   workers, and Document PiP. API presence alone is not a passing result.
5. Revoke site permission, power-cycle the terminal, sleep/wake, and discard the
   tab. Record which cases recover from an existing handle, which recover after
   reload, and which require a chooser. No silent-reconnect promise without this
   evidence.
6. Test a district-managed Chromebook early, including a policy-denied profile.
   `DefaultWebBluetoothGuardSetting=2` blocks access and `3` permits asking; it
   does not pre-authorize a device or replace physical pairing.
   See [Chrome Enterprise policy](https://chromeenterprise.google/policies/default-web-bluetooth-guard-setting/).

**M0 acceptance:** managed ChromeOS secure claim, persisted auth, trip transfer,
and basic recovery pass without flags. A Mac is sufficient for shared software
development and Mac Chrome testing, but not for Chromebook release acceptance.
A Windows PC with BLE and a physical terminal is required to claim Windows web
support: specifically Chrome's chooser/OS pairing, encrypted GATT notifications
and acknowledged writes, bond reuse, reconnect, sleep/wake, and installed PWA
behavior on Windows. **Windows behavior has not been verified by this review.**
No physical browser/platform combination has been verified by this review.

If ChromeOS secure BLE fails, stop the full-client rollout and report the exact
failing operation. An extension, native bridge, Web Serial, or protocol change
would require a revised architecture. Missing PiP or remembered-device enumeration
uses the fallbacks below and does not by itself block the client.

## 5. M1 — project skeleton, bootstrap, and contracts

Create these module groups; companion tests use the same basenames:

```text
web-client/
  package.json, package-lock.json, tsconfig.json, vite.config.ts
  index.html, public/manifest.webmanifest, public/icons/
  src/main.tsx, src/app/ApplicationController.ts, src/app/HallzeeProvider.tsx
  src/app/capabilities.ts, src/app/RuntimeLock.ts, src/app/errors.ts
  src/transport/BluetoothPort.ts, WebBluetoothTerminalConnection.ts, LineFramer.ts
  src/protocol/constants.ts, messages.ts, WebTerminalCrypto.ts
  src/protocol/WebTerminalSession.ts, TerminalOperationQueue.ts
  src/sync/SyncEngine.ts, ActivePassStore.ts, TerminalClock.ts
  src/lifecycle/AutoReconnectCoordinator.ts
  src/storage/LocalDatabase.ts, schema.ts, migrations.ts
  src/storage/CredentialRepository.ts, TripRepository.ts, WorkspaceRepository.ts
  src/storage/BackupService.ts
  src/domain/RosterService.ts, PolicyScheduleService.ts, BellPolicyProtocol.ts
  src/ui/Dashboard.tsx, TripsDialog.tsx, RosterDialog.tsx, PoliciesDialog.tsx
  src/ui/ConnectionDialog.tsx, TerminalSettingsDialog.tsx, DataSettingsDialog.tsx
  src/ui/ProjectionView.tsx, DocumentPictureInPictureButton.tsx
  src/pwa/service-worker.ts, updateCoordinator.ts
  tests/unit/, tests/browser/, tests/fixtures/
contracts/web-client/v1/identity.json, sessions.json, trips.json
contracts/web-client/v1/active-passes.json, roster.json, policies.json
contracts/web-client/v1/backup.schema.json, README.md
scripts/web-client-macos.sh, scripts/web-client-windows.ps1
.github/workflows/web-client.yml
```

Names after the first file in a directory are relative to that directory.
`contracts/` fixtures contain fictional data only. Record the source core/firmware
commit and an explanation for every deliberate compatibility difference.
Create the complete v1 schema definition in M1. M2 implements the database-opening
and credential subset; M3 implements the remaining repositories. Do not delay
real credential transactions until after the ownership package.

Provide `dev`, `build`, `typecheck`, `lint`, `test:unit`, `test:browser`, and `check`
package scripts. `check` runs typecheck, lint, unit tests, production build, and
browser tests against that build. Use Vitest and Playwright; pin exact dependency
versions in the first PR and commit the lockfile. Use the repository's Node 22
toolchain family; pin a maintained exact patch and official download checksums in
`web-client/toolchain.json`, consumed by bootstrap and CI. Do not use `latest`
downloads, unpinned scaffold commands, or require the preview server to run.

From a downloaded/extracted repository ZIP, these are the intended commands
**after M1 is implemented**, not commands that work today:

```sh
bash scripts/web-client-macos.sh dev
bash scripts/web-client-macos.sh check
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/web-client-windows.ps1 dev
powershell -ExecutionPolicy Bypass -File scripts/web-client-windows.ps1 check
```

Scripts locate the root from their own paths, install a checksum-verified local
Node/npm under ignored `.local/`, run `npm ci`, install the Playwright browser for
`check`, and invoke the requested action. Support paths with spaces, repeat runs,
Mac arm64/x64 and Windows x64; fail clearly on download/verification errors.
No Git, .NET, Arduino CLI, administrator install, or globally installed Node is
required. Bind development to `localhost:5173`; ordinary LAN HTTP is not the
Chromebook testing method. The managed Chromebook consumes the HTTPS test build.
Shared C# fixture checks run in CI with its installed .NET SDK.

Boundary types must include the following semantics (spellings may follow local
style, but do not merge the responsibilities):

| Interface | Required contract |
| --- | --- |
| `BluetoothPort` | `requestDevice()` only from a click; `getRememberedDevices()` optional; `connect(device, signal)` resolves after notifications are enabled; `sendLine(line, signal)` accepts no newline; line/disconnect subscriptions return disposers; `disconnect()` is idempotent. |
| `WebTerminalSession` | `open(device, expectedTerminalId?, passkey?, signal)` resolves only after verified `AUTH_OK`; `request(command, responseMatcher, signal)` permits authenticated commands only; application-message stream is typed. |
| `TerminalOperationQueue` | `run(kind, operation, signal)` serializes entire sync/settings/policy/release operations, with ACKs sent inside their owning sync operation. |
| `TripRepository` | `store(record, context)` resolves `saved`, `duplicate`, or `conflict` only after transaction completion; failures throw a typed storage error. `completeSync` persists watermark and last-success time atomically. |
| `ApplicationController` | Owns start/stop, runtime lock, services, subscriptions, action availability, and snapshots. A snapshot contains connection state, sync state, active-pass freshness, and storage state separately. |

Every async operation carries an `AbortSignal` and monotonically increasing
session generation. A timeout/cancel invalidates the generation. Browser GATT
promises cannot necessarily be cancelled; late resolution must disconnect the
stale connection and never update the current session. React Strict Mode
mount/unmount/remount must leave exactly one controller and one listener set.

**M1 acceptance:** both clean-machine commands work, no preview imports occur,
the production bundle has no simulator, and two tabs cannot start two runtimes.

## 6. M2 — transport, identity, and ownership

### BLE transport

Use exactly these UUIDs:

| Role | UUID |
| --- | --- |
| Service | `005924a2-c6e5-4340-9bb8-22d9dd37a283` |
| TX, terminal notifications | `44a359f3-9215-4189-a3cb-e7ce18ad40d6` |
| RX, client writes | `e80f9559-49eb-47bc-af04-8e92e98ced56` |

Request `{filters: [{services: [SERVICE_UUID]}]}` from a button handler. The
browser owns the device chooser. Do not reproduce the demo's RSSI list or claim
to know availability before reading `IDENTITY`. Persist `BluetoothDevice.id` only
as an opaque origin-specific hint, never as a MAC address or terminal identity.

On every connection, retrieve fresh service/characteristic objects, attach the
TX listener, await `startNotifications()`, then allow `HELLO`. Subscribe to
`gattserverdisconnected`. Require RX acknowledged writes and use
`writeValueWithResponse`; do not fall back to unacknowledged writes. Encode a
whole command plus one LF into UTF-8, then slice into **at most 20 bytes**. Await
each chunk; concurrent calls cannot interleave command bytes.

Reject embedded CR/LF and commands over **192 encoded bytes excluding LF** before
sending. For incoming values, honor `DataView.byteOffset/byteLength`, retain split
UTF-8 sequences, split on LF, and ignore CR. Bound a line to 4,096 bytes and queued
input to 64 KiB. Overflow or malformed UTF-8 aborts the session with a sanitized
protocol error. Clear partial bytes, decoder, and listeners on disconnect.
Notification handling must be serialized, including asynchronous storage work.

### Cryptography and validation

Create an installation ID with `crypto.randomUUID()`, canonicalize it to uppercase
hyphenated UUID format, and persist it before opening a session. Terminal IDs
match `HZ-[0-9A-F]{12}`; suffix is the final four hex digits; nonces are exactly
32 hex digits. Normalize terminal IDs/nonces to uppercase. The passkey is a
trimmed string of exactly six ASCII digits; preserve leading zeros.

Implement native Web Crypto operations with these exact byte inputs:

```text
P = UTF8(six-digit passkey)
T = normalized terminal ID
C = normalized client UUID
N = normalized handshake nonce

claimProof = HEX_UPPER(HMAC-SHA256(P, UTF8("CLAIM|2|" + T + "|" + C + "|" + N)))
ownerKey   = HKDF-SHA256(ikm=P, salt=UTF8(T),
                       info=UTF8("Hallzee owner v2|" + C), length=32 bytes)
authProof  = HEX_UPPER(HMAC-SHA256(ownerKey,
                       UTF8("AUTH|2|" + T + "|" + C + "|" + N)))
commitProof = authProof formula using CLAIM_OK's commit nonce, not N
```

Seed `identity.json` with these literal values. They were independently calculated
with Python HMAC/HKDF and cross-checked with Node Web Crypto during this review;
M2 must also verify them with the C# core and browser Web Crypto before treating
conformance as passed:

```text
T = HZ-A1B2C3D4E5F6
C = 12345678-1234-1234-1234-1234567890AB
N = 00112233445566778899AABBCCDDEEFF
commitNonce = FFEEDDCCBBAA99887766554433221100

passkey = 807481
ownerKeyHex = 9B17CE237306766920B0203FF2F20214E52A03567AAE538A18DDD4C2C29F75A9
claimProof = 04A7794FE6D144CD7C72DD78CEC7A12046829DC1B37E201AE1975ED3ACA9E5E8
authProof = 49977EFE46CAC1924BB43CAF46DC8CBAFEAF5F58439D798E9C0D25605D841D47
commitProof = D669E1B49A772B324ED1DC070769F86B45BB9B16D1ECA1A20D168899EC6CDAC6

passkey = 000123
ownerKeyHex = FD4A1154BA59762E060DED3A40FB1B8228ACD25DC76DC8D37F111340265A106C
claimProof = ACF3FE3D2CADB32F5CB086673FEA6C73D024149752F93408E9471C843FFE6EDD
authProof = 795E6A53E2642C8DC1B0A5A6100110FFC42B941095C6AAC48560A28A06D1ACAE
commitProof = 94328D7E370BB417E00ECE81C93760712BDD9230A90CA01CED37B08446C13C12
```

Do not hex-decode the HKDF salt or passkey. Output proofs contain 64 uppercase
hex characters. Derive a non-extractable HMAC-SHA256 `CryptoKey` with `sign` usage;
store the key object in IndexedDB. Never store the passkey or raw owner-key hex,
and never put credentials/proofs/raw lines in React snapshots or logs.
Non-extractability is an API restriction, not an OS vault or protection from
malicious same-origin scripts. Web Crypto specifies key serialization and
extractability separately; see [Web Cryptography](https://www.w3.org/TR/webcrypto-2/).

### Handshake and failure contract

```text
Disconnected -> Connecting -> AwaitingIdentity
  claimed + saved key -> AwaitingAuthentication -> Authenticated
  unclaimed + physical passkey -> AwaitingClaim -> SavingCredential
    -> AwaitingClaimCommit -> Authenticated
Any state -> Disconnected / Failed (typed reason)
```

First-pairing UI tells the teacher to open the physical five-second pairing
window, enter its six-digit code locally, then click **Choose terminal and pair**.
The OS may separately request that same code. Do not start `HELLO` while waiting
for the app passkey. Before HELLO, read the TX characteristic protected by
`ESP_GATT_PERM_READ_ENC_MITM`, allowing up to 60 seconds for operating-system
pairing; discard its value before attaching notification listeners. This keeps
the eight-second application handshake separate from the human OS prompt.
Discovery/notification/write steps keep ten-second per-operation limits; automatic
reconnect still has its overall 45-second budget. A saved-terminal reconnect does
not request an application passkey, although an OS bond may need to be repaired.

For a lost OS bond on an owned terminal, updated firmware adds **hold `*` alone
for five seconds** while idle (outside clock setup/touch mode). Wait for actual
disconnection before clearing terminal-side bonds; fail if it takes five seconds.
Display a fresh six-digit **BT REPAIR** code for two minutes. Keep CLAIM disabled,
retain the existing owner key and records, and require AUTH from the saved client.
The code belongs only in the OS prompt; the browser claim field stays empty.
On success or expiry, invalidate the temporary code. Expiry disconnects the
unauthenticated link. No reset of ownership is part of this operation. Physical
Mac/ChromeOS/Windows bond replacement remains a release acceptance gate.


1. Send `HELLO,2,<C>` and strictly validate `IDENTITY` including suffix/version,
   claimed/availability tokens, nonce, and any saved expected terminal ID.
2. For a claimed terminal, load the saved key and send `AUTH,2,<C>,<proof>`.
   `IN_USE` is allowed for its credentialed owner. Missing key, mismatched
   identity, or auth rejection stops retries and never deletes data.
3. For an unclaimed available terminal, send `CLAIM,2,<C>,<claimProof>`.
   Validate `CLAIM_OK,2,<T>,<commitNonce>` against the same terminal.
4. Commit `{clientId, terminalId, key, state:'pendingCommit'}` in one strict
   credential transaction; reopen/read and successfully sign a test challenge
   with the stored key. Only then send `CLAIM_COMMIT,2,<C>,<commitProof>`.
5. Validate `AUTH_OK,2,<T>,<customName>` in the correct state against the expected
   terminal. Mark the credential confirmed and save the workspace assignment.
   Only now can application commands run.

Use an eight-second client budget from successful `HELLO` write for the automatic
identity/claim/auth exchange, inside firmware's ten-second deadline. Show a
retryable timeout rather than looping while a teacher types. A wrong code gets
one attempt per submission; firmware ends pairing after three claim failures
and its physical claim window lasts 120 seconds.

If saving the credential fails **before commit is sent**, best-effort
`CLAIM_ABORT,2,<C>`, disconnect, and report storage failure. If commit might have
been delivered but `AUTH_OK` is lost, retain the pending key. On reconnect,
`CLAIMED` tries `AUTH` with that key; `UNCLAIMED` offers a fresh physical claim.
Never erase a potentially committed key on an ambiguous transport error.

Ordinary **Disconnect** suppresses auto-reconnect until explicit Connect and
retains everything. **Disconnect & Unpair** requires no active passes, completes
a final sync, then sends `RELEASE_OWNER`. Delete credential/assignment only after
`OWNER_RELEASED` and its local transaction commits; keep history/roster. A timeout
retains the key and says release was not confirmed. A subsequently observed
`UNCLAIMED` state can resolve a lost release acknowledgment through an explicit
local cleanup/reclaim action. Browser permission removal is separate from owner
release and must not substitute for it.

**M2 acceptance (A01–A05):** literal cross-language crypto vectors, complete
handshake transcripts, split/chunked byte tests, all identity mismatches,
out-of-order responses, pending-commit recovery, write/storage failures,
occupied-owner auth, and release-ack loss pass. No application command is emitted
before auth. M0 physical secure-pairing evidence remains required.

## 7. M3 — durable storage, synchronization, and time

### Database schema and lifecycle

Use these stores/key paths. All IDs are strings except positive terminal trip
IDs, which are integers in `1..4294967295`; student IDs always remain strings.
Local timestamps use ISO UTC strings only where explicitly marked UTC.

| Store | Primary key and required content |
| --- | --- |
| `app_meta` | `key`; entries for schema metadata, active workspace ID, installation UUID, auto-connect preference, last backup UTC, supported app build. |
| `workspaces` | `workspaceId`; name, teacher/school/room, classroom IANA time zone, created/updated UTC. UI creates one workspace. |
| `terminals` | `terminalId`; customName, protocolVersion, deviceIdHint, assignedWorkspaceId, cached maxIdLength, settings read UTC. Unique assignedWorkspaceId when assigned. |
| `credentials` | `terminalId`; installation clientId, non-extractable HMAC key, pendingCommit/confirmed state, created UTC. Excluded from data export. |
| `trips` | `[terminalId, tripId]`; studentId, tripDate, timeOut, nullable timeIn/durationSeconds, wire status, immutable normalized wire fields, received UTC, receivedWorkspaceId, nullable scheduleName/classSection and context source `resolved-on-receipt` or `unknown`. |
| `sync_state` | `terminalId`; completedCursor default 0, lastSuccessfulSyncUtc nullable, recoveryRequired, historyGeneration default 1. |
| `roster_students` | `[workspaceId, studentId]`; firstName, lastName, grade nullable, created/updated UTC. |
| `roster_enrollments` | `[workspaceId, studentId, classSection]`; section membership, referencing a roster student. |
| `policy_rules` | `workspaceId`; fields/defaults from `PolicyRule` (capacity 1, warning 420 seconds, daily guideline 2, first/last 10 minutes, Warn/Warn, terminal enforcement false); local revision and applied terminal revision/date nullable. Audible alerts omitted. |
| `bell_periods` | `[workspaceId, scheduleId]`; periodName, scheduleName, classSection, start/end `HH:mm`, weekday tokens. |
| `schedule_exceptions` | `[workspaceId, date]`; scheduleName and isNoSchool. |

Trip indexes: `[terminalId, tripDate, tripId]`,
`[receivedWorkspaceId, tripDate, tripId]`, and
`[receivedWorkspaceId, studentId, tripDate, tripId]`. Roster gets a workspace
index. Schema types must explicitly list fields; do not persist arbitrary UI
objects. Enforce references and unique assignment in repository transactions.

Use `readwrite` transactions with `{durability:'strict'}` for credentials, trips,
cursor updates, imports, and settings. Wait for transaction `complete`, not a
request's `success`, before resolving. Do crypto/CSV parsing outside live IDB
transactions so unrelated awaits do not auto-close them. Strict durability is a
browser storage contract, not a guarantee against physical device failure.
See [IndexedDB transactions](https://w3c.github.io/IndexedDB/).

Acquire an exclusive Web Lock named `hallzee-web-runtime` before opening the
active controller/database. Another tab/PWA shows **Hallzee is already open in
another window** and offers Retry, without connecting or editing. Do not steal
locks or use a timestamp heartbeat to decide another tab is dead. A frozen tab
could resume later. The [Web Locks API](https://w3c.github.io/web-locks/) provides
the same-origin exclusion mechanism. If unavailable, block live mode.

Use versioned `onupgradeneeded` migrations. Close on `versionchange`; surface
blocked upgrades. Failed migration must leave the old data intact and show a
recoverable error; never delete/recreate the database automatically. Probe
write/read/delete and key persistence before pairing. No private-mode detection
tricks: document Guest/Incognito/ephemeral profiles as unsupported storage modes.

After an explicit initial setup/import/pair action, request `storage.persist()`
once and show its actual result plus `storage.estimate()` usage/quota. A denial
permits use after a clear backup notice; it does not justify pretending storage
is permanent. User/IT clearing, profile removal, or device reset can still delete
data. See [persistent storage guidance](https://web.dev/articles/persistent-storage).

### Correct sync algorithm

After auth, run one serialized connection-initialization operation:

1. `GET_SETTINGS`; wait for valid `SETTINGS,MAX_ID_LENGTH,<4..16>`.
2. `GET_ACTIVE_PASSES`; wait for the snapshot. If and only if firmware answers
   `ERROR,UNKNOWN_COMMAND`, use `GET_ACTIVE_PASS` and label single-pass capability.
3. Validate the browser's configured time zone matches the workspace. Otherwise
   pause clock/sync writes and ask the teacher to correct the device time zone.
4. Send `TIME_CURSOR,YYYY-MM-DD,HH:MM:SS,<completedCursor>` using classroom local
   components. This command aligns time **and starts sync**. Do not separately
   issue `SYNC_ALL`, `SYNC_START`, or another clock command during the stream.
5. Accept `TIME_ACK,OK`, `SYNC_BEGIN,<count>`, `TRIP` messages, and `SYNC_END` through
   the typed stream parser. An error or timeout cannot mark sync successful.
6. After the stream, apply explicitly configured capacity and policy revision
   if needed, then re-query active passes. Policy writes are covered in M5.

Cursor correctness is stricter than taking `MAX(tripId)` from the database:

- A sync captures its starting `completedCursor` and tracks only records received
  as `TRIP` in that stream. For each `TRIP`, validate it, commit it durably, then
  send `ACK,<tripId>`. Identical duplicates are safe and are ACKed after verifying
  the stored content. Conflicting content is an error, never an overwrite.
- `LIVE_TRIP` is durably stored with the same key but **never ACKed and never
  advances the completed cursor**. Otherwise live trip 110 could skip missing
  historical trips 101–109 after a crash during catch-up from 100.
- On valid `SYNC_END`, after all preceding work/ACK writes have completed, commit
  the stream's maximum trip ID (or original cursor for an empty stream) and
  last-success UTC together. On interrupted sync, leave completedCursor unchanged;
  replaying already committed records is intentional.
- Trip IDs may skip integers. Require increasing stream IDs (allow an identical
  retransmission of the pending/last record); do not require numerical contiguity.
  `SYNC_BEGIN` count is an initial estimate: new trips can extend the stream.
  Do not reject a valid stream merely because it exceeds that count. A stream
  ending with fewer distinct streamed records than its initial count is incomplete.
- Enforce a ten-second inactivity timeout while expecting sync progress; reset
  on valid expected messages and successful durable processing. Do not impose a
  ten-second total timeout on a large history. Storage operations have their own
  ten-second timeout; on timeout stop the session without ACKing unconfirmed data.
- Malformed records, quota/transaction failures, or conflicts abort sync and
  disconnect to stop firmware waiting for an ACK. Preserve data and credentials;
  require explicit repair/Retry for storage or record errors.

Normal manual/periodic/reconnect sync uses this algorithm. Every five minutes
while running and connected, enqueue one reconciliation, coalescing duplicate
requests. `lastSuccessfulSyncUtc` changes only on completed streams.

**Recovery sync:** user chooses **Recover terminal history**, sees the target ID
and duplicate/conflict behavior, then sets `recoveryRequired=true` durably. At the
next sync, use `TIME_CURSOR,<local date>,<local time>,0` instead of the saved cursor;
this both aligns the clock and performs a full replay. Clear recoveryRequired
only on successful full replay. A disconnected recovery restarts at zero next
time. `SYNC_ALL` is an equivalent full-replay command for a separately requested
recovery on an already time-aligned session; do not send both commands for one
recovery or queue `SYNC_ALL` after every connection.

Current firmware has no history-epoch/high-watermark handshake. A physical factory
reset can reuse trip IDs on the same terminal and cannot always be detected.
Ordinary re-pairing does not mean history reset. For a **known** factory reset or
flash rollback, require export of the old terminal archive and an explicit
**Start a new terminal history** action: while disconnected, transactionally
remove only that terminal's trips/cursor, increment historyGeneration, and start
at 0. Reject conflicting replay and explain this recovery path. Do not promise
automatic reset detection or merge separate generations into one history.

### Wire records, provenance, and active passes

Firmware records are
`trip_id,student_id,date,time_out,time_in,duration_seconds,status,synced_flag`.
Accept the documented seven data fields and optional eighth `0|1` flag; ignore
the flag for immutable duplicate comparison. Reject extra fields until versioned
support exists. Accept statuses `COMPLETE`, `MANUAL`, `MANUAL_RESET`; preserve
empty return time/duration for reset records. Preserve legitimate empty time
fields from an unset terminal clock and show **Time unavailable**; do not invent
timestamps or durations. Reject malformed nonempty dates/times/numbers, negative
durations, and nonnumeric or over-16-digit student IDs. Do not reject older valid
IDs merely because the current terminal maximum was later shortened.

Identity comes from authenticated session context, never from an extra CSV field.
Compute schedule/class context on first insertion using checkout date/time and
workspace schedules; label it `resolved-on-receipt`, not historical proof of a
teacher assignment. Unknown times yield unknown context. Duplicate replay does
not replace context. Do not match names across workspaces or treat roster edits
as changes to wire records. Initial/recovered history is visibly identified as
terminal history; the setup flow states that only a same-classroom terminal is
supported. Assignment-aware historical isolation requires the separate design.

Parse `ACTIVE_PASSES` as zero or more `(studentId, epoch)` pairs, maximum eight,
with no duplicate student IDs; bare `ACTIVE_PASSES` means none. Also parse
`ACTIVE_PASS,NONE`, `ACTIVE_PASS,<id>,<epoch>`, and `EVENT,CHECKOUT|CHECKIN|RESET`.
Snapshots replace occupancy; events update the identified student only. After an
event, schedule a coalesced fresh snapshot through the operation queue. History
alone cannot prove occupancy. On disconnect, stale connection, or wake pending
reconciliation, occupancy becomes **Unknown**, never Available.

Terminal epochs are **local wall time encoded as though UTC**, per existing
`ActivePassProtocol.cs`. For a raw epoch, display its UTC date/time components
as classroom wall components; do not apply the browser UTC offset again. Compute
elapsed seconds from `Date.UTC(...current local components) / 1000 - rawEpoch`,
clamped at zero, recomputed each render tick rather than incrementing a counter.
Finalized trip duration from firmware is authoritative. DST or manual clock
changes can make wall-clock durations ambiguous; show a clock-change notice and
refresh, without rewriting recorded trips. Keep actual receipt/sync UTC separate.

**M3 acceptance (A06–A10):** transaction abort/quota faults send no ACK; a crash
before/after ACK or before cursor commit loses no recoverable records; interleaved
live/history regression passes; duplicates are stable; conflicts stop; sparse IDs
work; zero-trip and growing streams work; UTC offset/DST/reset-time fixtures and
eight-pass event/snapshot tests pass in a real browser database.

## 8. M4 — lifecycle and reconnection

`AutoReconnectCoordinator` may request work from the operation queue; it must not
directly write to GATT or call the picker. Store only the assigned terminal as
the automatic target. Use its in-memory device handle first. After reload, if
`getDevices` exists and succeeds, find the matching saved device ID hint. A
returned device is permission evidence, not evidence of proximity or availability.
Never try arbitrary permitted devices. Missing hint/permission/API produces
**Choose your saved terminal to reconnect**; verify its stable ID after selection.

Use one retry cycle: attempt immediately, then delays of **2, 5, 10, 15 seconds**
after failed attempts, repeating 15 seconds only if time remains, with a **45-second
wall-clock deadline including attempts**. Each GATT connect attempt has a
ten-second timeout bounded by the remaining cycle. No overlapping attempts.
At expiry show **Could not reconnect — Retry / Choose terminal**. Countdown is
derived from deadlines, not tick counts. A successful authenticated catch-up
ends the cycle and returns to Connected.
The 45-second budget applies to regaining an authenticated connection. Once that
connection succeeds, a long catch-up stream uses M3's progress timeout and may
outlast the reconnect budget; display Synchronizing until it completes.

| Trigger | Required action |
| --- | --- |
| Transient disconnect / powered-off terminal | Unknown occupancy; attempt same target within retry budget. |
| `visibilitychange` to visible, `focus`, `pageshow`, supported `resume` | Coalesce triggers; if still connected, query/reconcile rather than trust `gatt.connected`; otherwise start one reconnect cycle. |
| Long event-loop gap >10 seconds | Treat snapshot as stale; reconcile on next execution opportunity. |
| Hidden without freeze | Keep existing connection best-effort; timers may be throttled. No promise of alarms or uninterrupted delivery. |
| `freeze` or `pagehide` | Best-effort cancel work, disconnect, close DB, release runtime lock, and clear live state; normal correctness must also survive no lifecycle callback. |
| Resume or discarded-page startup | Acquire lock, reopen DB, discard all session state, restore assignment/key, reconnect if possible, reconcile. |
| User Disconnect / Unpair in progress | Cancel retry and queued work; do not auto-connect due to focus events. Explicit Connect re-enables the saved preference. |
| Permission/security error, missing key, identity mismatch, auth rejected, storage fault | Stop automatic retries. Offer the specific corrective action; retain records/keys. |
| Offline internet | Show offline-shell state; do not disable local BLE, roster, or history. `navigator.onLine` is not a Bluetooth test. |

`ApplicationController` teardown releases the runtime lock only after its
generation is invalidated and DB close/disconnect initiated. Do not depend on
unload to save records. A page that resumes without its lock cannot do work until
it reacquires it. A second window retries explicitly; it never steals leadership.

A PWA/service worker cannot keep this BLE session alive through OS sleep. Service
workers only cache the shell in this design; the Web Bluetooth API is exposed to
the window context in the [Web Bluetooth specification](https://webbluetoothcg.github.io/web-bluetooth/).

**M4 acceptance (A11–A13):** fake-clock tests cover retry bounds and cancellation;
browser tests cover two tabs, remount, permission/API absence, stale async
completion and reload; physical tests cover power-off, lid sleep, background tab,
discard and post-wake catch-up. Recovery latency is measured after execution
resumes and the radio is available, not while the laptop is asleep.

## 9. M5 — teacher workflows and backup

Implement only actions backed by working services. Use the existing screenshots
and preview layout as the visual reference, but start with an empty workspace,
Disconnected connection, Unknown occupancy, and no last-sync time.

| Surface | Specific completion criteria |
| --- | --- |
| Dashboard | Connection/permission/storage banners; unknown/available/occupied states; up to eight active passes; current period/window; recent completed trips; elapsed and overdue from the clock model. Roster absence shows the numeric ID. |
| Trips | Search trimmed case-insensitive student name or exact/partial ID; inclusive local date range, status and section filters; default newest date/time then trip ID; 50-row pages with stable sorting. Export all filtered rows, not just the visible page. |
| Roster | File input and drop zone, UTF-8/BOM/quoted CSV support, mapping preview, row errors, and confirmation before save. ID plus full-name or split-name mapping required. IDs remain text; duplicate IDs consolidate section enrollment only when student fields agree; conflicting duplicates are row errors. |
| Roster edits | Upsert by workspace/ID; preserve omitted students for merge import. Separate Replace roster action previews deletions and atomically replaces students/enrollments after confirmation. Reject an import with row errors until corrected; no silent partial import. Trip records remain. |
| Policies | Capacity 1–8, positive integer warning seconds, nonnegative daily guideline and window minutes, Allow/Warn/Lock choices, named schedules, weekdays, class sections, date exceptions/no-school. Defaults match the schema. Terminal enforcement is off until explicitly enabled. |
| Bell editor | Store `HH:mm`; require start < end within one local day for newly edited web schedules; reject overlapping periods on an effective date and duplicate date exceptions. Existing core cross-midnight fixtures still test resolver compatibility, but this web editor does not create overnight classroom periods. |
| Terminal settings | Read ID maximum each session; rename only after validating trimmed 1–24 printable ASCII excluding comma; save `SET,TERMINAL_NAME` and `SET,MAX_ID_LENGTH` through queue; success only after exact matching ACK. Cached values remain labelled until confirmed. |
| Teacher check-in | Confirm selected active student's check-in; send firmware's supported `MANUAL_CHECKIN,<studentId>`; await matching check-in event/live result and re-query occupancy. An error/timeout remains unconfirmed. Never fabricate a completed trip or auto-replay this mutation after reconnect. |
| Data/settings | Local storage status, real export/restore, app version, offline readiness, auto-connect preference, Disconnect, and distinct Disconnect & Unpair with consequences. |

Port roster mapping/name splitting and policy resolution from the source map and
use C# fixtures for behavior parity. First/last windows are start-inclusive and
end-exclusive; where both apply use the stronger action (`Lock > Warn > Allow`).
No-school dates have no period; date exceptions select the named template.
Unknown/no period is Allow. Do not infer terminal enforcement from UI color.

On explicit Save, persist policy locally as pending. Transfer capacity with
`SET,MAX_ACTIVE_PASSES,<n>` and await ACK. Build the next 14 local dates as
`POLICY_BEGIN,<enabled>`, ordered `POLICY_WINDOW` commands, and
`POLICY_COMMIT,<count>`, awaiting each matching ACK. Send at most 96 windows.
Unlike silently truncating a large schedule, block Apply with the computed count
if it exceeds 96; ask the teacher to simplify it. Retry an interrupted policy
only by restarting the whole group with `POLICY_BEGIN`; previous committed
terminal policy stays authoritative. Save the applied revision/date only after
commit ACK. Reapply pending revisions and refresh the date horizon at next
authenticated initialization or first reconciliation after local midnight.
Surface **Saved locally; terminal update pending** whenever appropriate.

Offline terminal enforcement contains only dates, minutes, and numeric actions;
no student names or class labels go over BLE. Missing cache dates fail open and
existing passes can always return, matching firmware behavior. Daily guideline
and overdue metrics are local advisory UI, not extra terminal enforcement.
Count completed non-reset trips on the local date per student; flag daily use
only when count exceeds the guideline (0 disables it), and overdue durations
only when greater than the warning threshold. Reset records have no duration.

### CSV export

Use `TripSqliteRepository.EnrichedCsvHeader` exactly:

```text
trip_id,student_id,student_name,class_section,schedule_name,grade,trip_date,time_out,time_in,duration_seconds,status,terminal_id,synced_at
```

Use current local roster lookup for name/grade and stored trip context for
section/schedule. RFC-style quoting doubles quotes and encloses commas/newlines;
CRLF row endings, UTF-8 BOM, empty nullable fields. For spreadsheet-safe exports,
prefix text cells beginning with `=`, `+`, `-`, `@`, tab or CR with an apostrophe.
Keep original values in the database/JSON backup. Explain that CSV is a report,
not a restore format, and spreadsheet programs may display numeric-looking IDs
without leading zeros unless imported as text. Download via Blob/object URL and
revoke the URL after the download is initiated; no file-system-picker dependency.

### Data backup and restore

Define `backup.schema.json` with an envelope:

```text
format: "hallzee-web-data", formatVersion: 1, schemaVersion: 1,
appVersion: string, exportedAtUtc: ISO string,
workspaces: [...], terminals: [...], trips: [...], rosterStudents: [...],
rosterEnrollments: [...], policyRules: [...], bellPeriods: [...],
scheduleExceptions: [...]
```

Explicitly whitelist fields. Exclude installation ID, credentials, Bluetooth
device hints, live passes, locks, reconnect intent, sync cursors, and diagnostics.
Export in one read-only transaction after pausing runtime writes so all stores
represent one snapshot. Backups are unencrypted local downloads containing student
data; the UI explains this before download and recommends the district-approved
local destination. The application does not choose a cloud drive or upload it.

Restore uses a file input, validates UTF-8/JSON, exact format/version, types,
enums, key uniqueness and references before changing anything. Limits: 50 MiB
backup file, 100,000 trips, 10,000 students, 1,000 bell periods, and 1,000 date
exceptions; reject larger/unsupported input with an explicit error. Roster CSV
limit is 5 MiB/10,000 data rows. Validate before allocating derived large arrays.
Apply the same field/range/domain validators used by normal repository writes;
JSON with correct types but invalid dates, policies, or terminal IDs is rejected.

Show a count/date/terminal preview and require **Replace local classroom data**.
Offer a current backup first. Stop BLE/retries, acquire exclusive runtime
ownership, and replace data stores in one transaction. Preserve existing local
installation ID/credentials. Preserve an existing assignment only if its terminal
and workspace also exist in the restored data; otherwise retain its key as
unassigned. Restore all other terminal assignments as unassigned. Reset every
restored cursor to zero and require recovery sync before normal operation.
Do not merge conflicting histories or silently activate a restored terminal.
Any error rolls back the entire replacement.

Restoring data on a new browser **does not restore ownership**. Claim only after
confirmed same-classroom owner release, or use the existing physical owner-reset
procedure if the previous credential is lost. That procedure preserves terminal
trips/settings; it cannot recreate local-only roster data. Clearly distinguish
the short pairing gesture from the destructive-to-ownership reset gesture and
verify the current firmware procedure in M0 before writing teacher instructions.

**M5 acceptance (A14–A18):** complete import/edit/filter/export/restore flows run
against real IndexedDB; reload preserves values; no simulator actions exist;
invalid imports/restores are atomic; owner keys never appear in downloaded files;
policy interruption preserves the prior terminal policy; check-in timeout cannot
create a trip or replay the command automatically.

## 10. M6 — projection, offline shell, and release security

### Projection and mini window

`ProjectionView` receives only `{connectionFreshness, occupiedCount, capacity,
elapsedSeconds[], periodLabel, windowDecision}`. It never receives roster names,
IDs, trip history, teacher details, or credentials. Render it in-page and via a
React portal into `documentPictureInPicture.requestWindow({width:340,height:180})`
called directly from a teacher click. Attach a same-origin compiled stylesheet;
do not copy arbitrary stylesheets or render the whole teacher dashboard.

Use one PiP window, handle rejection, theme changes and `pagehide`, and clean up
the portal/listeners on closure. No second controller, lock, BLE session, or DB
is created. If unsupported or denied, offer **Projection view** in the existing
page, with an explicit Full screen button and a visible Exit control. Rejection
of fullscreen leaves the usable in-page view. Both display **Status unknown**
when stale/disconnected; a timer must not imply a live connection.

PiP is tied to its opener and the browser controls placement; it is not a casting
API. The teacher moves/mirrors the window using normal display controls. A tab-only
share may omit PiP; test the actual projector/extended-display arrangement and
use the projection fallback when needed. See [Document Picture-in-Picture](https://developer.chrome.com/docs/web-platform/document-picture-in-picture).

Acceptance includes 340×180 and resized PiP, Chromebook 1366×768, 200% text zoom,
keyboard-only controls, dialog focus/return, readable status text independent of
color, and no student identifiers in projection DOM or accessibility tree.

### Offline and updates

Manifest: stable app `id`, `name: Hallzee`, `short_name: Hallzee`, `start_url: /`,
`scope: /`, `display: standalone`, matching theme/background colors, local
192/512 icons and a maskable icon. Installation is optional. First load needs
network; show **Ready offline** only once the service worker has cached and
verified the entire release shell. Browser clearing can remove that cache.

Generate the precache list from build output: HTML, hashed JS/CSS, icons, local
fonts, and manifest. Cache only allowlisted same-origin GET assets with successful
responses; no student data, exports, credential responses, or runtime API routes.
Use one cache per build, atomic install (failed fetch keeps the old worker),
cache-first immutable assets and the installed shell for navigation. Optional
update checks fetch only a public build descriptor on load/explicit Check updates.

Do not call unconditional `skipWaiting` or reload an active classroom. A waiting
worker shows **Update ready — Apply when finished**. Apply only after teacher
action, no pending import/claim/sync/settings operation, durable saves complete,
BLE disconnected, and other windows closed. Activate, reload once, migrate, then
reconnect. Keep the prior build cache until activation succeeds. A failed/newer
schema is never “fixed” by clearing storage. Rollback code must support the stored
schema or show recovery instructions; release only tested migration paths.

Browser tests must install build A, work offline, discover build B, interrupt its
download, finish and explicitly apply it, and verify data/key survival and no
mixed-version assets. Repeat offline launch in both tab and installed app during
physical acceptance.

### Privacy and hosting contract

Teacher-facing promise: **Hallzee stores classroom data in this browser and the
terminal. Hallzee does not upload student records or owner credentials. You can
explicitly download local exports/backups. The site downloads application files
and updates from its host.** Avoid an absolute guarantee about the whole operating
system, browser extensions, user-chosen backups, or third-party management tools.

Production is static hosting only. Bundle fonts/icons/scripts; exclude analytics,
ads, remote error tracking, remote fonts, waitlist code, runtime CDN imports and
server handlers. Student fields never enter URLs, request bodies/headers, page
titles, telemetry, or CSP reports. Render imported strings as text, not HTML.

Ship a production header configuration with at least:

```text
Content-Security-Policy: default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob:; font-src 'self'; connect-src 'self'; worker-src 'self'; manifest-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'
Permissions-Policy: bluetooth=(self), picture-in-picture=(self), camera=(), microphone=(), geolocation=()
Referrer-Policy: no-referrer
X-Content-Type-Options: nosniff
```

Inline styles support controlled React/PiP styling; inline/eval scripts remain
blocked. The limited `connect-src 'self'` permits static update fetching, not data
uploads. Enforce an application request allowlist and test it. No remote CSP
report endpoint. No COOP/COEP requirement is introduced by the selected storage
engine. Serve hashed assets with immutable caching and shell/worker/build
descriptor with revalidation. Do not allow parent-domain cookies or third-party
scripts to become a data path into the live origin.

Use fictional canary names/IDs/keys during network tests. Exercise pairing,
imports, sync, export, backup, errors, offline/online changes and update checks.
Fail if any application request is not an allowlisted static GET or contains a
canary. Inspect artifacts for demo/waitlist/telemetry imports and public source
maps containing fixture data. Diagnostics contain only build version, capability
booleans, counters and sanitized error codes; no raw protocol, device name/ID,
student data, proofs, or passkeys. Downloads need explicit teacher actions.

**M6 acceptance (A19–A22):** PiP and fallback update without identifiers, offline
reload and controlled A→B update preserve database/authentication, production
headers are verified on HTTPS, and network/privacy tests pass against the built
artifact. Host approval/deployment is a separate maintainer action after review.

## 11. Verification and reviewer evidence

### Automated acceptance matrix

Each PR maps changed requirements to named tests. Do not report only coverage
percentages or successful mock UI renders.

| ID | Test evidence required |
| --- | --- |
| A01 | Literal HMAC/HKDF/commit vectors independently checked against C# and Web Crypto, including leading-zero passkey, lowercase normalized input, altered nonce/ID/key and malformed fields. Expected outputs are committed literals, never calculated by the code under test. |
| A02 | Full first-claim/return-auth transcripts; no pre-auth app commands; claimed IN_USE with/without key; unexpected version/state/terminal ID at every response. |
| A03 | Every byte split of representative messages, multibyte boundaries, CRLF/multiple lines, offset DataView, 20-byte writes, serialization, bounds, disconnect mid-fragment and stale callbacks. |
| A04 | Crash/save failure before commit, lost commit/`AUTH_OK`, reloaded pending key, three independent wrong submissions, timeout/cancel; retained key authenticates when commit succeeded. |
| A05 | Ordinary disconnect retains ownership; release rejection/timeout retains it; confirmed release cleans assignment/key only, lost release ACK reconciles safely. |
| A06 | Real IndexedDB persistence, strict transaction completion, duplicate/conflict semantics, versionchange/blocked/failed migration, quota/abort fault injection, no ACK before commit. |
| A07 | Start cursor 100; live 110 arrives before historical 101–109; crash at each store/ACK/cursor boundary; next sync retrieves all missing records exactly once locally. |
| A08 | Sparse IDs, duplicate replay, zero-trip and growing streams, bad record, premature end, lost ACK, interrupted/full recovery, known history-reset workflow. |
| A09 | Local-wall-time fixture at UTC-04:00 and UTC+05:30, midnight, DST transition, browser clock jump, blank reset duration and missing terminal clock. No double offset. |
| A10 | 0/1/8-pass snapshots, checkout/check-in/reset interleaving, oldest-pass fallback, stale Unknown state, no fabricated trip from events. |
| A11 | Retry timing/budget, coalesced wake/focus, user disconnect, permission missing, absent/rejected getDevices, wrong saved device, no automatic chooser. |
| A12 | Second tab/PWA cannot connect/write; lock release/reacquisition, freeze without cleanup, discarded reload, stale generation and Strict Mode teardown. |
| A13 | Sleep/freeze gap triggers snapshot/cursor reconciliation; internet offline does not disable Bluetooth. Automated simulation supplemented by hardware evidence. |
| A14 | CSV BOM/quotes/newlines, mapped names, leading-zero IDs, duplicates/conflicts, merge/replace confirmation, invalid import leaves data unchanged. |
| A15 | Date/status/section/search filters and full filtered export, exact header, CSV escaping/formula protection, existing context preserved after schedule edits. |
| A16 | Policy boundary/no-school/exception fixtures against C#, 96/97-window handling, ACK/error/mid-transfer interruption, capacity and stale applied-revision UI. |
| A17 | Backup round-trip into empty/existing browser DB, malformed/version/oversize/reference failures, transaction rollback, reset cursor and excluded credentials/device hints. |
| A18 | Settings ACK validation, rename failure preserves prior value, targeted manual check-in and ambiguous timeout, empty state/accessibility, no demo controls. |
| A19 | Projection DOM/accessibility tree excludes names/IDs; PiP close/reopen/resize/theme and denied API fallback; stale banner in both views. |
| A20 | Service worker offline navigation, partial install failure, explicit A→B activation, no reload mid-operation, DB/key survival, incompatible schema refusal. |
| A21 | Canary network tests, static-only bundle/request allowlist, CSP headers/blocked scripts, redacted diagnostic/download content. |
| A22 | Bootstrap on clean Mac/Windows images, production build artifact/license inventory, existing preview checks unchanged, documented release evidence linked. |

Extend existing `TerminalIdentityProtocolTests`, `TerminalSessionTests`,
`SyncSessionTests`, `ActivePassProtocolTests`, `RosterCsvParserTests`, and
`PolicyScheduleServiceTests` to consume the relevant shared JSON fixtures. Add
fixture adapters, not changes to production C# behavior merely to match new TS.
Proposed fixtures exposing an existing bug must name the discrepancy and receive
a separately reviewed fix or an explicit web-only contract in this document.

Unit tests use a deterministic fake transport, clock and fault-injecting storage
port. Browser tests use actual Chromium IndexedDB, Web Crypto, locks, service
workers and downloads, with a fake **BLE boundary only**. Headless Bluetooth
simulation cannot verify encrypted hardware pairing.

### Physical release matrix and steps

All rows start **Not run**. Enter exact versions, date, tester, result and evidence
in `docs/testing/web-client-acceptance.md` and the Wiki mirror. Do not mark a
platform supported on the strength of another row.

| Platform | Purpose | Required before claim |
| --- | --- | --- |
| Mac + Chrome Stable + ESP32 | Development reference and Mac browser BLE/PWA | Mac web support |
| District-managed Chromebook + target Chrome build + ESP32 | Real policy, persistence, secure BLE, sleep and projection | Chromebook pilot and general availability |
| Windows 11 BLE PC + Chrome Stable + ESP32 | Chrome chooser/Windows OS passkey flow, GATT writes/notifications, bond reuse/reconnect/sleep and PWA | Windows web support |
| Actual classroom smartboard/projector | Mirrored/extended display and PiP versus tab/fullscreen sharing | Smartboard workflow claim |

A Mac is sufficient for shared automated logic/UI tests and Mac Chrome hardware
tests. It is **not** sufficient for Windows or Chromebook release acceptance.
A Windows PC is required for the Windows-specific browser Bluetooth/PWA
capabilities above. **Windows behavior has not yet been verified by this plan.**

On each hardware platform, execute this sequence with fictional data:

1. Fresh profile; verify an offline first load produces the browser's offline
   failure (the app is not cached yet), then load HTTPS,
   create workspace, import `docs/demo-roster.csv`, and pair with physical code.
2. Complete three keypad trips, including an ID with leading zeros; compare
   terminal records with local rows/export. Check one active pass remotely and
   confirm its `MANUAL` record. Repeat with capacity eight and targeted check-in.
3. Restart browser, reconnect using key, and repeat with a pass already active.
   Record whether a chooser is needed. Test revoked permission and wrong terminal
   selection beside a second terminal; no unintended data/settings writes occur.
4. Power terminal off during sync, restore it, and verify safe replay. Repeat
   ten power/reconnect cycles. Force a local storage failure: the pending trip
   must remain recoverable and no successful sync indicator may appear.
5. Close laptop lid for five minutes; complete two trips while disconnected,
   reopen, and verify catch-up and current occupancy. Repeat backgrounding for
   15 minutes and manual discard using Chrome's tab-discard controls. Open a
   second app window during the test to verify exclusion.
6. After Ready offline, disconnect internet and relaunch/reconnect; import, sync,
   display and export still work. Install as PWA and repeat restart/offline/wake.
7. Configure a bell policy, disconnect the browser, and verify Allow/Warn/Lock
   on the physical terminal and that an existing pass can return. Reconnect and
   verify policy pending/applied status and date refresh.
8. Open PiP and fallback on the actual display; names/IDs never appear. Check
   resize, 200% zoom, close opener, mirrored and extended desktop, and tab-only
   sharing. Record any presentation mode requiring the fallback.
9. Export backup, modify roster, restore, and verify counts/content. In a fresh
   profile confirm data restore does not pretend ownership was restored. Perform
   same-classroom confirmed release and new claim with no history loss.
10. Run one six-hour classroom-style session with at least 100 completed synthetic
    trips and a seeded 10,000-trip history. Require no lost/duplicate local rows,
    no unbounded reconnect loop, no listener growth across reconnects, and
    responsive controls. On the lowest-powered target Chromebook, dashboard
    after offline launch should be usable within 3 seconds and a 50-row history
    page within 500 ms; measure and record the hardware/build and any failure.

No fixed BLE full-history throughput is assumed. Record initial-sync duration
and post-wake recovery duration; unavailable radio and paused browser time are
reported separately. Catch-up begins within two seconds of a successful
authenticated connection while JS is running.

## 12. Delivery order, documentation, and final review

Deliver separate reviewable PRs in dependency order:

| PR | Contents | Gate |
| --- | --- | --- |
| 1 / M0–M1 | Feasibility spike, exact version manifest, bootstrap, skeleton, contracts and CI | Managed Chromebook secure BLE works; package `check` runs. |
| 2 / M2 | Transport, crypto, identity, credential persistence, release flow | A01–A05, with physical first/return claim evidence. |
| 3 / M3 | Schema, repositories, sync, time and active passes | A06–A10. |
| 4 / M4 | Lifecycle, retries, single-window ownership | A11–A13 and physical wake/disconnect evidence. |
| 5 / M5 | Teacher UI, roster/policies, reports and backup | A14–A18. |
| 6 / M6 | PiP/fallback, offline/update worker, static hosting configuration, privacy checks | A19–A22. |
| 7 / release | Clean-machine/platform/soak evidence, teacher/IT docs, release artifact | All included-platform gates pass; no claim exceeds evidence. |

PRs 2–6 may not ship a live classroom build with later mandatory safety/data
requirements missing. Development fixtures and feature branches are sufficient
until the integrated client meets all gates. No estimate here assumes junior
developers can validate hardware without access to the named equipment.

Add the web job to existing `pr-readiness.yml` path selection and aggregate result;
include `web-client/**`, `contracts/**`, bootstrap scripts, and relevant workflow
changes. Run C# conformance checks in CI. Preserve existing desktop/firmware/preview
checks when their paths change. Add `/web-client` to Dependabot and dependency/
license inventory tooling. Upload `Hallzee-Web-<version>-<commit>.zip` containing
static assets, headers, manifest/build metadata, license/notices, and a matching
source-commit link. CI does not silently deploy classroom builds.

Every package updates relevant source docs and its repository-owned Wiki page
in the same branch. Required final pages:

| Source page | Wiki mirror | Content |
| --- | --- | --- |
| `docs/testing-and-installation.md` | `wiki/Testing-and-Installation.md` | Short bootstrap/check commands, planned versus shipped status, platform requirements. |
| `docs/chromebook-guide.md` | `wiki/Chromebook-Guide.md` | Pairing, separate OS/app codes, restart/reconnect fallback, offline readiness, backup, ownership loss, projection and troubleshooting. |
| `docs/web-client-it-guide.md` | `wiki/Web-Client-IT-Guide.md` | Stable origin, actual approved Chrome versions, policy settings, persistent profiles/storage-clearing restrictions, optional managed PWA installation, header requirements and update rollout. |
| `docs/testing/web-client-feasibility.md` | `wiki/Web-Client-Feasibility.md` | M0 evidence and unsupported/fallback capabilities. |
| `docs/testing/web-client-acceptance.md` | `wiki/Web-Client-Acceptance.md` | A01–A22 evidence links and physical/soak result matrix. |
| `docs/client-ui-architecture.md` | `wiki/Client-UI-Architecture.md` | Separate production web implementation and shared contract authority; correct the current preview-only boundary when code actually ships. |
| `docs/architecture.md`, `docs/bluetooth-protocol.md` | `wiki/Architecture.md`, `wiki/Bluetooth-Protocol.md` | Browser adapter/storage boundaries; existing plural active-pass/targeted check-in syntax and local clock semantics; no invented commands. |
| `docs/releasing.md`, `docs/ci-and-security.md`, `docs/release-licensing.md` | Corresponding existing Wiki pages | Build, staged rollout, migration/rollback, privacy/network gates and new dependencies/licenses. |

Update README, Wiki Home and developer navigation when the web client is shipped;
before then link this document as a **proposed plan**, without advertising working
Chromebook downloads. Keep the contributor guide short; detailed matrices belong
in the testing pages. Publishing Wiki changes remains a separate repository
operation, not something this planning edit performs.

Final reviewer checklist:

- Every included workflow has a real implementation, a named test, and any required
  platform evidence. Every deferred workflow is absent or plainly documented.
- No drift in UUIDs, proof bytes, authorization, ACK ordering, record identity,
  clock semantics or policy decisions. Shared fixture changes are reviewed.
- Loss scenarios preserve durable rows/credentials, avoid skipped history, and
  present a recovery path without silently resetting local or terminal data.
- UI states distinguish connected/authenticated/synchronized, current/unknown
  occupancy, local/applied policy, downloaded-shell readiness and storage health.
- Production uses the approved stable origin, a static reviewed artifact, no data
  uploads, and controlled updates. New origins and ownership migration are explicit.
- Mac, managed ChromeOS and Windows results are separately recorded. Any unverified
  platform stays labelled unverified and is excluded from supported-platform claims.
- Documentation and Wiki mirrors match the final implementation and test results.
