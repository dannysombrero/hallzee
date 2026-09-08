#include "BluetoothSync.h"

#include "BellPolicy.h"
#include "Config.h"
#include "TerminalIdentity.h"
#ifdef ARDUINO
#include "TerminalSecurity.h"
#endif

BluetoothSync::BluetoothSync(
  TripStoragePort &tripStorage,
  BluetoothSerialPort &serial,
  ClockSetter clockSetter,
  ClockSetHandler clockSetHandler,
  ActivePassProvider activePassProvider,
  ManualCheckInHandler manualCheckInHandler,
  ActivePassListProvider activePassListProvider,
  CapacitySetter capacitySetter,
  TerminalIdentity *identity,
  TerminalSecurity *security,
  BellPolicy *bellPolicy
) : tripStorage(tripStorage),
    clockSetter(clockSetter),
    clockSetHandler(clockSetHandler),
    activePassProvider(activePassProvider),
    activePassListProvider(activePassListProvider),
    manualCheckInHandler(manualCheckInHandler),
    capacitySetter(capacitySetter),
    serial(serial),
    identity(identity),
    security(security),
    bellPolicy(bellPolicy) {}

void BluetoothSync::notifyCheckout(const String &studentId, uint32_t checkoutEpoch) {
  if (!ready || !serial.hasClient() || !isAuthorized()) return;
  serial.print("EVENT,CHECKOUT,");
  serial.print(studentId);
  serial.print(",");
  serial.println(String(checkoutEpoch));
}

void BluetoothSync::notifyCheckin(const String &studentId, unsigned long durationSeconds) {
  if (!ready || !serial.hasClient() || !isAuthorized()) return;
  serial.print("EVENT,CHECKIN,");
  serial.print(studentId);
  serial.print(",");
  serial.println(String(durationSeconds));
}

void BluetoothSync::notifyReset(const String &studentId, unsigned long durationSeconds) {
  if (!ready || !serial.hasClient() || !isAuthorized()) return;
  serial.print("EVENT,RESET,");
  serial.print(studentId);
  serial.print(",");
  serial.println(String(durationSeconds));
}

void BluetoothSync::notifyCompletedTrip(const String &record) {
  if (!ready || !serial.hasClient() || !isAuthorized() || record.length() == 0) return;
  serial.print("LIVE_TRIP,");
  serial.println(record);
}

void BluetoothSync::begin() {
  String advertisedName = BLUETOOTH_DEVICE_NAME;
#ifdef ARDUINO
  advertisedInUse = security && security->hasOwner();
  if (identity) advertisedName = identity->advertisedName(advertisedInUse);
#endif
  ready = serial.begin(advertisedName.c_str());

  if (!ready) {
    Serial.println("ERROR: Bluetooth LE could not start.");
    return;
  }

  Serial.print("Bluetooth LE ready as: ");
  Serial.println(advertisedName);
}

void BluetoothSync::updateAvailability(bool inUse) {
#ifdef ARDUINO
  inUse = inUse || (security && security->hasOwner());
  if (!identity || advertisedInUse == inUse) return;
  String advertisedName = identity->advertisedName(inUse);
  if (serial.setDeviceName(advertisedName.c_str())) advertisedInUse = inUse;
#else
  (void)inUse;
  (void)advertisedInUse;
#endif
}

void BluetoothSync::poll() {
  updateConnection();
#ifdef ARDUINO
  if (security && serial.hasClient() && authorizationStartedAt != 0 &&
      static_cast<unsigned long>(millis() - authorizationStartedAt) >= 10000 &&
      !security->isAuthorized()) {
    serial.println("ERROR,AUTH_TIMEOUT");
    security->clearSession();
    serial.disconnectClient();
    authorizationStartedAt = 0;
    return;
  }
#endif
  processCommands();
}

void BluetoothSync::updateConnection() {
  if (!ready) return;
  const bool isConnected = serial.hasClient();
  if (isConnected == wasConnected) return;

  wasConnected = isConnected;
  if (isConnected) {
    Serial.println("Bluetooth LE client connected.");
#ifdef ARDUINO
    if (security) {
      security->clearSession();
      handshakeNonce = "";
      commitNonce = "";
      authorizationStartedAt = 0;
    } else {
      serial.println("HALLZEE_READY,1");
    }
#else
    serial.println("HALLZEE_READY,1");
#endif
    return;
  }

  Serial.println("Bluetooth LE client disconnected.");
#ifdef ARDUINO
  if (security) security->clearSession();
#endif
  resetSyncState();
}

void BluetoothSync::resetSyncState() {
  syncInProgress = false;
  streamUsesCursor = false;
  pendingTripID = 0;
  lastStreamedTripID = 0;
  commandBuffer = "";
  discardingInput = false;
  handshakeNonce = "";
  commitNonce = "";
  authorizationStartedAt = 0;
}

bool BluetoothSync::isAuthorized() const {
#ifdef ARDUINO
  return security == nullptr || security->isAuthorized();
#else
  (void)identity;
  (void)security;
  return true;
#endif
}

void BluetoothSync::processAuthenticationCommand(const String &command) {
#ifndef ARDUINO
  (void)command;
  return;
#else
  if (!security || !identity) return;

  if (command.startsWith("HELLO,2,")) {
    const String clientId = command.substring(8);
    if (!security->beginHandshake(handshakeNonce)) {
      serial.println("ERROR,AUTH_FAILED");
      return;
    }
    authorizationStartedAt = millis();
    serial.print("IDENTITY,2,");
    serial.print(identity->terminalId());
    serial.print(",");
    serial.print(identity->terminalSuffix());
    serial.print(",");
    serial.print(security->hasOwner() ? "CLAIMED" : "UNCLAIMED");
    serial.print(",");
    bool inUse = false;
    if (activePassProvider) {
      String activeId;
      uint32_t checkoutEpoch = 0;
      inUse = activePassProvider(activeId, checkoutEpoch);
    }
    serial.print(inUse ? "IN_USE" : "AVAILABLE");
    serial.print(",");
    serial.println(handshakeNonce);
    return;
  }

  if (command == "HELLO,1") {
    serial.println("ERROR,UPGRADE_REQUIRED");
    serial.disconnectClient();
    return;
  }

  const int prefixLength = command.startsWith("CLAIM,2,") ? 8 :
                           command.startsWith("CLAIM_COMMIT,2,") ? 15 :
                           command.startsWith("AUTH,2,") ? 7 : 0;
  if (prefixLength > 0) {
    const String remainder = command.substring(prefixLength);
    const int separator = remainder.indexOf(',');
    if (separator <= 0) {
      serial.println(prefixLength == 8 ? "ERROR,AUTH_FAILED_CLAIM" :
                     prefixLength == 15 ? "ERROR,AUTH_FAILED_CLAIM_COMMIT" :
                     "ERROR,AUTH_FAILED_AUTH");
      return;
    }
    const String clientId = remainder.substring(0, separator);
    const String proof = remainder.substring(separator + 1);
    // An active checkout blocks new claims, but the remembered owner must
    // still be able to authenticate and reconnect in order to check the pass
    // back in. AUTH remains protected by the owner-key proof below.
    if ((prefixLength == 8 || prefixLength == 15) && activePassProvider) {
      String activeId;
      uint32_t checkoutEpoch = 0;
      if (activePassProvider(activeId, checkoutEpoch)) {
        serial.println("ERROR,TERMINAL_IN_USE");
        return;
      }
    }
    String nextNonce;
    if (prefixLength == 8) {
      if (security->hasOwner()) {
        serial.println("ERROR,ALREADY_CLAIMED");
      } else if (!security->claimModeActive(millis())) {
        serial.println("ERROR,PAIRING_MODE_REQUIRED");
      } else if (!security->acceptClaim(clientId, proof, handshakeNonce, nextNonce)) {
        serial.println("ERROR,AUTH_FAILED_CLAIM");
      } else {
        commitNonce = nextNonce;
        serial.print("CLAIM_OK,2,");
        serial.print(identity->terminalId());
        serial.print(",");
        serial.println(commitNonce);
      }
    } else if (prefixLength == 15) {
      if (!security->commitClaim(clientId, proof, commitNonce)) {
        serial.print("ERROR,AUTH_FAILED_CLAIM_COMMIT_");
        serial.println(security->lastClaimCommitFailure());
      } else {
        authorizationStartedAt = 0;
        commitNonce = "";
        serial.print("AUTH_OK,2,");
        serial.print(identity->terminalId());
        serial.print(",");
        const String customName = identity->customName().length() > 0
          ? identity->customName()
          : identity->advertisedName();
        serial.println(customName);
      }
    } else if (!security->acceptAuth(clientId, proof, handshakeNonce)) {
      serial.println(security->hasOwner() ? "ERROR,AUTH_FAILED_AUTH" : "ERROR,PAIRING_MODE_REQUIRED");
    } else {
      authorizationStartedAt = 0;
      handshakeNonce = "";
      serial.print("AUTH_OK,2,");
      serial.print(identity->terminalId());
      serial.print(",");
      const String customName = identity->customName().length() > 0
        ? identity->customName()
        : identity->advertisedName();
      serial.println(customName);
    }
    return;
  }

  if (command.startsWith("CLAIM_ABORT,2,")) {
    security->clearSession();
    handshakeNonce = "";
    commitNonce = "";
    authorizationStartedAt = 0;
    serial.println("CLAIM_ABORT_OK");
    return;
  }

  serial.println("ERROR,AUTH_REQUIRED");
#endif
}

bool BluetoothSync::processAuthorizedIdentityCommand(const String &command) {
  if (!identity || command == "GET_IDENTITY") {
    if (command == "GET_IDENTITY" && identity) {
      serial.print("IDENTITY_INFO,2,");
      serial.print(identity->terminalId());
      serial.print(",");
      serial.println(identity->customName());
      return true;
    }
    return false;
  }

  const String prefix = "SET,TERMINAL_NAME,";
  if (!command.startsWith(prefix.c_str())) return false;
  const String requestedName = command.substring(prefix.length());
  if (!TerminalIdentity::isValidCustomName(requestedName)) {
    serial.println("SETTINGS_ERROR,TERMINAL_NAME,INVALID_VALUE");
    return true;
  }
  if (!serial.setDeviceName((advertisedInUse ? requestedName.substring(0, 23) + "-INUSE" : requestedName).c_str())) {
    serial.println("SETTINGS_ERROR,TERMINAL_NAME,BLE_UPDATE_FAILED");
    return true;
  }
  if (!identity->setCustomName(requestedName)) {
    // Restore discovery to the persisted name if the write failed.
    serial.setDeviceName(identity->advertisedName(advertisedInUse).c_str());
    serial.println("SETTINGS_ERROR,TERMINAL_NAME,STORAGE_FAILED");
    return true;
  }
  serial.print("SETTINGS_ACK,TERMINAL_NAME,");
  serial.println(requestedName);
  return true;
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

bool BluetoothSync::processBellPolicyCommand(const String &command) {
  if (!bellPolicy) return false;
  if (command.startsWith("POLICY_BEGIN,")) {
    const String value = command.substring(13);
    if (value != "0" && value != "1") {
      serial.println("POLICY_ERROR,INVALID_BEGIN");
      return true;
    }
    bellPolicy->beginUpdate(value == "1");
    serial.println("POLICY_ACK,BEGIN");
    return true;
  }
  if (command.startsWith("POLICY_WINDOW,")) {
    unsigned long dateKey = 0;
    int start = 0;
    int end = 0;
    int firstEnd = 0;
    int lastStart = 0;
    int firstDecision = 0;
    int lastDecision = 0;
    char extra = '\0';
    const int parsed = sscanf(command.c_str(), "POLICY_WINDOW,%lu,%d,%d,%d,%d,%d,%d%c",
      &dateKey, &start, &end, &firstEnd, &lastStart, &firstDecision, &lastDecision, &extra);
    if (parsed != 7 || firstDecision < 0 || firstDecision > 2 ||
        lastDecision < 0 || lastDecision > 2) {
      serial.println("POLICY_ERROR,INVALID_WINDOW");
      return true;
    }
    BellPolicyWindow window;
    window.dateKey = static_cast<uint32_t>(dateKey);
    window.startMinute = static_cast<uint16_t>(start);
    window.endMinute = static_cast<uint16_t>(end);
    window.firstWindowEndMinute = static_cast<uint16_t>(firstEnd);
    window.lastWindowStartMinute = static_cast<uint16_t>(lastStart);
    window.firstDecision = static_cast<BellPolicyDecision>(firstDecision);
    window.lastDecision = static_cast<BellPolicyDecision>(lastDecision);
    if (!bellPolicy->addStagedWindow(window)) {
      serial.println("POLICY_ERROR,INVALID_WINDOW");
    } else {
      serial.println("POLICY_ACK,WINDOW");
    }
    return true;
  }
  if (command.startsWith("POLICY_COMMIT,")) {
    const String countText = command.substring(14);
    if (countText.length() == 0) {
      serial.println("POLICY_ERROR,COMMIT_FAILED");
      return true;
    }
    for (unsigned int index = 0; index < countText.length(); index++) {
      if (countText.charAt(index) < '0' || countText.charAt(index) > '9') {
        serial.println("POLICY_ERROR,COMMIT_FAILED");
        return true;
      }
    }
    const long count = countText.toInt();
    if (count < 0 || count > MAX_BELL_POLICY_WINDOWS || !bellPolicy->commitUpdate(static_cast<uint8_t>(count))) {
      serial.println("POLICY_ERROR,COMMIT_FAILED");
    } else {
      serial.print("POLICY_ACK,COMMIT,");
      serial.println(String(count));
    }
    return true;
  }
  return false;
}

void BluetoothSync::processCommands() {
  if (!ready) return;

  while (serial.available()) {
    const char received = static_cast<char>(serial.read());
    if (received == '\r') continue;

    if (received == '\n') {
      if (!discardingInput && commandBuffer.length() > 0) {
        if (security) {
          Serial.println("Bluetooth command received.");
        } else {
          Serial.print("Bluetooth command: ");
          Serial.println(commandBuffer);
        }
        if (!isAuthorized()) {
          processAuthenticationCommand(commandBuffer);
        } else if (commandBuffer == "RELEASE_OWNER") {
          String activeId;
          uint32_t checkoutEpoch = 0;
          if (activePassProvider && activePassProvider(activeId, checkoutEpoch)) {
            serial.println("ERROR,ACTIVE_PASS");
          } else if (!ownerReleaseHandler) {
            serial.println("ERROR,UNSUPPORTED_COMMAND");
          } else if (!ownerReleaseHandler()) {
            serial.println("ERROR,OWNER_RELEASE_FAILED");
          } else {
            authorizationStartedAt = 0;
            serial.println("OWNER_RELEASED");
          }
        } else if (processAuthorizedIdentityCommand(commandBuffer)) {
          // Identity command handled.
        } else if (commandBuffer == "HELLO,1") {
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
        } else if (processBellPolicyCommand(commandBuffer)) {
          // Bell policy command handled.
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
