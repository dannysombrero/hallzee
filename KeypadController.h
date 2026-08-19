#pragma once

#include <Keypad.h>

class KeypadController {
public:
  using IsSetupMode = bool (*)();
  using IsResetAllowed = bool (*)();
  using KeyHandler = void (*)(char key);
  using ActionHandler = void (*)();

  KeypadController(
    Keypad &keypad,
    IsSetupMode isSetupMode,
    IsResetAllowed isResetAllowed,
    KeyHandler onSetupKey,
    KeyHandler onNumberKey,
    ActionHandler onClear,
    ActionHandler onSubmit,
    ActionHandler onReset
  );

  void begin();
  void poll();

private:
  Keypad &keypad;
  IsSetupMode isSetupMode;
  IsResetAllowed isResetAllowed;
  KeyHandler onSetupKey;
  KeyHandler onNumberKey;
  ActionHandler onClear;
  ActionHandler onSubmit;
  ActionHandler onReset;

  bool starPressed = false;
  bool hashPressed = false;
  unsigned long resetHoldStarted = 0;
  bool resetHoldActive = false;
  bool suppressStarHash = false;

  void processEvents();
  void checkResetCombo();
};
