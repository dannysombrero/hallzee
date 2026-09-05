#pragma once

#include "AppTypes.h"
#include "Config.h"
#include "DisplayPort.h"

class TerminalDisplay {
public:
  explicit TerminalDisplay(DisplayPort &display);

  void prepareScreenTransition(uint16_t backgroundColor = UI_BACKGROUND);
  void showBluetoothClockSynced(const String &date, const String &time);
  void showCheckedOut(const String &id, const String &time);
  void showCheckedIn(unsigned long elapsedSeconds);
  void showPassOccupied();
  void showEnterId();
  void showStudentIdTooLong(uint8_t maximumLength);
  void showStorageError();
  void showTripLogSummary(bool logReady, uint32_t recordCount, uint32_t latestTripID);
  void showManualReset(const String &id);
  void drawIdEntry(const String &entry);
  void drawClock(const String &time);
  void drawBluetoothStatus(bool connected);
  void drawIdleScreen(const String &currentOutId, const String &entry);
  void drawClockSetupEntry(const String &entry);
  void drawClockSetupScreen(ClockSetupStep step, const String &entry);
  void showInvalidClockValue(const String &message);
  void showClockSet(const String &date, const String &time);
  void showPairing(const String &suffix, uint32_t passkey);
  void showPairingComplete(const String &suffix);
  void showPairingError(const String &message);
  void showOwnerReset();

private:
  DisplayPort &display;
  bool clockSetupActive = false;
};
