#include "MonotonicClock.h"

#include <Arduino.h>

unsigned long ArduinoMonotonicClock::milliseconds() const {
  return millis();
}
