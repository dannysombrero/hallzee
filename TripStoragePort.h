#pragma once

#include <Arduino.h>
#include <time.h>

enum class SettingWriteResult {
  Saved,
  InvalidValue,
  ActiveCheckoutTooLong,
  Unavailable
};

constexpr uint8_t MAX_ACTIVE_PASSES = 8;

struct ActiveCheckout {
  String studentID;
  time_t checkoutTime = 0;
};

class TripStoragePort {
public:
  virtual ~TripStoragePort() = default;

  virtual void loadActiveCheckout(String &studentID, time_t &checkoutTime) = 0;
  virtual bool saveActiveCheckout(const String &studentID, time_t checkoutTime) = 0;
  virtual void clearActiveCheckout() = 0;
  virtual uint8_t loadActiveCheckouts(ActiveCheckout *checkouts, uint8_t maximum) {
    if (maximum == 0) return 0;
    loadActiveCheckout(checkouts[0].studentID, checkouts[0].checkoutTime);
    return checkouts[0].studentID.length() > 0 ? 1 : 0;
  }
  virtual bool saveActiveCheckouts(const ActiveCheckout *checkouts, uint8_t count) {
    if (count == 0) { clearActiveCheckout(); return true; }
    if (count > 1) return false;
    return saveActiveCheckout(checkouts[0].studentID, checkouts[0].checkoutTime);
  }
  virtual uint8_t getMaxStudentIdLength() const = 0;
  virtual SettingWriteResult setMaxStudentIdLength(uint8_t value) = 0;
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
