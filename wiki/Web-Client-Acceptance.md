# Web client acceptance evidence

Historical software baseline: development build `1.2.0-dev.1`, tested 2026-09-11 on macOS arm64 with pinned
Node 22.23.2. The integrated `npm run check` passes: **62 unit tests, 5 Chromium
browser scenarios, type checking, lint and production build**. The portable
C# suite passed **119 tests**, including four shared fixture checks. These
results cover software; they do not approve a physical platform or deployment.

Reproduce from a repository ZIP with `bash scripts/web-client-macos.sh check`.
Windows x64: `powershell -ExecutionPolicy Bypass -File scripts/web-client-windows.ps1 check`.
CI runs the same check and C# fixtures, then packages the static artifact.
Exact package/browser revisions are in `web-client/package-lock.json`; build
hash/source provenance are in generated `web-client/dist/build.json`.

## Requirement-to-test map

Paths below are relative to `web-client/tests/`. The detailed A01–A22 design
matrix remains the release checklist; this table names present evidence rather
than asserting that every hardware, fault-injection and long-running variant
in that matrix has passed.

| Requirements | Present executable evidence |
| --- | --- |
| A01–A02 identity/proofs | `unit/protocol.test.ts`, `unit/session.test.ts`; literal HMAC/HKDF/commit vectors; malformed identity, pre-auth rejection; `WebClientContractTests.cs` independently checks C# |
| A03 framing/transport | `unit/protocol.test.ts`, `unit/transport.test.ts`; every representative byte split, UTF-8 boundaries, DataView offsets, limits, serial 20-byte writes, stale fragments, late connection cancellation |
| A04 claim persistence | `unit/session.test.ts`; pending key saved before commit, storage failure abort, ambiguous commit/return auth, wrong final ID; browser CryptoKey clone/reload |
| A05 owner release | `browser/client.spec.ts`; confirmed release removes key and preserves trip rows; normal offline reload authenticates with retained key |
| A06–A08 durable sync | `unit/sync.test.ts`, `unit/data.test.ts`; transaction rollback, no ACK before durable store, storage failure, conflicting IDs, interrupted cursor 100/live 110 case, sparse/growing/empty streams and premature end; actual IndexedDB inspected before ACK in browser fake |
| A09–A10 time/occupancy | `unit/protocol.test.ts`, `unit/transport.test.ts`; local wall epoch, blank reset fields, 0/1/8 passes, targeted reset/check-in, missing-clock Unknown; browser targeted check-in retains the other pass |
| A11 reconnect | `unit/lifecycle.test.ts`; 45-second budget, coalescing, cancellation, permission/ownership failures |
| A12–A13 lifecycle | `unit/runtime.test.ts` covers start/stop/start teardown, simulated freeze/reload, transaction abort and newer-schema refusal; `browser/client.spec.ts` checks second-window lock and offline reconnect. Actual OS sleep/discard remains pending |
| A14–A15 roster/reports | `unit/data.test.ts`; BOM, quotes/newlines, leading zeros, duplicate enrollments/conflicts, atomic roster replacement, CSV header/CRLF/formula escaping; browser edit/backup/restore/offline survival |
| A16 policies | `unit/data.test.ts`, `unit/transport.test.ts`, C# shared fixture; exact default window bytes/decisions, no-school exceptions, invalid overlapping/overnight edits, 96/97-window bound |
| A17 backups | `unit/data.test.ts`, `browser/client.spec.ts`; whitelist, invalid references/fields/timezone, rollback, cursor reset, preserved nonextractable keys, excluded credentials/device hints |
| A18 teacher operations | `browser/client.spec.ts`; real empty state, roster edit identity lock, targeted check-in and release; settings command acknowledgment enforced by controller/session |
| A19 projection | `browser/client.spec.ts` verifies fallback rendered DOM excludes fictional student name/ID; actual PiP/projector sharing remains pending |
| A20 offline/update | `browser/update.spec.ts`; build A offline after partial B failure, waiting B does not reload A, multiple windows block activation, explicit B apply retains data and key, offline B relaunch |
| A21 privacy/headers | Browser requests stay on the static origin during exercised workflows; production preview CSP checked; unsupported Bluetooth handled; no runtime service/API/demo imports; source maps disabled |
| A22 build/bootstrap | Pinned Node downloaded/verified and packages installed on existing Mac; complete build includes 85 static files including local dependency notices; CI ZIP job added. Clean Mac/Windows images and HTTPS host verification pending |

Further release work includes exhaustive fault injection at every disconnect/
ACK/cursor boundary, settings/policy timeout variants, migration/blocked-upgrade
and quota behavior on managed profiles, PiP lifecycle/accessibility checks,
ChromeOS performance and the six-hour soak. These are acceptance tasks, not
claims made by passing the smaller automated set above.

## Physical release gates

| Platform / environment | Versions / tester | Result |
| --- | --- | --- |
| Mac Chrome UI/offline indicator | Selected local regular profile; Codex visual inspection | Observed; no real pairing performed |
| Mac Edge UI/offline indicator | Selected local regular profile | Observed; shared Chromium implementation |
| Mac Chrome + ESP32 secure BLE / installed PWA | Local regular profile; user report 2026-09-11; exact versions not recorded | Connection succeeded after disconnecting terminal in macOS Bluetooth settings; full secure BLE/PWA matrix pending |
| Mac Edge + ESP32 secure BLE / installed PWA | Edge on Mac; testing establishes Edge-on-Mac support | Pending hardware verification |
| Chromebook + ESP32 | User report 2026-09-12; managed state/versions not recorded | Original flow connects then disconnects before claim; updated pairing flow pending |
| Windows 11 BLE PC + Chrome + ESP32 | Not recorded | Not run |
| Windows 11 BLE PC + Edge + ESP32 | Windows BLE PC with Microsoft Edge | Not run |
| Firefox local classroom / data features | Local profile; roster, trip history, reports, policies | Supported; terminal Bluetooth deferred |
| Actual smartboard/projector, mirrored/extended/tab sharing | Not recorded | Not run |
| Six-hour / 100-trip soak and 10,000-row startup | Not recorded | Not run |
| Clean-machine Mac and Windows bootstrap | Not recorded | Not run |
| Testing HTTPS origin, deployment headers and rollback | `https://web.hallzee.com`; full acceptance evidence not recorded here | Pending |

A Mac is sufficient for the shared automated checks and establishing Edge-on-Mac support.
A Windows BLE PC is required for Chrome's and Edge's Windows Just Works pairing, encrypted
GATT writes/notifications, bond reuse, reconnect/sleep and installed-PWA behavior.
**Windows behavior remains unverified.** ChromeOS persistence is only one of its distinct
checks: district policy, pairing, sleep/wake and projection must also pass on the managed device.

For each pending hardware row, execute the ten-step physical sequence in
[the design](Design-Chromebook-Web-Client.md#physical-release-matrix-and-steps),
using fictional data. Record OS/Chrome/firmware/build versions, tester/date,
counts before/after interruption, exported evidence and any failures here and
in the Wiki mirror. Keep unsupported platforms out of release claims.

## Local connection follow-up (2026-09-11)

A user reported a generic connection failure on localhost; the original native
error was not retained, so the exact physical cause is not yet confirmed.
The adapter now establishes encryption by reading the protected TX characteristic
before HELLO, with a 60-second OS-pairing allowance. GATT failures identify the
step and an allowlisted browser error category; raw native payloads are excluded.
A failed first claim no longer has its error overwritten by an automatic reconnect
attempt with no assigned terminal.

Follow-up verification: 69 unit tests and 8 browser scenarios pass, with type
checking, lint, production build, and repository hygiene checks passing.
Added software regressions cover encrypted-read ordering, discarded stale read
values, a 15-second OS-pairing delay, native error categories and sanitization,
and opening the cached app after stopping its local HTTP server. Physical Mac
retry reached `connect / NetworkError`, before encrypted pairing or Hallzee
authentication. No local Hallzee desktop Bluetooth helper was running. The exact
native cause was not retained. The user subsequently reported successful connection
after disconnecting the terminal in macOS Bluetooth settings. This confirms a
working recovery for this attempt, not completion of the full hardware matrix.
Allowlisted fixed Chrome reasons now distinguish
blocked permission, authentication, unavailable adapters, and connection failures
without emitting arbitrary native messages. The user also reported unexpected
extra numeric input on the physical keypad, which stopped when supported on a
hard surface. Physical cause remains unconfirmed. Mac suffices for a Mac connection retest. A Windows PC is not
required for the Mac fix; Windows OS pairing/GATT/reconnect/PWA remain unverified.


### Saved-owner bond repair and keypad follow-up (2026-09-11 baseline)

After OS disconnection/forgetting, the user reported `pairing / NotSupportedError`.
Code inspection found no physical bond-repair path for an already owned terminal.
New firmware adds a five-second hold of `*` alone, waits for disconnect before
bond removal, and requires the unchanged owner key for application authentication.
That earlier version displayed an OS code; the updated Just Works flow replaces
it with code-free reconnect instructions, as described below. Native tests cover the repair state transitions
and the distinct reset gesture. The keypad adapter now requires 40 ms stable
press/release observations; tests cover glitches, bounce, repeats, overlap, and
clock rollover. Firmware installation and physical Mac recovery/keypad testing
remain pending; no claim of a physical fix is made.


Verification completed: native firmware and touch suites pass; Arduino compile-only
builds passed for ST7735, ILI9341, and the ILI9341 touch variant. The final repair
UI changes were included in the touch-variant compile. Web typecheck/lint/build,
69 unit tests and 8 browser scenarios pass, including failed encrypted read then
saved-owner AUTH with no new CLAIM. Repository hygiene tests pass; modified and
new files were reviewed for secret/PII exposure. No terminal was flashed.


## Chromebook pairing simplification (2026-09-12)

The user reported a Chromebook connecting then immediately disconnecting before
claim, with a generic error that incorrectly suggested another owner. The exact
native failure was not captured, so the hardware cause remains unconfirmed.
Updated firmware requests encrypted Secure Connections Just Works bonding and
uses encrypted GATT permissions, removing the separate OS passkey ceremony.
The six-digit physical code remains an application-only v2 ownership claim.

The updated client flow selects a real terminal first, checks identity, then
asks for the physical code in Hallzee only for an unclaimed terminal. The
identity probe may close while the code dialog is open; a fresh handshake follows
entry so typing cannot expire its nonce. Saved owners authenticate without code
or pairing mode. Web reconnect uses a granted device handle when available;
otherwise a user-clicked chooser selects the same saved terminal without a new
claim. Known ownership appears in Hallzee, and Bluetooth names remain stable.
The browser owns its nearby chooser; the app cannot pre-label unknown nearby
terminals or remove browser-generated OS pairing indicators.

Firmware installation is required. The old firmware's OS passkey requirement
cannot be changed by downloading a new web app alone. The security tradeoff is
recorded in [the protocol](Bluetooth-Protocol.md#ble-transport): Just Works
has no initial-link MITM protection, and v2's six-digit HMAC/HKDF claim is not a
PAKE. Do not infer equivalent MITM protection from the single app-code dialog.

Software verification for this update: **76 web unit tests and 12 Chromium
browser scenarios pass**, along with type checking, lint, and production build.
The new scenarios cover terminal selection before code entry, no invented
nearby devices or RSSI, saved-owner reconnect, and retaining sanitized connection
errors. Automatic reconnect matches the saved browser device ID; a similar name
or a single unrelated granted device must not be used as a fallback. Regression
coverage also checks a factory-reset terminal overriding stale pairing status,
chooser cancellation releasing the operation queue, and saved terminal rows
remaining available when browser device enumeration fails.

Native verification: **98 Universal tests and 127 core tests pass**. The macOS
Bluetooth helper and shared Windows Bluetooth adapter cross-build succeeded.
NuGet vulnerability-feed lookup was unavailable in the sandbox; this does not
represent a successful dependency advisory refresh.

Final-source native firmware and touch suites pass. Compile-only firmware builds
passed for ST7735 (1,325,157 bytes), ILI9341 rotation 1 (1,364,185 bytes), and
ILI9341 touch rotation 1 (1,373,849 bytes), each within the 1,572,864-byte OTA slot.
Reproduce using `bash scripts/flash-terminal-macos.sh --fast --compile-only`,
adding `--display ili9341 --rotation 1`, then `--touch-test` for the touch variant.
The pairing-code window now preserves the BLE bond established by discovery;
explicit owner reset/release and BT REPAIR remain the bond-removal paths.
Repository hygiene tests pass, and working-tree/diff reviews found no sensitive
additions. No terminal was flashed and no client was deployed.

Physical acceptance for this update is pending. Use only fictional records:

1. On the Chromebook, update firmware and app, clear only test ownership if
   needed, enter pairing mode, select the stable terminal name, and enter the
   code once in Hallzee. No OS passkey entry; authenticated sync completes.
2. Test an incorrect code, cancellation, and more than eight seconds spent
   entering the code. Errors remain actionable; retry within the physical claim
   window succeeds using a fresh handshake.
3. Disconnect, reboot the terminal, close/reopen the app, and sleep/wake. Verify
   automatic reconnect where a handle is available; otherwise use **Reconnect**
   and select the same terminal, without code or pairing mode.
4. Check unclaimed, owned by this profile, and owned by another profile states
   inside Hallzee. A new/unknown browser chooser entry must not fabricate a
   status, signal reading, or device. Bluetooth names stay unchanged across
   ownership and active-pass changes.
5. With an active test pass, reconnect the saved owner and check it in. A second
   profile cannot authenticate or claim. Repair an intentionally stale OS bond
   with `*` alone for five seconds and the original profile, without a code.

A Mac is sufficient for shared automated tests and, with an ESP32, Mac-specific
physical checks. A Chromebook is required for ChromeOS chooser, district policy,
persistence, and sleep/wake verification. A Windows BLE PC is required to test
native WinRT and Chrome/Edge Windows Just Works pairing, encrypted GATT,
bond replacement/reuse, reconnect/sleep, and installed-PWA behavior.
**Windows behavior remains unverified.** This record does not claim that any
terminal was flashed or that the reported Chromebook failure is physically fixed.
