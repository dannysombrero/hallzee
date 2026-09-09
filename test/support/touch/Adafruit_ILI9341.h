#pragma once
#include "Arduino.h"
#include <cstring>
#include <vector>

extern uint32_t touchTestMillis;
inline uint32_t millis() { return touchTestMillis; }

class Adafruit_ILI9341 {
public:
  std::vector<std::string> text;
  void fillScreen(uint16_t) {}
  void fillRect(int, int, int, int, uint16_t) {}
  void fillRoundRect(int, int, int, int, int, uint16_t) {}
  void drawRoundRect(int, int, int, int, int, uint16_t) {}
  void drawCircle(int, int, int, uint16_t) {}
  void drawFastHLine(int, int, int, uint16_t) {}
  void drawFastVLine(int, int, int, uint16_t) {}
  void setFont(const void *) {}
  void setTextSize(int) {}
  void setTextColor(uint16_t) {}
  void setCursor(int, int) {}
  void setTextWrap(bool) {}
  void print(const char *value) { text.emplace_back(value); }
  void print(const String &value) { text.emplace_back(value.c_str()); }
  template<typename T> void print(T value) { text.emplace_back(std::to_string(value)); }
};
