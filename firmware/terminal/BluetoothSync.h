#pragma once

#include <Arduino.h>
#include "FirmwareFrame.h"
#include "BluetoothSerialPort.h"
#include "TripStoragePort.h"

class TerminalIdentity;
class TerminalSecurity;
class BellPolicy;

class BluetoothSync {
public:
  using ClockSetter = void (*)(int year, int month, int day, int hour,
                               int minute, int second);
  using ClockSetHandler = void (*)();
  using ActivePassProvider = bool (*)(String &activeId, uint32_t &checkoutEpoch);
  using ActivePassListProvider = uint8_t (*)(ActiveCheckout *checkouts, uint8_t maximum);
  using ManualCheckInHandler = bool (*)(const String &studentId);
  using CapacitySetter = bool (*)(uint8_t);
  using OwnerReleaseHandler = bool (*)();

  BluetoothSync(
    TripStoragePort &tripStorage,
    BluetoothSerialPort &serial,
    ClockSetter clockSetter,
    ClockSetHandler clockSetHandler,
    ActivePassProvider activePassProvider = nullptr,
    ManualCheckInHandler manualCheckInHandler = nullptr,
    ActivePassListProvider activePassListProvider = nullptr,
    CapacitySetter capacitySetter = nullptr,
    TerminalIdentity *identity = nullptr,
    TerminalSecurity *security = nullptr,
    BellPolicy *bellPolicy = nullptr
  );

  void setActivePassProvider(ActivePassProvider provider) {
    this->activePassProvider = provider;
  }

  void setFirmwareHandlers(bool (*command)(const String &), void (*frame)(const uint8_t *, size_t)) { firmwareCommand = command; firmwareFrame = frame; }
  void begin();
  void setOwnerReleaseHandler(OwnerReleaseHandler handler) { ownerReleaseHandler = handler; }
  void poll();
  void updatePairingStatus();

  void notifyCheckout(const String &studentId, uint32_t checkoutEpoch);
  void notifyCheckin(const String &studentId, unsigned long durationSeconds);
  void notifyReset(const String &studentId, unsigned long durationSeconds);
  void notifyCompletedTrip(const String &record);

private:
  bool (*firmwareCommand)(const String &) = nullptr;
  void (*firmwareFrame)(const uint8_t *, size_t) = nullptr;
  FirmwareFrame binaryFrame;
  TripStoragePort &tripStorage;
  ClockSetter clockSetter;
  ClockSetHandler clockSetHandler;
  ActivePassProvider activePassProvider;
  ActivePassListProvider activePassListProvider;
  ManualCheckInHandler manualCheckInHandler;
  CapacitySetter capacitySetter;
  BluetoothSerialPort &serial;
  TerminalIdentity *identity;
  TerminalSecurity *security;
  BellPolicy *bellPolicy;

  bool ready = false;
  bool wasConnected = false;
  uint32_t observedConnectionGeneration = 0;
  String commandBuffer;
  bool discardingInput = false;
  bool syncInProgress = false;
  bool streamUsesCursor = false;
  uint32_t pendingTripID = 0;
  uint32_t lastStreamedTripID = 0;
  String handshakeNonce;
  String commitNonce;
  unsigned long authorizationStartedAt = 0;
  OwnerReleaseHandler ownerReleaseHandler = nullptr;

  void updateConnection();
  void processCommands();
  void beginSync(bool includeSyncedRecords = false);
  void beginCursorSync(uint32_t afterTripID);
  void sendNextTrip();
  bool processAcknowledgement(const String &command);
  bool processTimeCommand(const String &command);
  bool processTimeCursorCommand(const String &command, uint32_t &afterTripID);
  bool processSettingsCommand(const String &command);
  bool processBellPolicyCommand(const String &command);
  void sendMaxStudentIdLength();
  void resetSyncState();
  void processAuthenticationCommand(const String &command);
  bool processAuthorizedIdentityCommand(const String &command);
  bool isAuthorized() const;

  static bool isLeapYear(int year);
  static int daysInMonth(int month, int year);
};
