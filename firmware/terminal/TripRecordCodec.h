#pragma once

#include <Arduino.h>

class TripRecordCodec {
public:
  static bool parse(const String &record, uint32_t &tripID, bool &isSynced);
  static bool markSynced(const String &record, String &updatedRecord);
};
