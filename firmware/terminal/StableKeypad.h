#pragma once

#include <stdint.h>
#include "KeypadPort.h"

// Keypad's debounce setting limits scans; a single scan can still emit PRESSED.
// Require 40 ms of unchanged observations on both edges before delivering them.
class StableKeypad {
public:
  static constexpr char keys[] = "123456789*0#";
  size_t update(uint32_t now, uint16_t down, TerminalKeypadEvent *events, size_t capacity) {
    size_t count = 0;
    for (unsigned i = 0; i < 12; ++i) {
      const uint16_t bit = uint16_t(1) << i;
      if ((candidate & bit) != (down & bit)) {
        candidate ^= bit;
        changed[i] = now;
      }
      if ((stable & bit) != (candidate & bit) && uint32_t(now - changed[i]) >= 40 && count < capacity) {
        stable ^= bit;
        events[count++] = {keys[i], (stable & bit) ? KeypadEventState::Pressed : KeypadEventState::Released};
      }
    }
    return count;
  }
private:
  uint16_t candidate = 0, stable = 0;
  uint32_t changed[12] = {};
};
