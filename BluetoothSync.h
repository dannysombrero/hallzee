#pragma once

#include <Arduino.h>
#include "BluetoothSerialPort.h"
#include "TripStoragePort.h"

class BluetoothSync {
public:
  using ClockSetter = void (*)(int year, int month, int day, int hour,
                               int minute, int second);
  using ClockSetHandler = void (*)();

  BluetoothSync(
    TripStoragePort &tripStorage,
    BluetoothSerialPort &serial,
    ClockSetter clockSetter,
    ClockSetHandler clockSetHandler
  );

  void begin();
  void poll();

private:
  TripStoragePort &tripStorage;
  ClockSetter clockSetter;
  ClockSetHandler clockSetHandler;
  BluetoothSerialPort &serial;

  bool ready = false;
  bool wasConnected = false;
  String commandBuffer;
  bool discardingInput = false;
  bool syncInProgress = false;
  bool syncAllRecords = false;
  uint32_t pendingTripID = 0;
  uint32_t lastStreamedTripID = 0;

  void updateConnection();
  void processCommands();
  void beginSync(bool includeSyncedRecords = false);
  void sendNextTrip();
  bool processAcknowledgement(const String &command);
  bool processTimeCommand(const String &command);
  void resetSyncState();

  static bool isLeapYear(int year);
  static int daysInMonth(int month, int year);
};
