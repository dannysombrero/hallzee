# Kiosk settings

Hallzee settings are read and changed from the published Windows client over
Bluetooth Low Energy. Reading settings is part of a normal sync. Writing a
setting requires an explicit user action and a confirmation from the kiosk.

## Maximum student-ID length

The default maximum is 10 digits. Staff can choose a value from 4 through 16
with the **Max student ID digits** control and then select **Apply ID Limit**.

The kiosk stores the value in non-volatile Preferences storage, so it survives
power loss. A shorter limit is rejected while the active checkout ID is longer
than the proposed value. The student must check in, or the pass must be reset,
before that lower value can be applied.

The firmware enforces the current value both while digits are entered and again
when `#` is pressed. This prevents an entry typed before a remote settings
change from bypassing the new limit. IDs longer than 10 digits use compact text
on the entry and occupied screens.

## BLE messages

- `GET_SETTINGS` returns `SETTINGS,MAX_ID_LENGTH,<value>`.
- `SET,MAX_ID_LENGTH,<value>` saves a valid value.
- Success returns `SETTINGS_ACK,MAX_ID_LENGTH,<value>`.
- Rejection returns `SETTINGS_ERROR,MAX_ID_LENGTH,<reason>`.

Commands and responses are newline-delimited and may span multiple 20-byte BLE
packets.

## Physical verification

A Windows PC is required to validate the actual BLE settings write. After
flashing the matching firmware and installing the matching Windows client:

1. Sync and confirm the client shows the current value.
2. Apply an eight-digit limit.
3. Confirm Hallzee ignores a ninth digit.
4. Restart Hallzee and confirm the client still reports eight.
5. Confirm a shorter value is rejected while a longer active ID is checked out.

Mac testing is sufficient only for the hardware-independent protocol and
rendering tests; it does not validate Windows BLE behavior.
