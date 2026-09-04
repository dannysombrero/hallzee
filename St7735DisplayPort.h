#pragma once

#include <Adafruit_ST7735.h>

#include "DisplayPort.h"

class St7735DisplayPort : public DisplayPort {
public:
  explicit St7735DisplayPort(Adafruit_ST7735 &display);

  void fillScreen(uint16_t color) override;
  void fillRect(int16_t x, int16_t y, int16_t width, int16_t height,
                uint16_t color) override;
  void fillRoundRect(int16_t x, int16_t y, int16_t width, int16_t height,
                     int16_t radius, uint16_t color) override;
  void drawRoundRect(int16_t x, int16_t y, int16_t width, int16_t height,
                     int16_t radius, uint16_t color) override;
  void drawLine(int16_t x0, int16_t y0, int16_t x1, int16_t y1, uint16_t color) override;
  void setTextWrap(bool enabled) override;
  void setTextColor(uint16_t color) override;
  void setTextSize(uint8_t size) override;
  void setCursor(int16_t x, int16_t y) override;
  void print(const char *text) override;
  void print(const String &text) override;
  void print(unsigned long value) override;
  void print(int value) override;
  void println(const char *text) override;
  void println(const String &text) override;
  void pause(unsigned long milliseconds) override;

private:
  Adafruit_ST7735 &display;
};
