#pragma once

#include <Arduino.h>
#include "BluetoothSerialPort.h"
#include "TripStoragePort.h"

class BluetoothSync {
public:
  using ClockSetter = void (*)(int year, int month, int day, int hour,
                               int minute, int second);
  using ClockSetHandler = void (*)();
  using ActivePassProvider = bool (*)(String &activeId, uint32_t &checkoutEpoch);

  BluetoothSync(
    TripStoragePort &tripStorage,
    BluetoothSerialPort &serial,
    ClockSetter clockSetter,
    ClockSetHandler clockSetHandler,
    ActivePassProvider activePassProvider = nullptr
  );

  void setActivePassProvider(ActivePassProvider provider) {
    this->activePassProvider = provider;
  }

  void begin();
  void poll();

  void notifyCheckout(const String &studentId, uint32_t checkoutEpoch);
  void notifyCheckin(const String &studentId, unsigned long durationSeconds);
  void notifyReset(const String &studentId, unsigned long durationSeconds);

private:
  TripStoragePort &tripStorage;
  ClockSetter clockSetter;
  ClockSetHandler clockSetHandler;
  ActivePassProvider activePassProvider;
  BluetoothSerialPort &serial;

  bool ready = false;
  bool wasConnected = false;
  String commandBuffer;
  bool discardingInput = false;
  bool syncInProgress = false;
  bool streamUsesCursor = false;
  uint32_t pendingTripID = 0;
  uint32_t lastStreamedTripID = 0;

  void updateConnection();
  void processCommands();
  void beginSync(bool includeSyncedRecords = false);
  void beginCursorSync(uint32_t afterTripID);
  void sendNextTrip();
  bool processAcknowledgement(const String &command);
  bool processTimeCommand(const String &command);
  bool processTimeCursorCommand(const String &command, uint32_t &afterTripID);
  bool processSettingsCommand(const String &command);
  void sendMaxStudentIdLength();
  void resetSyncState();

  static bool isLeapYear(int year);
  static int daysInMonth(int month, int year);
};
