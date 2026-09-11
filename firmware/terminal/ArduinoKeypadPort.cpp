#include "ArduinoKeypadPort.h"

ArduinoKeypadPort::ArduinoKeypadPort(Keypad &keypad) : keypad(keypad) {}

void ArduinoKeypadPort::configure(
  unsigned int debounceMilliseconds,
  unsigned int holdMilliseconds
) {
  keypad.setDebounceTime(debounceMilliseconds);
  keypad.setHoldTime(holdMilliseconds);
  stable = StableKeypad();
}

size_t ArduinoKeypadPort::readEvents(TerminalKeypadEvent *events, size_t capacity) {
  keypad.getKeys();
  uint16_t down = 0;
  for (int index = 0; index < LIST_MAX; index++) {
    if (keypad.key[index].kstate != PRESSED && keypad.key[index].kstate != HOLD) continue;
    for (unsigned i = 0; i < 12; ++i) {
      if (keypad.key[index].kchar == StableKeypad::keys[i]) down |= uint16_t(1) << i;
    }
  }
  return stable.update(millis(), down, events, capacity);
}
