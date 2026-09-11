# Web client acceptance evidence

Development build `1.2.0-dev.1`, tested 2026-09-11 on macOS arm64 with pinned
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
| Mac Chrome + ESP32 secure BLE / installed PWA | Not recorded | Not run |
| District-managed Chromebook + ESP32 | Device unavailable | Not run |
| Windows 11 BLE PC + Chrome + ESP32 | Not recorded | Not run |
| Actual smartboard/projector, mirrored/extended/tab sharing | Not recorded | Not run |
| Six-hour / 100-trip soak and 10,000-row startup | Not recorded | Not run |
| Clean-machine Mac and Windows bootstrap | Not recorded | Not run |
| Approved HTTPS origin, deployment headers and rollback | No origin approved; not deployed | Not run |

A Mac is sufficient for the shared automated checks. A Windows BLE PC is required
for Chrome's Windows OS passkey flow, encrypted GATT writes/notifications, bond
reuse, reconnect/sleep and installed-PWA behavior. **Windows behavior remains
unverified.** ChromeOS persistence is only one of its distinct checks: district
policy, pairing, sleep/wake and projection must also pass on the managed device.

For each pending hardware row, execute the ten-step physical sequence in
[the design](Design-Chromebook-Web-Client.md#physical-release-matrix-and-steps),
using fictional data. Record OS/Chrome/firmware/build versions, tester/date,
counts before/after interruption, exported evidence and any failures here and
in the Wiki mirror. Keep unsupported platforms out of release claims.
