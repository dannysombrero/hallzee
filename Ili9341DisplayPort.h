#pragma once

#if defined(HALLZEE_ILI9341)

#include <Adafruit_ILI9341.h>

#include "DisplayPort.h"

// The existing UI is authored against a 160x128 logical landscape canvas.
// This adapter maps it into a 240x320 portrait ILI9341 panel at 1.5x scale,
// centered vertically. Keeping that contract makes the hardware profile
// independent of the terminal's behavior and protocol code.
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
  static int16_t scaleCoordinate(int16_t value);
  static int16_t scaleDimension(int16_t value);

  Adafruit_ILI9341 &display;
};

#endif
