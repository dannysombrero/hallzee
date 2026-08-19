#include "TerminalController.h"

#include "Config.h"

TerminalController::TerminalController(
  TripStoragePort &tripStorage,
  const TimeProvider &timeProvider
) : tripStorage(tripStorage), timeProvider(timeProvider) {}

void TerminalController::restoreActivePass() {
  tripStorage.loadActiveCheckout(currentOutId, checkoutTime);
}

bool TerminalController::hasActivePass() const {
  return currentOutId.length() > 0;
}

const String &TerminalController::activeId() const {
  return currentOutId;
}

TerminalActionResult TerminalController::submit(const String &enteredId) {
  if (enteredId.length() == 0) {
    return {TerminalAction::EmptyId, "", 0};
  }
  if (enteredId == CLOCK_CODE) {
    return {TerminalAction::StartClockSetup, "", 0};
  }
  if (enteredId == LOG_SUMMARY_CODE) {
    return {TerminalAction::ShowTripLog, "", 0};
  }

  if (!hasActivePass()) {
    currentOutId = enteredId;
    checkoutTime = timeProvider.now();
    if (!tripStorage.saveActiveCheckout(currentOutId, checkoutTime)) {
      currentOutId = "";
      checkoutTime = 0;
      return {TerminalAction::StorageError, "", 0};
    }
    return {TerminalAction::CheckedOut, currentOutId};
  }

  if (enteredId != currentOutId) {
    return {TerminalAction::PassOccupied, "", 0};
  }

  const time_t checkinTime = timeProvider.now();
  long elapsedSeconds = static_cast<long>(difftime(checkinTime, checkoutTime));
  if (elapsedSeconds < 0) {
    elapsedSeconds = 0;
  }

  const String id = currentOutId;
  if (!tripStorage.appendTripRecord(
    id, checkoutTime, checkinTime, elapsedSeconds, "COMPLETE"
  )) {
    return {TerminalAction::StorageError, "", 0};
  }

  currentOutId = "";
  checkoutTime = 0;
  tripStorage.clearActiveCheckout();
  return {TerminalAction::CheckedIn, id, static_cast<unsigned long>(elapsedSeconds)};
}

bool TerminalController::resetActivePass(String &resetId) {
  if (!hasActivePass()) {
    return false;
  }

  resetId = currentOutId;
  if (!tripStorage.appendTripRecord(
    resetId, checkoutTime, 0, 0, "MANUAL_RESET"
  )) {
    return false;
  }

  currentOutId = "";
  checkoutTime = 0;
  tripStorage.clearActiveCheckout();
  return true;
}
