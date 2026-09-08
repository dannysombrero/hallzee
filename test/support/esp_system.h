#pragma once

#include <cstdint>

struct EspIdentityStub {
  uint64_t getEfuseMac() const { return 0xA1B2C3D4E5F6ULL; }
};
inline EspIdentityStub ESP;
