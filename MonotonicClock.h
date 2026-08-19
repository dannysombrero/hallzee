#pragma once

class MonotonicClock {
public:
  virtual ~MonotonicClock() = default;
  virtual unsigned long milliseconds() const = 0;
};

class ArduinoMonotonicClock : public MonotonicClock {
public:
  unsigned long milliseconds() const override;
};
