#pragma once

#include <Arduino.h>
#include <Preferences.h>
#include <time.h>

#include "TripStoragePort.h"

class TripStorage : public TripStoragePort {
public:
  bool begin();

  void loadActiveCheckout(String &studentID, time_t &checkoutTime) override;
  bool saveActiveCheckout(const String &studentID, time_t checkoutTime) override;
  void clearActiveCheckout() override;

  bool appendTripRecord(
    const String &studentID,
    time_t outTime,
    time_t inTime,
    long durationSeconds,
    const char *status
  ) override;

  bool isLogReady() const;
  uint32_t getTripRecordCount() override;
  uint32_t getUnsyncedTripRecordCount() override;
  uint32_t getLatestTripID();
  bool getNextUnsyncedRecord(String &record, uint32_t &tripID) override;
  bool getNextRecordAfter(
    uint32_t afterTripID,
    String &record,
    uint32_t &tripID
  ) override;
  bool markTripSynced(uint32_t tripID) override;
  void printTripLog();

private:
  Preferences preferences;
  bool preferencesReady = false;
  bool littleFSReady = false;
  uint32_t nextTripID = 1;

  void initializeTripLogStorage();
  bool getDateAndTimeForTimestamp(
    time_t timestamp,
    String &dateText,
    String &timeText
  );
  void recoverTripLogTransaction();
  bool parseTripRecord(
    const String &record,
    uint32_t &tripID,
    bool &isSynced
  );
};
