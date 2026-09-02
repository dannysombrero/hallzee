# Feature Design: Live Active-Pass Protocol & Real-Time Tracking

**Status:** Approved Design Spec  
**Target Milestone:** Phase 4 (Pair Implemented Backend Features)  
**Related Issues:** #13, #14  
**Scope:** Closing the active-pass gap between ESP32 Preferences and Desktop UI via BLE query & event notifications.

---

## 1. Problem Statement & Background

Currently, the ESP32 terminal persists an in-progress checkout in Non-Volatile Storage (`Preferences`), but the standard `TIME_CURSOR` sync command transfers only finalized (completed or manually reset) records from LittleFS flash. 

Consequently, the desktop client cannot natively determine whether a student is currently out of the room, who that student is, or how long they have been gone without an explicit real-time protocol query.

This feature adds an explicit `GET_ACTIVE_PASS` query command and asynchronous live event notifications over the existing BLE GATT channel.

---

## 2. Protocol Specification

### 2.1 Polling / Query Command (`GET_ACTIVE_PASS`)

Sent by the desktop client immediately after the `HELLO,1` / `SETTINGS` handshake.

**Command:**
```text
GET_ACTIVE_PASS\n
```

**Responses from Terminal:**
- When a pass is currently checked out:
  ```text
  ACTIVE_PASS,<student_id>,<epoch_timestamp_seconds>\n
  ```
  *(Example: `ACTIVE_PASS,10482,1725204120`)*
- When the pass is available:
  ```text
  ACTIVE_PASS,NONE\n
  ```

---

### 2.2 Asynchronous Event Notifications (When Connected)

When the desktop client maintains an active BLE connection with the terminal, the terminal emits live notifications on the TX characteristic whenever a pass changes state:

1. **Student Checks Out:**
   ```text
   EVENT,CHECKOUT,<student_id>,<epoch_timestamp_seconds>\n
   ```
2. **Student Checks In (Normal Completion):**
   ```text
   EVENT,CHECKIN,<student_id>,<duration_seconds>\n
   ```
3. **Teacher Manual Reset (`* + #`):**
   ```text
   EVENT,RESET,<student_id>,<duration_seconds>\n
   ```
4. **Teacher Manual Check In (desktop):** the desktop sends `MANUAL_CHECKIN`; the terminal records the trip as `MANUAL`, then sends the same `EVENT,CHECKIN` and `LIVE_TRIP` notifications as a kiosk check-in.

---

## 3. Desktop Client State Machine & Timer Sync

```mermaid
stateDiagram-v2
    [*] --> Unknown
    Unknown --> Available: ACTIVE_PASS,NONE
    Unknown --> Occupied: ACTIVE_PASS,ID,Time
    Available --> Occupied: EVENT,CHECKOUT,ID,Time
    Occupied --> Available: EVENT,CHECKIN / EVENT,RESET
    Occupied --> Unknown: BLE Disconnected
    Available --> Unknown: BLE Disconnected
```

### Timer Mechanics
1. Upon receiving `ACTIVE_PASS,<student_id>,<checkout_time>`, the client calculates initial elapsed time:
   $$\text{elapsedSeconds} = \text{CurrentSystemEpochSeconds} - \text{checkout\_time}$$
2. The client runs a local 1-second UI timer incrementing `elapsedSeconds` without polling the hardware repeatedly.
3. If the link disconnects, the UI displays `Disconnected (Pass status unknown)` rather than guessing occupancy from historical records.

---

## 4. Firmware Implementation Notes

### `BluetoothSync.cpp`
- Implement handler for `GET_ACTIVE_PASS`:
  ```cpp
  if (strcmp(cmd, "GET_ACTIVE_PASS") == 0) {
      if (terminalController.hasActivePass()) {
          char response[64];
          snprintf(response, sizeof(response), "ACTIVE_PASS,%s,%lu\n",
                   terminalController.getActiveStudentId(),
                   terminalController.getActiveCheckoutTime());
          sendNotification(response);
      } else {
          sendNotification("ACTIVE_PASS,NONE\n");
      }
  }
  ```
- Trigger `sendNotification()` inside `TerminalController` event callbacks when notifications are enabled.

---

## 5. Verification & Testing Matrix

## Deferred enhancement: multiple active passes and teacher controls

The current kiosk persists one active pass. A future firmware and protocol
revision will support up to eight simultaneous active passes, a terminal
setting to allow or disallow that mode, capacity warnings in the desktop
client, and teacher-initiated manual checkout. The desktop manual check-in
already records `MANUAL` rather than `MANUAL_RESET`. The desktop will continue to show
the oldest active pass until it is checked in, then show the next oldest pass.

| Capability | macOS Testing | Windows Testing | Hardware Required |
| :--- | :--- | :--- | :--- |
| Active-pass domain models & timer ViewModel tests | **Sufficient** | Optional | No |
| BLE mock session & packet framing tests | **Sufficient** | Optional | No |
| WinRT BLE GATT active-pass notifications | **Not Usable** | **Required** | **Yes (BLE PC)** |
| Physical ESP32 Preferences query & keypad event stream | **Not Usable** | **Required** | **Yes (ESP32 Kiosk)** |
