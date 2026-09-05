# Bluetooth sync protocol

## BLE transport

Hallzee advertises a Bluetooth Low Energy GATT service under `Hallzee-XXXX`,
where `XXXX` is the last four characters of the stable eFuse-derived terminal
ID. Protocol v2 requires LE Secure Connections with MITM protection and
bonding. The six-digit Bluetooth passkey is shown on the physical terminal only
while its physical claim window is open. During first claim, the same passkey
is used by the app to create the one-time claim proof; the desktop then derives
a 32-byte owner credential and does not reuse the six-digit value for returning
authentication.

| Role | UUID |
| --- | --- |
| Sync service | `005924a2-c6e5-4340-9bb8-22d9dd37a283` |
| Terminal → client notifications | `44a359f3-9215-4189-a3cb-e7ce18ad40d6` |
| Client → terminal writes | `e80f9559-49eb-47bc-af04-8e92e98ced56` |

The client enables notifications before sending commands. Commands and messages
remain UTF-8, newline-delimited text. Both physical clients divide writes into
20-byte chunks and use acknowledged writes for the encrypted/authenticated RX
characteristic. The firmware buffers fragments until a newline arrives.

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
window. A terminal reporting `IN_USE` rejects the connection before auth and
the client must show it as unavailable. A claimed terminal rejects a different
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
| `MANUAL_CHECKIN` | Teacher check-in of the active pass; records the completed trip with status `MANUAL` |
| `SET,MAX_ID_LENGTH,<4-16>` | Persist the maximum accepted student-ID length |
| `SET,MAX_ACTIVE_PASSES,<1-8>` | Persist the maximum number of simultaneous active passes; the oldest active pass remains the client-visible pass |
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
| `IDENTITY_INFO,2,<terminal_id>,<custom_name>` | Authenticated identity reread |
| `SETTINGS,MAX_ID_LENGTH,<value>` | Current persisted maximum student-ID length |
| `SETTINGS_ACK,MAX_ID_LENGTH,<value>` | Setting was saved successfully |
| `SETTINGS_ERROR,MAX_ID_LENGTH,<reason>` | Setting was rejected without changing the stored value |
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

## Kiosk settings

The first persisted kiosk setting is `MAX_ID_LENGTH`. Its default is 10 and
its accepted range is 4–16. The lower bound preserves access to the four-digit
local administrator codes. The firmware rejects a shorter value while an
active checkout has a longer ID, returning `ACTIVE_ID_TOO_LONG`. Other error
reasons are `INVALID_VALUE` and `STORAGE_UNAVAILABLE`.

Reading settings is safe during every sync. Writing is always an explicit
client action. Both settings commands and responses may exceed the default
20-byte ATT payload and therefore use the same newline buffering and chunking
as the rest of the protocol.

## Recovery and compatibility

`SYNC_ALL` is a deliberate repair operation, not part of each normal sync.
The desktop must still de-duplicate by trip ID. A restored or replaced client
database should run a full recovery once before returning to cursor mode.

On disconnect, both sides clear partial session state. The kiosk resumes BLE
advertising; Find terminal discards any stale Windows GATT object before its
five-second discovery attempt. If a terminal is replaced or factory-reset and
trip IDs restart, perform a full recovery into a new database rather than
assuming the old cursor belongs to the new device.

An unclaimed terminal removes stale ESP32 bond records at startup and whenever
physical pairing mode begins. This ensures the operating system negotiates the
currently displayed passkey instead of reusing a bond from an earlier claim.
