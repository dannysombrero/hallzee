#include "ArduinoKeypadPort.h"

ArduinoKeypadPort::ArduinoKeypadPort(Keypad &keypad) : keypad(keypad) {}

void ArduinoKeypadPort::configure(
  unsigned int debounceMilliseconds,
  unsigned int holdMilliseconds
) {
  keypad.setDebounceTime(debounceMilliseconds);
  keypad.setHoldTime(holdMilliseconds);
}

size_t ArduinoKeypadPort::readEvents(TerminalKeypadEvent *events, size_t capacity) {
  if (!keypad.getKeys()) {
    return 0;
  }

  size_t count = 0;
  for (int index = 0; index < LIST_MAX && count < capacity; index++) {
    if (!keypad.key[index].stateChanged) {
      continue;
    }

    events[count++] = {keypad.key[index].kchar, toEventState(keypad.key[index].kstate)};
  }
  return count;
}

KeypadEventState ArduinoKeypadPort::toEventState(KeyState state) {
  switch (state) {
    case PRESSED:
      return KeypadEventState::Pressed;
    case RELEASED:
      return KeypadEventState::Released;
    case HOLD:
      return KeypadEventState::Held;
    default:
      return KeypadEventState::Idle;
  }
}
