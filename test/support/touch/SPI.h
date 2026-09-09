#pragma once
constexpr int HSPI = 1;
constexpr int VSPI = 2;
struct SPIClass {
  explicit SPIClass(int bus = VSPI) : bus(bus) {}
  int bus, sck = -1, miso = -1, mosi = -1, cs = -1;
  bool initialized = false;
  bool begin(int clock = -1, int input = -1, int output = -1, int select = -1) {
    if (initialized) return true;
    sck = clock; miso = input; mosi = output; cs = select;
    initialized = true;
    return true;
  }
};
inline SPIClass SPI;
