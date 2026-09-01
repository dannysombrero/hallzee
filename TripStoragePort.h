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
  virtual uint32_t getTripRecordCount() = 0;
  virtual uint32_t getTripRecordCountAfter(uint32_t afterTripID) = 0;
  virtual uint32_t getUnsyncedTripRecordCount() = 0;
  virtual bool getNextUnsyncedRecord(String &record, uint32_t &tripID) = 0;
  virtual bool getNextRecordAfter(uint32_t afterTripID, String &record,
                                  uint32_t &tripID) = 0;
  virtual bool markTripSynced(uint32_t tripID) = 0;
};
