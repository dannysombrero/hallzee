#include "TerminalController.h"

#include "Config.h"

TerminalController::TerminalController(
  TripStoragePort &tripStorage,
  const TimeProvider &timeProvider,
  BellPolicy *bellPolicy
) : tripStorage(tripStorage), timeProvider(timeProvider), bellPolicy(bellPolicy) {}

void TerminalController::restoreActivePass() {
  activeCount = tripStorage.loadActiveCheckouts(activePasses, MAX_ACTIVE_PASSES);
}

bool TerminalController::hasActivePass() const {
  return activeCount > 0;
}

const String &TerminalController::activeId() const {
  static String empty;
  return activeCount > 0 ? activePasses[0].studentID : empty;
}

bool TerminalController::setCapacity(uint8_t value) {
  if (value < 1 || value > MAX_ACTIVE_PASSES || value < activeCount) return false;
  maxSimultaneousPasses = value;
  return true;
}

uint8_t TerminalController::copyActivePasses(ActiveCheckout *destination, uint8_t maximum) const {
  const uint8_t count = activeCount < maximum ? activeCount : maximum;
  for (uint8_t index = 0; index < count; index++) destination[index] = activePasses[index];
  return count;
}

time_t TerminalController::checkoutTimeFor(const String &studentId) const {
  const int index = findActivePass(studentId);
  return index >= 0 ? activePasses[index].checkoutTime : 0;
}

int TerminalController::findActivePass(const String &studentId) const {
  for (uint8_t index = 0; index < activeCount; index++) {
    if (activePasses[index].studentID == studentId) return index;
  }
  return -1;
}

bool TerminalController::persistActivePasses() {
  return tripStorage.saveActiveCheckouts(activePasses, activeCount);
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

  const int existingIndex = findActivePass(enteredId);
  if (existingIndex < 0 && activeCount < maxSimultaneousPasses) {
    const time_t checkoutTime = timeProvider.now();
    const BellPolicyDecision policyDecision = bellPolicy ? bellPolicy->evaluate(checkoutTime) : BellPolicyDecision::Allow;
    if (policyDecision == BellPolicyDecision::Lock) return {TerminalAction::PolicyLocked, enteredId, 0};
    activePasses[activeCount] = {enteredId, checkoutTime};
    activeCount++;
    if (!persistActivePasses()) {
      activeCount--;
      return {TerminalAction::StorageError, "", 0};
    }
    return {policyDecision == BellPolicyDecision::Warn ? TerminalAction::CheckedOutWithWarning : TerminalAction::CheckedOut, enteredId};
  }

  if (existingIndex < 0) {
    return {TerminalAction::PassOccupied, "", 0};
  }

  const time_t checkinTime = timeProvider.now();
  const ActiveCheckout checkout = activePasses[existingIndex];
  long elapsedSeconds = static_cast<long>(difftime(checkinTime, checkout.checkoutTime));
  if (elapsedSeconds < 0) {
    elapsedSeconds = 0;
  }

  const String id = checkout.studentID;
  if (!tripStorage.appendTripRecord(
    id, checkout.checkoutTime, checkinTime, elapsedSeconds, "COMPLETE"
  )) {
    return {TerminalAction::StorageError, "", 0};
  }

  for (uint8_t index = existingIndex; index + 1 < activeCount; index++) activePasses[index] = activePasses[index + 1];
  activeCount--;
  if (!persistActivePasses()) return {TerminalAction::StorageError, "", 0};
  return {TerminalAction::CheckedIn, id, static_cast<unsigned long>(elapsedSeconds)};
}

bool TerminalController::manualCheckIn(const String &requestedId, String &checkedInId, unsigned long &elapsedSeconds) {
  if (!hasActivePass()) return false;

  const int index = requestedId.length() == 0 ? 0 : findActivePass(requestedId);
  if (index < 0) return false;
  const ActiveCheckout checkout = activePasses[index];
  const time_t checkinTime = timeProvider.now();
  long elapsed = static_cast<long>(difftime(checkinTime, checkout.checkoutTime));
  if (elapsed < 0) elapsed = 0;

  checkedInId = checkout.studentID;
  elapsedSeconds = static_cast<unsigned long>(elapsed);
  if (!tripStorage.appendTripRecord(
    checkedInId, checkout.checkoutTime, checkinTime, elapsed, "MANUAL"
  )) {
    return false;
  }

  for (uint8_t cursor = static_cast<uint8_t>(index); cursor + 1 < activeCount; cursor++) activePasses[cursor] = activePasses[cursor + 1];
  activeCount--;
  return persistActivePasses();
}

bool TerminalController::resetActivePass(String &resetId) {
  if (!hasActivePass()) {
    return false;
  }

  const ActiveCheckout checkout = activePasses[0];
  resetId = checkout.studentID;
  if (!tripStorage.appendTripRecord(
    resetId, checkout.checkoutTime, 0, 0, "MANUAL_RESET"
  )) {
    return false;
  }

  for (uint8_t index = 0; index + 1 < activeCount; index++) activePasses[index] = activePasses[index + 1];
  activeCount--;
  return persistActivePasses();
}
