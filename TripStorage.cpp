#include "TripStorage.h"
#include "TripRecordCodec.h"

#include <LittleFS.h>

namespace {

const char *PREFERENCES_NAMESPACE = "bathroom";
const char *PREF_OUT_ID = "out_id";
const char *PREF_OUT_TIME = "out_time";
const char *PREF_NEXT_TRIP_ID = "next_trip";
const char *PREF_LOG_READY = "log_ready";
const char *PREF_MAX_STUDENT_ID_LENGTH = "max_id_len";
const char *TRIP_LOG_PATH = "/trips.csv";
const char *TRIP_LOG_TEMP_PATH = "/trips.tmp";
const char *TRIP_LOG_BACKUP_PATH = "/trips.bak";

}  // namespace

bool TripStorage::begin() {

  if (!preferences.begin(PREFERENCES_NAMESPACE, false)) {
    Serial.println("ERROR: Could not open Preferences storage.");
    return false;
  }

  preferencesReady = true;

  maxStudentIdLength = preferences.getUChar(
    PREF_MAX_STUDENT_ID_LENGTH,
    DEFAULT_STUDENT_ID_LENGTH
  );
  if (maxStudentIdLength < MIN_STUDENT_ID_LENGTH ||
      maxStudentIdLength > MAX_STUDENT_ID_LENGTH) {
    maxStudentIdLength = DEFAULT_STUDENT_ID_LENGTH;
    preferences.putUChar(PREF_MAX_STUDENT_ID_LENGTH, maxStudentIdLength);
  }

  if (!preferences.isKey(PREF_NEXT_TRIP_ID)) {
    preferences.putULong(PREF_NEXT_TRIP_ID, nextTripID);
  } else {
    nextTripID = preferences.getULong(PREF_NEXT_TRIP_ID, 1);
  }

  if (nextTripID == 0) {
    nextTripID = 1;
    preferences.putULong(PREF_NEXT_TRIP_ID, nextTripID);
  }

  initializeTripLogStorage();

  Serial.print("Next trip ID: ");
  Serial.println(nextTripID);

  return true;
}

void TripStorage::loadActiveCheckout(
  String &studentID,
  time_t &checkoutTime
) {

  studentID = "";
  checkoutTime = 0;

  if (!preferencesReady) {
    return;
  }

  studentID = preferences.getString(PREF_OUT_ID, "");
  checkoutTime = (time_t)preferences.getLong64(PREF_OUT_TIME, 0);

  if (studentID.length() > 0 && checkoutTime == 0) {
    Serial.println("Discarding incomplete saved checkout state.");
    studentID = "";
    clearActiveCheckout();
  }

  if (studentID.length() > 0) {
    Serial.print("Recovered active checkout for ID: ");
    Serial.println(studentID);
  } else {
    Serial.println("No active checkout recovered.");
  }
}

bool TripStorage::saveActiveCheckout(
  const String &studentID,
  time_t checkoutTime
) {

  if (!preferencesReady) {
    Serial.println("WARNING: Active checkout was not saved.");
    return false;
  }

  preferences.putString(PREF_OUT_ID, studentID);
  preferences.putLong64(PREF_OUT_TIME, (int64_t)checkoutTime);

  return true;
}

void TripStorage::clearActiveCheckout() {

  if (!preferencesReady) {
    return;
  }

  preferences.remove(PREF_OUT_ID);
  preferences.remove(PREF_OUT_TIME);
}

uint8_t TripStorage::getMaxStudentIdLength() const {
  return maxStudentIdLength;
}

SettingWriteResult TripStorage::setMaxStudentIdLength(uint8_t value) {
  if (value < MIN_STUDENT_ID_LENGTH || value > MAX_STUDENT_ID_LENGTH) {
    return SettingWriteResult::InvalidValue;
  }
  if (!preferencesReady) {
    return SettingWriteResult::Unavailable;
  }

  const String activeStudentId = preferences.getString(PREF_OUT_ID, "");
  if (activeStudentId.length() > value) {
    return SettingWriteResult::ActiveCheckoutTooLong;
  }

  if (preferences.putUChar(PREF_MAX_STUDENT_ID_LENGTH, value) != sizeof(uint8_t)) {
    return SettingWriteResult::Unavailable;
  }

  maxStudentIdLength = value;
  return SettingWriteResult::Saved;
}

void TripStorage::initializeTripLogStorage() {

  bool logWasInitialized =
    preferencesReady && preferences.getBool(PREF_LOG_READY, false);

  if (!LittleFS.begin(false)) {

    // Format only on the very first use of this app. If an initialized log
    // cannot mount, preserve it and fail safely instead of erasing records.
    if (!logWasInitialized) {

      Serial.println("Initializing empty LittleFS trip log...");

      if (!LittleFS.format() || !LittleFS.begin(false)) {
        Serial.println("ERROR: LittleFS initialization failed.");
        return;
      }

    } else {

      Serial.println("ERROR: LittleFS mount failed. Existing log was preserved.");
      return;
    }
  }

  littleFSReady = true;
  preferences.putBool(PREF_LOG_READY, true);

  recoverTripLogTransaction();

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_APPEND);

  if (!tripLog) {
    Serial.println("ERROR: Could not create/open trip log.");
    littleFSReady = false;
    return;
  }

  tripLog.close();
  Serial.println("LittleFS trip log ready.");
}

void TripStorage::recoverTripLogTransaction() {

  if (LittleFS.exists(TRIP_LOG_BACKUP_PATH)) {

    if (!LittleFS.exists(TRIP_LOG_PATH)) {
      Serial.println("Recovering trip log after interrupted sync update.");
      LittleFS.rename(TRIP_LOG_BACKUP_PATH, TRIP_LOG_PATH);
    } else {
      LittleFS.remove(TRIP_LOG_BACKUP_PATH);
    }
  }

  // A temporary file is only useful during the update that created it. The
  // original or recovered log remains authoritative after a reboot.
  if (LittleFS.exists(TRIP_LOG_TEMP_PATH)) {
    LittleFS.remove(TRIP_LOG_TEMP_PATH);
  }
}

bool TripStorage::getDateAndTimeForTimestamp(
  time_t timestamp,
  String &dateText,
  String &timeText
) {

  if (timestamp == 0) {
    dateText = "";
    timeText = "";
    return false;
  }

  struct tm timeInfo;
  localtime_r(&timestamp, &timeInfo);

  char dateBuffer[11];
  char timeBuffer[9];

  snprintf(
    dateBuffer,
    sizeof(dateBuffer),
    "%04d-%02d-%02d",
    timeInfo.tm_year + 1900,
    timeInfo.tm_mon + 1,
    timeInfo.tm_mday
  );

  snprintf(
    timeBuffer,
    sizeof(timeBuffer),
    "%02d:%02d:%02d",
    timeInfo.tm_hour,
    timeInfo.tm_min,
    timeInfo.tm_sec
  );

  dateText = String(dateBuffer);
  timeText = String(timeBuffer);

  return true;
}

bool TripStorage::appendTripRecord(
  const String &studentID,
  time_t outTime,
  time_t inTime,
  long durationSeconds,
  const char *status
) {

  if (!littleFSReady || !preferencesReady) {
    Serial.println("ERROR: Trip record was not saved; storage is unavailable.");
    return false;
  }

  String dateOut;
  String timeOut;
  String dateIn;
  String timeIn;

  getDateAndTimeForTimestamp(outTime, dateOut, timeOut);

  if (inTime != 0) {
    getDateAndTimeForTimestamp(inTime, dateIn, timeIn);
  }

  // Persist the next ID before appending. A power loss can leave a gap but
  // never permits a later record to reuse a written trip ID.
  uint32_t tripID = nextTripID;
  nextTripID++;
  preferences.putULong(PREF_NEXT_TRIP_ID, nextTripID);

  String durationText = (inTime == 0) ? "" : String(durationSeconds);

  String record =
    String(tripID) + "," +
    studentID + "," +
    dateOut + "," +
    timeOut + "," +
    timeIn + "," +
    durationText + "," +
    status + ",0";

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_APPEND);

  if (!tripLog) {
    Serial.println("ERROR: Could not open trip log for writing.");
    return false;
  }

  size_t bytesWritten = tripLog.println(record);
  tripLog.flush();
  tripLog.close();

  if (bytesWritten == 0) {
    Serial.println("ERROR: Trip record write failed.");
    return false;
  }

  Serial.print("Saved trip record: ");
  Serial.println(record);

  return true;
}

bool TripStorage::isLogReady() const {
  return littleFSReady;
}

uint32_t TripStorage::getTripRecordCount() {

  if (!littleFSReady) {
    return 0;
  }

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);

  if (!tripLog) {
    return 0;
  }

  uint32_t recordCount = 0;

  while (tripLog.available()) {
    String record = tripLog.readStringUntil('\n');
    record.trim();

    if (record.length() > 0) {
      recordCount++;
    }
  }

  tripLog.close();
  return recordCount;
}

uint32_t TripStorage::getTripRecordCountAfter(uint32_t afterTripID) {
  if (!littleFSReady) return 0;
  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);
  if (!tripLog) return 0;

  uint32_t recordCount = 0;
  while (tripLog.available()) {
    String record = tripLog.readStringUntil('\n');
    record.trim();
    uint32_t tripID;
    bool isSynced;
    if (record.length() > 0 &&
        parseTripRecord(record, tripID, isSynced) &&
        tripID > afterTripID) {
      recordCount++;
    }
  }

  tripLog.close();
  return recordCount;
}

uint32_t TripStorage::getUnsyncedTripRecordCount() {
  if (!littleFSReady) return 0;
  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);
  if (!tripLog) return 0;
  uint32_t recordCount = 0;
  while (tripLog.available()) {
    String record = tripLog.readStringUntil('\n');
    record.trim();
    uint32_t tripID;
    bool isSynced;
    if (record.length() > 0 && parseTripRecord(record, tripID, isSynced) && !isSynced) recordCount++;
  }
  tripLog.close();
  return recordCount;
}

uint32_t TripStorage::getLatestTripID() {

  if (!littleFSReady) {
    return 0;
  }

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);

  if (!tripLog) {
    return 0;
  }

  uint32_t latestTripID = 0;

  while (tripLog.available()) {
    String record = tripLog.readStringUntil('\n');
    record.trim();

    if (record.length() > 0) {
      int commaIndex = record.indexOf(',');
      String idText =
        (commaIndex >= 0) ? record.substring(0, commaIndex) : record;
      latestTripID = (uint32_t)idText.toInt();
    }
  }

  tripLog.close();
  return latestTripID;
}

bool TripStorage::parseTripRecord(
  const String &record,
  uint32_t &tripID,
  bool &isSynced
) {

  return TripRecordCodec::parse(record, tripID, isSynced);
}

bool TripStorage::getNextUnsyncedRecord(
  String &record,
  uint32_t &tripID
) {

  record = "";
  tripID = 0;

  if (!littleFSReady) {
    return false;
  }

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);

  if (!tripLog) {
    return false;
  }

  while (tripLog.available()) {

    String candidate = tripLog.readStringUntil('\n');
    candidate.trim();

    uint32_t candidateID;
    bool isSynced;

    if (
      candidate.length() > 0 &&
      parseTripRecord(candidate, candidateID, isSynced) &&
      !isSynced
    ) {
      record = candidate;
      tripID = candidateID;
      tripLog.close();
      return true;
    }
  }

  tripLog.close();
  return false;
}

bool TripStorage::getNextRecordAfter(
  uint32_t afterTripID,
  String &record,
  uint32_t &tripID
) {

  record = "";
  tripID = 0;

  if (!littleFSReady) {
    return false;
  }

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);

  if (!tripLog) {
    return false;
  }

  while (tripLog.available()) {

    String candidate = tripLog.readStringUntil('\n');
    candidate.trim();

    uint32_t candidateID;
    bool isSynced;

    if (
      candidate.length() > 0 &&
      parseTripRecord(candidate, candidateID, isSynced) &&
      candidateID > afterTripID
    ) {
      record = candidate;
      tripID = candidateID;
      tripLog.close();
      return true;
    }
  }

  tripLog.close();
  return false;
}

bool TripStorage::markTripSynced(uint32_t tripID) {

  if (!littleFSReady || tripID == 0) {
    return false;
  }

  File source = LittleFS.open(TRIP_LOG_PATH, FILE_READ);

  if (!source) {
    return false;
  }

  LittleFS.remove(TRIP_LOG_TEMP_PATH);
  File replacement = LittleFS.open(TRIP_LOG_TEMP_PATH, FILE_WRITE);

  if (!replacement) {
    source.close();
    return false;
  }

  bool found = false;

  while (source.available()) {

    String record = source.readStringUntil('\n');
    record.trim();

    uint32_t recordID;
    bool isSynced;

    if (
      !found &&
      parseTripRecord(record, recordID, isSynced) &&
      recordID == tripID
    ) {

      String updatedRecord;
      if (!TripRecordCodec::markSynced(record, updatedRecord)) {
        source.close();
        replacement.close();
        LittleFS.remove(TRIP_LOG_TEMP_PATH);
        return false;
      }
      record = updatedRecord;
      found = true;
    }

    if (record.length() > 0) {
      replacement.println(record);
    }
  }

  source.close();
  replacement.flush();
  replacement.close();

  if (!found) {
    LittleFS.remove(TRIP_LOG_TEMP_PATH);
    return false;
  }

  LittleFS.remove(TRIP_LOG_BACKUP_PATH);

  if (!LittleFS.rename(TRIP_LOG_PATH, TRIP_LOG_BACKUP_PATH)) {
    LittleFS.remove(TRIP_LOG_TEMP_PATH);
    return false;
  }

  if (!LittleFS.rename(TRIP_LOG_TEMP_PATH, TRIP_LOG_PATH)) {
    LittleFS.rename(TRIP_LOG_BACKUP_PATH, TRIP_LOG_PATH);
    return false;
  }

  LittleFS.remove(TRIP_LOG_BACKUP_PATH);
  return true;
}

void TripStorage::printTripLog() {

  if (!littleFSReady) {
    Serial.println("Trip log unavailable: LittleFS is not mounted.");
    return;
  }

  File tripLog = LittleFS.open(TRIP_LOG_PATH, FILE_READ);

  if (!tripLog) {
    Serial.println("Trip log is empty or could not be opened.");
    return;
  }

  Serial.println("--- TRIP LOG BEGIN ---");

  while (tripLog.available()) {
    Serial.write((uint8_t)tripLog.read());
  }

  tripLog.close();

  Serial.println();
  Serial.println("--- TRIP LOG END ---");
}
