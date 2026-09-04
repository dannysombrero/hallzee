#pragma once

#include <sstream>
#include <string>
#include <vector>

#include "DisplayPort.h"

class RecordingDisplay : public DisplayPort {
public:
  std::vector<std::string> commands;

  void fillScreen(uint16_t color) override { record("fillScreen", color); }
  void fillRect(int16_t x, int16_t y, int16_t width, int16_t height, uint16_t color) override { record("fillRect", x, y, width, height, color); }
  void fillRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) override { record("fillRoundRect", x, y, width, height, radius, color); }
  void drawRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) override { record("drawRoundRect", x, y, width, height, radius, color); }
  void drawLine(int16_t x0, int16_t y0, int16_t x1, int16_t y1, uint16_t color) override { record("drawLine", x0, y0, x1, y1, color); }
  void setTextWrap(bool enabled) override { record("setTextWrap", enabled); }
  void setTextColor(uint16_t color) override { record("setTextColor", color); }
  void setTextSize(uint8_t size) override { record("setTextSize", static_cast<unsigned int>(size)); }
  void setCursor(int16_t x, int16_t y) override { record("setCursor", x, y); }
  void print(const char *text) override { record("print", text); }
  void print(const String &text) override { record("print", std::string(text)); }
  void print(unsigned long value) override { record("print", value); }
  void print(int value) override { record("print", value); }
  void println(const char *text) override { record("println", text); }
  void println(const String &text) override { record("println", std::string(text)); }
  void pause(unsigned long milliseconds) override { record("pause", milliseconds); }

private:
  template <typename... Values>
  void record(const char *name, const Values &...values) {
    std::ostringstream stream;
    stream << name;
    ((stream << ':' << values), ...);
    commands.push_back(stream.str());
  }
};
