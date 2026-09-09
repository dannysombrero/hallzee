#pragma once
#include "SPI.h"
struct TS_Point { int16_t x = 0, y = 0, z = 0; };
inline TS_Point touchTestSample;
inline SPIClass *touchTestBus = nullptr;
class XPT2046_Touchscreen {
public:
  explicit XPT2046_Touchscreen(int) {}
  void begin(SPIClass &bus) { touchTestBus = &bus; bus.begin(); }
  void setRotation(int) {}
  TS_Point getPoint() { return touchTestSample; }
};
