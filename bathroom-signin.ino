#include <Adafruit_GFX.h>
#include <Adafruit_ST7735.h>
#include <SPI.h>
#include <Keypad.h>
#include "ArduinoKeypadPort.h"
#include "AppTypes.h"
#include "BluetoothSync.h"
#include "ClockService.h"
#include "Config.h"
#include "KeypadController.h"
#include "MonotonicClock.h"
#include "St7735DisplayPort.h"
#include "TerminalDisplay.h"
#include "TerminalController.h"
#include "TimeProvider.h"
#include "TripStorage.h"
#include <time.h>
#include <sys/time.h>

#if !defined(CONFIG_BT_ENABLED) || !defined(CONFIG_BLUEDROID_ENABLED)
#error Bluetooth Classic is not enabled for this ESP32 board configuration.
#endif

#if !defined(CONFIG_BT_SPP_ENABLED)
#error Bluetooth Serial Port Profile is not available for this ESP32 board configuration.
#endif

Adafruit_ST7735 tft =
  Adafruit_ST7735(TFT_CS, TFT_DC, TFT_MOSI, TFT_SCLK, TFT_RST);
St7735DisplayPort st7735Display(tft);
TerminalDisplay terminalDisplay(st7735Display);

char keys[KEYPAD_ROWS][KEYPAD_COLS] = {
  {'1', '2', '3'},
  {'4', '5', '6'},
  {'7', '8', '9'},
  {'*', '0', '#'}
};

byte rowPins[KEYPAD_ROWS] = {32, 33, 25, 26};
byte colPins[KEYPAD_COLS] = {27, 14, 13};

Keypad keypad = Keypad(
  makeKeymap(keys),
  rowPins,
  colPins,
  KEYPAD_ROWS,
  KEYPAD_COLS
);
ArduinoKeypadPort arduinoKeypad(keypad);
ArduinoMonotonicClock monotonicClock;

// ======================================================
// BATHROOM STATE
// ======================================================

TripStorage tripStorage;
SystemTimeProvider systemTime;
TerminalController terminal(tripStorage, systemTime);
ClockService terminalClock;

void setSystemClock24(
  int year,
  int month,
  int day,
  int hour,
  int minute,
  int second
);
void handleBluetoothClockSet();

BluetoothSync bluetoothSync(
  tripStorage,
  setSystemClock24,
  handleBluetoothClockSet
);

String enteredID = "";

// Used so clock only visually refreshes when minute changes
int lastDisplayedMinute = -1;

// ======================================================
// CLOCK SETUP STATE
// ======================================================

ClockSetupStep setupStep;

bool setupMode = false;

String setupEntry = "";

int setupMonth = 0;
int setupDay = 0;
int setupYear = 0;
int setupHour = 0;
int setupMinute = 0;

bool setupPM = false;

void handleSetupKey(char key);
void handleNormalNumber(char key);
void handleSingleStar();
void handleSingleHash();
void resetCurrentCheckout();

bool isSetupMode() {
  return setupMode;
}

bool isResetAllowed() {
  return terminal.hasActivePass();
}

KeypadController keypadController(
  arduinoKeypad,
  monotonicClock,
  isSetupMode,
  isResetAllowed,
  handleSetupKey,
  handleNormalNumber,
  handleSingleStar,
  handleSingleHash,
  resetCurrentCheckout
);

void setSystemClock24(
  int year,
  int month,
  int day,
  int hour,
  int minute,
  int second
) {
  terminalClock.set24Hour(year, month, day, hour, minute, second);
  lastDisplayedMinute = -1;
}

void setSystemClock(
  int year,
  int month,
  int day,
  int hour,
  int minute,
  bool pm
) {
  terminalClock.set12Hour(year, month, day, hour, minute, pm);
  lastDisplayedMinute = -1;
}

String getTimeString() {
  return terminalClock.timeString();
}

String getDateString() {
  return terminalClock.dateString();
}

int daysInMonth(int month, int year) {
  return ClockService::daysInMonth(month, year);
}

// Bluetooth Classic SPP transport lives in BluetoothSync.cpp.
void showBluetoothClockSync() {
  terminalDisplay.showBluetoothClockSynced(getDateString(), getTimeString());
}

void handleBluetoothClockSet() {

  if (!setupMode) {
    return;
  }

  setupMode = false;
  setupEntry = "";
  enteredID = "";

  showBluetoothClockSync();
  drawIdleScreen();
}

// ======================================================
// PARTIAL REDRAW: STUDENT ID
// ======================================================

void drawIDEntry() {
  terminalDisplay.drawIdEntry(enteredID);
}

void drawClock() {
  terminalDisplay.drawClock(getTimeString());
}

void updateClockIfNeeded() {
  if (!terminalClock.isSet()) {
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
  terminalDisplay.drawIdleScreen(terminal.activeId(), enteredID);
  lastDisplayedMinute = -1;
  updateClockIfNeeded();
}

// ======================================================
// CHECKED OUT
// ======================================================

void showCheckedOut(const String &id) {
  terminalDisplay.showCheckedOut(id, getTimeString());
}

void showCheckedIn(const String &, unsigned long elapsedSeconds) {
  terminalDisplay.showCheckedIn(elapsedSeconds);
}

void showWrongID() {
  terminalDisplay.showPassOccupied();
}

void showEnterID() {
  terminalDisplay.showEnterId();
}

void showStorageError() {
  terminalDisplay.showStorageError();
}

void showTripLogSummary() {
  terminalDisplay.showTripLogSummary(
    tripStorage.isLogReady(),
    tripStorage.getTripRecordCount(),
    tripStorage.getLatestTripID()
  );
}
void resetCurrentCheckout() {
  String oldID;
  if (!terminal.resetActivePass(oldID)) {
    showStorageError();
    drawIdleScreen();
    return;
  }

  enteredID = "";

  terminalDisplay.showManualReset(oldID);

  Serial.print("Manual reset. Cleared ID: ");
  Serial.println(oldID);

  drawIdleScreen();
}

// ======================================================
// CLOCK SETUP INPUT FIELD
// ======================================================

void drawSetupEntry() {
  terminalDisplay.drawClockSetupEntry(setupEntry);
}

void drawSetupScreen() {
  terminalDisplay.drawClockSetupScreen(setupStep, setupEntry);
}

void showInvalidValue(const String &message) {
  terminalDisplay.showInvalidClockValue(message);
  setupEntry = "";
  drawSetupScreen();
}
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

  terminalDisplay.showClockSet(getDateString(), getTimeString());

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
  const String submittedID = enteredID;
  const TerminalActionResult result = terminal.submit(submittedID);

  switch (result.action) {
    case TerminalAction::EmptyId:
      showEnterID();
      drawIdleScreen();
      return;

    case TerminalAction::StartClockSetup:
      Serial.println("Clock admin code accepted.");
      enteredID = "";
      beginClockSetup();
      return;

    case TerminalAction::ShowTripLog:
      Serial.println("Trip log summary requested.");
      enteredID = "";
      showTripLogSummary();
      drawIdleScreen();
      return;

    case TerminalAction::CheckedOut:
      Serial.print("CHECK OUT: ");
      Serial.println(result.id);
      Serial.print("Date: ");
      Serial.println(getDateString());
      Serial.print("Time: ");
      Serial.println(getTimeString());
      showCheckedOut(result.id);
      enteredID = "";
      drawIdleScreen();
      return;

    case TerminalAction::CheckedIn:
      Serial.print("CHECK IN: ");
      Serial.println(result.id);
      Serial.print("Date: ");
      Serial.println(getDateString());
      Serial.print("Time: ");
      Serial.println(getTimeString());
      Serial.print("Duration: ");
      Serial.print(result.elapsedSeconds);
      Serial.println(" seconds");
      showCheckedIn(result.id, result.elapsedSeconds);
      enteredID = "";
      drawIdleScreen();
      return;

    case TerminalAction::StorageError:
      enteredID = "";
      showStorageError();
      drawIdleScreen();
      return;

    case TerminalAction::PassOccupied:
      Serial.print("Rejected ID: ");
      Serial.println(submittedID);
      showWrongID();
      enteredID = "";
      drawIdleScreen();
      return;
  }
}
void handleNormalNumber(char key) {

  if (enteredID.length() < MAX_ID_LENGTH) {
    Serial.print("Key pressed: ");
    Serial.println(key);

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

// Keypad event interpretation and the * + # hold gesture live in KeypadController.cpp.

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

  bluetoothSync.begin();

  // Storage owns NVS and LittleFS, then restores an active pass before clock
  // setup. The display is intentionally deferred until the clock is set.
  tripStorage.begin();
  terminal.restoreActivePass();

  Serial.println("Serial command: p = print trip log");

  keypadController.begin();

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

  keypadController.poll();

  // Bluetooth is passive in Phase 3; it must never block student workflow.
  bluetoothSync.poll();

  if (setupMode) {
    return;
  }

  // Only visually update clock when minute changes
  updateClockIfNeeded();
}
