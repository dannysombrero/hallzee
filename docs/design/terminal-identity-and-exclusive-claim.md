# Feature Design: Stable Terminal Identity and Exclusive Client Claim

## Discovery and physical identification

Bluetooth names are stable across pairing, reconnect, and active passes:
`Hallzee-XXXX` by default or `[name] [XXXX]` for a custom name. `XXXX` is the
last four hex characters of the ESP32 eFuse-derived terminal ID. No ownership
or occupancy suffix is added to the name. Custom prefixes have 22 characters
within the 29-byte name budget, preserving the space and `[XXXX]` suffix. The
full custom name remains unchanged in storage, settings, and normal LCD use.
The full authenticated `HZ-XXXXXXXXXXXX` ID is authoritative; names and suffixes
are for physical matching, never authentication.

The service UUID is advertised separately from the name-bearing scan response.
Windows retains known scan-response information across service-only packets;
macOS prefers the advertised local name over the OS cache. A connected rename
updates the local name and is advertised after disconnect.

Pairing can begin from date/time setup using the five-second `*` + `#` chord.
The terminal displays its full ID, name, and six-digit application pairing code.
Release the keys; expiry resumes the same setup step without editing the clock.
Select the terminal first, then enter that code in Hallzee's pairing dialog.
The app performs a fresh identity handshake after the user finishes typing so
an expired pre-entry nonce cannot cause the claim to fail.

### Ownership labels belong in Hallzee

The client displays **Currently Paired**, **Not Paired**, or **Paired to other
device** when ownership is known. Native discovery reads manufacturer company
`0xFFFF` with six payload bytes: ASCII `HZ`, version byte `0x01`, flags (bit 0 means
claimed), then the hardware suffix high/low bytes. This fits with the service
UUID and flags in the 31-byte primary advertisement; the stable name remains
in the scan response. No owner ID or credential is advertised.

Native **Currently Paired** requires a saved transport ID mapped to the full
terminal ID and owner credential, with a consistent suffix. A suffix alone is
never ownership evidence. Missing/legacy metadata shows **Unable to check
pairing** until connection; scans do not connect to every nearby terminal.
Active-pass availability is separate: the owner may reconnect while a pass is
active. All discovery labels are advisory until HELLO verifies the full ID and
AUTH proves ownership.

The browser's chooser cannot be replaced with an arbitrary nearby-device list:
`requestDevice()` requires a user action, and `getDevices()` returns devices
already granted to that origin. Thus the web client checks a newly selected
terminal before displaying its ownership; it cannot pre-label all unknown
nearby devices. It must not invent devices or RSSI. A browser-provided **Paired**
indicator is separate OS UI and cannot be removed by changing Hallzee's name.
See [Chrome's Web Bluetooth guide](https://developer.chrome.com/docs/capabilities/bluetooth)
and the [granted-device API](https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetooth-getdevices).

**Status:** Implemented; physical multi-device/platform verification remains

**Target milestone:** Delivered; verification gate remains

**Scope:** Stable kiosk identity, one-owner authorization, safe multi-terminal storage, profile association, discovery UX, recovery, and platform verification

**Supersedes:** Any assumption that a BLE address, the advertised name Hallzee, or a process-local connection is sufficient terminal identity or authorization

**Implementation checkpoint (2026-09-03):** The v2 protocol/security primitives,
terminal-scoped SQLite migration, authenticated session boundary, firmware
identity/claim state, Secure Connections characteristic permissions, one-active-
central enforcement, physical claim gesture, and a single-terminal Universal
central enforcement, physical claim gesture, six-digit passkey claim flow,
availability reporting, same-client automatic reconnect, and a single-terminal
Universal client v2 path are implemented and compile validated. The physical
keypad owner-reset gesture and USB owner-reset command are implemented. Desktop
platform credential-vault adapters are wired for Windows and macOS. Terminal
rename is wired end-to-end, and Windows/macOS discovery surfaces RSSI when the
platform provides it. Physical multi-terminal and platform reconnect
verification remain. Unclaimed startup, pairing,
and owner reset clear stale terminal-side BLE bonds.
The Universal unit-test path uses an in-memory credential store; physical
clients use the platform credential-store factory. Cross-restart credential and
reconnect behavior still requires physical platform verification.

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
5. A teacher workspace selects one terminal. Class periods are schedule contexts
   inside that workspace, so they do not require separate profiles or terminal
   assignments.
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

## 2. Current State and Remaining Verification

The v2 path now uses stable firmware identity, an exclusive owner claim,
authenticated commands, terminal-scoped trip keys/cursors, teacher-workspace
assignment, persisted custom names, automatic reconnect to only the assigned
terminal, and RSSI-backed discovery labels. A shared operation coordinator
serializes sync, settings, manual actions, and policy transfers.

The remaining work in this design is physical validation: two nearby kiosks,
credential-vault reuse across a real app restart on both macOS and Windows,
radio-address rotation recovery, and long-running connection stability. These
cannot be proven by the cross-platform unit-test transports.

---

## 3. Definitions and Invariants

| Term | Meaning |
| --- | --- |
| transport_id | Windows BLE address or macOS CoreBluetooth UUID used to open the current radio connection. It may change and is not trusted as identity. |
| terminal_id | Stable firmware identity in the form HZ- plus the ESP32 eFuse MAC as 12 uppercase hexadecimal digits, for example HZ-A1B2C3D4E5F6. It is not a secret. |
| terminal_suffix | Last four characters of terminal_id, used in the advertised name and physical matching UX. |
| availability | `AVAILABLE` when no student checkout is active; `IN_USE` when the terminal has an active checkout. This is advisory in discovery and authoritative in the v2 identity response. |
| client_id | Random UUID generated once per desktop installation and stored locally. It identifies the claimant but is not itself a secret. |
| pairing_passkey | Random six-digit value displayed only on the physical kiosk while claim mode is active. It is used once to derive the owner key; it is not stored as the owner credential. |
| owner_key | 256-bit key derived during claim and stored by the terminal and in the desktop OS credential vault. It is never transmitted. |
| owner | The one desktop installation whose client_id and owner_key match the terminal's persisted claim. |
| associated teacher workspace | A local teacher workspace configured to use a terminal. Association does not grant ownership. |

The implementation must preserve these invariants:

- exactly zero or one owner record exists on a terminal;
- a claimed terminal fails closed when authentication is absent or invalid;
- only the authenticated session may issue application commands;
- a second BLE central is rejected while another connection is active;
- an active checkout makes the terminal unavailable for desktop connection;
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

Physical access to an unoccupied terminal is the recovery boundary. Losing the
owner desktop requires an ownership reset; it must not require deleting LittleFS
trip history. The reset can be initiated through the USB diagnostic channel or
by holding `*` and `#` for 10 seconds on the physical keypad. The keypad path is
available only when no checkout is active and requires physical possession of
the terminal.

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

1. **BLE link security:** use LE Secure Connections Just Works, bonding, and
   encrypted GATT permissions. No OS passkey is required, although the operating
   system may request pairing permission. Just Works does not authenticate the
   initial link against an active man-in-the-middle. Only one BLE central is
   admitted at a time. A Bluetooth bond by itself grants no Hallzee ownership.
2. **Application ownership:** use a challenge-response proof tied to the stable
   terminal_id and client_id. BLE transport identity alone is never accepted as
   ownership.

Do not implement new cryptographic primitives. Use ESP32/mbedTLS HMAC-SHA-256 and
HKDF-SHA-256 on firmware and System.Security.Cryptography on desktop.

**Security change (2026-09-12):** the single app-code workflow replaces the old
MITM-protected Bluetooth passkey ceremony. The v2 six-digit HMAC/HKDF protocol
is not a PAKE: an active intermediary that captures the initial claim transcript
can guess the code offline and derive the permanent owner key. Application
proofs, a short claim window, and online retry limits do not restore BLE MITM
protection. Saved-owner authentication and command gates remain, but do not claim
this first-pairing flow has equivalent security to authenticated BLE pairing.
See the [protocol security discussion](../bluetooth-protocol.md#ble-transport)
and [Bluetooth SIG Security Manager specification](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core_v6.3/out/en/host/security-manager-specification.html).

### 6.1 Physical claim mode

- Claim mode is available only when the terminal has no owner.
- From the idle screen, holding * and # for five seconds starts a two-minute
  claim window. The existing two-second active-pass reset behavior remains
  unchanged when a pass is active.
- The kiosk displays PAIR Hallzee-XXXX and one random six-digit pairing passkey.
- The six-digit value is entered only in Hallzee after terminal selection.
  It is an application claim code, not an OS Bluetooth passkey. Windows and
  macOS establish encrypted Just Works bonding; no client forwards these digits
  into an OS pairing ceremony. The client may disconnect its identity probe
  while displaying the dialog, then use a fresh HELLO nonce to claim.
- The passkey is held in RAM only and regenerated whenever claim mode restarts.
- Claim mode closes immediately after a successful claim or when the timer
  expires. Expiration clears the key and any pending nonce.
- Once claimed, the keypad cannot open claim mode and no BLE command can replace
  the owner.

### 6.2 Key derivation

Both sides derive the same owner key after validating the physical claim proof:

    owner_key = HKDF-SHA256(
      input_key_material = UTF8(pairing_passkey),
      salt = UTF8(terminal_id),
      info = UTF8("Hallzee owner v2|" + client_id),
      output_length = 32 bytes
    )

Firmware persists owner_client_id and owner_key in an atomic LittleFS owner
record, with one-time migration from the legacy Preferences namespace. The
desktop stores owner_key in an OS-protected credential vault, keyed by
terminal_id; SQLite stores only non-secret metadata.

### 6.3 Proof construction

All nonces are 16 random bytes encoded as 32 uppercase hexadecimal characters.
All HMAC values are 32 bytes encoded as 64 uppercase hexadecimal characters.
Fields are ASCII and joined exactly with |; no locale-sensitive formatting is
allowed.

    claim_proof = HMAC-SHA256(
      key = UTF8(pairing_passkey),
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

Current firmware does not map a BLE peer bond to the application owner or prune
all non-owner bonds on CLAIM_COMMIT. It admits one active central, permits public
HELLO identity inspection over encrypted GATT, and gates records/settings on the
application owner proof. Failed/expired application sessions are cleared and
unauthenticated sessions time out. Bond cleanup occurs on explicit owner
release/reset and Bluetooth repair. Normal startup and physical pairing-mode
entry preserve bonds, including those just created by discovery probes.
BLE address resolution remains the stack's job; the full terminal ID and
application proof determine ownership, not the existence of a Bluetooth bond.

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

    IDENTITY,2,<terminal_id>,<terminal_suffix>,<UNCLAIMED|CLAIMED>,<AVAILABLE|IN_USE>,<nonce>

The response intentionally does not assert that the caller is the owner. The
caller proves that separately with AUTH. `IN_USE` is included so the client can
fail closed if the kiosk became occupied after discovery.

### 7.3 First claim

    Desktop  -> HELLO,2,<client_id>
    Terminal -> IDENTITY,2,<terminal_id>,<suffix>,UNCLAIMED,AVAILABLE,<nonce>
    Desktop  -> CLAIM,2,<client_id>,<HMAC proof derived from pairing_passkey>
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
    Terminal -> IDENTITY,2,<terminal_id>,<suffix>,CLAIMED,AVAILABLE,<nonce>
    Desktop  -> AUTH,2,<client_id>,<auth_proof>
    Terminal -> AUTH_OK,2,<terminal_id>,<custom_name>

Errors are explicit and terminal state is unchanged:

| Response | Meaning |
| --- | --- |
| ERROR,PAIRING_MODE_REQUIRED | Terminal is unclaimed but its physical claim window is closed. |
| ERROR,ALREADY_CLAIMED | A claim was attempted against an owned terminal. |
| ERROR,OWNER_MISMATCH | client_id differs from the persisted owner. |
| ERROR,AUTH_FAILED_CLAIM | Initial claim proof is malformed or invalid. |
| ERROR,AUTH_FAILED_CLAIM_COMMIT | Final claim commit proof is malformed or invalid. |
| ERROR,AUTH_FAILED_CLAIM_COMMIT_<reason> | Final claim commit failed with a diagnostic reason: STATE, NONCE, CLIENT, PROOF_FORMAT, PROOF, STORAGE_CLIENT, or STORAGE_KEY. |
| ERROR,AUTH_FAILED_AUTH | Reconnection proof is malformed or invalid. |
| ERROR,AUTH_TIMEOUT | Authorization was not completed within 10 seconds. |
| ERROR,UPGRADE_REQUIRED | A claimed v2 terminal received legacy protocol. |

### 7.5 Authorized commands

After AUTH_OK, existing commands retain their meanings. Add:

| Command | Response | Meaning |
| --- | --- | --- |
| RELEASE_OWNER | OWNER_RELEASED | Authenticated owner releases pairing while no passes are active; terminal disconnects and clears BLE bonds after acknowledging. |
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
  - eFuse-based identity and custom-name persistence, with owner metadata in an
    atomic LittleFS record and legacy Preferences migration.
- TerminalSecurity.h/.cpp
  - claim window, nonce lifecycle, proof verification, HKDF derivation,
    authorization state, failure counter, and timeout;
  - mbedTLS only; constant-time comparison.

Change these existing files:

- ArduinoBluetoothSerialPort.h/.cpp
  - configure Secure Connections Just Works, encryption, and bonding;
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
- firmware/terminal/bathroom-signin.ino
  - instantiate identity/security services;
  - wire pairing callback and display lifecycle;
  - ensure offline checkout remains available when claim mode is closed.
- Config.h
  - add named timeout/length constants; do not add a static owner secret.

Before implementation, pin and document the supported ESP32 Arduino board
package version. Security APIs differ between major versions; do not silently
switch BLE libraries in this feature. If the current library cannot enforce
encrypted characteristics, stop after the compile spike and
record the required library migration as a separate prerequisite.

---

## 11. Ownership Recovery

Ownership reset is physical-only. The preferred recovery path when serial access
is available is:

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

When serial access is unavailable, the same owner-only state can be cleared from
the keypad. With no active checkout, hold `*` and `#` together for 10 seconds.
The firmware clears the owner record, disconnects the current Bluetooth client,
shows **OWNER RESET**, and returns to idle. Holding the same keys for five
seconds afterward starts the normal unclaimed pairing mode. This path preserves
trips, settings, the terminal name, and the stable terminal ID. It must not be
available while a student is checked out.

The authenticated BLE `RELEASE_OWNER` command is exposed by **Settings → Device
→ Disconnect & Unpair**. It rejects active passes, clears owner state, and sends
`OWNER_RELEASED` before a delayed disconnect and terminal bond cleanup (up to
500 ms). Only after that response does the desktop delete its owner credential,
remove every workspace assignment to this terminal, and mark its retained
terminal row unclaimed without deleting trips. Failed/unconfirmed requests keep
the local credential. Ordinary disconnects retain ownership. OS-side Bluetooth
entries can remain and may need Forget/Remove before a fresh claim.

Device settings shows connection status and the stable ID. Renaming starts via
a pencil icon with Save/Cancel controls; the client persists the new name only
after the matching `SETTINGS_ACK`. Applying the ID limit does not rename the
terminal or overwrite its saved claim/transport metadata.

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

Files: firmware/terminal/libraries.txt, setup scripts,
firmware/terminal/ArduinoBluetoothSerialPort.*, and temporary
compile-only test code if required.

- Record the exact ESP32 board package version installed by bootstrap scripts.
- Confirm the current BLE library exposes Secure Connections Just Works, bonding,
  encrypted characteristic permissions, bond removal, connection ID, and server
  disconnect APIs on the supported package. Peer-specific owner-bond retention
  is not implemented; do not report application ownership tests as proof of a
  bond-level owner whitelist.
- Compile on a clean bootstrap environment.
- Do not proceed if encrypted characteristics cannot be enforced; document
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

### Package 5 — Physical pairing and owner recovery

Files: keypad, display, composition root, reset scripts, and native tests.

- Add the idle/unclaimed five-second gesture and pairing screens.
- Preserve the occupied-pass reset gesture exactly.
- Add two-minute claim timeout and visual suffix matching.
- Add USB owner reset and both one-command scripts.
- Add the claimed, unoccupied 10-second keypad owner-reset gesture.
- Test that owner reset preserves trips/settings and that an occupied kiosk
  cannot clear ownership or reopen pairing mode from the keypad.

Exit criteria: automated gesture/state tests pass and either recovery path is
manually verified without erasing trip history.

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
| One-central rejection, encrypted characteristic access, claim/auth, timeout, reconnect, and bond retention | No | **Yes** | Physical ESP32 plus a BLE-capable Windows PC; repeat with a second PC or phone as competing central |
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

## 16. Owner Reconnect Feature Plan

### 16.1 Required user experience

After one successful physical claim, the owning desktop must be able to reconnect
without touching the terminal or entering the six-digit passkey. This applies
after a radio drop, app restart, computer restart, terminal restart, sleep/wake,
and temporary out-of-range condition.

The normal owner flow is:

1. Load the last assigned stable terminal ID and owner credential at startup.
2. Scan for Hallzee advertisements without placing the terminal in pairing mode.
3. Match candidates using the saved transport hint and advertised terminal
   suffix, but trust neither value until `IDENTITY` is received.
4. Open GATT, subscribe to notifications, send `HELLO,2,<client_id>`, and require
   the expected stable terminal ID.
5. If the terminal reports `CLAIMED`, send `AUTH` with the stored owner key.
6. After `AUTH_OK`, sync time, active-pass state, settings, and trips normally.

The UI reconnects automatically to the assigned authenticated terminal after
startup or an unexpected disconnect. It retries for 45 seconds and then leaves
manual **Reconnect** and **Find Terminal** recovery actions. There is no setting
that disables automatic reconnect.

Pairing mode and the six-digit passkey are used only for an unclaimed terminal
or after a deliberate physical owner reset. A non-owner desktop may discover a
claimed terminal but cannot authenticate, sync, rename, change settings, or
replace ownership.

### 16.2 Historical gaps (resolved)

The following list records the gaps that motivated packages R1–R7. They are
retained as design history and are implemented; physical platform verification
is the outstanding acceptance step.

- `MainViewModel` constructs `InMemoryTerminalCredentialStore` in production.
  The owner key therefore disappears whenever the app exits.
- `lastAuthenticatedDevice` and `lastAuthenticatedTerminalId` are memory-only.
- reconnect reuses the old `TerminalDevice` transport ID instead of scanning
  and refreshing the transport hint first;
- Windows immediately retries the opposite address type after a failed open,
  but does not perform a bounded GATT-ready retry for `Unavailable` or
  `Unreachable` results;
- discovery calls `DisconnectAsync`, so discovery and reconnect ownership need
  one coordinator to prevent races;
- the UI has no explicit startup “Reconnect with this device?” state;
- an OS Bluetooth bond failure is not distinguished from a missing Hallzee owner
  credential or an owner-authentication rejection.

### 16.3 Package R1 — Durable owner credential stores

Files:

- `receiver/windows/BathroomSync.Core/TerminalCredentialStore.cs`
- new platform implementations under `receiver/universal/Services/`
- `receiver/universal/App.axaml.cs`
- `receiver/universal/ViewModels/MainViewModel.cs`
- credential-store unit tests under `receiver/universal.tests/`

Tasks:

1. Keep `ITerminalCredentialStore` in the platform-neutral core.
2. Add `WindowsDpapiTerminalCredentialStore`. Store one encrypted 32-byte owner
   key per normalized terminal ID under the Hallzee application-data directory.
   Protect with Windows DPAPI `CurrentUser`; do not write plaintext keys to
   SQLite, logs, exceptions, or filenames.
3. Add `MacKeychainTerminalCredentialStore`. Use a generic-password Keychain
   item with service `com.hallzee.desktop.owner`, account equal to the normalized
   terminal ID, and value equal to the 32-byte owner key.
4. Keep `InMemoryTerminalCredentialStore` only for preview mode and tests.
5. Select the platform store in the composition root and inject it into
   `TerminalSession`; do not let `MainViewModel` instantiate the in-memory store
   for a real build.
6. Make save replace an existing credential atomically. Make delete idempotent.
   Return copies from reads and clear temporary byte arrays after use.
7. If secure storage is unavailable, fail the claim before `CLAIM_COMMIT` and
   send `CLAIM_ABORT`; never leave the terminal claimed without a durable desktop
   credential.

Tests:

- save/read/delete and overwrite by normalized terminal ID;
- app/service recreation can still read a saved credential;
- corrupt or wrong-user ciphertext fails closed;
- claim aborts when credential persistence fails;
- no test log or exception contains the key.

Exit criteria: close and reopen the app, then authenticate to the already
claimed terminal without a passkey.

### 16.4 Package R2 — Persisted reconnect target

Files:

- `receiver/windows/BathroomSync.Core/Storage/ProfileAndPolicySqliteRepository.cs`
- `receiver/windows/BathroomSync.Core/Storage/DatabaseMigrator.cs`
- repository tests in `receiver/windows/BathroomSync.Tests/`
- `receiver/universal/ViewModels/MainViewModel.cs`

Tasks:

1. Use the existing `terminals`, `client_identity`, and
   `profile_terminal_assignments` data as the reconnect registry. Add a migration
   only if a required field is absent; do not create a second source of truth.
2. Persist stable terminal ID, custom name, latest transport hint, last-seen UTC,
   protocol version, claim status, and active-profile assignment only after
   `AUTH_OK`.
3. Add a repository query returning the active profile's assigned terminal plus
   its transport hint. Return null when no assignment exists.
4. On startup, expose a reconnect candidate only when both an assignment and a
   secure owner credential exist.
5. When a verified reconnect observes a new transport ID, update only the
   transport hint. Never change the assigned stable terminal ID from discovery
   data.
6. Remove the assignment or mark it recovery-required when the user explicitly
   forgets the terminal; do not do so for ordinary connection failures.

Tests:

- target survives repository recreation;
- changed transport hint retains the same stable ID;
- no credential means no owner-reconnect offer;
- identity mismatch leaves assignment and credential unchanged but blocks sync.

Exit criteria: app restart reconstructs the same reconnect target without using
`lastAuthenticatedDevice` memory.

### 16.5 Package R3 — Reconnect coordinator and state machine

Files:

- new `receiver/universal/Services/TerminalReconnectCoordinator.cs`
- `receiver/windows/BathroomSync.Core/TerminalSession.cs`
- `receiver/universal/ViewModels/MainViewModel.cs`
- `receiver/universal/ViewModels/Modals/FindTerminalsViewModel.cs`
- corresponding unit tests

Define these states: `Idle`, `CandidateAvailable`, `Scanning`, `OpeningGatt`,
`VerifyingIdentity`, `AuthenticatingOwner`, `Syncing`, `Connected`,
`RetryWaiting`, `PairingRequired`, `RecoveryRequired`, and `Cancelled`.

Tasks:

1. Move retry ownership out of `MainViewModel` into one coordinator. It must use
   one cancellation token and one semaphore so manual scan, automatic reconnect,
   and disconnect cannot run concurrently.
2. For every attempt, scan first. Prefer an advertisement matching the saved
   transport hint; otherwise consider Hallzee advertisements whose suffix matches
   the expected stable ID. Always verify the full ID through `IDENTITY`.
3. Call `TerminalSession.OpenAsync(candidate, expectedTerminalId)` and then
   `AuthenticateAsync()` with no passkey. `TerminalSession` retrieves the saved
   owner key internally.
4. Use bounded retries: 1, 2, 3, 5, 8, 13, then 15 seconds, with ±20% jitter.
   Continue while the terminal is absent, Windows reports transient GATT status,
   or the computer is waking. Reset the delay after any successful `AUTH_OK`.
5. Stop immediately on identity mismatch, missing credential, invalid owner
   proof, explicit user cancellation, or an unclaimed terminal. These require a
   visible user decision rather than background retries.
6. If the terminal reports `IN_USE`, keep the owner relationship but pause sync;
   retry periodically or when the user presses Reconnect. Do not request pairing.
7. After authentication, run time sync first, then active-pass retrieval, policy
   settings, and trip synchronization through the authorized session.
8. Do not clear credentials or terminal assignment on timeout, power loss,
   unavailable GATT service, or notification-subscription failure.

Tests:

- reconnect after connection loss uses no passkey command;
- app restart loads target and reaches `CandidateAvailable`;
- stale transport hint is replaced after full-ID verification;
- wrong terminal suffix/full ID never reaches `AUTH`;
- retry schedule is deterministic with an injected clock/random source;
- cancellation stops scan, pending delay, GATT open, and authentication;
- only one connection attempt runs at a time;
- transient failures retain owner credential and assignment.

Exit criteria: the same mocked owner reconnects after drop and process restart;
a different client ID/key cannot authenticate.

### 16.6 Package R4 — Windows BLE reconnect hardening

Files:

- `receiver/windows/BluetoothConnectionManager.cs`
- Windows-specific connection tests/fakes where WinRT can be abstracted

Tasks:

1. Never feed the application code to custom OS pairing. Owner reconnect
   reuses the Windows bond; `UnpairAsync` belongs only to explicit bond repair.
2. Dispose the old characteristics, service, and `BluetoothLEDevice` before each
   retry and detach every event handler exactly once.
3. After advertisement discovery, open the reported address type first. Try the
   alternate type only for an address/open failure, not after an identity or auth
   failure.
4. Retry uncached service discovery for transient `Unavailable` and
   `Unreachable` responses for up to 15 seconds. Reopen the device between retry
   groups because WinRT can retain a stale GATT object after disconnect.
5. Request service access, rediscover TX/RX characteristics uncached, set required
   protection, and rewrite the CCCD on every connection.
6. Classify failures as `DeviceNotFound`, `GattNotReady`, `BondRepairRequired`,
   `NotificationSubscribeFailed`, or `AccessDenied`; expose the category to the
   reconnect coordinator without embedding platform text in business logic.
7. `BondRepairRequired` must present a recovery action. It must not silently
   unpair, request the claim passkey, or delete the Hallzee owner credential.

Windows tests/manual checks:

- disconnect/reconnect while terminal remains powered;
- terminal power cycle;
- Windows sleep/wake;
- app and PC restart;
- stale GATT cache and Random/Public address fallback;
- deliberately remove the Windows bond and verify recovery is offered rather
  than ownership being silently replaced.

Exit criteria: the owner's Windows PC reconnects without pairing UI or passkey;
the current “service unavailable / could not open Random device” scenario is
recovered or classified as bond repair.

### 16.7 Package R5 — macOS reconnect hardening

Files:

- `receiver/MacBLEAgent/Program.cs`
- `receiver/universal/Services/MacAgentTerminalConnection.cs`
- Mac adapter tests where feasible

Tasks:

1. Persist the last CoreBluetooth peripheral UUID as a hint, but fall back to a
   fresh service-UUID scan when retrieval/open fails.
2. Recreate notification subscription on every reconnect and do not report
   connected until notifications are active.
3. Map “Peer removed pairing information” to `BondRepairRequired`; do not delete
   the Hallzee owner key.
4. Forward disconnect reason and write/subscription completion exactly once.
5. After a Mac bond repair, continue with normal owner `AUTH`; do not require the
   terminal's claim mode unless the terminal itself reports `UNCLAIMED`.

Exit criteria: Mac reconnect works after app restart, terminal restart, and
CoreBluetooth identifier refresh without re-entering the claim passkey.

### 16.8 Package R6 — Firmware owner availability

Files:

- `ArduinoBluetoothSerialPort.*`
- `BluetoothSync.*`
- `TerminalSecurity.*`
- `firmware/terminal/bathroom-signin.ino`
- native firmware tests

Tasks:

1. A claimed terminal must advertise and accept `HELLO`/`AUTH` whenever powered
   and not occupied, regardless of claim-mode state or the clock-setup screen.
2. Retain the owner record and BLE bond across disconnect and reboot.
3. Clear bonds only during explicit owner reset/release or a documented
   bond-repair operation. Preserve bonds during ordinary boot, disconnect,
   and application pairing-mode entry.
4. Continue rejecting `CLAIM` while claimed. Reassignment requires the physical
   10-second owner reset or authenticated **Disconnect & Unpair** while unoccupied.
5. Keep the 10-second unauthenticated-session timeout, but restart it for each
   new GATT connection and allow immediate subsequent owner reconnect attempts.
6. Ensure `AUTH_OK` always includes a valid non-empty custom/advertised name.

Tests:

- claimed reboot retains owner and accepts valid `AUTH` outside pairing mode;
- disconnect does not clear owner or bond;
- invalid owner cannot run application commands;
- clock-setup UI does not block BLE authentication;
- occupied terminal reports `IN_USE` without reopening claim mode;
- physical owner reset clears only owner/bond state and preserves trips/settings.

Exit criteria: a wall-mounted terminal can be power-cycled and remotely
reconnected by its owner without keypad interaction.

### 16.9 Package R7 — Reconnect UI

Files:

- `receiver/universal/MainWindow.axaml`
- `receiver/universal/ViewModels/MainViewModel.cs`
- `receiver/universal/Views/Modals/FindTerminalsModalView.axaml`
- `receiver/universal/ViewModels/Modals/FindTerminalsViewModel.cs`
- UI/ViewModel tests

Tasks:

1. Add a startup banner/card: **Previously connected terminal found**,
   terminal name/suffix, **Reconnect**, and **Choose another terminal**.
2. During live-drop retry, show `Reconnecting…`, attempt number, and **Cancel**.
   Keep the rest of the app usable for viewing local history.
3. Do not show the six-digit field for an owner reconnect.
4. For a claimed non-owner terminal, show **Claimed by another computer** and
   disable Connect. Explain that physical owner reset is required.
5. For `BondRepairRequired`, show platform-specific repair instructions and a
   **Repair Bluetooth connection** action. Make clear that Hallzee ownership is
   retained.
6. If the terminal is unclaimed, route to the existing physical pairing flow.
7. On success, close the reconnect prompt, refresh the terminal name/status, and
   start authorized synchronization.

Exit criteria: a teacher can recover from an ordinary disconnect with one click
and no terminal interaction; routine automatic retries require no click.

### 16.10 Implementation order and review boundaries

Implement in this order: R1, R2, R3, R4 and R5 in parallel, R6, then R7. Each
package should be a reviewable commit with passing tests. Do not enable the
startup reconnect prompt until R1–R3 are complete. Do not call Windows reconnect
verified until R4's physical matrix passes.

Before handoff, run:

    make -C test test
    dotnet test receiver/windows/BathroomSync.Tests/BathroomSync.Tests.csproj
    dotnet test receiver/universal.tests/BathroomSync.Universal.Tests.csproj
    dotnet build receiver/MacBLEAgent/BathroomSync.MacBLEAgent.csproj

Mac testing is sufficient for the platform-neutral coordinator and macOS path.
A Windows PC is required to validate WinRT device reopening, uncached GATT
rediscovery, CCCD resubscription, Random/Public address handling, DPAPI, and bond
repair. Windows behavior remains unverified until those checks are recorded.

---

## 17. Definition of Done

The feature is complete only when all statements are true:

- two terminals can each store trip ID 1 without collision;
- each terminal resumes from its own durable cursor;
- the desktop verifies stable identity before sending any application command;
- a saved profile cannot silently operate a different terminal;
- an unowned terminal requires its physically displayed six-digit pairing passkey;
- a claimed terminal accepts only its owner and rejects legacy clients;
- an unexpected drop retries only the last authenticated terminal with the
  existing owner credential; it stops when that terminal reports `IN_USE` or
  an identity mismatch;
- a second central cannot become an authorized session;
- replayed or expired proofs fail without side effects;
- ownership reset requires physical access and preserves classroom data;
- terminal rename is implemented end-to-end or removed from shipped UI/docs;
- no credential appears in SQLite, logs, exports, screenshots, or test output;
- all automated tests pass;
- required Windows and two-terminal hardware checks are recorded;
- current docs and Wiki mirrors describe exactly the shipped behavior.
