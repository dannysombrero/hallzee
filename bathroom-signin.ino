#include <Adafruit_GFX.h>
#include <Adafruit_ST7735.h>
#include <SPI.h>
#include <Keypad.h>
#include "ArduinoKeypadPort.h"
#include "ArduinoBluetoothSerialPort.h"
#include "AppTypes.h"
#include "BluetoothSync.h"
#include "ClockService.h"
#include "Config.h"
#include "KeypadController.h"
#include "MonotonicClock.h"
#include "St7735DisplayPort.h"
#include "StudentIdPolicy.h"
#include "TerminalIdentity.h"
#include "TerminalSecurity.h"
#include "TerminalDisplay.h"
#include "TerminalController.h"
#include "TimeProvider.h"
#include "TripStorage.h"
#include <time.h>
#include <sys/time.h>

#if !defined(CONFIG_BT_ENABLED) || !defined(CONFIG_BLUEDROID_ENABLED)
#error Bluetooth BLE is not enabled for this ESP32 board configuration.
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
// HALLZEE STATE
// ======================================================

TripStorage tripStorage;
ArduinoBluetoothSerialPort bluetoothSerial;
SystemTimeProvider systemTime;
TerminalController terminal(tripStorage, systemTime);
ClockService terminalClock;
TerminalIdentity terminalIdentity;
TerminalSecurity terminalSecurity(terminalIdentity);

void setSystemClock24(
  int year,
  int month,
  int day,
  int hour,
  int minute,
  int second
);
void handleBluetoothClockSet();
bool getActivePassState(String &activeId, uint32_t &checkoutEpoch);
uint8_t getActivePassStates(ActiveCheckout *checkouts, uint8_t maximum);
bool manualCheckInFromDesktop(const String &studentId);
bool setActivePassCapacity(uint8_t capacity);
bool isPairingAllowed();
void startPairingMode();
bool isOwnerResetAllowed();
void resetOwnerFromKeypad();

void drawStartupLogo();

BluetoothSync bluetoothSync(
  tripStorage,
  bluetoothSerial,
  setSystemClock24,
  handleBluetoothClockSet,
  getActivePassState,
  manualCheckInFromDesktop,
  getActivePassStates,
  setActivePassCapacity,
  &terminalIdentity,
  &terminalSecurity
);

String enteredID = "";
String serialCommandBuffer = "";

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
void drawIdleScreen();

bool pairingUiActive = false;

bool isPairingAllowed() {
  return !terminal.hasActivePass() && !terminalSecurity.hasOwner();
}

bool isOwnerResetAllowed() {
  return !terminal.hasActivePass() && terminalSecurity.hasOwner();
}

void resetOwnerFromKeypad() {
  bluetoothSerial.disconnectClient();
  if (terminalSecurity.resetOwner() && bluetoothSerial.clearBondedDevices()) {
    // Clock setup can be active immediately after boot. Leave setup mode so
    // the reset result is not stranded on the setup screen and the terminal
    // can return to normal operation after the owner is cleared.
    setupMode = false;
    setupEntry = "";
    terminalDisplay.showOwnerReset();
    drawIdleScreen();
  } else {
    terminalDisplay.showPairingError("RESET FAILED");
    drawIdleScreen();
  }
}

void startPairingMode() {
  if (!isPairingAllowed() ||
      !terminalSecurity.startClaimMode(monotonicClock.milliseconds())) {
    return;
  }
  bluetoothSerial.disconnectClient();
  if (!bluetoothSerial.clearBondedDevices()) {
    terminalSecurity.stopClaimMode();
    terminalDisplay.showPairingError("PAIR RESET FAILED");
    drawIdleScreen();
    return;
  }
  bluetoothSerial.setPairingPasskey(terminalSecurity.pairingPasskey());
  pairingUiActive = true;
  terminalDisplay.showPairing(
    terminalIdentity.terminalSuffix(),
    terminalSecurity.pairingPasskey()
  );
}

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
  resetCurrentCheckout,
  isPairingAllowed,
  startPairingMode,
  isOwnerResetAllowed,
  resetOwnerFromKeypad
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

// BLE transport lives in BluetoothSync.cpp.
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
// MAIN HALLZEE SCREEN
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
bool getActivePassState(String &activeId, uint32_t &checkoutEpoch) {
  if (terminal.hasActivePass()) {
    activeId = terminal.activeId();
    checkoutEpoch = static_cast<uint32_t>(terminal.activeCheckoutTime());
    return true;
  }
  return false;
}

uint8_t getActivePassStates(ActiveCheckout *checkouts, uint8_t maximum) {
  return terminal.copyActivePasses(checkouts, maximum);
}

bool manualCheckInFromDesktop(const String &requestedId) {
  const String oldestId = terminal.activeId();
  String studentId;
  unsigned long elapsedSeconds = 0;
  if (!terminal.manualCheckIn(requestedId, studentId, elapsedSeconds)) return false;

  bluetoothSync.notifyCheckin(studentId, elapsedSeconds);
  String completedRecord;
  uint32_t completedTripID = 0;
  if (tripStorage.getLatestTripRecord(completedRecord, completedTripID)) {
    bluetoothSync.notifyCompletedTrip(completedRecord);
  }
  if (studentId == oldestId && terminal.hasActivePass()) {
    bluetoothSync.notifyCheckout(terminal.activeId(), static_cast<uint32_t>(terminal.activeCheckoutTime()));
  }
  showCheckedIn(studentId, elapsedSeconds);
  enteredID = "";
  drawIdleScreen();
  return true;
}

bool setActivePassCapacity(uint8_t capacity) {
  return terminal.setCapacity(capacity) && tripStorage.setMaxActivePasses(capacity);
}

void resetCurrentCheckout() {
  String oldID;
  if (!terminal.resetActivePass(oldID)) {
    showStorageError();
    drawIdleScreen();
    return;
  }

  enteredID = "";

  bluetoothSync.notifyReset(oldID, 0);

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
  const uint8_t currentIdLimit = tripStorage.getMaxStudentIdLength();
  if (!isStudentIdWithinLimit(submittedID, currentIdLimit)) {
    enteredID = "";
    terminalDisplay.showStudentIdTooLong(currentIdLimit);
    drawIdleScreen();
    return;
  }

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
      bluetoothSync.notifyCheckout(result.id, static_cast<uint32_t>(terminal.checkoutTimeFor(result.id)));
      showCheckedOut(result.id);
      enteredID = "";
      drawIdleScreen();
      return;

    case TerminalAction::CheckedIn: {
      Serial.print("CHECK IN: ");
      Serial.println(result.id);
      Serial.print("Date: ");
      Serial.println(getDateString());
      Serial.print("Time: ");
      Serial.println(getTimeString());
      Serial.print("Duration: ");
      Serial.print(result.elapsedSeconds);
      Serial.println(" seconds");
      bluetoothSync.notifyCheckin(result.id, result.elapsedSeconds);
      String completedRecord;
      uint32_t completedTripID = 0;
      if (tripStorage.getLatestTripRecord(completedRecord, completedTripID)) {
        bluetoothSync.notifyCompletedTrip(completedRecord);
      }
      if (terminal.hasActivePass()) {
        bluetoothSync.notifyCheckout(terminal.activeId(), static_cast<uint32_t>(terminal.activeCheckoutTime()));
      }
      showCheckedIn(result.id, result.elapsedSeconds);
      enteredID = "";
      drawIdleScreen();
      return;
    }

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

  if (enteredID.length() < tripStorage.getMaxStudentIdLength()) {
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
    const char command = static_cast<char>(Serial.read());
    if (command == '\r') continue;
    if (command == '\n') {
      serialCommandBuffer.trim();
      if (serialCommandBuffer == "p" || serialCommandBuffer == "P") {
      tripStorage.printTripLog();
      } else if (serialCommandBuffer == "OWNER_RESET") {
        bluetoothSerial.disconnectClient();
        if (terminalSecurity.resetOwner() && bluetoothSerial.clearBondedDevices()) {
          Serial.println("OWNER_RESET,OK");
        } else {
          Serial.println("OWNER_RESET,FAILED");
        }
      }
      serialCommandBuffer = "";
      continue;
    }

    if (serialCommandBuffer.length() < MAX_BLUETOOTH_COMMAND_LENGTH) {
      serialCommandBuffer += command;
    } else {
      serialCommandBuffer = "";
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
    "Hallzee Starting..."
  );

  if (!terminalIdentity.begin() || !terminalSecurity.begin()) {
    Serial.println("ERROR: terminal identity/security storage unavailable.");
  }

  bluetoothSync.begin();
  // Do not clear BLE bonds merely because the owner record is unavailable at
  // boot. A transient NVS read failure must not make macOS report
  // "Peer removed pairing information" on the next reconnect. Bond removal is
  // reserved for explicit pairing mode and owner reset.
  Serial.print("Terminal owner: ");
  Serial.println(terminalSecurity.hasOwner() ? "CLAIMED" : "UNCLAIMED");

  // Storage owns NVS and LittleFS, then restores an active pass before clock
  // setup. The display is intentionally deferred until the clock is set.
  tripStorage.begin();
  terminal.setCapacity(tripStorage.getMaxActivePasses());
  terminal.restoreActivePass();

  Serial.println("Serial commands: p = print trip log; OWNER_RESET = clear terminal owner");

  keypadController.begin();

  // Initialize TFT
  tft.initR(INITR_BLACKTAB);

  tft.setRotation(1);

  drawStartupLogo();

  delay(800);

  // Until Bluetooth auto-time is added,
  // every true startup asks for date/time.
  beginClockSetup();
}

// The source logo is receiver/universal/Assets/hallzee-logo.png. The ESP32
// cannot load that PNG directly, so this is its display-sized RGB565
// approximation: the same paired doorway shapes and bidirectional arrow,
// centered in the complete 160 x 128 landscape viewport.
void drawStartupLogo() {
  tft.fillScreen(ST77XX_BLACK);

  const uint16_t logoGreen = tft.color565(118, 220, 40);
  const uint16_t logoBlue = tft.color565(2, 132, 199);
  const uint16_t logoMid = tft.color565(27, 185, 137);

  // Left and right doorway halves. Each half is split into two colors to
  // preserve the source logo's green-to-blue vertical gradient.
  tft.fillTriangle(50, 20, 68, 33, 68, 49, logoGreen);
  tft.fillTriangle(50, 20, 50, 64, 68, 49, logoMid);
  tft.fillTriangle(50, 64, 68, 79, 68, 108, logoBlue);
  tft.fillTriangle(50, 64, 50, 108, 68, 79, logoBlue);
  tft.fillTriangle(110, 20, 92, 33, 92, 49, logoGreen);
  tft.fillTriangle(110, 20, 110, 64, 92, 49, logoMid);
  tft.fillTriangle(110, 64, 92, 79, 92, 108, logoBlue);
  tft.fillTriangle(110, 64, 110, 108, 92, 79, logoBlue);

  // The center passage arrow, rendered in the source logo's two accent
  // colors instead of the old white person-shaped splash.
  tft.fillTriangle(56, 64, 69, 48, 69, 58, logoGreen);
  tft.fillTriangle(56, 64, 69, 80, 69, 70, logoGreen);
  tft.fillRect(69, 58, 22, 12, logoMid);
  tft.fillTriangle(104, 64, 91, 48, 91, 58, logoBlue);
  tft.fillTriangle(104, 64, 91, 80, 91, 70, logoBlue);
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
  bluetoothSync.updateAvailability(terminal.hasActivePass());

  if (pairingUiActive && terminalSecurity.hasOwner()) {
    pairingUiActive = false;
    terminalDisplay.showPairingComplete(terminalIdentity.terminalSuffix());
    drawIdleScreen();
  } else if (pairingUiActive &&
             !terminalSecurity.claimModeActive(monotonicClock.milliseconds())) {
    pairingUiActive = false;
    drawIdleScreen();
  }

  if (setupMode) {
    return;
  }

  // Only visually update clock when minute changes
  updateClockIfNeeded();
}
