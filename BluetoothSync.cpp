#include "BluetoothSync.h"

#include "Config.h"

BluetoothSync::BluetoothSync(
  TripStoragePort &tripStorage,
  BluetoothSerialPort &serial,
  ClockSetter clockSetter,
  ClockSetHandler clockSetHandler,
  ActivePassProvider activePassProvider,
  ManualCheckInHandler manualCheckInHandler,
  ActivePassListProvider activePassListProvider,
  CapacitySetter capacitySetter
) : tripStorage(tripStorage),
    clockSetter(clockSetter),
    clockSetHandler(clockSetHandler),
    activePassProvider(activePassProvider),
    activePassListProvider(activePassListProvider),
    manualCheckInHandler(manualCheckInHandler),
    capacitySetter(capacitySetter),
    serial(serial) {}

void BluetoothSync::notifyCheckout(const String &studentId, uint32_t checkoutEpoch) {
  if (!ready || !serial.hasClient()) return;
  serial.print("EVENT,CHECKOUT,");
  serial.print(studentId);
  serial.print(",");
  serial.println(String(checkoutEpoch));
}

void BluetoothSync::notifyCheckin(const String &studentId, unsigned long durationSeconds) {
  if (!ready || !serial.hasClient()) return;
  serial.print("EVENT,CHECKIN,");
  serial.print(studentId);
  serial.print(",");
  serial.println(String(durationSeconds));
}

void BluetoothSync::notifyReset(const String &studentId, unsigned long durationSeconds) {
  if (!ready || !serial.hasClient()) return;
  serial.print("EVENT,RESET,");
  serial.print(studentId);
  serial.print(",");
  serial.println(String(durationSeconds));
}

void BluetoothSync::notifyCompletedTrip(const String &record) {
  if (!ready || !serial.hasClient() || record.length() == 0) return;
  serial.print("LIVE_TRIP,");
  serial.println(record);
}

void BluetoothSync::begin() {
  ready = serial.begin(BLUETOOTH_DEVICE_NAME);

  if (!ready) {
    Serial.println("ERROR: Bluetooth LE could not start.");
    return;
  }

  Serial.print("Bluetooth LE ready as: ");
  Serial.println(BLUETOOTH_DEVICE_NAME);
}

void BluetoothSync::poll() {
  updateConnection();
  processCommands();
}

void BluetoothSync::updateConnection() {
  if (!ready) return;
  const bool isConnected = serial.hasClient();
  if (isConnected == wasConnected) return;

  wasConnected = isConnected;
  if (isConnected) {
    Serial.println("Bluetooth LE client connected.");
    serial.println("HALLZEE_READY,1");
    return;
  }

  Serial.println("Bluetooth LE client disconnected.");
  resetSyncState();
}

void BluetoothSync::resetSyncState() {
  syncInProgress = false;
  streamUsesCursor = false;
  pendingTripID = 0;
  lastStreamedTripID = 0;
  commandBuffer = "";
  discardingInput = false;
}

void BluetoothSync::sendNextTrip() {
  if (!ready || !serial.hasClient()) {
    syncInProgress = false;
    pendingTripID = 0;
    return;
  }

  String record;
  uint32_t tripID;
  const bool hasNextRecord = streamUsesCursor
    ? tripStorage.getNextRecordAfter(lastStreamedTripID, record, tripID)
    : tripStorage.getNextUnsyncedRecord(record, tripID);

  if (!hasNextRecord) {
    serial.println("SYNC_END");
    resetSyncState();
    return;
  }

  pendingTripID = tripID;
  serial.print("TRIP,");
  serial.println(record);
}

void BluetoothSync::beginSync(bool includeSyncedRecords) {
  if (includeSyncedRecords) {
    beginCursorSync(0);
    return;
  }

  if (!ready || !serial.hasClient()) return;
  syncInProgress = true;
  streamUsesCursor = false;
  pendingTripID = 0;
  lastStreamedTripID = 0;
  serial.print("SYNC_BEGIN,");
  serial.println(String(tripStorage.getUnsyncedTripRecordCount()));
  sendNextTrip();
}

void BluetoothSync::beginCursorSync(uint32_t afterTripID) {
  if (!ready || !serial.hasClient()) return;
  syncInProgress = true;
  streamUsesCursor = true;
  pendingTripID = 0;
  lastStreamedTripID = afterTripID;
  serial.print("SYNC_BEGIN,");
  serial.println(String(tripStorage.getTripRecordCountAfter(afterTripID)));
  sendNextTrip();
}

bool BluetoothSync::processAcknowledgement(const String &command) {
  if (!command.startsWith("ACK,")) return false;

  String idText = command.substring(4);
  idText.trim();
  if (idText.length() == 0) {
    serial.println("ACK_ERROR,INVALID_ID");
    return true;
  }
  for (unsigned int i = 0; i < idText.length(); i++) {
    if (idText.charAt(i) < '0' || idText.charAt(i) > '9') {
      serial.println("ACK_ERROR,INVALID_ID");
      return true;
    }
  }

  const uint32_t acknowledgedID = static_cast<uint32_t>(idText.toInt());
  if (!syncInProgress || pendingTripID == 0 || acknowledgedID != pendingTripID) {
    serial.println("ACK_ERROR,UNEXPECTED_ID");
    return true;
  }

  if (streamUsesCursor) {
    // The desktop cursor is advanced only after its durable SQLite write.
    // Avoid rewriting the entire flash log for every cursor-mode ACK.
    lastStreamedTripID = acknowledgedID;
  } else if (!tripStorage.markTripSynced(acknowledgedID)) {
    serial.println("ACK_ERROR,MARK_FAILED");
    syncInProgress = false;
    pendingTripID = 0;
    return true;
  }

  Serial.print("Trip synced: ");
  Serial.println(acknowledgedID);
  sendNextTrip();
  return true;
}

bool BluetoothSync::processTimeCommand(const String &command) {
  int year, month, day, hour, minute, second;
  char extraCharacter;
  const int parsedValues = sscanf(
    command.c_str(), "TIME,%d-%d-%d,%d:%d:%d%c", &year, &month, &day,
    &hour, &minute, &second, &extraCharacter
  );

  if (parsedValues != 6 || year < 2024 || year > 2099 || month < 1 ||
      month > 12 || day < 1 || day > daysInMonth(month, year) || hour < 0 ||
      hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59) {
    serial.println("TIME_ACK,ERROR");
    return false;
  }

  clockSetter(year, month, day, hour, minute, second);
  serial.println("TIME_ACK,OK");
  clockSetHandler();
  return true;
}

bool BluetoothSync::processTimeCursorCommand(
  const String &command,
  uint32_t &afterTripID
) {
  int year, month, day, hour, minute, second;
  unsigned long cursor;
  char extraCharacter;
  const int parsedValues = sscanf(
    command.c_str(), "TIME_CURSOR,%d-%d-%d,%d:%d:%d,%lu%c", &year, &month,
    &day, &hour, &minute, &second, &cursor, &extraCharacter
  );

  if (parsedValues != 7 || year < 2024 || year > 2099 || month < 1 ||
      month > 12 || day < 1 || day > daysInMonth(month, year) || hour < 0 ||
      hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59 ||
      cursor > UINT32_MAX) {
    serial.println("TIME_ACK,ERROR");
    return false;
  }

  clockSetter(year, month, day, hour, minute, second);
  serial.println("TIME_ACK,OK");
  clockSetHandler();
  afterTripID = static_cast<uint32_t>(cursor);
  return true;
}

void BluetoothSync::sendMaxStudentIdLength() {
  serial.print("SETTINGS,MAX_ID_LENGTH,");
  serial.println(String(tripStorage.getMaxStudentIdLength()));
}

bool BluetoothSync::processSettingsCommand(const String &command) {
  if (command == "GET_SETTINGS") {
    sendMaxStudentIdLength();
    return true;
  }

  if (!command.startsWith("SET,MAX_ID_LENGTH,")) return false;

  String valueText = command.substring(18);
  valueText.trim();
  if (valueText.length() == 0) {
    serial.println("SETTINGS_ERROR,MAX_ID_LENGTH,INVALID_VALUE");
    return true;
  }
  for (unsigned int i = 0; i < valueText.length(); i++) {
    if (valueText.charAt(i) < '0' || valueText.charAt(i) > '9') {
      serial.println("SETTINGS_ERROR,MAX_ID_LENGTH,INVALID_VALUE");
      return true;
    }
  }

  const long requestedValue = valueText.toInt();
  if (requestedValue < MIN_STUDENT_ID_LENGTH ||
      requestedValue > MAX_STUDENT_ID_LENGTH) {
    serial.println("SETTINGS_ERROR,MAX_ID_LENGTH,INVALID_VALUE");
    return true;
  }

  const SettingWriteResult result = tripStorage.setMaxStudentIdLength(
    static_cast<uint8_t>(requestedValue)
  );
  if (result == SettingWriteResult::Saved) {
    serial.print("SETTINGS_ACK,MAX_ID_LENGTH,");
    serial.println(String(tripStorage.getMaxStudentIdLength()));
  } else if (result == SettingWriteResult::ActiveCheckoutTooLong) {
    serial.println("SETTINGS_ERROR,MAX_ID_LENGTH,ACTIVE_ID_TOO_LONG");
  } else if (result == SettingWriteResult::InvalidValue) {
    serial.println("SETTINGS_ERROR,MAX_ID_LENGTH,INVALID_VALUE");
  } else {
    serial.println("SETTINGS_ERROR,MAX_ID_LENGTH,STORAGE_UNAVAILABLE");
  }
  return true;
}

void BluetoothSync::processCommands() {
  if (!ready) return;

  while (serial.available()) {
    const char received = static_cast<char>(serial.read());
    if (received == '\r') continue;

    if (received == '\n') {
      if (!discardingInput && commandBuffer.length() > 0) {
        Serial.print("Bluetooth command: ");
        Serial.println(commandBuffer);
        if (commandBuffer == "HELLO,1") {
          serial.println("HALLZEE_READY,1");
        } else if (commandBuffer == "GET_ACTIVE_PASS") {
          String activeId;
          uint32_t checkoutEpoch = 0;
          if (activePassProvider && activePassProvider(activeId, checkoutEpoch) && activeId.length() > 0) {
            serial.print("ACTIVE_PASS,");
            serial.print(activeId);
            serial.print(",");
            serial.println(String(checkoutEpoch));
          } else {
            serial.println("ACTIVE_PASS,NONE");
          }
        } else if (commandBuffer == "GET_ACTIVE_PASSES") {
          ActiveCheckout checkouts[MAX_ACTIVE_PASSES];
          const uint8_t count = activePassListProvider ? activePassListProvider(checkouts, MAX_ACTIVE_PASSES) : 0;
          serial.print("ACTIVE_PASSES");
          for (uint8_t index = 0; index < count; index++) {
            serial.print(",");
            serial.print(checkouts[index].studentID);
            serial.print(",");
            serial.print(String(static_cast<uint32_t>(checkouts[index].checkoutTime)));
          }
          serial.println("");
        } else if (commandBuffer == "MANUAL_CHECKIN" || commandBuffer.startsWith("MANUAL_CHECKIN,")) {
          const String studentId = commandBuffer.length() > 15 ? commandBuffer.substring(15) : "";
          if (!manualCheckInHandler || !manualCheckInHandler(studentId)) {
            serial.println("MANUAL_CHECKIN_ERROR,NO_ACTIVE_PASS");
          }
        } else if (commandBuffer.startsWith("SET,MAX_ACTIVE_PASSES,")) {
          const String valueText = commandBuffer.substring(String("SET,MAX_ACTIVE_PASSES,").length());
          const int value = valueText.toInt();
          if (value < 1 || value > MAX_ACTIVE_PASSES || !capacitySetter || !capacitySetter(static_cast<uint8_t>(value))) {
            serial.println("SETTINGS_ERROR,MAX_ACTIVE_PASSES,INVALID_VALUE");
          } else {
            serial.print("SETTINGS_ACK,MAX_ACTIVE_PASSES,");
            serial.println(String(value));
          }
        } else if (processSettingsCommand(commandBuffer)) {
          // Settings command handled.
        } else if (commandBuffer.startsWith("TIME_CURSOR,")) {
          uint32_t afterTripID = 0;
          if (processTimeCursorCommand(commandBuffer, afterTripID)) {
            beginCursorSync(afterTripID);
          }
        } else if (commandBuffer.startsWith("TIME,")) {
          if (processTimeCommand(commandBuffer)) beginSync();
        } else if (commandBuffer == "SYNC_START") {
          beginSync();
        } else if (commandBuffer == "SYNC_ALL") {
          beginCursorSync(0);
        } else if (!processAcknowledgement(commandBuffer)) {
          serial.println("ERROR,UNKNOWN_COMMAND");
        }
      }
      commandBuffer = "";
      discardingInput = false;
      continue;
    }

    if (discardingInput) continue;
    if (commandBuffer.length() >= MAX_BLUETOOTH_COMMAND_LENGTH) {
      commandBuffer = "";
      discardingInput = true;
      serial.println("ERROR,COMMAND_TOO_LONG");
      continue;
    }
    commandBuffer += received;
  }
}

bool BluetoothSync::isLeapYear(int year) {
  if (year % 400 == 0) return true;
  if (year % 100 == 0) return false;
  return year % 4 == 0;
}

int BluetoothSync::daysInMonth(int month, int year) {
  switch (month) {
    case 4: case 6: case 9: case 11: return 30;
    case 2: return isLeapYear(year) ? 29 : 28;
    default: return 31;
  }
}
