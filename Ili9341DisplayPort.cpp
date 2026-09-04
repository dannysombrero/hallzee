#include "Ili9341DisplayPort.h"

#if defined(HALLZEE_ILI9341)

Ili9341DisplayPort::Ili9341DisplayPort(Adafruit_ILI9341 &display)
  : display(display) {}

int16_t Ili9341DisplayPort::scaleCoordinate(int16_t value) {
  return static_cast<int16_t>((static_cast<int32_t>(value) * 3) / 2);
}

int16_t Ili9341DisplayPort::scaleDimension(int16_t value) {
  return static_cast<int16_t>((static_cast<int32_t>(value) * 3 + 1) / 2);
}

void Ili9341DisplayPort::fillScreen(uint16_t color) { display.fillScreen(color); }
void Ili9341DisplayPort::fillRect(int16_t x, int16_t y, int16_t width, int16_t height, uint16_t color) {
  display.fillRect(scaleCoordinate(x), 64 + scaleCoordinate(y), scaleDimension(width), scaleDimension(height), color);
}
void Ili9341DisplayPort::fillRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) {
  display.fillRoundRect(scaleCoordinate(x), 64 + scaleCoordinate(y), scaleDimension(width), scaleDimension(height), scaleDimension(radius), color);
}
void Ili9341DisplayPort::drawRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) {
  display.drawRoundRect(scaleCoordinate(x), 64 + scaleCoordinate(y), scaleDimension(width), scaleDimension(height), scaleDimension(radius), color);
}
void Ili9341DisplayPort::setTextWrap(bool enabled) { display.setTextWrap(enabled); }
void Ili9341DisplayPort::setTextColor(uint16_t color) { display.setTextColor(color); }
void Ili9341DisplayPort::setTextSize(uint8_t size) { display.setTextSize(static_cast<uint8_t>(size * 3 / 2)); }
void Ili9341DisplayPort::setCursor(int16_t x, int16_t y) { display.setCursor(scaleCoordinate(x), 64 + scaleCoordinate(y)); }
void Ili9341DisplayPort::print(const char *text) { display.print(text); }
void Ili9341DisplayPort::print(const String &text) { display.print(text); }
void Ili9341DisplayPort::print(unsigned long value) { display.print(value); }
void Ili9341DisplayPort::print(int value) { display.print(value); }
void Ili9341DisplayPort::println(const char *text) { display.println(text); }
void Ili9341DisplayPort::println(const String &text) { display.println(text); }
void Ili9341DisplayPort::pause(unsigned long milliseconds) { delay(milliseconds); }

#endif
