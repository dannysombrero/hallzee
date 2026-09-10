#include "TripRecordCodec.h"

#include <limits.h>

bool TripRecordCodec::parse(const String &record, uint32_t &tripID, bool &isSynced) {
  const int firstComma = record.indexOf(',');
  const int lastComma = record.lastIndexOf(',');
  if (firstComma <= 0 || lastComma <= firstComma) {
    return false;
  }

  String idText = record.substring(0, firstComma);
  String syncedText = record.substring(lastComma + 1);
  idText.trim();
  syncedText.trim();

  if (idText.length() == 0 || (syncedText != "0" && syncedText != "1")) {
    return false;
  }

  uint32_t parsedTripID = 0;
  for (size_t index = 0; index < idText.length(); index++) {
    const char character = idText.charAt(index);
    if (character < '0' || character > '9') {
      return false;
    }

    const uint8_t digit = static_cast<uint8_t>(character - '0');
    if (parsedTripID > (UINT32_MAX - digit) / 10) {
      return false;
    }
    parsedTripID = parsedTripID * 10 + digit;
  }

  if (parsedTripID == 0) {
    return false;
  }

  tripID = parsedTripID;
  isSynced = syncedText == "1";
  return true;
}

bool TripRecordCodec::markSynced(const String &record, String &updatedRecord) {
  uint32_t tripID;
  bool isSynced;
  if (!parse(record, tripID, isSynced)) {
    return false;
  }

  const int lastComma = record.lastIndexOf(',');
  updatedRecord = record.substring(0, lastComma + 1) + "1";
  return true;
}
