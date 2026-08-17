#include <Adafruit_GFX.h>
#include <Adafruit_ST7735.h>
#include <SPI.h>
#include <Keypad.h>
#include "TripStorage.h"
#include "BluetoothSerial.h"
#include <time.h>
#include <sys/time.h>

#if !defined(CONFIG_BT_ENABLED) || !defined(CONFIG_BLUEDROID_ENABLED)
#error Bluetooth Classic is not enabled for this ESP32 board configuration.
#endif

#if !defined(CONFIG_BT_SPP_ENABLED)
#error Bluetooth Serial Port Profile is not available for this ESP32 board configuration.
#endif

// ======================================================
// SETTINGS
// ======================================================

const String CLOCK_CODE = "1234";
const String LOG_SUMMARY_CODE = "9999";
const char *BLUETOOTH_DEVICE_NAME = "Bathroom-Terminal";
const int MAX_BLUETOOTH_COMMAND_LENGTH = 48;

const int MAX_ID_LENGTH = 10;

// Hold * + # this long to reset current checkout
const unsigned long RESET_HOLD_MS = 2000;

// ======================================================
// TFT WIRING
// ======================================================

#define TFT_CS    5
#define TFT_RST   22
#define TFT_DC    21
#define TFT_MOSI  23
#define TFT_SCLK  18

Adafruit_ST7735 tft =
  Adafruit_ST7735(TFT_CS, TFT_DC, TFT_MOSI, TFT_SCLK, TFT_RST);

// ======================================================
// KEYPAD WIRING
// ======================================================

const byte ROWS = 4;
const byte COLS = 3;

char keys[ROWS][COLS] = {
  {'1', '2', '3'},
  {'4', '5', '6'},
  {'7', '8', '9'},
  {'*', '0', '#'}
};

byte rowPins[ROWS] = {32, 33, 25, 26};
byte colPins[COLS] = {27, 14, 13};

Keypad keypad = Keypad(
  makeKeymap(keys),
  rowPins,
  colPins,
  ROWS,
  COLS
);

// ======================================================
// BATHROOM STATE
// ======================================================

TripStorage tripStorage;
BluetoothSerial SerialBT;

String enteredID = "";
String currentOutID = "";

time_t checkoutTime = 0;

bool clockHasBeenSet = false;
bool bluetoothReady = false;
bool bluetoothWasConnected = false;
String bluetoothCommandBuffer = "";
bool bluetoothDiscardingInput = false;
bool bluetoothSyncInProgress = false;
bool bluetoothSyncAllRecords = false;
uint32_t bluetoothPendingTripID = 0;
uint32_t bluetoothLastStreamedTripID = 0;

// Used so clock only visually refreshes when minute changes
int lastDisplayedMinute = -1;

// ======================================================
// * + # RESET STATE
// ======================================================

bool starPressed = false;
bool hashPressed = false;

unsigned long resetHoldStarted = 0;

bool resetHoldActive = false;

// Stops * or # from firing individually after combo reset
bool suppressStarHash = false;

// ======================================================
// CLOCK SETUP STATE
// ======================================================

enum SetupStep {
  SET_MONTH,
  SET_DAY,
  SET_YEAR,
  SET_HOUR,
  SET_MINUTE,
  SET_AMPM
};

SetupStep setupStep;

bool setupMode = false;

String setupEntry = "";

int setupMonth = 0;
int setupDay = 0;
int setupYear = 0;
int setupHour = 0;
int setupMinute = 0;

bool setupPM = false;

// ======================================================
// DATE HELPERS
// ======================================================

bool isLeapYear(int year) {

  if (year % 400 == 0) return true;
  if (year % 100 == 0) return false;

  return (year % 4 == 0);
}

int daysInMonth(int month, int year) {

  switch (month) {

    case 1:
    case 3:
    case 5:
    case 7:
    case 8:
    case 10:
    case 12:
      return 31;

    case 4:
    case 6:
    case 9:
    case 11:
      return 30;

    case 2:
      return isLeapYear(year) ? 29 : 28;
  }

  return 31;
}

// ======================================================
// SET REAL SYSTEM CLOCK
// ======================================================

void setSystemClock24(
  int year,
  int month,
  int day,
  int hour24,
  int minute,
  int second
) {
  struct tm timeInfo = {};

  timeInfo.tm_year = year - 1900;
  timeInfo.tm_mon  = month - 1;
  timeInfo.tm_mday = day;

  timeInfo.tm_hour = hour24;
  timeInfo.tm_min  = minute;
  timeInfo.tm_sec  = second;

  // Treat entered time directly as classroom local time.
  setenv("TZ", "UTC0", 1);
  tzset();

  time_t newTime = mktime(&timeInfo);

  struct timeval now = {
    .tv_sec = newTime,
    .tv_usec = 0
  };

  settimeofday(&now, nullptr);

  clockHasBeenSet = true;

  // Force clock to update once after setting
  lastDisplayedMinute = -1;

  Serial.println("Clock updated.");
}

void setSystemClock(
  int year,
  int month,
  int day,
  int hour12,
  int minute,
  bool pm
) {

  int hour24 = hour12;

  if (pm) {

    if (hour12 != 12) {
      hour24 += 12;
    }

  } else if (hour12 == 12) {

    hour24 = 0;
  }

  setSystemClock24(
    year,
    month,
    day,
    hour24,
    minute,
    0
  );
}

// ======================================================
// TIME FORMATTING
// ======================================================

String getTimeString() {

  if (!clockHasBeenSet) {
    return "--:--";
  }

  time_t now;
  time(&now);

  struct tm timeInfo;
  localtime_r(&now, &timeInfo);

  int hour = timeInfo.tm_hour;

  bool pm = (hour >= 12);

  hour %= 12;

  if (hour == 0) {
    hour = 12;
  }

  char buffer[16];

  snprintf(
    buffer,
    sizeof(buffer),
    "%d:%02d %s",
    hour,
    timeInfo.tm_min,
    pm ? "PM" : "AM"
  );

  return String(buffer);
}

String getDateString() {

  if (!clockHasBeenSet) {
    return "--/--/----";
  }

  time_t now;
  time(&now);

  struct tm timeInfo;
  localtime_r(&now, &timeInfo);

  char buffer[16];

  snprintf(
    buffer,
    sizeof(buffer),
    "%02d/%02d/%04d",
    timeInfo.tm_mon + 1,
    timeInfo.tm_mday,
    timeInfo.tm_year + 1900
  );

  return String(buffer);
}

// ======================================================
// BLUETOOTH CLASSIC SERIAL (PHASE 3)
// ======================================================

void beginBluetooth() {

  bluetoothReady = SerialBT.begin(BLUETOOTH_DEVICE_NAME);

  if (!bluetoothReady) {
    Serial.println("ERROR: Bluetooth Classic could not start.");
    return;
  }

  // Use a conventional legacy PIN so computers that require pairing can
  // authenticate this headless terminal. Android serial clients also support it.
  SerialBT.setPin("1234", 4);

  Serial.print("Bluetooth ready as: ");
  Serial.println(BLUETOOTH_DEVICE_NAME);
}

void updateBluetoothConnection() {

  if (!bluetoothReady) {
    return;
  }

  bool bluetoothIsConnected = SerialBT.hasClient();

  if (bluetoothIsConnected == bluetoothWasConnected) {
    return;
  }

  bluetoothWasConnected = bluetoothIsConnected;

  if (bluetoothIsConnected) {

    Serial.println("Bluetooth client connected.");
    SerialBT.println("BATHROOM_TERMINAL_READY");
    SerialBT.println("Phase 3 Bluetooth transport connected.");

  } else {

    Serial.println("Bluetooth client disconnected.");
    bluetoothSyncInProgress = false;
    bluetoothSyncAllRecords = false;
    bluetoothPendingTripID = 0;
    bluetoothLastStreamedTripID = 0;
  }
}

void sendNextTripForBluetoothSync() {

  if (!bluetoothReady || !SerialBT.hasClient()) {
    bluetoothSyncInProgress = false;
    bluetoothPendingTripID = 0;
    return;
  }

  String record;
  uint32_t tripID;

  bool hasNextRecord = bluetoothSyncAllRecords
    ? tripStorage.getNextRecordAfter(
        bluetoothLastStreamedTripID,
        record,
        tripID
      )
    : tripStorage.getNextUnsyncedRecord(record, tripID);

  if (!hasNextRecord) {
    SerialBT.println("SYNC_END");
    bluetoothSyncInProgress = false;
    bluetoothSyncAllRecords = false;
    bluetoothPendingTripID = 0;
    bluetoothLastStreamedTripID = 0;
    return;
  }

  bluetoothPendingTripID = tripID;
  SerialBT.print("TRIP,");
  SerialBT.println(record);
}

void beginBluetoothSync(bool includeSyncedRecords = false) {

  if (!bluetoothReady || !SerialBT.hasClient()) {
    return;
  }

  bluetoothSyncInProgress = true;
  bluetoothSyncAllRecords = includeSyncedRecords;
  bluetoothPendingTripID = 0;
  bluetoothLastStreamedTripID = 0;
  SerialBT.println("SYNC_BEGIN");
  sendNextTripForBluetoothSync();
}

bool processBluetoothAcknowledgement(const String &command) {

  if (!command.startsWith("ACK,")) {
    return false;
  }

  String idText = command.substring(4);
  idText.trim();

  if (idText.length() == 0) {
    SerialBT.println("ACK_ERROR,INVALID_ID");
    return true;
  }

  for (unsigned int i = 0; i < idText.length(); i++) {
    if (idText.charAt(i) < '0' || idText.charAt(i) > '9') {
      SerialBT.println("ACK_ERROR,INVALID_ID");
      return true;
    }
  }

  uint32_t acknowledgedID = (uint32_t)idText.toInt();

  if (
    !bluetoothSyncInProgress ||
    bluetoothPendingTripID == 0 ||
    acknowledgedID != bluetoothPendingTripID
  ) {
    SerialBT.println("ACK_ERROR,UNEXPECTED_ID");
    return true;
  }

  if (!tripStorage.markTripSynced(acknowledgedID)) {
    SerialBT.println("ACK_ERROR,MARK_FAILED");
    bluetoothSyncInProgress = false;
    bluetoothPendingTripID = 0;
    return true;
  }

  Serial.print("Trip synced: ");
  Serial.println(acknowledgedID);

  if (bluetoothSyncAllRecords) {
    bluetoothLastStreamedTripID = acknowledgedID;
  }

  sendNextTripForBluetoothSync();
  return true;
}

void showBluetoothClockSync() {

  tft.fillScreen(ST77XX_GREEN);

  tft.setTextColor(ST77XX_BLACK);
  tft.setTextSize(2);
  tft.setCursor(10, 24);
  tft.println("CLOCK SYNCED");

  tft.setTextSize(1);
  tft.setCursor(10, 66);
  tft.println(getDateString());

  tft.setCursor(10, 82);
  tft.println(getTimeString());

  delay(1200);
}

bool applyBluetoothTimeCommand(const String &command) {

  int year;
  int month;
  int day;
  int hour;
  int minute;
  int second;
  char extraCharacter;

  int parsedValues = sscanf(
    command.c_str(),
    "TIME,%d-%d-%d,%d:%d:%d%c",
    &year,
    &month,
    &day,
    &hour,
    &minute,
    &second,
    &extraCharacter
  );

  if (
    parsedValues != 6 ||
    year < 2024 ||
    year > 2099 ||
    month < 1 ||
    month > 12 ||
    day < 1 ||
    day > daysInMonth(month, year) ||
    hour < 0 ||
    hour > 23 ||
    minute < 0 ||
    minute > 59 ||
    second < 0 ||
    second > 59
  ) {

    SerialBT.println("TIME_ACK,ERROR");
    return false;
  }

  setSystemClock24(year, month, day, hour, minute, second);
  SerialBT.println("TIME_ACK,OK");

  if (setupMode) {

    setupMode = false;
    setupEntry = "";
    enteredID = "";

    showBluetoothClockSync();
    drawIdleScreen();
  }

  return true;
}

void processBluetoothCommands() {

  if (!bluetoothReady) {
    return;
  }

  while (SerialBT.available()) {

    char received = (char)SerialBT.read();

    if (received == '\r') {
      continue;
    }

    if (received == '\n') {

      if (!bluetoothDiscardingInput && bluetoothCommandBuffer.length() > 0) {

        Serial.print("Bluetooth command: ");
        Serial.println(bluetoothCommandBuffer);

        if (bluetoothCommandBuffer.startsWith("TIME,")) {
          if (applyBluetoothTimeCommand(bluetoothCommandBuffer)) {
            beginBluetoothSync();
          }
        } else if (bluetoothCommandBuffer == "SYNC_START") {
          beginBluetoothSync();
        } else if (bluetoothCommandBuffer == "SYNC_ALL") {
          beginBluetoothSync(true);
        } else if (processBluetoothAcknowledgement(bluetoothCommandBuffer)) {
          // Acknowledgements are handled one record at a time.
        } else {
          SerialBT.println("ERROR,UNKNOWN_COMMAND");
        }
      }

      bluetoothCommandBuffer = "";
      bluetoothDiscardingInput = false;
      continue;
    }

    if (bluetoothDiscardingInput) {
      continue;
    }

    if (bluetoothCommandBuffer.length() >= MAX_BLUETOOTH_COMMAND_LENGTH) {
      bluetoothCommandBuffer = "";
      bluetoothDiscardingInput = true;
      SerialBT.println("ERROR,COMMAND_TOO_LONG");
      continue;
    }

    bluetoothCommandBuffer += received;
  }
}

// ======================================================
// PARTIAL REDRAW: STUDENT ID
// ======================================================

void drawIDEntry() {

  // ONLY redraw the student-ID area.
  tft.fillRect(
    8,
    78,
    145,
    27,
    ST77XX_BLACK
  );

  tft.setTextColor(ST77XX_CYAN);
  tft.setTextSize(2);
  tft.setCursor(10, 82);

  if (enteredID.length() == 0) {

    tft.print("_");

  } else {

    tft.print(enteredID);
  }
}

// ======================================================
// PARTIAL REDRAW: CLOCK
// ======================================================

void drawClock() {

  // ONLY redraw clock area.
  tft.fillRect(
    85,
    110,
    75,
    18,
    ST77XX_BLACK
  );

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(1);

  tft.setCursor(90, 114);
  tft.print(getTimeString());
}

// ======================================================
// ONLY VISUALLY UPDATE CLOCK ON MINUTE CHANGE
// ======================================================

void updateClockIfNeeded() {

  if (!clockHasBeenSet) {
    return;
  }

  time_t now;
  time(&now);

  struct tm timeInfo;
  localtime_r(&now, &timeInfo);

  if (timeInfo.tm_min != lastDisplayedMinute) {

    lastDisplayedMinute = timeInfo.tm_min;

    drawClock();
  }
}

// ======================================================
// MAIN BATHROOM SCREEN
// ======================================================

void drawIdleScreen() {

  tft.fillScreen(ST77XX_BLACK);
  tft.setTextWrap(false);

  // Title
  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 8);
  tft.println("BATHROOM");

  // Status
  if (currentOutID == "") {

    tft.setTextColor(ST77XX_GREEN);
    tft.setCursor(10, 35);

    tft.println("AVAILABLE");

  } else {

    tft.setTextColor(ST77XX_RED);
    tft.setCursor(10, 35);

    tft.println("OCCUPIED");

    tft.setTextColor(ST77XX_WHITE);
    tft.setTextSize(1);
    tft.setCursor(10, 54);
    tft.print("Student ID: ");
    tft.println(currentOutID);
  }

  // ID prompt
  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(1);

  tft.setCursor(10, 67);
  tft.println("Enter Student ID:");

  // Controls
  tft.setCursor(6, 114);
  tft.println("*Clr #Go");

  drawIDEntry();

  lastDisplayedMinute = -1;
  updateClockIfNeeded();
}

// ======================================================
// CHECKED OUT
// ======================================================

void showCheckedOut(String id) {

  tft.fillScreen(ST77XX_GREEN);

  tft.setTextColor(ST77XX_BLACK);
  tft.setTextSize(2);

  tft.setCursor(10, 20);
  tft.println("CHECKED");

  tft.setCursor(10, 44);
  tft.println("OUT");

  tft.setTextSize(1);

  tft.setCursor(10, 75);
  tft.print("ID: ");
  tft.println(id);

  tft.setCursor(10, 95);
  tft.print("Time: ");
  tft.println(getTimeString());

  delay(2000);
}

// ======================================================
// CHECKED IN
// ======================================================

void showCheckedIn(
  String id,
  unsigned long elapsedSeconds
) {

  tft.fillScreen(ST77XX_BLUE);

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 12);
  tft.println("CHECKED");

  tft.setCursor(10, 36);
  tft.println("IN");

  unsigned long hours =
    elapsedSeconds / 3600;

  unsigned long minutes =
    (elapsedSeconds % 3600) / 60;

  unsigned long seconds =
    elapsedSeconds % 60;

  tft.setTextSize(1);

  tft.setCursor(10, 68);
  tft.println("Time away:");

  tft.setTextSize(2);

  tft.setCursor(10, 85);

  if (hours > 0) {

    tft.print(hours);
    tft.print("h ");
  }

  tft.print(minutes);
  tft.print("m ");

  if (seconds < 10) {
    tft.print("0");
  }

  tft.print(seconds);
  tft.print("s");

  delay(3000);
}

// ======================================================
// WRONG STUDENT WHILE PASS OCCUPIED
// ======================================================

void showWrongID() {

  tft.fillScreen(ST77XX_RED);

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 20);
  tft.println("PASS");

  tft.setCursor(10, 44);
  tft.println("OCCUPIED");

  tft.setTextSize(1);

  tft.setCursor(10, 80);
  tft.println("Waiting for current");

  tft.setCursor(10, 95);
  tft.println("student to return.");

  delay(2000);
}

// ======================================================
// EMPTY ID
// ======================================================

void showEnterID() {

  tft.fillScreen(ST77XX_RED);

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 30);
  tft.println("ENTER ID");

  tft.setTextSize(1);

  tft.setCursor(10, 72);
  tft.println("Type your student ID");

  tft.setCursor(10, 87);
  tft.println("before submitting.");

  delay(1500);
}

// ======================================================
// LOCAL STORAGE ERROR
// ======================================================

void showStorageError() {

  tft.fillScreen(ST77XX_RED);

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 20);
  tft.println("NOT SAVED");

  tft.setTextSize(1);

  tft.setCursor(10, 62);
  tft.println("Trip log unavailable.");

  tft.setCursor(10, 77);
  tft.println("Pass remains occupied.");

  delay(2200);
}

// ======================================================
// TRIP LOG SUMMARY (ADMIN: 9999#)
// ======================================================

void showTripLogSummary() {

  tft.fillScreen(ST77XX_BLACK);

  tft.setTextColor(ST77XX_YELLOW);
  tft.setTextSize(2);
  tft.setCursor(10, 12);
  tft.println("TRIP LOG");

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(1);

  if (!tripStorage.isLogReady()) {

    tft.setCursor(10, 58);
    tft.println("Storage unavailable");

  } else {

    uint32_t recordCount = tripStorage.getTripRecordCount();
    uint32_t latestTripID = tripStorage.getLatestTripID();

    tft.setCursor(10, 52);
    tft.print("Saved records: ");
    tft.println(recordCount);

    tft.setCursor(10, 70);

    if (latestTripID == 0) {
      tft.println("No trips recorded yet.");
    } else {
      tft.print("Latest trip ID: ");
      tft.println(latestTripID);
    }
  }

  tft.setCursor(10, 110);
  tft.println("Returning...");

  delay(2500);
}

// ======================================================
// FORCE RESET CURRENT CHECKOUT
// ======================================================

void resetCurrentCheckout() {

  String oldID = currentOutID;
  time_t oldCheckoutTime = checkoutTime;

  // Preserve the incomplete trip before allowing the pass to be cleared.
  if (!tripStorage.appendTripRecord(
    oldID,
    oldCheckoutTime,
    0,
    0,
    "MANUAL_RESET"
  )) {

    showStorageError();
    drawIdleScreen();
    return;
  }

  currentOutID = "";
  checkoutTime = 0;
  enteredID = "";

  tripStorage.clearActiveCheckout();

  tft.fillScreen(ST77XX_YELLOW);

  tft.setTextColor(ST77XX_BLACK);
  tft.setTextSize(2);

  tft.setCursor(10, 22);
  tft.println("PASS");

  tft.setCursor(10, 46);
  tft.println("RESET");

  tft.setTextSize(1);

  tft.setCursor(10, 80);

  tft.print("Cleared ID: ");
  tft.println(oldID);

  Serial.print("Manual reset. Cleared ID: ");
  Serial.println(oldID);

  delay(1800);

  drawIdleScreen();
}

// ======================================================
// CLOCK SETUP INPUT FIELD
// ======================================================

void drawSetupEntry() {

  // ONLY redraw setup-input area.
  tft.fillRect(
    7,
    58,
    146,
    35,
    ST77XX_BLACK
  );

  tft.setTextColor(ST77XX_CYAN);
  tft.setTextSize(2);

  tft.setCursor(8, 63);

  if (setupEntry.length() == 0) {

    tft.print("_");

  } else {

    tft.print(setupEntry);
  }
}

// ======================================================
// CLOCK SETUP SCREEN
// ======================================================

void drawSetupScreen() {

  // Full redraw is appropriate here because we've moved
  // to a DIFFERENT setup field/screen.
  tft.fillScreen(ST77XX_BLACK);

  tft.setTextWrap(false);

  tft.setTextColor(ST77XX_YELLOW);
  tft.setTextSize(2);

  tft.setCursor(8, 8);
  tft.println("SET CLOCK");

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(1);

  tft.setCursor(8, 40);

  switch (setupStep) {

    case SET_MONTH:
      tft.println("Enter MONTH (1-12)");
      break;

    case SET_DAY:
      tft.println("Enter DAY");
      break;

    case SET_YEAR:
      tft.println("Enter YEAR (YYYY)");
      break;

    case SET_HOUR:
      tft.println("Enter HOUR (1-12)");
      break;

    case SET_MINUTE:
      tft.println("Enter MINUTE (0-59)");
      break;

    case SET_AMPM:
      tft.println("1 = AM     2 = PM");
      break;
  }

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(1);

  tft.setCursor(8, 105);
  tft.println("* Clear");

  tft.setCursor(8, 117);
  tft.println("# Next");

  drawSetupEntry();
}

// ======================================================
// INVALID CLOCK VALUE
// ======================================================

void showInvalidValue(String message) {

  tft.fillScreen(ST77XX_RED);

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 25);
  tft.println("INVALID");

  tft.setTextSize(1);

  tft.setCursor(10, 70);
  tft.println(message);

  delay(1200);

  setupEntry = "";

  drawSetupScreen();
}

// ======================================================
// START CLOCK SETUP
// ======================================================

void beginClockSetup() {

  setupMode = true;

  setupStep = SET_MONTH;
  setupEntry = "";

  setupMonth = 0;
  setupDay = 0;
  setupYear = 0;

  setupHour = 0;
  setupMinute = 0;

  setupPM = false;

  drawSetupScreen();

  Serial.println("Clock setup started.");
}

// ======================================================
// FINISH CLOCK SETUP
// ======================================================

void finishClockSetup() {

  setSystemClock(
    setupYear,
    setupMonth,
    setupDay,
    setupHour,
    setupMinute,
    setupPM
  );

  setupMode = false;

  setupEntry = "";

  tft.fillScreen(ST77XX_GREEN);

  tft.setTextColor(ST77XX_BLACK);
  tft.setTextSize(2);

  tft.setCursor(10, 18);
  tft.println("CLOCK SET");

  tft.setTextSize(1);

  tft.setCursor(10, 60);
  tft.println(getDateString());

  tft.setCursor(10, 80);
  tft.println(getTimeString());

  delay(1800);

  enteredID = "";

  drawIdleScreen();
}

// ======================================================
// PROCESS CLOCK FIELD
// ======================================================

void processSetupEntry() {

  if (setupEntry.length() == 0) {
    return;
  }

  int value = setupEntry.toInt();

  switch (setupStep) {

    // --------------------------------------------------
    // MONTH
    // --------------------------------------------------

    case SET_MONTH:

      if (value < 1 || value > 12) {

        showInvalidValue(
          "Month must be 1-12"
        );

        return;
      }

      setupMonth = value;

      setupStep = SET_DAY;
      setupEntry = "";

      drawSetupScreen();

      break;

    // --------------------------------------------------
    // DAY
    // --------------------------------------------------

    case SET_DAY:

      if (
        value < 1 ||
        value > 31 ||
        (setupMonth == 2 && value > 29) ||
        (
          (
            setupMonth == 4 ||
            setupMonth == 6 ||
            setupMonth == 9 ||
            setupMonth == 11
          )
          &&
          value > 30
        )
      ) {

        showInvalidValue("Invalid day");

        return;
      }

      setupDay = value;

      setupStep = SET_YEAR;
      setupEntry = "";

      drawSetupScreen();

      break;

    // --------------------------------------------------
    // YEAR
    // --------------------------------------------------

    case SET_YEAR:

      if (value < 2024 || value > 2099) {

        showInvalidValue(
          "Use YYYY: 2024-2099"
        );

        return;
      }

      setupYear = value;

      if (
        setupDay >
        daysInMonth(
          setupMonth,
          setupYear
        )
      ) {

        showInvalidValue(
          "Date does not exist"
        );

        setupStep = SET_DAY;
        setupEntry = "";

        return;
      }

      setupStep = SET_HOUR;
      setupEntry = "";

      drawSetupScreen();

      break;

    // --------------------------------------------------
    // HOUR
    // --------------------------------------------------

    case SET_HOUR:

      if (value < 1 || value > 12) {

        showInvalidValue(
          "Hour must be 1-12"
        );

        return;
      }

      setupHour = value;

      setupStep = SET_MINUTE;
      setupEntry = "";

      drawSetupScreen();

      break;

    // --------------------------------------------------
    // MINUTE
    // --------------------------------------------------

    case SET_MINUTE:

      if (value < 0 || value > 59) {

        showInvalidValue(
          "Minute must be 0-59"
        );

        return;
      }

      setupMinute = value;

      setupStep = SET_AMPM;
      setupEntry = "";

      drawSetupScreen();

      break;

    // --------------------------------------------------
    // AM / PM
    // --------------------------------------------------

    case SET_AMPM:

      if (value != 1 && value != 2) {

        showInvalidValue(
          "1 = AM, 2 = PM"
        );

        return;
      }

      setupPM = (value == 2);

      finishClockSetup();

      break;
  }
}

// ======================================================
// CLOCK SETUP KEY HANDLING
// ======================================================

void handleSetupKey(char key) {

  if (key >= '0' && key <= '9') {

    int maxLength = 2;

    if (setupStep == SET_YEAR) {
      maxLength = 4;
    }

    if (setupStep == SET_AMPM) {
      maxLength = 1;
    }

    if (setupEntry.length() < maxLength) {

      setupEntry += key;

      // Partial redraw only
      drawSetupEntry();
    }

    return;
  }

  if (key == '*') {

    setupEntry = "";

    // Partial redraw only
    drawSetupEntry();

    return;
  }

  if (key == '#') {

    processSetupEntry();

    return;
  }
}

// ======================================================
// NORMAL SUBMIT LOGIC
// ======================================================

void submitID() {

  if (enteredID.length() == 0) {

    showEnterID();

    drawIdleScreen();

    return;
  }

  // --------------------------------------------------
  // CLOCK ADMIN CODE
  // --------------------------------------------------

  if (enteredID == CLOCK_CODE) {

    Serial.println(
      "Clock admin code accepted."
    );

    enteredID = "";

    beginClockSetup();

    return;
  }

  // --------------------------------------------------
  // LOG SUMMARY ADMIN CODE
  // --------------------------------------------------

  if (enteredID == LOG_SUMMARY_CODE) {

    Serial.println("Trip log summary requested.");

    enteredID = "";
    showTripLogSummary();
    drawIdleScreen();

    return;
  }

  Serial.print("Submitted ID: ");
  Serial.println(enteredID);

  // --------------------------------------------------
  // CHECK OUT
  // --------------------------------------------------

  if (currentOutID == "") {

    currentOutID = enteredID;

    time(&checkoutTime);

    // Save immediately. A power loss after this point will restore OCCUPIED
    // once the clock has been set again on the next boot.
    tripStorage.saveActiveCheckout(currentOutID, checkoutTime);

    Serial.print("CHECK OUT: ");
    Serial.println(currentOutID);

    Serial.print("Date: ");
    Serial.println(getDateString());

    Serial.print("Time: ");
    Serial.println(getTimeString());

    showCheckedOut(currentOutID);

    enteredID = "";

    drawIdleScreen();

    return;
  }

  // --------------------------------------------------
  // CHECK IN
  // --------------------------------------------------

  if (enteredID == currentOutID) {

    time_t checkinTime;

    time(&checkinTime);

    long elapsedSeconds =
      (long)difftime(
        checkinTime,
        checkoutTime
      );

    if (elapsedSeconds < 0) {
      elapsedSeconds = 0;
    }

    Serial.print("CHECK IN: ");
    Serial.println(currentOutID);

    Serial.print("Date: ");
    Serial.println(getDateString());

    Serial.print("Time: ");
    Serial.println(getTimeString());

    Serial.print("Duration: ");
    Serial.print(elapsedSeconds);
    Serial.println(" seconds");

    if (!tripStorage.appendTripRecord(
      currentOutID,
      checkoutTime,
      checkinTime,
      elapsedSeconds,
      "COMPLETE"
    )) {

      enteredID = "";
      showStorageError();
      drawIdleScreen();
      return;
    }

    showCheckedIn(
      currentOutID,
      elapsedSeconds
    );

    currentOutID = "";
    checkoutTime = 0;

    // The checkout is complete, so it must not be restored after a reboot.
    tripStorage.clearActiveCheckout();

    enteredID = "";

    drawIdleScreen();

    return;
  }

  // --------------------------------------------------
  // SOMEBODY ELSE IS OUT
  // --------------------------------------------------

  Serial.print("Rejected ID: ");
  Serial.println(enteredID);

  showWrongID();

  enteredID = "";

  drawIdleScreen();
}

// ======================================================
// HANDLE NORMAL NUMBER KEY
// ======================================================

void handleNormalNumber(char key) {

  if (enteredID.length() < MAX_ID_LENGTH) {

    enteredID += key;

    // Partial redraw only
    drawIDEntry();
  }
}

// ======================================================
// HANDLE SINGLE * RELEASE
// ======================================================

void handleSingleStar() {

  enteredID = "";

  // Partial redraw only
  drawIDEntry();
}

// ======================================================
// HANDLE SINGLE # RELEASE
// ======================================================

void handleSingleHash() {

  submitID();
}

// ======================================================
// SERIAL DIAGNOSTIC COMMANDS
// ======================================================

void processSerialCommands() {

  while (Serial.available()) {

    char command = (char)Serial.read();

    if (command == 'p' || command == 'P') {
      tripStorage.printTripLog();
    }
  }
}

// ======================================================
// MULTI-KEY PROCESSING
// ======================================================

void processKeypad() {

  if (!keypad.getKeys()) {
    return;
  }

  for (int i = 0; i < LIST_MAX; i++) {

    if (!keypad.key[i].stateChanged) {
      continue;
    }

    char key = keypad.key[i].kchar;

    KeyState state =
      keypad.key[i].kstate;

    // ==================================================
    // CLOCK SETUP MODE
    //
    // During clock setup we do NOT use the *+# reset
    // shortcut. Keys behave normally.
    // ==================================================

    if (setupMode) {

      if (state == PRESSED) {

        handleSetupKey(key);
      }

      continue;
    }

    // ==================================================
    // NORMAL MODE: NUMBER KEYS
    // ==================================================

    if (key >= '0' && key <= '9') {

      if (state == PRESSED) {

        Serial.print("Key pressed: ");
        Serial.println(key);

        handleNormalNumber(key);
      }

      continue;
    }

    // ==================================================
    // TRACK *
    // ==================================================

    if (key == '*') {

      if (state == PRESSED) {

        starPressed = true;

      } else if (state == RELEASED) {

        starPressed = false;

        // If this was NOT part of a combo,
        // treat it as a normal Clear press.
        if (!suppressStarHash) {

          handleSingleStar();
        }
      }
    }

    // ==================================================
    // TRACK #
    // ==================================================

    if (key == '#') {

      if (state == PRESSED) {

        hashPressed = true;

      } else if (state == RELEASED) {

        hashPressed = false;

        // If this was NOT part of a combo,
        // treat it as normal Submit.
        if (!suppressStarHash) {

          handleSingleHash();
        }
      }
    }
  }
}

// ======================================================
// CHECK * + # HOLD
// ======================================================

void checkResetCombo() {

  if (setupMode) {
    return;
  }

  // --------------------------------------------------
  // BOTH KEYS CURRENTLY HELD
  // --------------------------------------------------

  if (starPressed && hashPressed) {

    // Only allow reset when someone is actually out
    if (currentOutID == "") {

      resetHoldStarted = 0;
      resetHoldActive = false;

      return;
    }

    // Start timer
    if (!resetHoldActive) {

      resetHoldActive = true;

      resetHoldStarted = millis();

      Serial.println(
        "* + # detected. Hold to reset..."
      );
    }

    // Held long enough
    if (
      millis() - resetHoldStarted >=
      RESET_HOLD_MS
    ) {

      suppressStarHash = true;

      resetHoldActive = false;
      resetHoldStarted = 0;

      resetCurrentCheckout();
    }

    return;
  }

  // --------------------------------------------------
  // COMBO NO LONGER HELD
  // --------------------------------------------------

  resetHoldActive = false;
  resetHoldStarted = 0;

  // After a successful combo, wait until BOTH keys
  // have been released before normal * / # behavior
  // becomes active again.
  if (
    suppressStarHash &&
    !starPressed &&
    !hashPressed
  ) {

    suppressStarHash = false;
  }
}

// ======================================================
// SETUP
// ======================================================

void setup() {

  Serial.begin(115200);

  delay(500);

  Serial.println();
  Serial.println(
    "Bathroom Terminal Starting..."
  );

  beginBluetooth();

  // Storage owns NVS and LittleFS, then restores an active pass before clock
  // setup. The display is intentionally deferred until the clock is set.
  tripStorage.begin();
  tripStorage.loadActiveCheckout(currentOutID, checkoutTime);

  Serial.println("Serial command: p = print trip log");

  // Allow the Keypad library to track multiple keys
  keypad.setDebounceTime(20);
  keypad.setHoldTime(500);

  // Initialize TFT
  tft.initR(INITR_BLACKTAB);

  tft.setRotation(1);

  tft.fillScreen(ST77XX_BLACK);

  tft.setTextColor(ST77XX_WHITE);
  tft.setTextSize(2);

  tft.setCursor(10, 35);
  tft.println("STARTING");

  delay(800);

  // Until Bluetooth auto-time is added,
  // every true startup asks for date/time.
  beginClockSetup();
}

// ======================================================
// MAIN LOOP
// ======================================================

void loop() {

  // Diagnostic only: send p in Serial Monitor to print local trip records.
  processSerialCommands();

  // Read keypad, including simultaneous keys
  processKeypad();

  // Bluetooth is passive in Phase 3; it must never block student workflow.
  updateBluetoothConnection();
  processBluetoothCommands();

  if (setupMode) {
    return;
  }

  // Detect/measure * + # combo
  checkResetCombo();

  // Only visually update clock when minute changes
  updateClockIfNeeded();
}
