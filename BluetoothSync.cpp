#include "BluetoothSync.h"

#include "Config.h"

BluetoothSync::BluetoothSync(
  TripStoragePort &tripStorage,
  BluetoothSerialPort &serial,
  ClockSetter clockSetter,
  ClockSetHandler clockSetHandler
) : tripStorage(tripStorage),
    clockSetter(clockSetter),
    clockSetHandler(clockSetHandler),
    serial(serial) {}

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
    serial.println("HALLZEE_READY");
    serial.println("BLE transport connected.");
    return;
  }

  Serial.println("Bluetooth LE client disconnected.");
  resetSyncState();
}

void BluetoothSync::resetSyncState() {
  syncInProgress = false;
  syncAllRecords = false;
  pendingTripID = 0;
  lastStreamedTripID = 0;
}

void BluetoothSync::sendNextTrip() {
  if (!ready || !serial.hasClient()) {
    syncInProgress = false;
    pendingTripID = 0;
    return;
  }

  String record;
  uint32_t tripID;
  const bool hasNextRecord = syncAllRecords
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
  if (!ready || !serial.hasClient()) return;
  syncInProgress = true;
  syncAllRecords = includeSyncedRecords;
  pendingTripID = 0;
  lastStreamedTripID = 0;
  const uint32_t recordCount = includeSyncedRecords
    ? tripStorage.getTripRecordCount()
    : tripStorage.getUnsyncedTripRecordCount();
  serial.print("SYNC_BEGIN,");
  serial.println(String(recordCount));
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
  if (!tripStorage.markTripSynced(acknowledgedID)) {
    serial.println("ACK_ERROR,MARK_FAILED");
    syncInProgress = false;
    pendingTripID = 0;
    return true;
  }

  Serial.print("Trip synced: ");
  Serial.println(acknowledgedID);
  if (syncAllRecords) lastStreamedTripID = acknowledgedID;
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

void BluetoothSync::processCommands() {
  if (!ready) return;

  while (serial.available()) {
    const char received = static_cast<char>(serial.read());
    if (received == '\r') continue;

    if (received == '\n') {
      if (!discardingInput && commandBuffer.length() > 0) {
        Serial.print("Bluetooth command: ");
        Serial.println(commandBuffer);
        if (commandBuffer.startsWith("TIME,")) {
          if (processTimeCommand(commandBuffer)) beginSync();
        } else if (commandBuffer == "SYNC_START") {
          beginSync();
        } else if (commandBuffer == "SYNC_ALL") {
          beginSync(true);
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
