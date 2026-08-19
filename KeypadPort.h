#pragma once

#include <cstddef>

enum class KeypadEventState {
  Pressed,
  Released,
  Held,
  Idle
};

struct TerminalKeypadEvent {
  char key;
  KeypadEventState state;
};

class KeypadPort {
public:
  virtual ~KeypadPort() = default;

  virtual void configure(unsigned int debounceMilliseconds,
                         unsigned int holdMilliseconds) = 0;
  virtual size_t readEvents(TerminalKeypadEvent *events, size_t capacity) = 0;
};
