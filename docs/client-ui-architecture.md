# Client UI Architecture

**Status:** Current implementation reference

**Scope:** The shared Avalonia desktop client, native BLE adapters, local
repositories, and UI-facing state boundaries.

## 1. Supported client

`receiver/universal/` is the current shared Windows/macOS Avalonia client.
`preview-site/` is a browser design preview and must not become a second source
of protocol, policy, or persistence rules. The older receivers under
`receiver/windows/` and `receiver/` remain compatibility/reference hosts.

The Universal client composes:

- Avalonia Views and ViewModels for dashboard, trips, roster, policies, terminal
  discovery, and settings;
- `BathroomSync.Core` for protocol parsing, identity/authentication, schedules,
  CSV import, and SQLite repositories;
- WinRT BLE on Windows and the Mac BLE agent/CoreBluetooth adapter on macOS;
- a preview terminal adapter for deterministic UI tests.

## 2. Teacher workspace and class sections

The schema retains the internal type/table name `profile` for compatibility,
but the user-facing concept is a **teacher workspace**. A workspace owns the
teacher's local roster, policy, schedules, terminal assignment, and last
successful sync time.

Bell periods identify the active **class section** inside the workspace. They do
not switch teacher workspaces. `roster_enrollments` permits one student to
belong to multiple class sections, while roster names remain local to the
desktop.

## 3. State ownership

| State | Owner | Persistence |
| --- | --- | --- |
| Active teacher workspace | `ProfileAndPolicySqliteRepository` | SQLite |
| Terminal assignment and custom name | Terminal repository plus authenticated kiosk setting | SQLite and kiosk Preferences |
| Discovery results and RSSI | Platform connection adapter | Session only |
| Owner credential | Platform credential store | Windows DPAPI or macOS Keychain |
| Trips and schedule/class attribution | `TripSqliteRepository` | SQLite |
| Roster and class enrollments | `RosterSqliteRepository` | SQLite |
| Policy, named schedules, and date exceptions | Policy repository | SQLite |
| Last successful sync | Profile repository | SQLite after `SYNC_END` |
| Live pass state | Active-pass query/snapshot/events | Memory; unknown after disconnect |

## 4. Connection and synchronization

Only the terminal assigned to the active teacher workspace is an automatic
reconnect target. The client never picks an arbitrary nearby kiosk.

1. Restore the saved terminal identity, transport hint, and owner credential.
2. Open GATT and verify the expected stable terminal ID.
3. Authenticate with the owner proof.
4. Query active passes/settings, send pass capacity and the optional bell-policy
   cache, align the clock, and start `TIME_CURSOR`.
5. Keep the connection active for `EVENT`/`LIVE_TRIP` updates.
6. Run a cursor reconciliation every five minutes.
7. On link loss, retry the same verified terminal for up to 45 seconds, using
   discovery fallback if its transport address changed.

`TerminalOperationCoordinator` serializes cursor sync, terminal settings,
manual terminal check-in, and policy-transfer command groups. Trip ACKs remain
inside the active cursor operation so the firmware can advance its stream.
Manual **Sync Now** uses the same coordinator.

## 5. Policy and schedule flow

`PolicyScheduleService` selects periods from weekday-assigned named templates
or a date-specific override. No-school dates resolve no active period.
First/last windows independently evaluate `Allow`, `Warn`, or `Lock`.

Terminal enforcement is optional and off by default. When enabled,
`BellPolicyProtocol` resolves the next 14 days into no more than 96 compact
dated windows. The firmware stages and commits that copy, persists it, and
evaluates only new checkouts. Missing cache dates fail open; an existing pass can
always check back in. Current terminal hardware has no speaker, so kiosk
warnings are visual and configured sounds remain desktop-only.

On receipt of a trip, the client resolves its checkout timestamp and records the
teacher workspace, schedule name, and class section. Enriched CSV export uses
that captured context rather than whatever schedule happens to be active later.

## 6. UI surfaces

| Surface | Current behavior |
| --- | --- |
| Find Terminal | Lists matching kiosks with name, availability, signal quality, and RSSI dBm |
| Dashboard | Live pass state/timers, recent trips, overdue report, and daily-guideline alerts |
| Trips | Search, filter, sort, paginate, and export enriched local history |
| Roster | Two-phase CSV mapping/import and teacher-workspace roster management |
| Policies & Bell Times | Named templates, weekday periods, class sections, date exceptions, actions, sound preview, and opt-in kiosk enforcement |
| Terminal Settings | Teacher details, authenticated kiosk rename, ID-length setting, and last paired device |
| Mini Window | Current period/window and pass availability; student names are not shown |

The teacher-started checkout action is labelled **Start Pass**. It is distinct
from the authenticated `MANUAL_CHECKIN` protocol command, which closes an
existing kiosk pass at the teacher's request.

## 7. Privacy boundary

Student names, rosters, grades, and class-section labels remain in the local
desktop database/UI. They are not sent to the kiosk or a website. BLE trip
records contain student IDs and timestamps. The policy cache contains only
dates, minute offsets, and numeric actions. Diagnostic data is not uploaded
automatically.

## 8. Verification boundary

Mac testing is sufficient for shared ViewModel/core behavior, SQLite migration,
schedule evaluation, exports, operation serialization, preview behavior, native
firmware unit tests, and the macOS BLE adapter.

A Windows PC is required for WinRT discovery/RSSI, GATT connection and
reconnect, bond reuse, credential-vault persistence, and packaged-app behavior.
Those Windows-specific behaviors have not yet been verified on physical Windows
hardware. A physical ESP32 is required for final persisted rename, offline
policy screen/enforcement, two-nearby-terminal isolation, and long-running BLE
stability tests.

## Development web client

`web-client/` is a separate production-code browser implementation, currently
under acceptance. Its React provider owns an application controller, exclusive
Web Lock, serialized terminal queue, Web Bluetooth adapter and IndexedDB store.
It does not import simulated preview state. The ESP32 firmware and shared literal
fixtures in `contracts/web-client/v1/` remain the protocol authority. See
[web design](design/chromebook-web-client.md) and [acceptance](testing/web-client-acceptance.md).
Desktop-only policy modes introduced after the baseline contract are not exposed
by the web UI.
