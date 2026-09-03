#pragma once

#include "KeypadPort.h"
#include "MonotonicClock.h"

class KeypadController {
public:
  using IsSetupMode = bool (*)();
  using IsResetAllowed = bool (*)();
  using KeyHandler = void (*)(char key);
  using ActionHandler = void (*)();
  using IsPairingAllowed = bool (*)();

  KeypadController(
    KeypadPort &keypad,
    const MonotonicClock &clock,
    IsSetupMode isSetupMode,
    IsResetAllowed isResetAllowed,
    KeyHandler onSetupKey,
    KeyHandler onNumberKey,
    ActionHandler onClear,
    ActionHandler onSubmit,
    ActionHandler onReset,
    IsPairingAllowed isPairingAllowed = nullptr,
    ActionHandler onPairing = nullptr
  );

  void begin();
  void poll();

private:
  KeypadPort &keypad;
  const MonotonicClock &clock;
  IsSetupMode isSetupMode;
  IsResetAllowed isResetAllowed;
  KeyHandler onSetupKey;
  KeyHandler onNumberKey;
  ActionHandler onClear;
  ActionHandler onSubmit;
  ActionHandler onReset;
  IsPairingAllowed isPairingAllowed;
  ActionHandler onPairing;

  bool starPressed = false;
  bool hashPressed = false;
  unsigned long resetHoldStarted = 0;
  bool resetHoldActive = false;
  bool suppressStarHash = false;
  unsigned long pairingHoldStarted = 0;
  bool pairingHoldActive = false;

  void processEvents();
  void checkResetCombo();
};
