# Bluetooth sync protocol

## Current transport

The terminal advertises Bluetooth Classic Serial Port Profile (SPP) under the
name `Bathroom-Terminal`. It uses RFCOMM and a legacy pairing PIN of `1234`.
The device accepts one connected serial client at a time.

Bluetooth pairing establishes a trusted relationship; it does not guarantee an
active RFCOMM session. A receiver must open the serial service and wait for the
terminal readiness message before declaring a connection successful.

## Session flow

```text
Receiver opens RFCOMM stream
Terminal -> BATHROOM_TERMINAL_READY
Receiver -> TIME,YYYY-MM-DD,HH:MM:SS
Terminal -> TIME_ACK,OK
Terminal -> SYNC_BEGIN
Terminal -> TRIP,...             (one at a time)
Receiver -> ACK,<trip_id>
... repeat ...
Terminal -> SYNC_END
```

The receiver can send `SYNC_ALL` after a normal sync to request the full
history. The receiver must de-duplicate CSV writes by `trip_id` because a
record can be retransmitted after a connection interruption.

## Commands sent to the terminal

| Command | Meaning |
| --- | --- |
| `TIME,YYYY-MM-DD,HH:MM:SS` | Set local terminal time, then begin normal sync |
| `SYNC_START` | Send unsynced records |
| `SYNC_ALL` | Send all records in ascending trip-ID order |
| `ACK,<trip_id>` | Confirm durable receipt of the most recently sent trip |

Commands are newline-delimited. Carriage returns are ignored. Maximum command
length is 48 characters; longer commands receive `ERROR,COMMAND_TOO_LONG`.

## Messages sent by the terminal

| Message | Meaning |
| --- | --- |
| `BATHROOM_TERMINAL_READY` | RFCOMM connection is established and usable |
| `TIME_ACK,OK` / `TIME_ACK,ERROR` | Result of a `TIME` command |
| `SYNC_BEGIN` | A sync sequence has started |
| `TRIP,<record>` | The next record; its first CSV field is the trip ID |
| `SYNC_END` | No more records in the requested sequence |
| `ACK_ERROR,INVALID_ID` | ACK did not contain a numeric ID |
| `ACK_ERROR,UNEXPECTED_ID` | ACK did not match the currently pending trip |
| `ACK_ERROR,MARK_FAILED` | Terminal could not persist the synced state |
| `ERROR,UNKNOWN_COMMAND` | Unrecognized command |

## Windows client requirements

The next Windows client should use native Windows Bluetooth/RFCOMM APIs rather
than ask users to select a COM port. Its expected flow is:

1. Discover nearby SPP devices and identify the selected terminal.
2. Initiate Windows pairing from the app; Windows may display the required PIN
   prompt.
3. Open the terminal's RFCOMM service directly.
4. Wait for `BATHROOM_TERMINAL_READY` before enabling sync.
5. Send `TIME`, process one `TRIP` at a time, write the CSV row durably, and
   only then send its `ACK`.
6. Remember the chosen Windows device identity for future one-click syncs.
7. On a temporary failure, retry the RFCOMM session briefly; offer explicit
   reconnect and forget/re-pair actions rather than exposing generic Windows
   Bluetooth connection states.
