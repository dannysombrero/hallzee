#pragma once

#include <Arduino.h>

enum class DisplayFont {
  BuiltIn,
  DMSansRegular6,
  DMSansRegular9,
  DMSansRegular12,
  DMSansRegular18,
  DMSansBold8,
  DMSansBold9,
  DMSansBold12,
  DMSansBold18,
  DMSansBold24
};

class DisplayPort {
public:
  virtual ~DisplayPort() = default;

  virtual void fillScreen(uint16_t color) = 0;
  virtual void fillRect(int16_t x, int16_t y, int16_t width, int16_t height,
                        uint16_t color) = 0;
  virtual void fillRoundRect(int16_t x, int16_t y, int16_t width, int16_t height,
                             int16_t radius, uint16_t color) = 0;
  virtual void drawRoundRect(int16_t x, int16_t y, int16_t width, int16_t height,
                             int16_t radius, uint16_t color) = 0;
  virtual void drawLine(int16_t x0, int16_t y0, int16_t x1, int16_t y1,
                        uint16_t color) = 0;
  virtual void setTextWrap(bool enabled) = 0;
  virtual void setTextColor(uint16_t color) = 0;
  virtual void setTextSize(uint8_t size) = 0;
  virtual void setFont(DisplayFont font) { (void)font; }
  virtual void setCursor(int16_t x, int16_t y) = 0;
  virtual void print(const char *text) = 0;
  virtual void print(const String &text) = 0;
  virtual void print(unsigned long value) = 0;
  virtual void print(int value) = 0;
  virtual void println(const char *text) = 0;
  virtual void println(const String &text) = 0;
  virtual void pause(unsigned long milliseconds) = 0;
  // True when the adapter is using the native 320x240 ILI9341 canvas.
  virtual bool isNative320x240() const { return false; }
};
