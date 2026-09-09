# Hallzee v1.0 feature and UI review

Reviewed September 9, 2026. **The classroom feature set is close; do not label
this a broad production release yet.** The teacher-started pass defect found below has been fixed. The remaining
work is public distribution and physical release acceptance, rather than more
classroom features. A supervised pilot is a reasonable next step after
installation/pairing checks.

This review inspected the shared client, Bluetooth paths, firmware/update
code, release workflows, and the packaged Mac UI. It includes findings beyond
the documentation backlog. No public release or physical firmware flash was
performed.

## Fix or resolve before general release

| Priority | Finding | Smallest useful resolution |
| --- | --- | --- |
| High | **Public updates currently return HTTP 404.** The installed development feed points to `dannysombrero/hallzee-mono`, unavailable to unauthenticated clients. | Choose a stable public downloads repository, configure it before building, then publish tested desktop and firmware releases. Verify Help/download/check-for-update from a signed-out browser and both apps. |
| High | **Physical cross-platform release evidence is missing.** Automated tests cannot prove Windows Bluetooth, firmware interruption recovery, or a teacher's clean-machine install. | Complete the short acceptance run below. |
| Medium | **Terminal reassignment preserves previous trips.** Unpairing is not a classroom-data handoff. | For v1.0, have IT manage reassignment and document that limit. Do not promise teachers they can safely swap terminals themselves until the [reassignment design](Design-Terminal-Reassignment) is implemented. |
| Medium | **Distribution signing is absent.** The Mac package is ad-hoc signed; there is no configured Developer ID notarization or Windows publisher signing. | Agree on an IT-approved pilot installation route. For broad teacher self-installation, configure signing/notarization and test the downloaded, quarantined package. |

The manual-pass defect found during the initial review is now resolved. Active
teacher passes are stored separately from terminal snapshots in database schema
7, restored per workspace, and completed in the same transaction that saves
history. Matching student IDs do not merge desktop and terminal passes. Existing
completed history is preserved; passes already lost by an older build cannot
be reconstructed. New regression tests cover the original failures, disconnects,
workspace isolation, failed writes, and repeated/concurrent completion.


## Improvements prepared in this branch

- **Teacher guide:** installation, pairing, daily use, roster/policies, desktop
  updates, firmware/USB setup, and troubleshooting in plain language. Each
  published release includes it, and publishing updates the public README.
- **Windows and Mac packaging:** a numbered desktop build produces Windows x64,
  Mac Apple-silicon, and Mac Intel ZIPs. The Mac `.app` includes its native BLE
  helper. The build version is passed into the app and core assembly.
- **Separate publishing action:** `Publish Tested Release` promotes the exact
  artifacts from a successful build run, with guide, notes, and checksums.
  Desktop and firmware use independent immutable version tags. The old moving
  Windows release no longer publishes automatically on every source push.
- **One release destination:** a build-time repository setting is used by
  desktop update checks, firmware downloads, and Help. Terminals receive signed
  firmware over BLE and do not need their own internet link.
- **UI clarity:** sidebar Help/Updates, actual running version instead of the
  hardcoded `v1.5.2-win64`, a new-version message after a successful manual check,
  and an explanation of desktop replacement versus terminal installation.
  Firmware controls appear first in Device settings. Sidebar content scrolls
  on short windows; Exceeded Time filters and sorting use separate rows to avoid
  clipping observed at the normal window width.
- **Mac reconnect:** helper startup now times out after 10 seconds and cleans
  up a failed helper instead of waiting indefinitely for its local connection.

These are local changes awaiting normal review, configuration, and release
validation. See [Build, test, and publish](Releasing).

## Already sufficient for v1.0

The app has the essential classroom workflows: terminal checkout/check-in,
live status and elapsed time, history/CSV export, roster entry/import, teacher
workspaces, bell schedules, optional terminal lockouts, remembered pairing,
reconnect, and signed firmware installation. A major UI redesign is unnecessary.

Keep the distinctions visible: daily pass counts are guidance; duration warnings
do not automatically return students; bell-time enforcement is optional; a
workspace export contains rules/schedules, not a complete classroom backup.
The guide explains these limits. The Mini Window is useful for daily teaching.

## Acceptance run — use the downloadable artifacts

1. **Install and launch:** Windows ZIP, Apple-silicon Mac ZIP, and Intel Mac ZIP
   on supported machines without Git/.NET/Arduino tools. Confirm the correct
   version and the school's permission/security prompts.
2. **One classroom session:** pair, import a small test roster with leading-zero
   IDs, check out at the terminal, return, sync, and export. Confirm one trip.
3. **Recovery:** quit/reopen, interrupt Bluetooth, reconnect, and confirm an
   active terminal pass and completed trips recover without duplication.
4. **Policies:** capacity, Warn versus Lock, check-in during a lock, and a bell
   schedule exception. Confirm the Mini Window matches the intended guidance.
5. **Desktop update:** use an older test build to discover a newer numbered
   release; replace the app and verify roster/history/pairing remain.
6. **Firmware update:** discover and install one compatible signed update on
   each display, retain identity/data/pairing, and confirm version after reboot.
   Test interruption and recovery per [firmware updates](Firmware-Updates).
7. **Public access:** signed-out teacher guide, all download links, firmware
   list, and an unauthenticated update check. Test offline error messaging too.

**Mac testing is sufficient** for shared logic/UI and the macOS CoreBluetooth
path. **A Windows PC is required** for WinRT scan/pairing, credential/bond reuse,
file dialogs, binary write pacing/notifications, and reconnect after firmware
restart. Windows-specific behavior has not yet been verified for this release.
Mac alone is not sufficient for the release acceptance run. Physical OTA and
USB migration on both platforms still require verification.

## Validation in this review

- Shared client tests: **86 passed**, including 13 teacher-pass lifecycle cases.
- Core/protocol tests: **107 passed**, including atomic teacher-pass completion, rollback,
  concurrent retry, and preservation of existing history.
- Publication logic: **6 offline tests passed** (failed build, wrong revision,
  missing Mac asset, private destination, existing tag, successful draft flow).
- All workflow YAML parses; shell syntax and whitespace checks pass.
- The initial Apple-silicon package opened and its dashboard/settings were
  visually inspected, but whole-app signature verification missed invalid
  signatures on 13 Bluetooth-helper libraries in `Contents/MonoBundle`. The
  September 9 helper crash (`CODESIGNING, Code 2, Invalid Page`) exposed this
  packaging defect. Packaging now explicitly signs and verifies native libraries
  before sealing bundles and checks helper startup on matching architectures.
  The corrected local ARM64 package passed all 30 native-library signature
  checks, whole-app verification, and a no-argument helper launch (exit 0).
  This launch exits before Bluetooth use; physical BLE remains unverified here.
- The original manual-pass failure probes are now covered by passing permanent
  regression tests for restart and terminal status messages.
- Windows/Intel Mac packaging in Actions and public publication have not yet
  been executed. Network restrictions prevented NuGet vulnerability-feed lookup;
  these builds are not a fresh dependency vulnerability audit.

## Defer without expanding v1.0

Automatic background update checks, automatic desktop installers, cloud sync,
new dashboards, touch experiments, and a broad visual redesign can wait.
A manual **Updates** button plus a stable public guide is enough for a pilot.
Full backup/restore and teacher self-service terminal reassignment deserve their
own follow-up; do not describe workspace export or unpair as those features.
