#pragma once

#include "AppTypes.h"
#include "DisplayPort.h"

class TerminalDisplay {
public:
  explicit TerminalDisplay(DisplayPort &display);

  void showBluetoothClockSynced(const String &date, const String &time);
  void showCheckedOut(const String &id, const String &time);
  void showCheckedIn(unsigned long elapsedSeconds);
  void showPassOccupied();
  void showEnterId();
  void showStorageError();
  void showTripLogSummary(bool logReady, uint32_t recordCount, uint32_t latestTripID);
  void showManualReset(const String &id);
  void drawIdEntry(const String &entry);
  void drawClock(const String &time);
  void drawIdleScreen(const String &currentOutId, const String &entry);
  void drawClockSetupEntry(const String &entry);
  void drawClockSetupScreen(ClockSetupStep step, const String &entry);
  void showInvalidClockValue(const String &message);
  void showClockSet(const String &date, const String &time);

private:
  DisplayPort &display;
};
