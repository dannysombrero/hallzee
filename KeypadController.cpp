#include "KeypadController.h"

#include "Config.h"

KeypadController::KeypadController(
  KeypadPort &keypad,
  const MonotonicClock &clock,
  IsSetupMode isSetupMode,
  IsResetAllowed isResetAllowed,
  KeyHandler onSetupKey,
  KeyHandler onNumberKey,
  ActionHandler onClear,
  ActionHandler onSubmit,
  ActionHandler onReset,
  IsPairingAllowed isPairingAllowed,
  ActionHandler onPairing
) : keypad(keypad),
    clock(clock),
    isSetupMode(isSetupMode),
    isResetAllowed(isResetAllowed),
    onSetupKey(onSetupKey),
    onNumberKey(onNumberKey),
    onClear(onClear),
    onSubmit(onSubmit),
    onReset(onReset),
    isPairingAllowed(isPairingAllowed),
    onPairing(onPairing) {}

void KeypadController::begin() {
  keypad.configure(20, 500);
}

void KeypadController::poll() {
  processEvents();
  checkResetCombo();
}

void KeypadController::processEvents() {
  TerminalKeypadEvent events[10];
  const size_t eventCount = keypad.readEvents(events, 10);
  for (size_t index = 0; index < eventCount; index++) {
    const char key = events[index].key;
    const KeypadEventState state = events[index].state;
    if (isSetupMode()) {
      if (state == KeypadEventState::Pressed) {
        onSetupKey(key);
      }
      continue;
    }

    if (key >= '0' && key <= '9') {
      if (state == KeypadEventState::Pressed) {
        onNumberKey(key);
      }
      continue;
    }

    if (key == '*') {
      if (state == KeypadEventState::Pressed) {
        starPressed = true;
      } else if (state == KeypadEventState::Released) {
        starPressed = false;
        if (!suppressStarHash) {
          onClear();
        }
      }
    }

    if (key == '#') {
      if (state == KeypadEventState::Pressed) {
        hashPressed = true;
      } else if (state == KeypadEventState::Released) {
        hashPressed = false;
        if (!suppressStarHash) {
          onSubmit();
        }
      }
    }
  }
}

void KeypadController::checkResetCombo() {
  if (isSetupMode()) {
    return;
  }

  if (starPressed && hashPressed) {
    if (!isResetAllowed()) {
      resetHoldStarted = 0;
      resetHoldActive = false;
      if (!isPairingAllowed || !isPairingAllowed()) {
        pairingHoldStarted = 0;
        pairingHoldActive = false;
        return;
      }
      if (!pairingHoldActive) {
        pairingHoldActive = true;
        pairingHoldStarted = clock.milliseconds();
      }
      if (clock.milliseconds() - pairingHoldStarted >= PAIRING_HOLD_MS) {
        suppressStarHash = true;
        pairingHoldActive = false;
        pairingHoldStarted = 0;
        if (onPairing) onPairing();
      }
      return;
    }

    if (!resetHoldActive) {
      resetHoldActive = true;
      resetHoldStarted = clock.milliseconds();
    }

    if (clock.milliseconds() - resetHoldStarted >= RESET_HOLD_MS) {
      suppressStarHash = true;
      resetHoldActive = false;
      resetHoldStarted = 0;
      onReset();
    }
    return;
  }

  resetHoldActive = false;
  resetHoldStarted = 0;
  pairingHoldActive = false;
  pairingHoldStarted = 0;
  if (suppressStarHash && !starPressed && !hashPressed) {
    suppressStarHash = false;
  }
}
