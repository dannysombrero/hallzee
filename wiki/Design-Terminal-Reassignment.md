# Terminal reassignment and record provenance

Status: recommendation; assignment-aware storage, sync, and handoff are not yet
implemented. The fast USB option is independent of this proposed data change.

## What exists today

- Firmware derives a stable `HZ-…` terminal ID from the ESP32 eFuse MAC. Renaming,
  unpairing, moving classrooms, and ordinary firmware updates do not change it.
- Desktop sync stamps each received trip with the authenticated terminal ID.
  SQLite deduplicates by `(terminal_id, trip_id)`, with a separate sync cursor
  for each terminal. Enriched CSV exports already include `terminal_id`.
- The terminal's CSV records contain the local trip number and trip details,
  but no teacher/workspace/assignment identity captured at checkout.
- Desktop sync assigns trip context using the active workspace and its schedule,
  including replayed records. Display queries resolve names from that workspace's
  current roster. Owner reset removes credentials and bonds while retaining
  trips and settings. A new computer can therefore receive older retained trips.

Terminal ID identifies hardware, not the teacher at the time of a trip. Adding
only that ID to the terminal CSV would not distinguish two teachers who used the
same device. Renaming the terminal also cannot establish historical ownership.
The current workflow is not sufficient for transferring classroom data safely
between independent teachers. Do not treat Disconnect & Unpair as a completed
data handoff. Existing untagged history cannot be reliably attributed later.

## Recommended identity model

| Field | Meaning | Lifetime |
| --- | --- | --- |
| `terminal_id` | Physical device | Stable across ownership changes |
| `workspace_id` | Teacher's logical workspace | Stable independently of computer and terminal |
| `assignment_id` | One assignment of a terminal to a workspace | New random ID for every handoff, even back to the same teacher |
| `storage_epoch` | Trip-number namespace | New random ID after a destructive reset; preserved by ordinary updates |
| `trip_id` | Local monotonically increasing trip number | Unique within terminal + storage epoch |

Keep authentication's client ID/key separate from workspace and assignment IDs.
A teacher replacing a laptop need not create a new classroom assignment; a new
teacher using the same laptop must create one. A name is a label, not an ID.

Persist assignment configuration before admitting new passes. Capture
`terminal_id`, `storage_epoch`, and `assignment_id` when a pass starts, including
in its persisted active-pass state, and carry them unchanged through completion,
sync, storage, and export. Store the workspace mapping in an assignment table;
optionally carry the workspace ID in the event for independent validation.
Use `(terminal_id, storage_epoch, trip_id)` for event deduplication and scope sync
cursors to terminal, epoch, and authorized assignment. Never infer assignment
from sync time, current terminal name, or the currently open workspace.

Preserve the assignment's historical label for audit/export, while allowing its
display name to change. Join students through the record's originating workspace;
identical student IDs in two workspaces must never resolve to each other's
rosters. Use immutable student IDs or record a historical label if later roster
renames/deletions must not change old reports. Show terminal and original
workspace/assignment in trip details, filters, and exports; routine activity
remains scoped to the teacher's workspace.

No school server is needed now. Later, add an organization and workspace access
model around these same IDs, and per-destination delivery cursors. The existing
single synced flag must not stand for delivery to every future computer/server.

## Deliberate handoff

1. The current teacher chooses **Transfer terminal**. Stop new checkouts and
   require all active passes to finish; freeze handoff while an update/sync is
   running. Capture the old assignment's final trip number.
2. Sync all old-assignment records to durable local storage and verify the final
   cursor. Offer an archive/export carrying provenance. Transport receipt alone
   does not prove a durable save; retries must not delete or relabel records.
3. Retire the old assignment. For the individual-teacher product, remove its
   student records and classroom settings from the terminal only after the
   archive is verified and the teacher explicitly completes the handoff.
   Retain history on the old teacher's computer. Reset ownership credentials and
   bonds as part of the handoff, not as a substitute for the data steps.
4. The new teacher claims the device and creates a new assignment. Explicitly
   provision their policy, schedule, ID limits, and time before accepting passes.
   Keep the physical terminal ID. Ordinary handoffs can retain the trip counter
   and epoch; do not reset them simply because ownership changed.

Make this a persisted, resumable state machine. Power loss/retry at any step
must not make old data visible to a new owner or permit records with no assignment.
If the former computer is unavailable, offer a separate physical recovery flow
that explains any irreversible data loss. Physical owner reset must put the
terminal in a transfer/recovery state, not immediately expose old records to
whoever claims next. Recovery must not silently purge unsynced trips.

If a future school deployment retains multiple assignments on a terminal, enforce
assignment authorization on the terminal before sending records. A desktop label
or hidden row is insufficient: the new owner must not receive another teacher's
student data. Keep unrecognized/legacy history outside ordinary rosters and
analytics; present an explicit reconciliation path only to an authorized owner.

## Migration and other edge cases

- Introduce a versioned trip format and protocol capability. Reject unsupported
  clients before syncing or handing off; do not append fields and let old parsers
  guess. Coordinate firmware, desktop database, active-pass, ACK/cursor, export,
  and minimum-version changes in one rollout.
- Mark existing records `legacy/unassigned`. Preserve known terminal provenance,
  but never backfill a teacher based solely on the profile open during upgrade.
  An authorized teacher can explicitly adopt a verified archive. Never label a
  record “previous teacher” unless assignment evidence establishes that fact.
- Replaying a record must not overwrite its workspace/assignment, schedule, or
  original source. Import should flag conflicting immutable content under the
  same event key rather than silently replacing it.
- A factory reset can reuse trip numbers on the same hardware; the storage epoch
  prevents collision. Restoring an old full-flash backup can rewind the counter
  and epoch together. Detect restore/recovery and start a fresh epoch for newly
  created trips, retaining the old epoch on restored records and reconciling
  active passes. USB recovery must participate in this rule.
- A stale old-owner computer must fail authentication after handoff. A device
  moved without an internet connection must still enforce its assignment.
- Capture event time and sync time separately. Clock correction, daylight-saving
  changes, and schedule edits cannot establish assignment boundaries reliably.
- Desktop-created trips also need a unique installation/source namespace; the
  current shared `DESKTOP` source is insufficient for future merged databases.
- Define capacity/retention explicitly. Refuse new work visibly if storage is
  full; never overwrite unarchived history to make a handoff succeed.

## Implementation order and acceptance

First add immutable event provenance, schema/protocol migrations, and scoped
roster queries. Then implement firmware filtering and transactional handoff.
Expose Transfer terminal only once the complete flow and legacy recovery work.
School-wide sync can follow without changing device/event identity.

Mac testing is sufficient for shared migration, import, deduplication, and
simulated handoff tests. Required cases include two devices with trip ID 1, two
teachers with student ID 123, A → B → A reassignment, duplicate old syncs while
another workspace is active, offline handoff, interrupted archive/erase/claim,
legacy records, storage-full refusal, and reset/backup-restore counter reuse.
Use two physical terminals for identity, persistence, and owner enforcement;
a Mac is sufficient for those firmware checks. A Windows PC is required for
WinRT reconnect/bond removal and DPAPI credential replacement during handoff.
The proposed workflow has not been implemented or hardware-verified on either
platform; Windows handoff behavior remains unverified.
