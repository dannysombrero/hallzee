#pragma once

#include <Arduino.h>
#include <time.h>

#include "TimeProvider.h"
#include "TripStoragePort.h"
#include "BellPolicy.h"

enum class TerminalAction {
  EmptyId,
  StartClockSetup,
  ShowTripLog,
  CheckedOut,
  CheckedIn,
  StorageError,
  PassOccupied,
  PolicyLocked,
  CheckedOutWithWarning
};

struct TerminalActionResult {
  TerminalAction action;
  String id;
  unsigned long elapsedSeconds = 0;
};

class TerminalController {
public:
  TerminalController(TripStoragePort &tripStorage, const TimeProvider &timeProvider, BellPolicy *bellPolicy = nullptr);

  void restoreActivePass();
  bool hasActivePass() const;
  const String &activeId() const;
  time_t activeCheckoutTime() const { return activeCount > 0 ? activePasses[0].checkoutTime : 0; }
  time_t checkoutTimeFor(const String &studentId) const;
  uint8_t activePassCount() const { return activeCount; }
  uint8_t copyActivePasses(ActiveCheckout *destination, uint8_t maximum) const;
  bool setCapacity(uint8_t value);
  uint8_t capacity() const { return maxSimultaneousPasses; }
  TerminalActionResult submit(const String &enteredId);
  bool manualCheckIn(const String &requestedId, String &checkedInId, unsigned long &elapsedSeconds);
  bool resetActivePass(String &resetId);

private:
  TripStoragePort &tripStorage;
  const TimeProvider &timeProvider;
  BellPolicy *bellPolicy;
  ActiveCheckout activePasses[MAX_ACTIVE_PASSES];
  uint8_t activeCount = 0;
  uint8_t maxSimultaneousPasses = 1;
  int findActivePass(const String &studentId) const;
  bool persistActivePasses();
};
