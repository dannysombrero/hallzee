#pragma once

#include <Keypad.h>

#include "KeypadPort.h"
#include "StableKeypad.h"

class ArduinoKeypadPort : public KeypadPort {
public:
  explicit ArduinoKeypadPort(Keypad &keypad);

  void configure(unsigned int debounceMilliseconds,
                 unsigned int holdMilliseconds) override;
  size_t readEvents(TerminalKeypadEvent *events, size_t capacity) override;

private:
  Keypad &keypad;

  StableKeypad stable;
};
