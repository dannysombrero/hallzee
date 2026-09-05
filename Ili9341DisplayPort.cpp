#include "Ili9341DisplayPort.h"

#if defined(HALLZEE_ILI9341)
#include "fonts/DMSansBold8pt7b.h"
#include "fonts/DMSansBold9pt7b.h"
#include "fonts/DMSansBold12pt7b.h"
#include "fonts/DMSansBold18pt7b.h"
#include "fonts/DMSansBold24pt7b.h"
#include "fonts/DMSansRegular6pt7b.h"
#include "fonts/DMSansRegular9pt7b.h"
#include "fonts/DMSansRegular12pt7b.h"
#include "fonts/DMSansRegular18pt7b.h"
#endif

#if defined(HALLZEE_ILI9341)
Ili9341DisplayPort::Ili9341DisplayPort(Adafruit_ILI9341 &display)
  : display(display) {}

int16_t Ili9341DisplayPort::scaleX(int16_t value) {
  return value;
}

int16_t Ili9341DisplayPort::scaleY(int16_t value) {
  return value;
}

int16_t Ili9341DisplayPort::scaleWidth(int16_t value) {
  return value;
}

int16_t Ili9341DisplayPort::scaleHeight(int16_t value) {
  return value;
}

void Ili9341DisplayPort::fillScreen(uint16_t color) { display.fillScreen(color); }
void Ili9341DisplayPort::fillRect(int16_t x, int16_t y, int16_t width, int16_t height, uint16_t color) {
  display.fillRect(scaleX(x), scaleY(y), scaleWidth(width), scaleHeight(height), color);
}
void Ili9341DisplayPort::fillRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) {
  display.fillRoundRect(scaleX(x), scaleY(y), scaleWidth(width), scaleHeight(height), scaleWidth(radius), color);
}
void Ili9341DisplayPort::drawRoundRect(int16_t x, int16_t y, int16_t width, int16_t height, int16_t radius, uint16_t color) {
  display.drawRoundRect(scaleX(x), scaleY(y), scaleWidth(width), scaleHeight(height), scaleWidth(radius), color);
}
void Ili9341DisplayPort::drawLine(int16_t x0, int16_t y0, int16_t x1, int16_t y1, uint16_t color) {
  display.drawLine(scaleX(x0), scaleY(y0), scaleX(x1), scaleY(y1), color);
}
void Ili9341DisplayPort::setTextWrap(bool enabled) { display.setTextWrap(enabled); }
void Ili9341DisplayPort::setTextColor(uint16_t color) { display.setTextColor(color); }
void Ili9341DisplayPort::setTextSize(uint8_t size) { display.setTextSize(size); }
void Ili9341DisplayPort::setFont(DisplayFont font) {
  switch (font) {
    case DisplayFont::DMSansRegular6: display.setFont(&DMSansRegular6pt7b); break;
    case DisplayFont::DMSansRegular9: display.setFont(&DMSansRegular9pt7b); break;
    case DisplayFont::DMSansRegular12: display.setFont(&DMSansRegular12pt7b); break;
    case DisplayFont::DMSansRegular18: display.setFont(&DMSansRegular18pt7b); break;
    case DisplayFont::DMSansBold8: display.setFont(&DMSansBold8pt7b); break;
    case DisplayFont::DMSansBold9: display.setFont(&DMSansBold9pt7b); break;
    case DisplayFont::DMSansBold12: display.setFont(&DMSansBold12pt7b); break;
    case DisplayFont::DMSansBold18: display.setFont(&DMSansBold18pt7b); break;
    case DisplayFont::DMSansBold24: display.setFont(&DMSansBold24pt7b); break;
    case DisplayFont::BuiltIn: display.setFont(nullptr); break;
  }
}
void Ili9341DisplayPort::setCursor(int16_t x, int16_t y) { display.setCursor(scaleX(x), scaleY(y)); }
void Ili9341DisplayPort::print(const char *text) { display.print(text); }
void Ili9341DisplayPort::print(const String &text) { display.print(text); }
void Ili9341DisplayPort::print(unsigned long value) { display.print(value); }
void Ili9341DisplayPort::print(int value) { display.print(value); }
void Ili9341DisplayPort::println(const char *text) { display.println(text); }
void Ili9341DisplayPort::println(const String &text) { display.println(text); }
void Ili9341DisplayPort::pause(unsigned long milliseconds) { delay(milliseconds); }

#endif
