#pragma once

#if defined(HALLZEE_ILI9341)

#ifndef HALLZEE_DISPLAY_ROTATION
#define HALLZEE_DISPLAY_ROTATION 1
#endif

#include <Adafruit_ILI9341.h>

#include "DisplayPort.h"

// The ILI9341 profile uses the panel's native 320x240 landscape canvas.
class Ili9341DisplayPort : public DisplayPort {
public:
  explicit Ili9341DisplayPort(Adafruit_ILI9341 &display);

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
  void setFont(DisplayFont font) override;
  void setCursor(int16_t x, int16_t y) override;
  void print(const char *text) override;
  void print(const String &text) override;
  void print(unsigned long value) override;
  void print(int value) override;
  void println(const char *text) override;
  void println(const String &text) override;
  void pause(unsigned long milliseconds) override;
  bool isNative320x240() const override { return true; }

private:
  static int16_t scaleX(int16_t value);
  static int16_t scaleY(int16_t value);
  static int16_t scaleWidth(int16_t value);
  static int16_t scaleHeight(int16_t value);

  Adafruit_ILI9341 &display;
};

#endif
