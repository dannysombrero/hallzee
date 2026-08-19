#pragma once

#include <Arduino.h>
#include <time.h>

#include "TimeProvider.h"
#include "TripStoragePort.h"

enum class TerminalAction {
  EmptyId,
  StartClockSetup,
  ShowTripLog,
  CheckedOut,
  CheckedIn,
  StorageError,
  PassOccupied
};

struct TerminalActionResult {
  TerminalAction action;
  String id;
  unsigned long elapsedSeconds = 0;
};

class TerminalController {
public:
  TerminalController(TripStoragePort &tripStorage, const TimeProvider &timeProvider);

  void restoreActivePass();
  bool hasActivePass() const;
  const String &activeId() const;
  TerminalActionResult submit(const String &enteredId);
  bool resetActivePass(String &resetId);

private:
  TripStoragePort &tripStorage;
  const TimeProvider &timeProvider;
  String currentOutId;
  time_t checkoutTime = 0;
};
