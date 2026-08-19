#pragma once

#include <time.h>

class TimeProvider {
public:
  virtual ~TimeProvider() = default;
  virtual time_t now() const = 0;
};

class SystemTimeProvider : public TimeProvider {
public:
  time_t now() const override;
};
