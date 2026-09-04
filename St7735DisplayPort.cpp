#include "St7735DisplayPort.h"

St7735DisplayPort::St7735DisplayPort(Adafruit_ST7735 &display)
  : display(display) {}

void St7735DisplayPort::fillScreen(uint16_t color) { display.fillScreen(color); }
void St7735DisplayPort::fillRect(int16_t x, int16_t y, int16_t width, int16_t height, uint16_t color) { display.fillRect(x, y, width, height, color); }
void St7735DisplayPort::fillRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) { display.fillRoundRect(x, y, width, height, radius, color); }
void St7735DisplayPort::drawRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) { display.drawRoundRect(x, y, width, height, radius, color); }
void St7735DisplayPort::drawLine(int16_t x0, int16_t y0, int16_t x1, int16_t y1, uint16_t color) { display.drawLine(x0, y0, x1, y1, color); }
void St7735DisplayPort::setTextWrap(bool enabled) { display.setTextWrap(enabled); }
void St7735DisplayPort::setTextColor(uint16_t color) { display.setTextColor(color); }
void St7735DisplayPort::setTextSize(uint8_t size) { display.setTextSize(size); }
void St7735DisplayPort::setCursor(int16_t x, int16_t y) { display.setCursor(x, y); }
void St7735DisplayPort::print(const char *text) { display.print(text); }
void St7735DisplayPort::print(const String &text) { display.print(text); }
void St7735DisplayPort::print(unsigned long value) { display.print(value); }
void St7735DisplayPort::print(int value) { display.print(value); }
void St7735DisplayPort::println(const char *text) { display.println(text); }
void St7735DisplayPort::println(const String &text) { display.println(text); }
void St7735DisplayPort::pause(unsigned long milliseconds) { delay(milliseconds); }
