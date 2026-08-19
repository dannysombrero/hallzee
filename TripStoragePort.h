#pragma once

#include <Arduino.h>
#include <time.h>

class TripStoragePort {
public:
  virtual ~TripStoragePort() = default;

  virtual void loadActiveCheckout(String &studentID, time_t &checkoutTime) = 0;
  virtual bool saveActiveCheckout(const String &studentID, time_t checkoutTime) = 0;
  virtual void clearActiveCheckout() = 0;
  virtual bool appendTripRecord(
    const String &studentID,
    time_t outTime,
    time_t inTime,
    long durationSeconds,
    const char *status
  ) = 0;
};
