# Bluetooth sync protocol

## BLE transport

Hallzee advertises a Bluetooth Low Energy GATT service under `Hallzee-XXXX`,
where `XXXX` is the last four characters of the stable eFuse-derived terminal
ID. Names do not change when ownership or occupancy changes. Custom names retain
that hardware suffix as `[name] [XXXX]`. Protocol v2 uses LE Secure Connections
Just Works with bonding and encrypted GATT reads/writes, without an OS passkey.
The six-digit code shown during physical claim mode is entered only in Hallzee
to create the one-time claim proof and derive a 32-byte owner credential.
Returning authentication uses that credential, never the six-digit code.

Just Works encrypts the link but does not authenticate it against an active
man-in-the-middle during pairing; see the [Bluetooth SIG security specification](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core_v6.3/out/en/host/security-manager-specification.html).
The existing v2 six-digit HMAC/HKDF claim is not a password-authenticated key
exchange (PAKE). An active intermediary that captures a claim or authentication
transcript on an intercepted link can try the finite code space offline and
derive the owner key. This also matters when replacing a lost BLE bond. Physical
claim mode and retry limits do not remove that risk. This changes the initial-claim
security guarantee from the former MITM-protected BLE design; it does not provide
equivalent MITM protection through the app code. Saved-owner challenge-response
and command authorization remain enforced.

Native ownership discovery uses manufacturer company `0xFFFF` with six payload
bytes: ASCII `H`, ASCII `Z`, version byte `0x01`, flags (bit 0 = claimed), suffix high byte,
and suffix low byte. The name is carried separately in the scan response. This
is an unauthenticated hint: a saved exact transport-ID/full-terminal-ID/key match
is needed to label **Currently Paired**, and HELLO/AUTH remain authoritative.
Web Bluetooth cannot inspect every ungranted chooser device; it checks identity
after explicit selection and labels only real saved/selected devices. Saved web
rows show **Status unknown** until the live identity check; a saved credential
or Chrome's chooser badge alone cannot confirm current ownership.

Web transport deadlines do not cancel native browser promises. GATT operations
remain serialized until those promises settle, including late-connect cleanup,
even after cancellation or disconnect. New connects wait up to ten seconds for
old work to drain, then allow an 800 ms disconnect cooldown. Briefly busy setup
operations retry at most twice, 800 ms apart; command writes are not replayed.
An unsettled old operation pauses retries with a specific diagnostic.

| Role | UUID |
| --- | --- |
| Sync service | `005924a2-c6e5-4340-9bb8-22d9dd37a283` |
| Terminal → client notifications | `44a359f3-9215-4189-a3cb-e7ce18ad40d6` |
| Client → terminal writes | `e80f9559-49eb-47bc-af04-8e92e98ced56` |

The client enables notifications before sending commands. Commands and messages
remain UTF-8, newline-delimited text. Both physical clients divide writes into
20-byte chunks and use acknowledged writes for the encrypted RX
characteristic. The firmware buffers fragments until a newline arrives.
On the supported ESP32 Bluedroid transport, TX reads remain empty and exist only
to establish link encryption; notifications target the admitted connection.
Firmware clears application authentication and partial frames on every link
change, including a disconnect/reconnect occurring between main-loop polls,
before allowing output on the new connection.

## Normal incremental session

```text
Client enables notifications
Client -> HELLO,2,<client_id>
Terminal -> IDENTITY,2,<terminal_id>,<suffix>,<UNCLAIMED|CLAIMED>,<AVAILABLE|IN_USE>,<nonce>
Client -> AUTH,2,<client_id>,<proof>
Terminal -> AUTH_OK,2,<terminal_id>,<custom_name>
Client -> GET_SETTINGS
Terminal -> SETTINGS,MAX_ID_LENGTH,<value>
Client -> TIME_CURSOR,YYYY-MM-DD,HH:MM:SS,<last_durable_trip_id>
Terminal -> TIME_ACK,OK
Terminal -> SYNC_BEGIN,<count_after_cursor>
Terminal -> TRIP,...                 (one at a time)
Client -> ACK,<trip_id>              (only after SQLite commit)
... repeat ...
Terminal -> SYNC_END
```

While the BLE connection remains open, a completed or manually reset pass is
also delivered immediately as `LIVE_TRIP,<record>`. The desktop stores this
record without an ACK; a later cursor sync safely retransmits it if the live
notification was missed.

The client must not send application commands until `AUTH_OK`. An unclaimed
terminal requires the physical claim flow (`CLAIM` followed by
`CLAIM_COMMIT`) before it can be used. The physical passkey is valid only
while the terminal is unclaimed, unoccupied, and inside its physical claim
window. `IN_USE` prevents a new claim but still permits the saved owner to
authenticate and check in active passes. A claimed terminal rejects a different
client installation, and the terminal disconnects an additional BLE central
while another central is active. The client cursor is the largest trip ID
durably stored in SQLite. A normal sync therefore transfers only newer records
and does not replay the full history.

The terminal advances a cursor stream only after the matching ACK. Cursor ACKs
do not rewrite the terminal's complete flash log. If the connection drops
before an ACK arrives, that record is sent again; the client primary key makes
the retry a safe duplicate and ACKs it again.

## Commands sent to the terminal

| Command | Meaning |
| --- | --- |
| `HELLO,2,<client_id>` | Start the identity handshake |
| `CLAIM,2,<client_id>,<proof>` | Claim an unowned, available terminal using an HMAC proof derived from the six-digit physical passkey |
| `CLAIM_COMMIT,2,<client_id>,<proof>` | Persist the pending claim after the desktop stores its credential |
| `CLAIM_ABORT,2,<client_id>` | Cancel a pending claim |
| `AUTH,2,<client_id>,<proof>` | Authenticate the persisted owner |
| `GET_SETTINGS` | Read all supported persisted kiosk settings |
| `GET_ACTIVE_PASS` | Query current active in-flight checkout pass status |
| `GET_ACTIVE_PASSES` | Query all current active checkouts |
| `MANUAL_CHECKIN` | Teacher check-in of the active pass; records the completed trip with status `MANUAL` |
| `SET,MAX_ID_LENGTH,<4-16>` | Persist the maximum accepted student-ID length |
| `SET,MAX_ACTIVE_PASSES,<1-8>` | Persist the maximum number of simultaneous active passes; the oldest active pass remains the client-visible pass |
| `RELEASE_OWNER` | Authenticated owner-only unpair; rejects active passes, preserves trips/settings/identity, and clears owner state and terminal BLE bonds |
| `SET,TERMINAL_NAME,<name>` | Persist and advertise a trimmed 1–24 printable ASCII character kiosk name; commas are not allowed |
| `POLICY_BEGIN,<0\|1>` | Begin an atomic offline bell-policy update; `0` disables terminal enforcement |
| `POLICY_WINDOW,<YYYYMMDD>,<start>,<end>,<first_end>,<last_start>,<first_action>,<last_action>` | Stage one resolved dated window; minutes are after midnight and action values are Allow=0, Warn=1, Lock=2 |
| `POLICY_COMMIT,<count>` | Commit the staged policy only when `count` matches the received window count |
| `TIME_CURSOR,...,<id>` | Set local time and send records with trip ID greater than `id` |
| `TIME,...` | Legacy compatibility: set time and send records whose terminal sync flag is unset |
| `SYNC_START` | Legacy compatibility: send records whose terminal sync flag is unset |
| `SYNC_ALL` | Explicit recovery: send all records from trip ID zero |
| `ACK,<trip_id>` | Confirm durable receipt of the pending trip |

Commands longer than 192 characters receive `ERROR,COMMAND_TOO_LONG`.
Carriage returns are ignored.

## Messages sent by the terminal

| Message | Meaning |
| --- | --- |
| `IDENTITY,2,<terminal_id>,<suffix>,<UNCLAIMED\|CLAIMED>,<AVAILABLE\|IN_USE>,<nonce>` | Stable terminal identity, availability, and fresh handshake challenge |
| `CLAIM_OK,2,<terminal_id>,<commit_nonce>` | Claim proof accepted; desktop may store its derived credential |
| `AUTH_OK,2,<terminal_id>,<custom_name>` | Ownership committed and application commands are authorized |
| `OWNER_RELEASED` | Owner release succeeded; the client may remove its saved owner credential and workspace assignments before disconnecting |
| `IDENTITY_INFO,2,<terminal_id>,<custom_name>` | Authenticated identity reread |
| `SETTINGS,MAX_ID_LENGTH,<value>` | Current persisted maximum student-ID length |
| `SETTINGS_ACK,MAX_ID_LENGTH,<value>` | Setting was saved successfully |
| `SETTINGS_ERROR,MAX_ID_LENGTH,<reason>` | Setting was rejected without changing the stored value |
| `SETTINGS_ACK,TERMINAL_NAME,<name>` | Kiosk name was persisted and applied to BLE advertising |
| `SETTINGS_ERROR,TERMINAL_NAME,INVALID_VALUE` | Name violates the 1–24 character ASCII name rules |
| `SETTINGS_ERROR,TERMINAL_NAME,STORAGE_FAILED` | Name storage could not be opened or written; previous name remains authoritative |
| `SETTINGS_ERROR,TERMINAL_NAME,BLE_UPDATE_FAILED` | Bluetooth could not apply the name; no new name was persisted |
| `POLICY_ACK,BEGIN` / `POLICY_ACK,WINDOW` / `POLICY_ACK,COMMIT,<count>` | Offline bell-policy transfer step succeeded |
| `POLICY_ERROR,<reason>` | Bell-policy transfer was rejected; the previous committed policy remains authoritative |
| `ACTIVE_PASS,<student_id>,<epoch>` | Active checkout student ID and unix epoch timestamp |
| `ACTIVE_PASS,NONE` | Pass is currently available (no active checkout) |
| `EVENT,CHECKOUT,<id>,<epoch>` | Real-time notification: student checked out |
| `EVENT,CHECKIN,<id>,<duration>` | Real-time notification: student checked back in |
| `EVENT,RESET,<id>,<duration>` | Real-time notification: pass manually reset |
| `TIME_ACK,OK` / `TIME_ACK,ERROR` | Result of a time or cursor command |
| `SYNC_BEGIN,<count>` | A sequence started with the expected record count |
| `TRIP,<record>` | Next record; the first CSV field is the trip ID |
| `LIVE_TRIP,<record>` | A newly completed record delivered while connected; it does not require an ACK |
| `MANUAL_CHECKIN_ERROR,NO_ACTIVE_PASS` | A manual check-in was requested when no pass was active |
| `SYNC_END` | No more records in the requested sequence |
| `ACK_ERROR,INVALID_ID` | ACK did not contain a numeric ID |
| `ACK_ERROR,UNEXPECTED_ID` | ACK did not match the pending trip |
| `ACK_ERROR,MARK_FAILED` | Legacy unsynced mode could not persist its sync flag |
| `ERROR,ACTIVE_PASS` | Owner release refused while a student pass is active |
| `ERROR,OWNER_RELEASE_FAILED` | Owner release could not be persisted |
| `ERROR,UNSUPPORTED_COMMAND` | Owner-release handler is unavailable |
| `ERROR,UNKNOWN_COMMAND` | Command was not recognized |
| `ERROR,AUTH_REQUIRED` | Application command arrived before authorization |
| `ERROR,AUTH_FAILED_CLAIM` | Initial claim proof was malformed or invalid |
| `ERROR,AUTH_FAILED_CLAIM_COMMIT` | Final claim commit proof was malformed or invalid |
| `ERROR,AUTH_FAILED_CLAIM_COMMIT_<reason>` | Final claim commit failed; reason is `STATE`, `NONCE`, `CLIENT`, `PROOF_FORMAT`, `PROOF`, `STORAGE_CLIENT`, or `STORAGE_KEY` |
| `ERROR,AUTH_FAILED_AUTH` | Reconnection proof was malformed or invalid |
| `ERROR,AUTH_TIMEOUT` | Authorization was not completed in time |
| `ERROR,PAIRING_MODE_REQUIRED` | An unclaimed terminal is not in its physical claim window |
| `ERROR,ALREADY_CLAIMED` | A claim was attempted against an owned terminal |
| `ERROR,TERMINAL_IN_USE` | The kiosk currently has an active checkout and cannot accept a new claim; its authenticated owner may still reconnect |
| `ERROR,UPGRADE_REQUIRED` | Legacy protocol is not accepted by the secured firmware |

## Owner release

`RELEASE_OWNER` passes the same authenticated-session gate as settings writes.
The firmware refuses it while any pass is active, clears owner authorization,
and sends `OWNER_RELEASED`. It allows up to 500 ms for the acknowledgement to
leave before disconnecting and clearing terminal-side BLE bonds (or cleans up
sooner if the client disconnects). The terminal ID, friendly name, settings, and
trip history remain. A new owner must enter physical pairing mode.

The desktop waits for that exact response before deleting its owner credential
and workspace assignments. On rejection, timeout, or connection failure, it
keeps the saved credential and reports that release was not confirmed. If the
ACK was lost after firmware released ownership, local and terminal state may
need recovery via a fresh physical claim. Ordinary connection loss/close keeps
ownership and supports reconnect. OS-side cached Bluetooth entries are not
removed by this protocol.

## Kiosk settings

Persisted kiosk settings include `MAX_ID_LENGTH`, terminal name, active-pass
capacity, and the optional dated bell-policy cache. The ID-length default is 10 and
its accepted range is 4–16. The lower bound preserves access to the four-digit
local administrator codes. The firmware rejects a shorter value while an
active checkout has a longer ID, returning `ACTIVE_ID_TOO_LONG`. Other error
reasons are `INVALID_VALUE` and `STORAGE_UNAVAILABLE`.

Terminal names are trimmed before validation. They accept 1–24 printable ASCII
characters except comma, which remains reserved as the protocol field separator.

Reading settings is safe during every sync. Writing is always an explicit
client action. Both settings commands and responses may exceed the default
20-byte ATT payload and therefore use the same newline buffering and chunking
as the rest of the protocol.

## Recovery and compatibility

`SYNC_ALL` is a deliberate repair operation, not part of each normal sync.
The desktop must still de-duplicate by trip ID. A restored or replaced client
database should run a full recovery once before returning to cursor mode.

On disconnect, both sides clear partial session state. The kiosk resumes BLE
advertising; discovery preserves an existing authenticated connection. A new
connection attempt discards stale GATT state before opening the selected terminal. If a terminal is replaced or factory-reset and
trip IDs restart, perform a full recovery into a new database rather than
assuming the old cursor belongs to the new device.

Normal startup, disconnect, and opening an application pairing-code window
preserve OS encryption bonds, including a bond created by a discovery probe.
Only explicit owner reset/release or BT REPAIR removes terminal-side bonds.
This prevents a newly displayed application code from invalidating the browser’s
existing encrypted connection. Application ownership still requires CLAIM/AUTH.


## Firmware update protocol (schema 1)

Firmware operations use the existing owner-authenticated encrypted GATT service.
`GET_FIRMWARE_INFO` returns:

```text
FIRMWARE_INFO,1,<version>,<build>,<variant>,<layout>,<slotBytes>,<otaSupported>,<CONFIRMED|PENDING|FAILED>,<bootstrap>
```

An unsupported older terminal returns its normal command error. The client
labels cached values until a fresh authenticated query succeeds.

`FW_BEGIN,<sessionHex>,<manifestBytes>,<signatureHex>` requires no active passes
and `ota-v1`. The nonzero session ID is eight hex digits; manifest size is at most
2048 and signature is a DER ECDSA P-256 signature in hex. Success is
`FW_READY,<sessionHex>`. During a session, other application commands are rejected
with `FW_ERROR,BUSY` and keypad/serial checkout/reset operations are paused.

Binary frames share RX but bypass newline parsing. Their 15-byte header is:

| Offset | Field |
| --- | --- |
| 0–3 | `00 48 5A 01` (NUL, H, Z, protocol 1) |
| 4 | Kind: 1 = signed manifest; 2 = application image |
| 5–8 | Session ID, unsigned little-endian 32-bit |
| 9–12 | Offset within that kind, unsigned little-endian 32-bit |
| 13–14 | Payload bytes, little-endian 16-bit, 1–512 |
| 15 onward | Exactly that many binary bytes |

Frames may span negotiated GATT writes (20-byte fallback, up to 244-byte
payload). A bounded 4096-byte FreeRTOS queue separates BLE callbacks from main-loop
flash writes; overflow disconnects instead of allocating unbounded memory. Stop-and-wait uses `FW_ACK,<sessionHex>,<kind>,<nextOffset>`. An already
acknowledged prefix is not written twice; missing/out-of-order offsets are
rejected. Transfer requests retry a missing ACK twice. Metadata signature,
variant, layout, bootstrap, version, and image size are checked before opening
the inactive slot. Image bytes stream to that slot and a SHA-256 accumulator.

`FW_ABORT,<sessionHex>` returns `FW_ABORTED` before commit and restores normal
operation. Link/auth loss or 30 seconds without accepted data aborts an incomplete
session; retry begins from zero with a new session after authentication.
`FW_COMMIT,<sessionHex>` requires the exact signed length/digest, validates the
ESP image, and selects the inactive slot. `FW_COMMITTED` precedes reboot by about
500 ms; cancellation is no longer allowed. The desktop must reconnect to the
same ID and verify the running build/confirmed boot to report completion.

Errors use `FW_ERROR,<reason>`: `BUSY`, `ACTIVE_PASS`, `USB_SETUP_REQUIRED`,
`INVALID_BEGIN`, `INVALID_SESSION`, `NOT_READY`, `SESSION`, `FRAME`, `OFFSET`, `STATE`,
`METADATA`, `SIGNATURE_OR_COMPATIBILITY`, `WRITE`, `INCOMPLETE`, `HASH`, `IMAGE`,
or `COMMITTED`. Failures before commit cannot select the partially written image.
The rollback-enabled bootloader retains the previous slot; initial application
health checks run under a watchdog before confirming the new boot. No BLE
command rewrites the partition table, bootloader, NVS, or filesystem.

See [Firmware packages and releases](firmware-updates.md) for the signed manifest
format, key management, bootstrap migration, and outstanding physical tests.

## Web adapter conformance

The development browser adapter uses the existing v2 UUIDs and protocol.
It reads TX using the firmware's encrypted read permission before subscribing
or sending HELLO, allowing up to 60 seconds for OS pairing. The returned value
is discarded before notification listeners attach. The application authentication
timeout therefore starts after the encrypted link is ready. It subscribes to TX
before HELLO, sends newline-delimited UTF-8 in serialized
20-byte RX writes **with response**, and limits commands to 192 bytes before LF.
CLAIM_COMMIT follows durable pending-key storage; AUTH_OK is required before
application commands. Returning owners may authenticate a claimed IN_USE device.
`GET_ACTIVE_PASSES` returns `ACTIVE_PASSES` followed by student/epoch pairs
(no count field); singular `GET_ACTIVE_PASS` is used only after UNKNOWN_COMMAND.
`MANUAL_CHECKIN,<studentId>` targets one pass. `TIME_CURSOR,<date>,<time>,<cursor>`
sets the local wall clock and starts history reconciliation. Active-pass epoch
numbers encode local wall time as UTC fields, not true UTC instants: compare
against the browser's local components encoded the same way. See
[shared fixtures](../contracts/web-client/v1/README.md) and
[web recovery requirements](design/chromebook-web-client.md).


### Repairing only the operating-system bond

Updated firmware supports holding `*` alone for five seconds while owned and idle
(outside clock setup/touch mode). It waits up to five seconds for disconnection,
clears OS bonds, and displays reconnect instructions for two minutes, without
a code. This does not enable CLAIM mode or alter the saved owner key. Clients
reconnect using AUTH, not CLAIM, after Just Works bonding. Completion requires
successful owner authentication; expiry disconnects the unauthenticated client.
The `*`+`#` ownership-reset gesture remains separate.
