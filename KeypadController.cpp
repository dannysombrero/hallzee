#include "KeypadController.h"

#include "Config.h"

KeypadController::KeypadController(
  Keypad &keypad,
  IsSetupMode isSetupMode,
  IsResetAllowed isResetAllowed,
  KeyHandler onSetupKey,
  KeyHandler onNumberKey,
  ActionHandler onClear,
  ActionHandler onSubmit,
  ActionHandler onReset
) : keypad(keypad),
    isSetupMode(isSetupMode),
    isResetAllowed(isResetAllowed),
    onSetupKey(onSetupKey),
    onNumberKey(onNumberKey),
    onClear(onClear),
    onSubmit(onSubmit),
    onReset(onReset) {}

void KeypadController::begin() {
  keypad.setDebounceTime(20);
  keypad.setHoldTime(500);
}

void KeypadController::poll() {
  processEvents();
  checkResetCombo();
}

void KeypadController::processEvents() {
  if (!keypad.getKeys()) {
    return;
  }

  for (int i = 0; i < LIST_MAX; i++) {
    if (!keypad.key[i].stateChanged) {
      continue;
    }

    const char key = keypad.key[i].kchar;
    const KeyState state = keypad.key[i].kstate;
    if (isSetupMode()) {
      if (state == PRESSED) {
        onSetupKey(key);
      }
      continue;
    }

    if (key >= '0' && key <= '9') {
      if (state == PRESSED) {
        Serial.print("Key pressed: ");
        Serial.println(key);
        onNumberKey(key);
      }
      continue;
    }

    if (key == '*') {
      if (state == PRESSED) {
        starPressed = true;
      } else if (state == RELEASED) {
        starPressed = false;
        if (!suppressStarHash) {
          onClear();
        }
      }
    }

    if (key == '#') {
      if (state == PRESSED) {
        hashPressed = true;
      } else if (state == RELEASED) {
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
      return;
    }

    if (!resetHoldActive) {
      resetHoldActive = true;
      resetHoldStarted = millis();
      Serial.println("* + # detected. Hold to reset...");
    }

    if (millis() - resetHoldStarted >= RESET_HOLD_MS) {
      suppressStarHash = true;
      resetHoldActive = false;
      resetHoldStarted = 0;
      onReset();
    }
    return;
  }

  resetHoldActive = false;
  resetHoldStarted = 0;
  if (suppressStarHash && !starPressed && !hashPressed) {
    suppressStarHash = false;
  }
}
