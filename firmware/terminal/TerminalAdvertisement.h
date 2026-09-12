#pragma once

#include <array>
#include <stdint.h>

// Manufacturer company 0xFFFF is used for this project-specific format.
// The six-byte payload fits alongside flags and the 128-bit service UUID in
// a legacy 31-byte advertisement; the stable name occupies the scan response.
// This is discovery metadata, never proof of ownership. Verify with HELLO/AUTH.
inline std::array<uint8_t, 8> terminalAdvertisement(bool claimed, uint16_t suffix) {
  return {0xFF, 0xFF, 'H', 'Z', 1, static_cast<uint8_t>(claimed ? 1 : 0),
          static_cast<uint8_t>(suffix >> 8), static_cast<uint8_t>(suffix)};
}
