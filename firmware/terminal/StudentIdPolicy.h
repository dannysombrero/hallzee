#pragma once

#include <Arduino.h>

inline bool isStudentIdWithinLimit(const String &studentId, uint8_t maximumLength) {
  return studentId.length() <= maximumLength;
}
