#include "FirmwareUpdater.h"
#include "TouchExperiment.h"
#include <esp_task_wdt.h>
#include <Adafruit_GFX.h>
#if defined(HALLZEE_ILI9341)
#include "HallzeeLogoData.h"
#include "Ili9341DisplayPort.h"
#include "fonts/DMSansBold12pt7b.h"
#include "fonts/DMSansRegular6pt7b.h"
#include <Adafruit_ILI9341.h>
#else
#include "St7735DisplayPort.h"
#include <Adafruit_ST7735.h>
#endif
#include "AppTypes.h"
#include "ArduinoBluetoothSerialPort.h"
#include "ArduinoKeypadPort.h"
#include "BellPolicy.h"
#include "BluetoothSync.h"
#include "ClockService.h"
#include "Config.h"
#include "KeypadController.h"
#include "BondRepair.h"
#include "MonotonicClock.h"
#include "StudentIdPolicy.h"
#include "TerminalController.h"
#include "TerminalDisplay.h"
#include "TerminalIdentity.h"
#include "TerminalSecurity.h"
#include "TimeProvider.h"
#include "TripStorage.h"
#include <Keypad.h>
#include <SPI.h>
#include <sys/time.h>
#include <time.h>

#if !defined(CONFIG_BT_ENABLED) || !defined(CONFIG_BLUEDROID_ENABLED)
#error Bluetooth BLE is not enabled for this ESP32 board configuration.
#endif

#if defined(HALLZEE_ILI9341)
Adafruit_ILI9341 tft(TFT_CS, TFT_DC, TFT_RST);
Ili9341DisplayPort displayPort(tft);
#else
Adafruit_ST7735 tft =
    Adafruit_ST7735(TFT_CS, TFT_DC, TFT_MOSI, TFT_SCLK, TFT_RST);
St7735DisplayPort displayPort(tft);
#endif
TerminalDisplay terminalDisplay(displayPort);
#if defined(HALLZEE_TOUCH_TEST)
TouchExperiment touchExperiment(tft);
#endif

bool touchOverlayActive() {
#if defined(HALLZEE_TOUCH_TEST)
  return touchExperiment.active();
#else
  return false;
#endif
}

char keys[KEYPAD_ROWS][KEYPAD_COLS] = {
    {'1', '2', '3'}, {'4', '5', '6'}, {'7', '8', '9'}, {'*', '0', '#'}};

byte rowPins[KEYPAD_ROWS] = {32, 33, 25, 26};
byte colPins[KEYPAD_COLS] = {27, 14, 13};

Keypad keypad =
    Keypad(makeKeymap(keys), rowPins, colPins, KEYPAD_ROWS, KEYPAD_COLS);
ArduinoKeypadPort arduinoKeypad(keypad);
ArduinoMonotonicClock monotonicClock;

// ======================================================
// HALLZEE STATE
// ======================================================

TripStorage tripStorage;
ArduinoBluetoothSerialPort bluetoothSerial;
SystemTimeProvider systemTime;
BellPolicy bellPolicy;
TerminalController terminal(tripStorage, systemTime, &bellPolicy);
ClockService terminalClock;
TerminalIdentity terminalIdentity;
TerminalSecurity terminalSecurity(terminalIdentity);

void setSystemClock24(int year, int month, int day, int hour, int minute,
                      int second);
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

BluetoothSync bluetoothSync(tripStorage, bluetoothSerial, setSystemClock24,
                            handleBluetoothClockSet, getActivePassState,
                            manualCheckInFromDesktop, getActivePassStates,
                            setActivePassCapacity, &terminalIdentity,
                            &terminalSecurity, &bellPolicy);

String enteredID = "";
String serialCommandBuffer = "";

// Used so clock only visually refreshes when minute changes
int lastDisplayedMinute = -1;
bool lastDisplayedBluetoothState = false;

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
void transitionToIdle(bool clearEnteredId = true);

bool firmwareAuthorized() { return terminalSecurity.isAuthorized(); }
bool firmwareIdle() { return !terminal.hasActivePass(); }
FirmwareUpdater firmwareUpdater(bluetoothSerial, firmwareAuthorized, firmwareIdle);
bool firmwareCommand(const String &command) {
  if (firmwareAuthorized() && touchOverlayActive() && command.startsWith("FW_BEGIN,")) {
    bluetoothSerial.println("FW_ERROR,BUSY");
    return true;
  }
  return firmwareUpdater.command(command);
}
void firmwareFrame(const uint8_t *data, size_t size) { firmwareUpdater.frame(data, size); }
bool firmwareScreenVisible = false;
int lastFirmwareProgress = -1;

bool pairingUiActive = false;
BondRepair bondRepair(bluetoothSerial);
bool bondRepairUiActive = false;
String displayedFriendlyName;
bool ownerReleaseCleanupPending = false;
unsigned long ownerReleaseStartedAt = 0;

bool releaseOwnerFromDesktop() {
  if (!terminalSecurity.isAuthorized() || terminal.hasActivePass() || !terminalSecurity.resetOwner()) return false;
  ownerReleaseCleanupPending = true;
  ownerReleaseStartedAt = monotonicClock.milliseconds();
  return true;
}

bool isPairingAllowed() {
  return !touchOverlayActive() && !ownerReleaseCleanupPending && !terminal.hasActivePass() && !terminalSecurity.hasOwner();
}

bool isOwnerResetAllowed() {
  return !touchOverlayActive() && !terminal.hasActivePass() && terminalSecurity.hasOwner();
}

void resetOwnerFromKeypad() {
  bondRepair.reset();
  bondRepairUiActive = false;
  bluetoothSerial.disconnectClient();
  if (terminalSecurity.resetOwner() && bluetoothSerial.clearBondedDevices()) {
    // Releasing ownership does not set the clock. Preserve the current setup
    // step and entry so the reset notice returns to date/time setup if needed.
    terminalDisplay.showOwnerReset();
    transitionToIdle();
  } else {
    terminalDisplay.showPairingError("RESET FAILED");
    transitionToIdle();
  }
}

bool isBondRepairAllowed() {
  return !setupMode && !touchOverlayActive() && !pairingUiActive &&
    !bondRepairUiActive && !ownerReleaseCleanupPending && !firmwareUpdater.busy() &&
    !terminal.hasActivePass() && terminalSecurity.hasOwner();
}

void startBondRepair() {
  if (!bondRepair.start(monotonicClock.milliseconds(), isBondRepairAllowed(),
                        100000 + (esp_random() % 900000))) return;
  // New link must prove the existing owner key; this never opens CLAIM mode.
  terminalSecurity.clearSession();
  enteredID = "";
  bondRepairUiActive = true;
  terminalDisplay.showBondRepairWaiting();
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
    transitionToIdle();
    return;
  }
  bluetoothSerial.setPairingPasskey(terminalSecurity.pairingPasskey());
  pairingUiActive = true;
  terminalDisplay.showPairing(terminalIdentity.terminalId(),
                              terminalIdentity.customName(),
                              terminalSecurity.pairingPasskey());
}

bool isSetupMode() { return setupMode; }

bool isResetAllowed() { return !touchOverlayActive() && terminal.hasActivePass(); }

KeypadController keypadController(arduinoKeypad, monotonicClock, isSetupMode,
                                  isResetAllowed, handleSetupKey,
                                  handleNormalNumber, handleSingleStar,
                                  handleSingleHash, resetCurrentCheckout,
                                  isPairingAllowed, startPairingMode,
                                  isOwnerResetAllowed, resetOwnerFromKeypad,
                                  isBondRepairAllowed, startBondRepair);

void setSystemClock24(int year, int month, int day, int hour, int minute,
                      int second) {
  terminalClock.set24Hour(year, month, day, hour, minute, second);
  lastDisplayedMinute = -1;
}

void setSystemClock(int year, int month, int day, int hour, int minute,
                    bool pm) {
  terminalClock.set12Hour(year, month, day, hour, minute, pm);
  lastDisplayedMinute = -1;
}

String getTimeString() { return terminalClock.timeString(); }

String getDateString() { return terminalClock.dateString(); }

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

  showBluetoothClockSync();
  transitionToIdle();
}

// ======================================================
// PARTIAL REDRAW: STUDENT ID
// ======================================================

void drawIDEntry() {
#if defined(HALLZEE_TOUCH_TEST)
  touchExperiment.suppress();
#endif
  terminalDisplay.drawIdEntry(enteredID);
}

void drawClock() { terminalDisplay.drawClock(getTimeString()); }

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
  terminalDisplay.setFriendlyName(terminalIdentity.customName());
  terminalDisplay.drawIdleScreen(terminal.activeId(), enteredID);
  lastDisplayedBluetoothState = bluetoothSerial.hasClient();
  terminalDisplay.drawBluetoothStatus(lastDisplayedBluetoothState);
  lastDisplayedMinute = -1;
  updateClockIfNeeded();
#if defined(HALLZEE_TOUCH_TEST)
  touchExperiment.decorateIdle(!terminal.hasActivePass());
#endif
}

void transitionToIdle(bool clearEnteredId) {
  if (clearEnteredId) {
    enteredID = "";
  }
  if (setupMode) terminalDisplay.drawClockSetupScreen(setupStep, setupEntry);
  else drawIdleScreen();
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

void showWrongID() { terminalDisplay.showPassOccupied(); }

void showEnterID() { terminalDisplay.showEnterId(); }

void showStorageError() { terminalDisplay.showStorageError(); }

void showTripLogSummary() {
  terminalDisplay.showTripLogSummary(tripStorage.isLogReady(),
                                     tripStorage.getTripRecordCount(),
                                     tripStorage.getLatestTripID());
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
  if (!terminal.manualCheckIn(requestedId, studentId, elapsedSeconds))
    return false;

  bluetoothSync.notifyCheckin(studentId, elapsedSeconds);
  String completedRecord;
  uint32_t completedTripID = 0;
  if (tripStorage.getLatestTripRecord(completedRecord, completedTripID)) {
    bluetoothSync.notifyCompletedTrip(completedRecord);
  }
  if (studentId == oldestId && terminal.hasActivePass()) {
    bluetoothSync.notifyCheckout(
        terminal.activeId(),
        static_cast<uint32_t>(terminal.activeCheckoutTime()));
  }
  showCheckedIn(studentId, elapsedSeconds);
  transitionToIdle();
  return true;
}

bool setActivePassCapacity(uint8_t capacity) {
  return terminal.setCapacity(capacity) &&
         tripStorage.setMaxActivePasses(capacity);
}

void resetCurrentCheckout() {
  String oldID;
  if (!terminal.resetActivePass(oldID)) {
    showStorageError();
    transitionToIdle();
    return;
  }

  bluetoothSync.notifyReset(oldID, 0);

  terminalDisplay.showManualReset(oldID);

  Serial.print("Manual reset. Cleared ID: ");
  Serial.println(oldID);

  transitionToIdle();
}

// ======================================================
// CLOCK SETUP INPUT FIELD
// ======================================================

void drawSetupEntry() { terminalDisplay.drawClockSetupEntry(setupEntry); }

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

  setSystemClock(setupYear, setupMonth, setupDay, setupHour, setupMinute,
                 setupPM);

  setupMode = false;

  setupEntry = "";

  terminalDisplay.showClockSet(getDateString(), getTimeString());

  transitionToIdle();
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

      showInvalidValue("Month must be 1-12");

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

    if (value < 1 || value > 31 || (setupMonth == 2 && value > 29) ||
        ((setupMonth == 4 || setupMonth == 6 || setupMonth == 9 ||
          setupMonth == 11) &&
         value > 30)) {

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

      showInvalidValue("Use YYYY: 2024-2099");

      return;
    }

    setupYear = value;

    if (setupDay > daysInMonth(setupMonth, setupYear)) {

      showInvalidValue("Date does not exist");

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

      showInvalidValue("Hour must be 1-12");

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

      showInvalidValue("Minute must be 0-59");

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

      showInvalidValue("1 = AM, 2 = PM");

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
  if (pairingUiActive || bondRepairUiActive) return;

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
    terminalDisplay.showStudentIdTooLong(currentIdLimit);
    transitionToIdle();
    return;
  }

  const TerminalActionResult result = terminal.submit(submittedID);

  switch (result.action) {
  case TerminalAction::EmptyId:
    showEnterID();
    transitionToIdle();
    return;

  case TerminalAction::StartClockSetup:
    Serial.println("Clock admin code accepted.");
    enteredID = "";
    beginClockSetup();
    return;

  case TerminalAction::ShowTripLog:
    Serial.println("Trip log summary requested.");
    showTripLogSummary();
    transitionToIdle();
    return;

  case TerminalAction::CheckedOut:
    Serial.print("CHECK OUT: ");
    Serial.println(result.id);
    Serial.print("Date: ");
    Serial.println(getDateString());
    Serial.print("Time: ");
    Serial.println(getTimeString());
    bluetoothSync.notifyCheckout(
        result.id, static_cast<uint32_t>(terminal.checkoutTimeFor(result.id)));
    showCheckedOut(result.id);
    transitionToIdle();
    return;

  case TerminalAction::CheckedOutWithWarning:
    Serial.print("CHECK OUT WITH BELL WARNING: ");
    Serial.println(result.id);
    bluetoothSync.notifyCheckout(
        result.id, static_cast<uint32_t>(terminal.checkoutTimeFor(result.id)));
    terminalDisplay.showCheckedOutWarning(result.id, getTimeString());
    transitionToIdle();
    return;

  case TerminalAction::PolicyLocked:
    Serial.println("Checkout blocked by bell-time policy.");
    terminalDisplay.showPolicyLocked();
    transitionToIdle();
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
      bluetoothSync.notifyCheckout(
          terminal.activeId(),
          static_cast<uint32_t>(terminal.activeCheckoutTime()));
    }
    showCheckedIn(result.id, result.elapsedSeconds);
    transitionToIdle();
    return;
  }

  case TerminalAction::StorageError:
    showStorageError();
    transitionToIdle();
    return;

  case TerminalAction::PassOccupied:
    Serial.print("Rejected ID: ");
    Serial.println(submittedID);
    showWrongID();
    transitionToIdle();
    return;
  }
}
void handleNormalNumber(char key) {
  if (touchOverlayActive()) return;
  if (pairingUiActive || bondRepairUiActive) return;

  if (enteredID.length() < tripStorage.getMaxStudentIdLength()) {
    // Do not log typed digits: successive events can reconstruct a student ID.
    enteredID += key;

    // Partial redraw only
    drawIDEntry();
  }
}

// ======================================================
// HANDLE SINGLE * RELEASE
// ======================================================

void handleSingleStar() {
#if defined(HALLZEE_TOUCH_TEST)
  if (touchExperiment.active()) {
    touchExperiment.close();
    transitionToIdle();
    return;
  }
#endif
  if (pairingUiActive || bondRepairUiActive) return;

  enteredID = "";

  // Partial redraw only
  drawIDEntry();
}

// ======================================================
// HANDLE SINGLE # RELEASE
// ======================================================

void handleSingleHash() { if (!pairingUiActive && !bondRepairUiActive && !touchOverlayActive()) submitID(); }

// ======================================================
// SERIAL DIAGNOSTIC COMMANDS
// ======================================================

void processSerialCommands() {
  while (Serial.available()) {
    const char command = static_cast<char>(Serial.read());
    if (command == '\r')
      continue;
    if (command == '\n') {
      serialCommandBuffer.trim();
      if (serialCommandBuffer == "p" || serialCommandBuffer == "P") {
        tripStorage.printTripLog();
#if defined(HALLZEE_TOUCH_TEST)
      } else if (serialCommandBuffer == "TOUCH_TEST") {
        if (!setupMode && !pairingUiActive && !bondRepairUiActive && !terminal.hasActivePass()) {
          touchExperiment.open();
          Serial.println("TOUCH_TEST,OK");
        } else Serial.println("TOUCH_TEST,BUSY");
#endif
      } else if (serialCommandBuffer == "OWNER_RESET") {
        bluetoothSerial.disconnectClient();
        if (terminalSecurity.resetOwner() &&
            bluetoothSerial.clearBondedDevices()) {
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

// Keypad event interpretation and the * + # hold gesture live in
// KeypadController.cpp.

// ======================================================
// SETUP
// ======================================================

void setup() {

  Serial.begin(115200);
  // A stalled first boot must reset so the bootloader can restore the last image.
  esp_task_wdt_config_t firmwareWatchdog = {15000, 0, true};
  if (esp_task_wdt_reconfigure(&firmwareWatchdog) == ESP_ERR_INVALID_STATE) esp_task_wdt_init(&firmwareWatchdog);
  enableLoopWDT();
  const bool firmwareWatchdogReady = esp_task_wdt_status(nullptr) == ESP_OK;

  delay(500);

  Serial.println();
  Serial.println("Hallzee Starting...");

  const bool identityStorageReady = terminalIdentity.begin();
  displayedFriendlyName = terminalIdentity.customName();
  terminalDisplay.setFriendlyName(displayedFriendlyName);
  const bool securityStorageReady = terminalSecurity.begin();
  if (!identityStorageReady || !securityStorageReady) {
    Serial.println("ERROR: terminal identity/security storage unavailable.");
    if (!identityStorageReady)
      Serial.println("Identity storage: UNAVAILABLE");
    if (!securityStorageReady)
      Serial.println("Owner storage: UNAVAILABLE");
  }

  bluetoothSync.setOwnerReleaseHandler(releaseOwnerFromDesktop);
  bluetoothSync.setFirmwareHandlers(firmwareCommand, firmwareFrame);
  bluetoothSync.begin();
  // Do not clear BLE bonds merely because the owner record is unavailable at
  // boot. A transient NVS read failure must not make macOS report
  // "Peer removed pairing information" on the next reconnect. Bond removal is
  // reserved for explicit pairing mode and owner reset.
  Serial.print("Terminal owner: ");
  Serial.println(terminalSecurity.hasOwner() ? "CLAIMED" : "UNCLAIMED");

  // Storage owns NVS and LittleFS, then restores an active pass before clock
  // setup. The display is intentionally deferred until the clock is set.
  const bool tripStorageReady = tripStorage.begin();
  const bool bellPolicyReady = bellPolicy.begin();
  if (!bellPolicyReady) {
    Serial.println("WARNING: Bell policy storage unavailable; terminal enforcement is off.");
  }
  terminal.setCapacity(tripStorage.getMaxActivePasses());
  terminal.restoreActivePass();

  Serial.println("Serial commands: p = print trip log; OWNER_RESET = clear "
                 "terminal owner");

  keypadController.begin();

  // Initialize TFT
#if defined(HALLZEE_TOUCH_TEST)
  // The display is write-only. Leave MISO unassigned on VSPI so GPIO19 can
  // belong exclusively to the separate touch HSPI bus.
  pinMode(TOUCH_CS, OUTPUT);
  digitalWrite(TOUCH_CS, HIGH);
  SPI.begin(TFT_SCLK, -1, TFT_MOSI, TFT_CS);
#endif
#if defined(HALLZEE_ILI9341)
  // The native 320x240 layout performs substantially more SPI writes than
  // the legacy 160x128 renderer. 20 MHz leaves useful margin for longer
  // jumper wires and inexpensive ILI9341 modules while remaining responsive.
  tft.begin(20000000);
  // In the physical Hallzee enclosure, rotation 3 is right-side up.
  // Map rotation 1 to 3 (and 3 to 1) so standard rotation 1 displays upright.
  const uint8_t effectiveRotation =
      (HALLZEE_DISPLAY_ROTATION == 1)
          ? 3
          : ((HALLZEE_DISPLAY_ROTATION == 3) ? 1 : HALLZEE_DISPLAY_ROTATION);
  tft.setRotation(effectiveRotation);
#else
  tft.initR(INITR_BLACKTAB);
  tft.setRotation(1);
#endif

#if defined(HALLZEE_TOUCH_TEST)
  touchExperiment.begin();
  Serial.println("Serial command: TOUCH_TEST = calibration / typing sandbox");
#endif
  drawStartupLogo();

  delay(1800);

  // Until Bluetooth auto-time is added,
  // every true startup asks for date/time.
  beginClockSetup();
  firmwareUpdater.confirmBoot(identityStorageReady && terminalIdentity.storageReady() && securityStorageReady && tripStorageReady && bellPolicyReady && bluetoothSerial.isReady() && firmwareWatchdogReady);
}

// Hallzee brand logo splash screen.
void drawStartupLogo() {
#if defined(HALLZEE_ILI9341)
  tft.fillScreen(ILI9341_WHITE);
  const int16_t startX = (320 - LOGO_BITMAP_WIDTH) / 2;
  const int16_t startY = 40;

  int16_t x = 0;
  int16_t y = 0;
  for (size_t i = 0; i < LOGO_RLE_COUNT; i++) {
    LogoRleSpan span;
    memcpy_P(&span, &HALLZEE_LOGO_RLE[i], sizeof(LogoRleSpan));
    int16_t remaining = span.count;
    while (remaining > 0) {
      int16_t runInRow = min((int)remaining, (int)(LOGO_BITMAP_WIDTH - x));
      if (span.color != 0xFFFF) {
        tft.drawFastHLine(startX + x, startY + y, runInRow, span.color);
      }
      x += runInRow;
      remaining -= runInRow;
      if (x >= LOGO_BITMAP_WIDTH) {
        x = 0;
        y++;
      }
    }
  }

  // Brand title below logo
  tft.setFont(&DMSansBold12pt7b);
  tft.setTextColor(0x03B1); // UI_HEADER_NAVY
  tft.setTextSize(1);
  tft.setCursor(118, 142);
  tft.print("HALLZEE");

  tft.setFont(&DMSansRegular6pt7b);
  tft.setTextColor(0x6B6D); // UI_MUTED
  tft.setCursor(118, 160);
  tft.print("T E R M I N A L");

  tft.setFont(&DMSansRegular6pt7b);
  tft.setTextColor(0x9CF3); // UI_BLUETOOTH_MUTED
  tft.setCursor(126, 202);
  tft.print("STARTING...");

#else
  tft.fillScreen(ST77XX_BLACK);
  const uint16_t logoGreen = tft.color565(118, 220, 40);
  const uint16_t logoBlue = tft.color565(2, 132, 199);
  const uint16_t logoMid = tft.color565(27, 185, 137);

  tft.fillTriangle(50, 20, 68, 33, 68, 49, logoGreen);
  tft.fillTriangle(50, 20, 50, 64, 68, 49, logoMid);
  tft.fillTriangle(50, 64, 68, 79, 68, 108, logoBlue);
  tft.fillTriangle(50, 64, 50, 108, 68, 79, logoBlue);
  tft.fillTriangle(110, 20, 92, 33, 92, 49, logoGreen);
  tft.fillTriangle(110, 20, 110, 64, 92, 49, logoMid);
  tft.fillTriangle(110, 64, 92, 79, 92, 108, logoBlue);
  tft.fillTriangle(110, 64, 110, 108, 92, 79, logoBlue);

  tft.fillTriangle(56, 64, 69, 48, 69, 58, logoGreen);
  tft.fillTriangle(56, 64, 69, 80, 69, 70, logoGreen);
  tft.fillRect(69, 58, 22, 12, logoMid);
  tft.fillTriangle(104, 64, 91, 48, 91, 58, logoBlue);
  tft.fillTriangle(104, 64, 91, 80, 91, 70, logoBlue);
#endif
}

// ======================================================
// MAIN LOOP
// ======================================================

void loop() {

  // Diagnostic only: send p in Serial Monitor to print local trip records.
  if (!firmwareUpdater.busy()) processSerialCommands();

#if defined(HALLZEE_TOUCH_TEST)
  if (!firmwareUpdater.busy() && !setupMode && !pairingUiActive && !bondRepairUiActive &&
      !terminal.hasActivePass() && touchExperiment.pending()) touchExperiment.open();
  if (firmwareUpdater.busy() || setupMode || pairingUiActive || bondRepairUiActive) touchExperiment.suppress();
#endif
  if (!firmwareUpdater.busy()) keypadController.poll();

  // Bluetooth is passive in Phase 3; it must never block student workflow.
  bluetoothSync.poll();
  firmwareUpdater.poll();
  if (firmwareUpdater.busy()) {
    if (!firmwareScreenVisible || lastFirmwareProgress != int(firmwareUpdater.progress())) {
      firmwareScreenVisible = true;
      lastFirmwareProgress = firmwareUpdater.progress();
      tft.fillScreen(UI_NAVY); tft.setFont(nullptr); tft.setTextColor(0xffff); tft.setTextSize(1);
      tft.setCursor(10, 35); tft.print("Updating firmware");
      tft.setCursor(10, 55); tft.print(String(lastFirmwareProgress) + "% - keep power on");
    }
    return;
  }
  if (firmwareScreenVisible) {
    firmwareScreenVisible = false; lastFirmwareProgress = -1;
    if (setupMode) drawSetupScreen(); else drawIdleScreen();
  }
  // Allow the release acknowledgement to reach the owner before closing BLE.
  if (ownerReleaseCleanupPending && (!bluetoothSerial.hasClient() ||
      monotonicClock.milliseconds() - ownerReleaseStartedAt >= 500)) {
    bluetoothSerial.disconnectClient();
    bluetoothSerial.clearBondedDevices();
    ownerReleaseCleanupPending = false;
  }
  bluetoothSync.updateAvailability(terminal.hasActivePass());
  if (displayedFriendlyName != terminalIdentity.customName()) {
    displayedFriendlyName = terminalIdentity.customName();
    terminalDisplay.setFriendlyName(displayedFriendlyName);
    if (!pairingUiActive && !bondRepairUiActive && !setupMode) drawIdleScreen();
  }

  // The Bluetooth indicator is a small independent region; do not redraw the
  // rest of the kiosk screen while a desktop client connects or disconnects.
  const bool bluetoothConnected = bluetoothSerial.hasClient();
  if (!touchOverlayActive() && !pairingUiActive && !bondRepairUiActive && !setupMode && bluetoothConnected != lastDisplayedBluetoothState) {
    lastDisplayedBluetoothState = bluetoothConnected;
    terminalDisplay.drawBluetoothStatus(bluetoothConnected);
  }

  if (bondRepairUiActive) {
    const auto previous = bondRepair.status();
    bondRepair.poll(monotonicClock.milliseconds(), terminalSecurity.isAuthorized());
    const auto current = bondRepair.status();
    if (current == BondRepair::State::Ready && previous != current) {
      terminalDisplay.showPairing(terminalIdentity.terminalId(),
        terminalIdentity.customName(), bondRepair.code(), true);
    } else if (!bondRepair.active()) {
      // Expire the displayed code at the BLE layer too; existing bonds survive.
      bluetoothSerial.setPairingPasskey(100000 + (esp_random() % 900000));
      bondRepairUiActive = false;
      if (current == BondRepair::State::Complete)
        terminalDisplay.showPairingComplete(terminalIdentity.terminalSuffix(), true);
      else terminalDisplay.showPairingError(current == BondRepair::State::Expired ? "REPAIR EXPIRED" : "REPAIR FAILED");
      bondRepair.reset();
      transitionToIdle();
    }
  }

  if (pairingUiActive && terminalSecurity.hasOwner()) {
    pairingUiActive = false;
    terminalDisplay.showPairingComplete(terminalIdentity.terminalSuffix());
    transitionToIdle();
  } else if (pairingUiActive &&
             !terminalSecurity.claimModeActive(monotonicClock.milliseconds())) {
    pairingUiActive = false;
    transitionToIdle();
  }

  if (setupMode || pairingUiActive || bondRepairUiActive) {
    return;
  }

#if defined(HALLZEE_TOUCH_TEST)
  const auto touchAction = touchExperiment.poll(!terminal.hasActivePass());
  if (touchAction == TouchExperiment::Action::Clear) handleSingleStar();
  else if (touchAction == TouchExperiment::Action::Submit) handleSingleHash();
  else if (touchAction == TouchExperiment::Action::Closed) transitionToIdle();
  if (touchExperiment.active()) return;
#endif

  // Only visually update clock when minute changes
  updateClockIfNeeded();
}
