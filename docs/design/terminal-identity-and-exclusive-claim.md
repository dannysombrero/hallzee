# Feature Design: Stable Terminal Identity and Exclusive Client Claim

**Status:** Implementation-ready design

**Target milestone:** Before multi-terminal or background auto-sync rollout

**Scope:** Stable kiosk identity, one-owner authorization, safe multi-terminal storage, profile association, discovery UX, recovery, and platform verification

**Supersedes:** Any assumption that a BLE address, the advertised name Hallzee, or a process-local connection is sufficient terminal identity or authorization

**Implementation checkpoint (2026-09-03):** The v2 protocol/security primitives,
terminal-scoped SQLite migration, authenticated session boundary, firmware
identity/claim state, Secure Connections characteristic permissions, one-active-
central enforcement, and physical claim gesture are implemented and compile
validated. Desktop platform credential-vault adapters, universal-client wiring,
owner-reset USB flow, and physical multi-terminal verification remain planned
work in the agent packages below.

---

## 1. Outcome

Hallzee must safely support two or more nearby terminals without mixing their
records or allowing a desktop client to operate the wrong kiosk.

After this design is implemented:

1. Every physical ESP32 has a stable terminal_id supplied by the firmware.
2. Windows and macOS transport identifiers remain discovery hints only.
3. A terminal can be claimed by exactly one desktop installation.
4. A desktop installation may own multiple terminals, but the application keeps
   only one active terminal connection at a time.
5. A classroom profile selects one terminal; several profiles may intentionally
   select the same terminal for different class periods.
6. No trip, active-pass, clock, recovery, or settings command is accepted until
   the connected desktop proves ownership.
7. Trip IDs and sync cursors are scoped by terminal_id, so terminal A trip 1 and
   terminal B trip 1 are separate durable records.
8. Replacing a terminal, changing a BLE address, or reinstalling the desktop app
   cannot silently redirect commands to another kiosk.

This is both a correctness and a security feature. The stable identity prevents
accidental data crossover; the exclusive claim prevents an unauthorized nearby
client from reading records or changing terminal state.

---

## 2. Current State and Gaps

The current implementation has useful pieces, but they do not yet form a safe
multi-terminal model:

- Windows discovery de-duplicates advertisements by the BLE address and places
  that address in TerminalDevice.Id.
- macOS discovery uses CoreBluetooth's per-host CBPeripheral.Identifier.
- every kiosk advertises the same Hallzee name and service UUID;
- ITerminalConnection owns one process-local transport connection and calls
  DisconnectAsync() before connecting to another device;
- SQLite has a terminals table and a trips.terminal_id column, but the active
  sync workflow does not bind the selected device to either one;
- firmware trip records do not contain a terminal identity, so imported records
  currently fall back to DEFAULT;
- trips.trip_id is the sole primary key and the sync cursor is the global
  maximum trip ID. Two kiosks can therefore collide or skip records;
- BLE pairing is not required, there is no owner credential, and the firmware
  accepts commands from whichever client has the connection;
- the architecture mentions SET,TERMINAL_NAME, but the current firmware and
  protocol do not implement it.

Serializing commands inside one app process protects the protocol from local
write races. It does not establish terminal identity and does not prevent a
second computer or app instance from connecting later.

---

## 3. Definitions and Invariants

| Term | Meaning |
| --- | --- |
| transport_id | Windows BLE address or macOS CoreBluetooth UUID used to open the current radio connection. It may change and is not trusted as identity. |
| terminal_id | Stable firmware identity in the form HZ- plus the ESP32 eFuse MAC as 12 uppercase hexadecimal digits, for example HZ-A1B2C3D4E5F6. It is not a secret. |
| terminal_suffix | Last four characters of terminal_id, used in the advertised name and physical matching UX. |
| client_id | Random UUID generated once per desktop installation and stored locally. It identifies the claimant but is not itself a secret. |
| claim_key | Random 80-bit, 16-character Crockford Base32 code displayed only on the physical kiosk while claim mode is active. |
| owner_key | 256-bit key derived during claim and stored by the terminal and in the desktop OS credential vault. It is never transmitted. |
| owner | The one desktop installation whose client_id and owner_key match the terminal's persisted claim. |
| associated profile | A local classroom profile configured to use a terminal. Association does not grant ownership. |

The implementation must preserve these invariants:

- exactly zero or one owner record exists on a terminal;
- a claimed terminal fails closed when authentication is absent or invalid;
- only the authenticated session may issue application commands;
- a second BLE central is rejected while another connection is active;
- an unauthenticated connection is disconnected after 10 seconds;
- the authenticated terminal_id, never a discovery result or UI label, scopes
  every cursor lookup and trip write;
- changing custom_name cannot change identity;
- no remote command can erase or replace an existing owner;
- legacy protocol v1 is never accepted by a claimed terminal.

---

## 4. Threat Model

### Protected scenarios

- A teacher sees two nearby Hallzee devices and selects the wrong one.
- A BLE address changes or is reused and points at different hardware.
- Two terminals independently produce the same numeric trip ID.
- A nearby user runs another Hallzee client and attempts to read trips, change
  settings, set the clock, acknowledge records, or manually check in a student.
- A previously captured authentication response is replayed.
- A second central attempts to connect while the owner session is active.

### Explicit non-goals

- radio jamming or repeated connection attempts that cause denial of service;
- a compromised operating-system account that can read the owner's live process;
- an attacker with physical USB access to the ESP32;
- cloud account identity or school-wide fleet administration;
- sharing one terminal between independent desktop owners.

Physical USB access is the recovery boundary. Losing the owner desktop requires a
USB ownership reset; it must not require deleting LittleFS trip history.

---

## 5. Identity and Discovery Design

### 5.1 Firmware identity

At boot, firmware obtains the ESP32 eFuse MAC with ESP.getEfuseMac() and formats
it as HZ-XXXXXXXXXXXX. The value is stable for the hardware and does not depend
on Preferences, LittleFS, firmware flashing, BLE random addressing, or the
desktop operating system.

The advertised local name is Hallzee-XXXX, using terminal_suffix. A persisted
custom name is returned after authentication but is not used as the canonical
identifier. This gives a teacher a short value that can be matched between the
desktop list and the kiosk screen.

### 5.2 Desktop discovery

TerminalDevice.Id is renamed conceptually to TransportId; compatibility
properties may remain temporarily while callers migrate. Discovery results show:

- advertised name, such as Hallzee-E5F6;
- platform transport ID in an expandable details area;
- RSSI when the platform provides it;
- saved status only when a prior terminal_id has been mapped to that transport
  hint. Saved status is advisory until the handshake verifies identity.

The app must never automatically send sync or settings commands immediately
after opening a transport. It first runs the identity and authorization state
machine in section 7.

### 5.3 Identity mismatch

After IDENTITY, compare the observed terminal_id with the terminal the user or
profile expected. If it differs:

1. send no application command;
2. disconnect;
3. retain both database records unchanged;
4. show “Connected hardware does not match the saved terminal” with expected and
   observed suffixes;
5. require an explicit return to discovery. Never rewrite the saved association
   automatically.

---

## 6. Exclusive Claim and Link Security

Ownership uses two layers:

1. **BLE link security:** require LE Secure Connections, MITM protection, and
   bonding for the GATT characteristics. The operating system may present its
   normal Bluetooth confirmation UI. A claimed terminal retains only its owner
   bond; additional bonds are rejected.
2. **Application ownership:** use a challenge-response proof tied to the stable
   terminal_id and client_id. BLE transport identity alone is never accepted as
   ownership.

Do not implement new cryptographic primitives. Use ESP32/mbedTLS HMAC-SHA-256 and
HKDF-SHA-256 on firmware and System.Security.Cryptography on desktop.

### 6.1 Physical claim mode

- Claim mode is available only when the terminal has no owner.
- From the idle screen, holding * and # for five seconds starts a two-minute
  claim window. The existing two-second active-pass reset behavior remains
  unchanged when a pass is active.
- The kiosk displays PAIR Hallzee-XXXX and a randomly generated 16-character
  Crockford Base32 claim_key, grouped as XXXX-XXXX-XXXX-XXXX.
- The kiosk also displays a separate random six-digit Bluetooth passkey. The
  operating system requests this passkey while establishing the MITM-protected
  bond; the Hallzee app requests claim_key after the secure link is open. Label
  the two values distinctly and alternate them on the small display if they do
  not fit together.
- The key is held in RAM only and regenerated whenever claim mode restarts.
- Claim mode closes immediately after a successful claim or when the timer
  expires. Expiration clears the key and any pending nonce.
- Once claimed, the keypad cannot open claim mode and no BLE command can replace
  the owner.

### 6.2 Key derivation

Both sides derive the same owner key after validating the physical claim proof:

    owner_key = HKDF-SHA256(
      input_key_material = UTF8(claim_key_without_hyphens),
      salt = UTF8(terminal_id),
      info = UTF8("Hallzee owner v2|" + client_id),
      output_length = 32 bytes
    )

Firmware persists owner_client_id and owner_key in a dedicated Preferences
namespace. The desktop stores owner_key in an OS-protected credential vault,
keyed by terminal_id; SQLite stores only non-secret metadata.

### 6.3 Proof construction

All nonces are 16 random bytes encoded as 32 uppercase hexadecimal characters.
All HMAC values are 32 bytes encoded as 64 uppercase hexadecimal characters.
Fields are ASCII and joined exactly with |; no locale-sensitive formatting is
allowed.

    claim_proof = HMAC-SHA256(
      key = UTF8(claim_key_without_hyphens),
      message = UTF8("CLAIM|2|" + terminal_id + "|" + client_id + "|" + nonce)
    )

    auth_proof = HMAC-SHA256(
      key = owner_key,
      message = UTF8("AUTH|2|" + terminal_id + "|" + client_id + "|" + nonce)
    )

Firmware compares proofs in constant time. A nonce is single-use and is cleared
after its proof stage, on failure, timeout, or disconnect. A successful CLAIM
replaces the identity nonce with a new commit nonce; it never reuses the claim
challenge for CLAIM_COMMIT. Three failed proofs in one claim window close the
window and disconnect the client.

During the claim window, firmware permits one pending bond. A failed, aborted,
or expired claim deletes that pending bond. CLAIM_COMMIT records the authenticated
peer identity as the owner bond and removes every other bond. On later boots,
connections from a non-owner bonded peer are disconnected before application
commands are processed. BLE address resolution remains the BLE stack's job;
terminal_id and the application proof remain the final authorization check.

### 6.4 Credential storage

- Windows: protect the 32-byte owner key with DPAPI for CurrentUser before
  writing it under the Hallzee application-data directory.
- macOS: store it as a generic-password item in Keychain with service
  com.hallzee.terminal-owner and account equal to terminal_id.
- preview/tests: use an in-memory implementation only.
- logs, exceptions, telemetry, SQLite, and exports must never contain the claim
  key, owner key, proof, or full nonce.

---

## 7. Protocol Version 2

Protocol remains newline-delimited UTF-8 over the existing GATT service. Increase
the firmware command buffer limit from 48 to 192 bytes; existing 20-byte ATT
fragmentation remains unchanged.

Terminal names are 1–24 characters and limited to letters, digits, spaces,
hyphens, underscores, and parentheses so they remain safe CSV fields. Reject
commas, newlines, control characters, and leading/trailing whitespace.

### 7.1 Pre-authorization commands

Only these commands are legal before authorization:

| Command | Meaning |
| --- | --- |
| HELLO,2,<client_id> | Start a v2 identity handshake. |
| CLAIM,2,<client_id>,<proof> | Claim an unowned terminal while physical claim mode is active. |
| CLAIM_COMMIT,2,<client_id>,<proof> | Atomically persist a pending claim after desktop credential storage. |
| CLAIM_ABORT,2,<client_id> | Discard a pending claim. |
| AUTH,2,<client_id>,<proof> | Authenticate the existing owner for this connection. |

All other commands receive ERROR,AUTH_REQUIRED and are not executed. A claimed
terminal receiving HELLO,1 responds ERROR,UPGRADE_REQUIRED and disconnects.

### 7.2 Identity response

For each valid HELLO,2, firmware creates a fresh nonce and sends:

    IDENTITY,2,<terminal_id>,<terminal_suffix>,<UNCLAIMED|CLAIMED>,<nonce>

The response intentionally does not assert that the caller is the owner. The
caller proves that separately with AUTH.

### 7.3 First claim

    Desktop  -> HELLO,2,<client_id>
    Terminal -> IDENTITY,2,<terminal_id>,<suffix>,UNCLAIMED,<nonce>
    Desktop  -> CLAIM,2,<client_id>,<claim_proof>
    Terminal -> CLAIM_OK,2,<terminal_id>,<commit_nonce>

The terminal derives an owner key but holds it in RAM. The desktop derives and
stores its key only after CLAIM_OK. If desktop vault storage fails, it sends
CLAIM_ABORT and firmware clears the pending owner.

To commit atomically:

    Desktop  -> CLAIM_COMMIT,2,<client_id>,<auth_proof_using_commit_nonce>
    Terminal -> AUTH_OK,2,<terminal_id>,<custom_name>

If commit is absent after 10 seconds, the pending owner is discarded. CLAIM_OK
therefore means proof accepted; AUTH_OK means ownership committed and the
application session is authorized. CLAIM_COMMIT uses the auth_proof formula from
section 6.3 with commit_nonce; returning AUTH uses the fresh nonce from IDENTITY.

### 7.4 Returning owner

    Desktop  -> HELLO,2,<client_id>
    Terminal -> IDENTITY,2,<terminal_id>,<suffix>,CLAIMED,<nonce>
    Desktop  -> AUTH,2,<client_id>,<auth_proof>
    Terminal -> AUTH_OK,2,<terminal_id>,<custom_name>

Errors are explicit and terminal state is unchanged:

| Response | Meaning |
| --- | --- |
| ERROR,PAIRING_MODE_REQUIRED | Terminal is unclaimed but its physical claim window is closed. |
| ERROR,ALREADY_CLAIMED | A claim was attempted against an owned terminal. |
| ERROR,OWNER_MISMATCH | client_id differs from the persisted owner. |
| ERROR,AUTH_FAILED | Proof is malformed or invalid. |
| ERROR,AUTH_TIMEOUT | Authorization was not completed within 10 seconds. |
| ERROR,UPGRADE_REQUIRED | A claimed v2 terminal received legacy protocol. |

### 7.5 Authorized commands

After AUTH_OK, existing commands retain their meanings. Add:

| Command | Response | Meaning |
| --- | --- | --- |
| GET_IDENTITY | IDENTITY_INFO,2,<terminal_id>,<custom_name> | Re-read authenticated identity. |
| SET,TERMINAL_NAME,<name> | SETTINGS_ACK,TERMINAL_NAME,<name> | Persist and advertise a validated custom name. |

TIME, TIME_CURSOR, SYNC_START, SYNC_ALL, ACK, active-pass commands, manual
check-in, and every settings read/write must all pass the same authorized session
gate. Asynchronous pass/trip notifications are emitted only after authorization.

---

## 8. Storage Model and Migration

Increment DatabaseMigrator.CurrentSchemaVersion from 3 to 4. Migration 4 must run
in one transaction and preserve all existing data.

### 8.1 Tables

    CREATE TABLE terminals_v4 (
        terminal_id TEXT PRIMARY KEY,
        custom_name TEXT NOT NULL,
        transport_id TEXT,
        protocol_version INTEGER NOT NULL DEFAULT 2,
        claim_status TEXT NOT NULL DEFAULT 'UNKNOWN',
        last_seen_at TEXT,
        max_id_length INTEGER NOT NULL DEFAULT 10
    );

    CREATE TABLE client_installation (
        singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
        client_id TEXT NOT NULL UNIQUE,
        created_at TEXT NOT NULL
    );

    CREATE TABLE profile_terminal_assignments (
        profile_id TEXT PRIMARY KEY,
        terminal_id TEXT NOT NULL,
        assigned_at TEXT NOT NULL,
        FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE,
        FOREIGN KEY (terminal_id) REFERENCES terminals_v4(terminal_id) ON DELETE RESTRICT
    );
    CREATE INDEX idx_profile_terminal_terminal
        ON profile_terminal_assignments(terminal_id);

    CREATE TABLE trips_v4 (
        terminal_id TEXT NOT NULL,
        trip_id INTEGER NOT NULL,
        student_id TEXT NOT NULL,
        trip_date TEXT NOT NULL,
        time_out TEXT NOT NULL,
        time_in TEXT NOT NULL,
        duration_seconds TEXT NOT NULL,
        status TEXT NOT NULL,
        synced_at TEXT NOT NULL,
        PRIMARY KEY (terminal_id, trip_id)
    );
    CREATE INDEX idx_trips_v4_date ON trips_v4(trip_date);
    CREATE INDEX idx_trips_v4_student ON trips_v4(student_id);

The implementation may retain the final table names terminals and trips by
creating temporary _v4 tables, copying data, dropping old tables, and renaming
inside the migration transaction.

### 8.2 Legacy data rules

- Copy existing trips using their current terminal_id, defaulting null/blank to
  LEGACY-DEFAULT.
- Insert one terminals row for LEGACY-DEFAULT named “Legacy imported trips”
  with claim status LEGACY.
- Do not associate LEGACY-DEFAULT with an active profile automatically.
- Preserve timestamps, statuses, IDs, and roster joins exactly.
- Never infer that a newly discovered physical terminal owns legacy records.

### 8.3 Repository API changes

Replace global operations with terminal-scoped operations:

    TripStoreResult Store(string terminalId, string payload);
    long GetLatestTripId(string terminalId);
    IReadOnlyList<EnrichedTripRecord> GetRecentTrips(
        string terminalId, int count = 10, string? profileId = null);

Trip queries intended to show all terminals may accept a nullable terminal
filter, but sync code must never use the all-terminal overload. SyncSession must
be constructed or started with a non-empty authenticated terminal_id and must
pass it on every store. The firmware trip CSV payload stays unchanged; the
desktop injects session identity at the repository boundary.

CSV export keeps terminal_id as a separate column. Rows are ordered by trip_date,
activity time, terminal_id, then numeric trip_id; no export may imply that numeric
trip IDs are globally unique.

---

## 9. Desktop Architecture

### 9.1 New core types

Add under receiver/windows/BathroomSync.Core/:

- Domain/TerminalIdentityModels.cs
  - TerminalIdentity
  - TerminalSecurityState
  - AuthenticatedTerminalSession
  - TerminalIdentityMismatchException
- Protocol/TerminalIdentityProtocol.cs
  - strict parsers/builders for all section 7 messages;
  - UUID, terminal ID, nonce, proof, and terminal-name validation;
  - HMAC/HKDF helpers using framework cryptography.
- TerminalSession.cs
  - owns the identity/authentication state machine;
  - buffers fragmented lines during handshake;
  - exposes post-authorization protocol text to SyncSession;
  - times out and disconnects on failure.
- TerminalCredentialStore.cs
  - ITerminalCredentialStore contract;
  - in-memory implementation for tests.
- repository methods for client identity and profile-terminal association.

Keep ITerminalConnection as the raw platform transport. It must not decide
ownership. TerminalSession is the only class allowed to expose an
application-ready connection.

### 9.2 Platform adapters

- receiver/windows/BluetoothConnectionManager.cs
  - keep address-based opening;
  - capture RSSI and advertised suffix;
  - surface pairing/security failures distinctly;
  - continue disconnect-before-connect behavior.
- receiver/MacBLEAgent/Program.cs
  - continue using CBPeripheral.Identifier only as transport_id;
  - include RSSI and advertisement local name in discovery events;
  - surface CoreBluetooth bonding/encryption errors to the parent process.
- receiver/universal/Services/MacAgentTerminalConnection.cs
  - map the additional discovery fields;
  - do not mark a terminal trusted merely because CoreBluetooth remembers it.
- add Windows DPAPI and macOS Keychain credential-store implementations selected
  in receiver/universal/App.axaml.cs.

### 9.3 ViewModel integration

Update MainViewModel to depend on TerminalSession, not directly on raw connection
events. ConnectAndSyncAsync() must execute in this order:

1. open the selected transport;
2. receive and validate IDENTITY;
3. enforce expected profile/terminal match;
4. authenticate, or launch the explicit claim flow for an unclaimed terminal;
5. persist the verified transport hint and last-seen timestamp;
6. start SyncSession with the authenticated terminal_id;
7. send active-pass, settings, capacity, time, and cursor commands;
8. display connected state only after AUTH_OK.

FindTerminalsViewModel must not preselect the first result when more than one
terminal is found. Require deliberate selection and display the suffix in both
the row and confirmation text.

TerminalSettingsViewModel.ApplySettingsAsync() must remove the preview-hallzee
default. It receives the active authenticated session identity, sends the rename
command, waits for the matching ACK, and then updates local metadata. A local
database write must not claim that a firmware rename succeeded.

### 9.4 User-visible states

The discovery/connection UI must distinguish:

- Nearby, identity not verified
- Unclaimed; physical pairing mode required
- Enter code shown on Hallzee-XXXX
- Claimed by this installation
- Claimed by another installation
- Saved terminal not found
- Identity mismatch; connection closed
- Legacy unsecured firmware; upgrade required

Never offer a remote Take over, Replace owner, or Forget owner button.

---

## 10. Firmware Architecture

Add these modules at the repository root:

- TerminalIdentity.h/.cpp
  - eFuse-based ID and suffix formatting;
  - validated custom-name persistence;
  - owner metadata persistence in a dedicated Preferences namespace.
- TerminalSecurity.h/.cpp
  - claim window, nonce lifecycle, proof verification, HKDF derivation,
    authorization state, failure counter, and timeout;
  - mbedTLS only; constant-time comparison.

Change these existing files:

- ArduinoBluetoothSerialPort.h/.cpp
  - configure Secure Connections, MITM, and bonding;
  - track the active connection ID;
  - reject/disconnect an additional central;
  - expose disconnectClient() through BluetoothSerialPort;
  - restart advertising only after complete disconnect cleanup.
- BluetoothSync.h/.cpp
  - implement the v2 pre-auth state machine;
  - gate every existing command and outbound event;
  - reset auth, command buffer, nonce, and sync stream on disconnect;
  - raise command limit to 192 bytes;
  - implement authenticated identity and terminal-name operations.
- KeypadController.h/.cpp
  - preserve active-pass * + # reset at two seconds;
  - when idle and unclaimed, invoke a separate pairing callback after five
    seconds and suppress ordinary clear/submit release actions.
- TerminalDisplay.h/.cpp
  - add pairing code, pairing timeout, claimed-success, and pairing-error views;
  - always include the same four-character suffix shown by desktop discovery.
- bathroom-signin.ino
  - instantiate identity/security services;
  - wire pairing callback and display lifecycle;
  - ensure offline checkout remains available when claim mode is closed.
- Config.h
  - add named timeout/length constants; do not add a static owner secret.

Before implementation, pin and document the supported ESP32 Arduino board
package version. Security APIs differ between major versions; do not silently
switch BLE libraries in this feature. If the current library cannot enforce
encrypted MITM-protected characteristics, stop after the compile spike and
record the required library migration as a separate prerequisite.

---

## 11. Ownership Recovery

Ownership reset is physical-USB-only:

1. Add a newline command OWNER_RESET to the USB serial diagnostic channel.
2. Clear only the owner client ID, owner key, BLE bond, and pending claim state.
3. Preserve LittleFS trips, active passes, max ID length, capacity, terminal name,
   and stable terminal_id.
4. Print a success/failure status without printing credentials.
5. Add scripts/reset-terminal-owner-macos.sh and
   scripts/reset-terminal-owner-windows.ps1, reusing the existing serial-port
   detection conventions from the flash scripts.
6. Refuse automatic selection when multiple serial devices are present unless a
   port is supplied explicitly.

There is no BLE ownership-reset command. A later transfer feature may add an
owner-authorized transfer window, but it is outside this design.

---

## 12. Compatibility and Rollout

- Unclaimed upgraded firmware may answer HELLO,1 only in an explicitly enabled
  temporary migration mode. The mode is disabled permanently once claimed.
- Claimed firmware always rejects v1 with ERROR,UPGRADE_REQUIRED.
- New desktop builds may identify v1 firmware but must label it “Unsecured legacy
  terminal”; production sync requires an explicit temporary compatibility flag.
- Do not enable auto-sync until stable identity, authentication, and per-terminal
  cursors are complete.
- Upgrade and claim one kiosk at a time. Verify its suffix and perform a full sync
  before moving to the next kiosk.

Implementation must update docs/bluetooth-protocol.md, docs/architecture.md,
docs/testing-and-installation.md, their matching Wiki pages, and the receiver
README files in the same branch. Those pages should describe shipped behavior;
this design page describes the target until rollout is complete.

---

## 13. Granular Single-Agent Implementation Plan

Complete work packages in order. Each package must leave tests compiling and may
be submitted as its own reviewable commit or pull request.

### Package 0 — Baseline and API spike

Files: libraries.txt, setup scripts, ArduinoBluetoothSerialPort.*, and temporary
compile-only test code if required.

- Record the exact ESP32 board package version installed by bootstrap scripts.
- Confirm the current BLE library exposes Secure Connections, MITM, bonding,
  encrypted characteristic permissions, bond removal, connection ID, and server
  disconnect APIs on the supported package. Confirm that security callbacks
  expose enough peer identity to retain one owner bond and remove a failed
  pending bond.
- Compile on a clean bootstrap environment.
- Do not proceed if encrypted MITM characteristics cannot be enforced; document
  the prerequisite library decision instead.

Exit criteria: clean firmware compile and a short checked-in compatibility note;
no product behavior change.

### Package 1 — Pure protocol and cryptography core

Files: new core domain/protocol files and
TerminalIdentityProtocolTests.cs.

- Implement strict builders/parsers and validators from section 7.
- Implement HMAC/HKDF using framework cryptography.
- Add published RFC test vectors plus fixed Hallzee claim/auth vectors.
- Test malformed UUIDs, IDs, nonces, proof lengths, names, unknown versions,
  fragmented lines, replayed response handling, and constant casing.
- Add the in-memory credential store.

Exit criteria: core tests pass on macOS; no BLE or UI dependency in tests.

### Package 2 — Database migration and terminal-scoped repositories

Files: DatabaseMigrator.cs, TripSqliteRepository.cs, domain repository contracts,
and database/repository tests.

- Add migration 4 exactly as section 8 defines.
- Change trip uniqueness and cursor queries to (terminal_id, trip_id).
- Add client installation identity and profile assignment operations.
- Remove implicit DEFAULT from new writes; reject blank terminal IDs.
- Update every repository caller and test fixture so the solution compiles.
- Test two terminals both storing trip 1, independent cursors, migration rollback,
  legacy preservation, idempotence, cross-terminal queries, and CSV export.

Exit criteria: all .NET tests pass and no sync path calls a global cursor API.

### Package 3 — Firmware identity and claim state

Files: new TerminalIdentity.*, TerminalSecurity.*, Config.h, native test support,
and firmware tests.

- Implement stable ID, suffix, name validation, owner persistence, random claim
  key, nonce lifecycle, proof verification, and timeout.
- Inject clock/random/storage/crypto seams where needed for deterministic tests.
- Add tests for fresh/claimed boot, valid claim, wrong client, wrong proof,
  timeout, three-failure lockout, nonce replay, disconnect cleanup, and owner
  persistence.

Exit criteria: native tests and firmware compile pass; no BLE command is changed
yet.

### Package 4 — Firmware BLE enforcement and protocol v2

Files: Bluetooth transport/sync modules and their native tests.

- Enforce one central and secure bonded transport.
- Add pre-auth protocol, atomic claim commit, application-command gate, and
  10-second unauthenticated disconnect.
- Gate outgoing live events and trip records.
- Implement terminal identity/name commands and error responses.
- Verify oversized, fragmented, duplicated, out-of-order, and unauthorized
  commands cannot mutate storage or active-pass state.

Exit criteria: tests prove every existing command is denied before AUTH_OK, and
an authenticated session retains all existing protocol behavior.

### Package 5 — Physical pairing and USB recovery

Files: keypad, display, composition root, reset scripts, and native tests.

- Add the idle/unclaimed five-second gesture and pairing screens.
- Preserve the occupied-pass reset gesture exactly.
- Add two-minute claim timeout and visual suffix matching.
- Add USB owner reset and both one-command scripts.
- Test that owner reset preserves trips/settings and that claimed kiosks cannot
  reopen pairing mode from the keypad.

Exit criteria: automated gesture/state tests pass and USB reset is manually
verified without erasing trip history.

### Package 6 — Desktop terminal session and credential vaults

Files: new TerminalSession.cs, credential adapters, App.axaml.cs, Windows BLE
manager, Mac agent/adapter, and session tests.

- Centralize connection event consumption in TerminalSession.
- Implement identity, mismatch, claim, commit, returning auth, timeout, and
  disconnect state transitions.
- Add DPAPI, Keychain, and preview vaults.
- Ensure secrets are redacted from all logs and exception messages.
- Preserve platform transport IDs only as replaceable hints.

Exit criteria: mock tests cover both platforms' contracts; raw connection cannot
be used by MainViewModel or WinForms sync code to bypass authorization.

### Package 7 — Universal and WinForms workflow integration

Files: MainViewModel.cs, discovery/settings ViewModels and XAML, WinForms
Program.cs, preview adapter, and UI/ViewModel tests.

- Implement the section 9 connection order.
- Add claim-code entry and all explicit security states.
- Remove first-result auto-selection when multiple terminals exist.
- Scope sync and live-trip writes to authenticated identity.
- Use profile assignment for reconnect/auto-sync targeting.
- Remove placeholder terminal IDs and optimistic rename persistence.

Exit criteria: tests demonstrate wrong-terminal rejection, deliberate selection,
claim/auth success, independent terminal cursors, and no connected UI before
AUTH_OK.

### Package 8 — Documentation, end-to-end verification, and rollout gate

Files: protocol, architecture, contributor testing guide, Wiki mirrors, receiver
READMEs, and release notes.

- Replace current no-PIN language with shipped secure behavior.
- Document claim, reconnect, identity mismatch, USB recovery, and migration.
- Run all automated suites and the platform/hardware matrix below.
- Keep auto-sync disabled until every required row passes.

Exit criteria: documentation matches implementation, clean-machine bootstrap
works, and required physical tests are recorded with board package, OS, and
hardware identifiers.

---

## 14. Required Automated Tests

Run these repository commands before requesting human hardware verification:

    bash scripts/flash-terminal-macos.sh
    make -C test coverage
    dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
    dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj

If the clean computer does not have the required toolchain, use the project flash
or setup scripts documented in docs/testing-and-installation.md rather than
assuming Git, .NET, Arduino CLI, board packages, or libraries are preinstalled.

At minimum, add these named scenarios:

- TerminalIdentityProtocolTests.ParsesValidIdentityAndRejectsInvalidFields
- TerminalIdentityProtocolTests.ClaimAndAuthProofsMatchFixedVectors
- TerminalSessionTests.DoesNotExposeApplicationSessionBeforeAuthOk
- TerminalSessionTests.DisconnectsWhenObservedIdentityDiffersFromExpected
- TerminalSessionTests.ReplayedNonceCannotAuthenticate
- TerminalSessionTests.FailedClaimDoesNotPersistCredential
- DatabaseMigrationTests.Migration4PreservesLegacyTrips
- TripRepositoryTests.SameTripIdFromTwoTerminalsDoesNotCollide
- TripRepositoryTests.CursorsAreIndependentPerTerminal
- SyncSessionTests.StoresTripsUnderAuthenticatedTerminalIdentity
- FindTerminalsViewModelTests.MultipleResultsRequireExplicitSelection
- TerminalSettingsViewModelTests.RenamePersistsOnlyAfterTerminalAck
- firmware tests for unauthorized command denial, second-client rejection,
  authentication timeout, claim timeout, owner persistence, and USB reset scope.

Tests must assert absence of side effects, not only error text. For every rejected
command, verify trip cursors, active passes, clock, settings, and owner state are
unchanged as applicable.

---

## 15. Human Verification Matrix

| Capability | Mac sufficient? | Windows required? | Exact platform/hardware capability |
| --- | --- | --- | --- |
| Core protocol, crypto, migration, repository, and ViewModel tests | **Yes** | No | Platform-neutral .NET behavior |
| macOS discovery, Keychain persistence, bonding prompt, reconnect, and CoreBluetooth identifier change handling | **Yes, for macOS only** | No | macOS CoreBluetooth and Keychain |
| Windows discovery, DPAPI persistence, bonding prompt, address-type fallback, and reconnect | No | **Yes** | WinRT BLE GATT, Windows Bluetooth security UI, and DPAPI |
| One-central rejection, MITM-protected characteristic access, claim/auth, timeout, reconnect, and bond retention | No | **Yes** | Physical ESP32 plus a BLE-capable Windows PC; repeat with a second PC or phone as competing central |
| Two nearby terminals with identical original firmware names and overlapping trip IDs | No | **Yes** | Two physical ESP32 kiosks and one Windows PC |
| USB owner reset preserving LittleFS history and settings | Mac script can be checked | **Yes for Windows script** | Physical ESP32 USB serial on each platform |

Mac-only automated testing is sufficient for the platform-neutral core. It is
not sufficient to release this feature. A Windows PC is required specifically to
verify WinRT discovery/connection behavior, Windows bonding UI, DPAPI credential
recovery, BLE address-type fallback, and the Windows USB reset script. Physical
ESP32 hardware is required for security, bonding, competing-client, and retained-
state behavior. Windows behavior is not considered verified until every required
Windows row above has been run and recorded.

---

## 16. Definition of Done

The feature is complete only when all statements are true:

- two terminals can each store trip ID 1 without collision;
- each terminal resumes from its own durable cursor;
- the desktop verifies stable identity before sending any application command;
- a saved profile cannot silently operate a different terminal;
- an unowned terminal requires its physically displayed claim key;
- a claimed terminal accepts only its owner and rejects legacy clients;
- a second central cannot become an authorized session;
- replayed or expired proofs fail without side effects;
- ownership reset requires USB and preserves classroom data;
- terminal rename is implemented end-to-end or removed from shipped UI/docs;
- no credential appears in SQLite, logs, exports, screenshots, or test output;
- all automated tests pass;
- required Windows and two-terminal hardware checks are recorded;
- current docs and Wiki mirrors describe exactly the shipped behavior.
