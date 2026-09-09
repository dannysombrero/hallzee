#pragma once
#include <cstddef>
#include <cstdint>

// Binary envelope on the existing encrypted RX stream. UART text never starts NUL.
class FirmwareFrame {
public:
  uint8_t bytes[527] = {};
  size_t count = 0;
  size_t expected = 15;
  void reset() { count = 0; expected = 15; }
  bool push(uint8_t byte) {
    if (count >= sizeof(bytes)) reset();
    bytes[count++] = byte;
    if (count == 15) {
      size_t payload = bytes[13] | size_t(bytes[14]) << 8;
      if (bytes[1] != 'H' || bytes[2] != 'Z' || bytes[3] != 1 || payload == 0 || payload > 512) { reset(); return false; }
      expected = 15 + payload;
    }
    return count == expected;
  }
};
