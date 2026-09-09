#include <algorithm>
#include <cstdlib>
#include <deque>
#include <iostream>
#include <string>
#include <vector>

#include "Config.h"
#include "BluetoothSync.h"
#include "DisplayColors.h"
#include "KeypadController.h"
#include "StudentIdPolicy.h"
#include "TerminalController.h"
#include "TripRecordCodec.h"
#include "TerminalDisplay.h"
#include "TerminalIdentity.h"
#include "FirmwareFrame.h"
#include "TouchInput.h"
#include "support/RecordingDisplay.h"

namespace {

int failures = 0;

void expectEqual(
  const std::vector<std::string> &actual,
  const std::vector<std::string> &expected,
  const char *testName
) {
  if (actual == expected) {
    return;
  }

  std::cerr << "FAIL: " << testName << "\nExpected:\n";
  for (const auto &command : expected) std::cerr << "  " << command << "\n";
  std::cerr << "Actual:\n";
  for (const auto &command : actual) std::cerr << "  " << command << "\n";
  failures++;
}

void expectTrue(bool condition, const char *testName) {
  if (!condition) {
    std::cerr << "FAIL: " << testName << "\n";
    failures++;
  }
}

bool contains(const std::vector<std::string> &commands, const std::string &command) {
  return std::find(commands.begin(), commands.end(), command) != commands.end();
}

class FakeTimeProvider : public TimeProvider {
public:
  time_t currentTime = 0;
  time_t now() const override { return currentTime; }
};

class FakeTripStorage : public TripStoragePort {
public:
  struct Record {
    String id;
    time_t outTime;
    time_t inTime;
    long duration;
    std::string status;
  };

  String restoredId;
  time_t restoredTime = 0;
  bool saveSucceeds = true;
  bool appendSucceeds = true;
  bool markSucceeds = true;
  SettingWriteResult settingWriteResult = SettingWriteResult::Saved;
  uint8_t maxStudentIdLength = DEFAULT_STUDENT_ID_LENGTH;
  bool cleared = false;
  String savedId;
  time_t savedTime = 0;
  std::vector<Record> records;
  std::vector<ActiveCheckout> activeCheckouts;
  std::vector<std::pair<uint32_t, String>> syncRecords;
  std::vector<uint32_t> markedSynced;

  void loadActiveCheckout(String &studentID, time_t &checkoutTime) override {
    studentID = restoredId;
    checkoutTime = restoredTime;
  }
  bool saveActiveCheckout(const String &studentID, time_t checkoutTime) override {
    savedId = studentID;
    savedTime = checkoutTime;
    return saveSucceeds;
  }
  void clearActiveCheckout() override { cleared = true; }
  uint8_t loadActiveCheckouts(ActiveCheckout *checkouts, uint8_t maximum) override {
    uint8_t count = 0;
    for (const auto &checkout : activeCheckouts) {
      if (count >= maximum) break;
      checkouts[count++] = checkout;
    }
    if (count == 0 && restoredId.length() > 0 && maximum > 0) {
      checkouts[0] = {restoredId, restoredTime};
      return 1;
    }
    return count;
  }
  bool saveActiveCheckouts(const ActiveCheckout *checkouts, uint8_t count) override {
    if (!saveSucceeds) return false;
    activeCheckouts.assign(checkouts, checkouts + count);
    cleared = count == 0;
    if (count > 0) { savedId = checkouts[0].studentID; savedTime = checkouts[0].checkoutTime; }
    return true;
  }
  uint8_t getMaxStudentIdLength() const override { return maxStudentIdLength; }
  SettingWriteResult setMaxStudentIdLength(uint8_t value) override {
    if (settingWriteResult == SettingWriteResult::Saved) maxStudentIdLength = value;
    return settingWriteResult;
  }
  bool appendTripRecord(const String &studentID, time_t outTime, time_t inTime,
                        long durationSeconds, const char *status) override {
    records.push_back({studentID, outTime, inTime, durationSeconds, status});
    return appendSucceeds;
  }
  uint32_t getTripRecordCount() override { return static_cast<uint32_t>(syncRecords.size()); }
  uint32_t getTripRecordCountAfter(uint32_t afterTripID) override {
    return static_cast<uint32_t>(std::count_if(syncRecords.begin(), syncRecords.end(), [&](const auto &record) {
      return record.first > afterTripID;
    }));
  }
  uint32_t getUnsyncedTripRecordCount() override {
    return static_cast<uint32_t>(std::count_if(syncRecords.begin(), syncRecords.end(), [&](const auto &record) {
      return std::find(markedSynced.begin(), markedSynced.end(), record.first) == markedSynced.end();
    }));
  }
  bool getNextUnsyncedRecord(String &record, uint32_t &tripID) override {
    for (const auto &candidate : syncRecords) {
      if (std::find(markedSynced.begin(), markedSynced.end(), candidate.first) == markedSynced.end()) {
        tripID = candidate.first;
        record = candidate.second;
        return true;
      }
    }
    return false;
  }
  bool getNextRecordAfter(uint32_t afterTripID, String &record, uint32_t &tripID) override {
    for (const auto &candidate : syncRecords) {
      if (candidate.first > afterTripID) {
        tripID = candidate.first;
        record = candidate.second;
        return true;
      }
    }
    return false;
  }
  bool markTripSynced(uint32_t tripID) override {
    markedSynced.push_back(tripID);
    return markSucceeds;
  }
};

class FakeBluetoothSerial : public BluetoothSerialPort {
public:
  bool beginSucceeds = true;
  bool renameSucceeds = true;
  bool connected = false;
  std::string input;
  std::vector<std::string> output;
  std::string deviceName;
  std::string pin;
  std::string partialLine;

  bool begin(const char *name) override { deviceName = name; return beginSucceeds; }
  bool setDeviceName(const char *name) override {
    if (!renameSucceeds) return false;
    deviceName = name;
    return true;
  }
  void setPin(const char *value, size_t) override { pin = value; }
  bool hasClient() override { return connected; }
  int available() override { return static_cast<int>(input.size()); }
  int read() override { const char value = input.front(); input.erase(0, 1); return value; }
  void print(const char *text) override { partialLine += text; }
  void print(const String &text) override { partialLine += std::string(text); }
  void println(const char *text) override { output.push_back(partialLine + text); partialLine = ""; }
  void println(const String &text) override { output.push_back(partialLine + std::string(text)); partialLine = ""; }
};

int bluetoothClockSetCount = 0;
int bluetoothClockYear = 0;
void setBluetoothClock(int year, int, int, int, int, int) {
  bluetoothClockSetCount++;
  bluetoothClockYear = year;
}
void onBluetoothClockSet() {}

class FakeMonotonicClock : public MonotonicClock {
public:
  unsigned long currentMilliseconds = 0;
  unsigned long milliseconds() const override { return currentMilliseconds; }
};

class FakeKeypad : public KeypadPort {
public:
  unsigned int debounceMilliseconds = 0;
  unsigned int holdMilliseconds = 0;
  std::deque<std::vector<TerminalKeypadEvent>> batches;

  void configure(unsigned int debounce, unsigned int hold) override {
    debounceMilliseconds = debounce;
    holdMilliseconds = hold;
  }
  size_t readEvents(TerminalKeypadEvent *events, size_t capacity) override {
    if (batches.empty()) return 0;
    const auto batch = batches.front();
    batches.pop_front();
    const size_t count = std::min(batch.size(), capacity);
    for (size_t index = 0; index < count; index++) events[index] = batch[index];
    return count;
  }
};

bool keypadSetupMode = false;
bool keypadResetAllowed = false;
bool keypadOwnerResetAllowed = false;
std::vector<char> setupKeys;
std::vector<char> numberKeys;
int clearCount = 0;
int submitCount = 0;
int resetCount = 0;
int ownerResetCount = 0;
int pairingCount = 0;

bool isKeypadSetupMode() { return keypadSetupMode; }
bool isKeypadResetAllowed() { return keypadResetAllowed; }
bool isKeypadOwnerResetAllowed() { return keypadOwnerResetAllowed; }
bool isKeypadPairingAllowed() { return true; }
void onSetupKey(char key) { setupKeys.push_back(key); }
void onNumberKey(char key) { numberKeys.push_back(key); }
void onClear() { clearCount++; }
void onSubmit() { submitCount++; }
void onReset() { resetCount++; }
void onOwnerReset() { ownerResetCount++; }
void onPairing() { pairingCount++; }

void resetKeypadCallbacks() {
  keypadSetupMode = false;
  keypadResetAllowed = false;
  keypadOwnerResetAllowed = false;
  setupKeys.clear();
  numberKeys.clear();
  clearCount = 0;
  submitCount = 0;
  resetCount = 0;
  ownerResetCount = 0;
  pairingCount = 0;
}

void expectAction(TerminalAction actual, TerminalAction expected, const char *testName) {
  expectTrue(actual == expected, testName);
}

void testEmptyIdEntryGoldenInstructions() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.drawIdEntry("");

  expectEqual(display.commands, {
    "fillRect:13:79:134:17:59196",
    "setTextColor:8484",
    "setTextSize:2",
    "setCursor:16:79",
    "print:_"
  }, "empty ID field golden instructions");
}

void testLongIdEntryUsesCompactText() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.drawIdEntry("1234567890123456");

  expectEqual(display.commands, {
    "fillRect:13:79:134:17:59196",
    "setTextColor:8484",
    "setTextSize:1",
    "setCursor:16:83",
    "print:1234567890123456"
  }, "long ID entry compact instructions");
}

void testStudentIdLimitIsRecheckedAtSubmission() {
  expectTrue(isStudentIdWithinLimit("12345678", 8),
    "ID at configured limit is accepted");
  expectTrue(!isStudentIdWithinLimit("123456789", 8),
    "ID typed before a limit change is rejected at submission");
}

void testLongOccupiedIdUsesCompactLabel() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.drawIdleScreen("1234567890123456", "");

  expectTrue(contains(display.commands, "print:OUT ID "),
    "long occupied ID uses compact label");
  expectTrue(contains(display.commands, "println:1234567890123456"),
    "long occupied ID remains fully visible");
}

void testStudentIdTooLongMessage() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.showStudentIdTooLong(8);

  expectTrue(contains(display.commands, "println:ID TOO LONG"),
    "over-limit submission explains the rejection");
  expectTrue(contains(display.commands, "println:8"),
    "over-limit submission shows the current limit");
}

void testOccupiedIdleScreenGoldenInstructions() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.drawIdleScreen("42", "123");

  expectEqual(display.commands, {
    "fillScreen:48631",
    "setTextWrap:0",
    "fillRect:0:0:160:24:6733",
    "setTextColor:65535",
    "setTextSize:1",
    "setCursor:9:3",
    "println:HALLZEE",
    "setTextColor:48631",
    "setCursor:9:14",
    "setTextSize:1",
    "print:Terminal: ",
    "println:",
    "fillRoundRect:8:28:144:31:6:65535",
    "fillRoundRect:8:28:5:31:3:59782",
    "setTextColor:59782",
    "setTextSize:2",
    "setCursor:20:34",
    "println:OCCUPIED",
    "setTextColor:27501",
    "setTextSize:1",
    "setCursor:21:50",
    "print:OUT WITH ID ",
    "println:42",
    "setTextColor:27501",
    "setTextSize:1",
    "setCursor:10:66",
    "println:STUDENT ID",
    "drawRoundRect:9:75:142:25:5:27501",
    "fillRoundRect:10:76:140:23:4:59196",
    "setCursor:10:114",
    "print:* CLEAR",
    "setCursor:103:114",
    "print:# SUBMIT",
    "fillRect:13:79:134:17:59196",
    "setTextColor:8484",
    "setTextSize:2",
    "setCursor:16:79",
    "print:123"
  }, "occupied idle screen golden instructions");
}

void testClockSetupStepUsesCorrectPromptAndHint() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.drawClockSetupScreen(SET_YEAR, "2026");

  const auto &commands = display.commands;
  expectTrue(contains(commands, "println:YEAR"), "clock setup year prompt");
  expectTrue(contains(commands, "println:YYYY"), "clock setup year hint");
  expectTrue(contains(commands, "print:2026"), "clock setup entered value");
}

void testEveryStatusViewEmitsItsContentAndExpectedPause() {
  struct StatusCase {
    const char *name;
    void (*render)(TerminalDisplay &);
    const char *requiredCommand;
    const char *pauseCommand;
  };

  const StatusCase cases[] = {
    {"bluetooth clock", [](TerminalDisplay &terminal) { terminal.showBluetoothClockSynced("01/02/2026", "9:05 AM"); }, "println:CLOCK SYNCED", "pause:1200"},
    {"checked in", [](TerminalDisplay &terminal) { terminal.showCheckedIn(65); }, "print:1", "pause:3000"},
    {"checked in hours", [](TerminalDisplay &terminal) { terminal.showCheckedIn(3670); }, "print:1", "pause:3000"},
    {"pass occupied", [](TerminalDisplay &terminal) { terminal.showPassOccupied(); }, "println:OCCUPIED", "pause:2000"},
    {"empty ID", [](TerminalDisplay &terminal) { terminal.showEnterId(); }, "println:ENTER ID", "pause:1500"},
    {"storage error", [](TerminalDisplay &terminal) { terminal.showStorageError(); }, "println:NOT SAVED", "pause:2200"},
    {"trip log unavailable", [](TerminalDisplay &terminal) { terminal.showTripLogSummary(false, 0, 0); }, "println:Storage unavailable", "pause:2500"},
    {"trip log empty", [](TerminalDisplay &terminal) { terminal.showTripLogSummary(true, 0, 0); }, "println:No trips recorded yet.", "pause:2500"},
    {"trip log latest ID", [](TerminalDisplay &terminal) { terminal.showTripLogSummary(true, 7, 42); }, "println:42", "pause:2500"},
    {"manual reset", [](TerminalDisplay &terminal) { terminal.showManualReset("AB12"); }, "println:RESET", "pause:1800"},
    {"invalid clock value", [](TerminalDisplay &terminal) { terminal.showInvalidClockValue("Invalid day"); }, "println:Invalid day", "pause:1200"},
    {"clock set", [](TerminalDisplay &terminal) { terminal.showClockSet("01/02/2026", "9:05 AM"); }, "println:CLOCK SET", "pause:1800"}
  };

  for (const auto &testCase : cases) {
    RecordingDisplay display;
    TerminalDisplay terminal(display);
    testCase.render(terminal);
    expectTrue(contains(display.commands, testCase.requiredCommand), testCase.name);
    expectTrue(contains(display.commands, testCase.pauseCommand), testCase.name);
  }
}

void testPartialRedrawsAndAllClockSetupPrompts() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);

  terminal.drawIdEntry("123");
  expectTrue(contains(display.commands, "print:123"), "populated ID redraw");
  terminal.drawClock("9:05 AM");
  expectTrue(contains(display.commands, "print:9:05 AM"), "clock redraw");
  terminal.drawClockSetupEntry("");
  expectTrue(contains(display.commands, "print:_"), "empty clock setup entry redraw");

  RecordingDisplay idleDisplay;
  TerminalDisplay idleTerminal(idleDisplay);
  idleTerminal.drawIdleScreen("", "");
  expectTrue(contains(idleDisplay.commands, "println:OPEN"), "open idle screen state");

  const ClockSetupStep steps[] = {
    SET_MONTH, SET_DAY, SET_YEAR, SET_HOUR, SET_MINUTE, SET_AMPM
  };
  const char *prompts[] = {"MONTH", "DAY", "YEAR", "HOUR", "MINUTE", "AM / PM"};
  const char *hints[] = {"1-12", "1-31", "YYYY", "1-12", "0-59", "1 or 2"};

  for (size_t index = 0; index < 6; index++) {
    RecordingDisplay setupDisplay;
    TerminalDisplay setupTerminal(setupDisplay);
    setupTerminal.drawClockSetupScreen(steps[index], "7");
    expectTrue(contains(setupDisplay.commands, std::string("println:") + prompts[index]), "clock setup prompt");
    expectTrue(contains(setupDisplay.commands, std::string("println:") + hints[index]), "clock setup hint");
    expectTrue(contains(setupDisplay.commands, "print:7"), "clock setup entry redraw");
  }
}

void testTerminalCheckoutAndCheckinAreDeterministic() {
  FakeTripStorage storage;
  FakeTimeProvider time;
  time.currentTime = 1000;
  TerminalController terminal(storage, time);

  const auto checkout = terminal.submit("STUDENT-1");
  expectAction(checkout.action, TerminalAction::CheckedOut, "checkout action");
  expectTrue(storage.savedId == "STUDENT-1" && storage.savedTime == 1000, "checkout persistence");
  expectTrue(terminal.hasActivePass(), "checkout creates active pass");

  time.currentTime = 1365;
  const auto checkin = terminal.submit("STUDENT-1");
  expectAction(checkin.action, TerminalAction::CheckedIn, "checkin action");
  expectTrue(checkin.elapsedSeconds == 365, "checkin duration uses injected time");
  expectTrue(storage.records.size() == 1 && storage.records[0].status == "COMPLETE", "checkin record");
  expectTrue(storage.cleared && !terminal.hasActivePass(), "checkin clears persisted active pass");
}

void testTerminalStorageFailuresDoNotLosePassState() {
  FakeTripStorage storage;
  FakeTimeProvider time;
  time.currentTime = 100;
  TerminalController terminal(storage, time);

  storage.saveSucceeds = false;
  expectAction(terminal.submit("A").action, TerminalAction::StorageError, "checkout save failure");
  expectTrue(!terminal.hasActivePass(), "failed checkout does not become active");

  storage.saveSucceeds = true;
  expectAction(terminal.submit("A").action, TerminalAction::CheckedOut, "checkout after storage recovery");
  storage.appendSucceeds = false;
  time.currentTime = 200;
  expectAction(terminal.submit("A").action, TerminalAction::StorageError, "checkin append failure");
  expectTrue(terminal.hasActivePass(), "failed checkin keeps active pass");

  String resetId;
  expectTrue(!terminal.resetActivePass(resetId), "failed reset keeps active pass");
  expectTrue(terminal.hasActivePass(), "reset failure preserves active pass");
}

void testTerminalRestorationAndSubmissionClassification() {
  FakeTripStorage storage;
  FakeTimeProvider time;
  storage.restoredId = "RESTORED";
  storage.restoredTime = 20;
  TerminalController terminal(storage, time);
  terminal.restoreActivePass();

  expectTrue(terminal.hasActivePass() && terminal.activeId() == "RESTORED", "restored pass");
  expectAction(terminal.submit("").action, TerminalAction::EmptyId, "empty ID");
  expectAction(terminal.submit(CLOCK_CODE).action, TerminalAction::StartClockSetup, "clock admin code");
  expectAction(terminal.submit(LOG_SUMMARY_CODE).action, TerminalAction::ShowTripLog, "log admin code");
  expectAction(terminal.submit("OTHER").action, TerminalAction::PassOccupied, "different ID rejected");
}

void testTerminalClampsBackwardTimeAndPersistsManualReset() {
  FakeTripStorage storage;
  FakeTimeProvider time;
  TerminalController terminal(storage, time);

  String resetId;
  expectTrue(!terminal.resetActivePass(resetId), "cannot reset without an active pass");

  time.currentTime = 100;
  expectAction(terminal.submit("A").action, TerminalAction::CheckedOut, "checkout before backward time");
  time.currentTime = 50;
  const auto checkin = terminal.submit("A");
  expectAction(checkin.action, TerminalAction::CheckedIn, "checkin after backward time");
  expectTrue(checkin.elapsedSeconds == 0, "backward time is clamped to zero");

  time.currentTime = 200;
  expectAction(terminal.submit("B").action, TerminalAction::CheckedOut, "checkout before reset");
  expectTrue(terminal.resetActivePass(resetId), "manual reset succeeds");
  expectTrue(resetId == "B", "manual reset returns active ID");
  expectTrue(
    storage.records.back().status == "MANUAL_RESET" && !terminal.hasActivePass(),
    "manual reset records and clears active pass"
  );
}

void testTerminalManualCheckInPersistsManualTrip() {
  FakeTripStorage storage;
  FakeTimeProvider time;
  time.currentTime = 1000;
  TerminalController terminal(storage, time);
  expectAction(terminal.submit("TEACHER-1").action, TerminalAction::CheckedOut,
    "checkout before desktop checkin");

  time.currentTime = 1120;
  String checkedInId;
  unsigned long elapsedSeconds = 0;
  expectTrue(terminal.manualCheckIn("TEACHER-1", checkedInId, elapsedSeconds),
    "desktop manual checkin succeeds");
  expectTrue(checkedInId == "TEACHER-1" && elapsedSeconds == 120,
    "desktop manual checkin returns the active pass details");
  expectTrue(storage.records.size() == 1 && storage.records[0].status == "MANUAL",
    "desktop manual checkin records a MANUAL trip");
  expectTrue(storage.cleared && !terminal.hasActivePass(),
    "desktop manual checkin clears the active pass");
}

void testTerminalEnforcesConfiguredMultiPassCapacity() {
  FakeTripStorage storage;
  FakeTimeProvider time;
  TerminalController terminal(storage, time);
  expectTrue(terminal.setCapacity(2), "capacity of two is accepted");
  time.currentTime = 100;
  expectAction(terminal.submit("A").action, TerminalAction::CheckedOut, "first student checks out");
  time.currentTime = 110;
  expectAction(terminal.submit("B").action, TerminalAction::CheckedOut, "second student checks out");
  expectTrue(terminal.activePassCount() == 2 && terminal.activeId() == "A", "oldest pass remains primary");
  expectAction(terminal.submit("C").action, TerminalAction::PassOccupied, "capacity blocks a third checkout");
  time.currentTime = 130;
  expectAction(terminal.submit("A").action, TerminalAction::CheckedIn, "oldest student checks in");
  expectTrue(terminal.activePassCount() == 1 && terminal.activeId() == "B", "next oldest pass becomes primary");
}

time_t localTimestamp(int year, int month, int day, int hour, int minute) {
  struct tm value = {};
  value.tm_year = year - 1900;
  value.tm_mon = month - 1;
  value.tm_mday = day;
  value.tm_hour = hour;
  value.tm_min = minute;
  value.tm_isdst = -1;
  return mktime(&value);
}

void testBellPolicyLocksWarnsAndFailsOpenOutsideCache() {
  BellPolicy policy;
  policy.beginUpdate(true);
  BellPolicyWindow window;
  window.dateKey = 20260908;
  window.startMinute = 480;
  window.endMinute = 600;
  window.firstWindowEndMinute = 490;
  window.lastWindowStartMinute = 590;
  window.firstDecision = BellPolicyDecision::Lock;
  window.lastDecision = BellPolicyDecision::Warn;
  expectTrue(policy.addStagedWindow(window), "valid bell policy window stages");
  expectTrue(policy.commitUpdate(1), "bell policy update commits atomically");

  FakeTripStorage storage;
  FakeTimeProvider time;
  time.currentTime = localTimestamp(2026, 9, 8, 8, 5);
  TerminalController terminal(storage, time, &policy);
  expectAction(terminal.submit("LOCKED").action, TerminalAction::PolicyLocked,
    "first bell window locks a new checkout");
  expectTrue(!terminal.hasActivePass(), "locked checkout does not occupy the pass");

  time.currentTime = localTimestamp(2026, 9, 8, 9, 55);
  expectAction(terminal.submit("WARNED").action, TerminalAction::CheckedOutWithWarning,
    "last bell window warns while allowing checkout");

  FakeTripStorage otherStorage;
  time.currentTime = localTimestamp(2026, 9, 9, 8, 5);
  TerminalController outsideCache(otherStorage, time, &policy);
  expectAction(outsideCache.submit("OPEN").action, TerminalAction::CheckedOut,
    "a date missing from the offline cache fails open");
}

void testKeypadControllerInterpretsKeysAndResetGesture() {
  resetKeypadCallbacks();
  FakeKeypad keypad;
  FakeMonotonicClock clock;
  KeypadController controller(
    keypad, clock, isKeypadSetupMode, isKeypadResetAllowed, onSetupKey,
    onNumberKey, onClear, onSubmit, onReset
  );
  controller.begin();
  expectTrue(
    keypad.debounceMilliseconds == 20 && keypad.holdMilliseconds == 500,
    "keypad hardware configuration"
  );

  keypad.batches.push_back({{'4', KeypadEventState::Pressed}});
  controller.poll();
  expectTrue(numberKeys == std::vector<char>{'4'}, "number key press");

  keypad.batches.push_back({{'*', KeypadEventState::Pressed}});
  controller.poll();
  keypad.batches.push_back({{'*', KeypadEventState::Released}});
  controller.poll();
  expectTrue(clearCount == 1, "star release clears entry");

  keypad.batches.push_back({{'#', KeypadEventState::Pressed}});
  controller.poll();
  keypad.batches.push_back({{'#', KeypadEventState::Released}});
  controller.poll();
  expectTrue(submitCount == 1, "hash release submits entry");

  keypadSetupMode = true;
  keypad.batches.push_back({{'#', KeypadEventState::Pressed}});
  controller.poll();
  keypad.batches.push_back({{'#', KeypadEventState::Released}});
  controller.poll();
  expectTrue(setupKeys == std::vector<char>{'#'}, "setup mode accepts hash confirmation");
  keypadSetupMode = false;

  keypadResetAllowed = true;
  clock.currentMilliseconds = 100;
  keypad.batches.push_back({
    {'*', KeypadEventState::Pressed}, {'#', KeypadEventState::Pressed}
  });
  controller.poll();
  clock.currentMilliseconds = 2099;
  controller.poll();
  expectTrue(resetCount == 0, "reset waits for full hold duration");
  clock.currentMilliseconds = 2100;
  controller.poll();
  expectTrue(resetCount == 1, "reset triggers at configured duration");

  keypad.batches.push_back({
    {'*', KeypadEventState::Released}, {'#', KeypadEventState::Released}
  });
  controller.poll();
  expectTrue(clearCount == 1 && submitCount == 1, "reset suppresses release actions");

  keypad.batches.push_back({{'*', KeypadEventState::Pressed}});
  controller.poll();
  keypad.batches.push_back({{'*', KeypadEventState::Released}});
  controller.poll();
  expectTrue(clearCount == 2, "clear resumes after reset keys release");

  keypadResetAllowed = false;
  keypadOwnerResetAllowed = true;
  clock.currentMilliseconds = 3000;
  KeypadController ownerResetController(
    keypad, clock, isKeypadSetupMode, isKeypadResetAllowed, onSetupKey,
    onNumberKey, onClear, onSubmit, onReset, nullptr, nullptr,
    isKeypadOwnerResetAllowed, onOwnerReset
  );
  keypadSetupMode = true;
  keypad.batches.push_back({
    {'*', KeypadEventState::Pressed}, {'#', KeypadEventState::Pressed}
  });
  ownerResetController.poll();
  clock.currentMilliseconds = 12999;
  ownerResetController.poll();
  expectTrue(ownerResetCount == 0, "owner reset waits during clock setup");
  clock.currentMilliseconds = 13000;
  ownerResetController.poll();
  expectTrue(ownerResetCount == 1, "owner reset triggers during clock setup");
  keypad.batches.push_back({
    {'*', KeypadEventState::Released}, {'#', KeypadEventState::Released}
  });
  ownerResetController.poll();
  keypadSetupMode = false;
  clock.currentMilliseconds = 3000;
  ownerResetCount = 0;
  keypad.batches.push_back({
    {'*', KeypadEventState::Pressed}, {'#', KeypadEventState::Pressed}
  });
  ownerResetController.poll();
  clock.currentMilliseconds = 12999;
  ownerResetController.poll();
  expectTrue(ownerResetCount == 0, "owner reset waits for full hold duration");
  clock.currentMilliseconds = 13000;
  ownerResetController.poll();
  expectTrue(ownerResetCount == 1, "owner reset triggers at configured duration");
  ownerResetController.poll();
  expectTrue(ownerResetCount == 1, "owner reset triggers only once per hold");
  keypad.batches.push_back({
    {'*', KeypadEventState::Released}, {'#', KeypadEventState::Released}
  });
  ownerResetController.poll();
  expectTrue(clearCount == 2 && submitCount == 1, "owner reset suppresses release actions");

  KeypadController pairingController(
    keypad, clock, isKeypadSetupMode, isKeypadResetAllowed, onSetupKey,
    onNumberKey, onClear, onSubmit, onReset, isKeypadPairingAllowed,
    onPairing
  );
  keypadOwnerResetAllowed = false;
  clock.currentMilliseconds = 14000;
  keypad.batches.push_back({
    {'*', KeypadEventState::Pressed}, {'#', KeypadEventState::Pressed}
  });
  pairingController.poll();
  clock.currentMilliseconds = 19000;
  pairingController.poll();
  expectTrue(pairingCount == 1, "pairing triggers at configured duration");
  clock.currentMilliseconds = 24000;
  pairingController.poll();
  expectTrue(pairingCount == 1, "pairing triggers only once per hold");
  keypad.batches.push_back({
    {'*', KeypadEventState::Released}, {'#', KeypadEventState::Released}
  });
  pairingController.poll();

  keypadSetupMode = true;
  setupKeys.clear();
  clock.currentMilliseconds = 25000;
  keypad.batches.push_back({{'*', KeypadEventState::Pressed}, {'#', KeypadEventState::Pressed}});
  pairingController.poll();
  clock.currentMilliseconds = 29999;
  pairingController.poll();
  expectTrue(pairingCount == 1, "setup pairing waits five seconds");
  clock.currentMilliseconds = 30000;
  pairingController.poll();
  pairingController.poll();
  expectTrue(pairingCount == 2, "setup pairing fires once");
  keypad.batches.push_back({{'*', KeypadEventState::Released}, {'#', KeypadEventState::Released}});
  pairingController.poll();
  expectTrue(setupKeys.empty(), "pairing chord does not edit the clock");
  keypadSetupMode = false;
}

static bool fakeHasActivePass = false;
static String fakeActiveId = "";
static uint32_t fakeActiveEpoch = 0;

static bool fakeGetActivePass(String &activeId, uint32_t &checkoutEpoch) {
  if (fakeHasActivePass) {
    activeId = fakeActiveId;
    checkoutEpoch = fakeActiveEpoch;
    return true;
  }
  return false;
}

int ownerReleaseRequests = 0;
bool ownerReleaseSucceeds = true;
bool fakeReleaseOwner() { ownerReleaseRequests++; return ownerReleaseSucceeds; }

void testFirmwareFrameBoundaries() {
  FirmwareFrame decoder;
  std::vector<uint8_t> frame(527, 0);
  frame[1]='H'; frame[2]='Z'; frame[3]=1; frame[4]=2;
  frame[13]=0; frame[14]=2;
  for (size_t i=0;i<frame.size();i++) {
    expectTrue(decoder.push(frame[i]) == (i==526), "binary frame completes only at exact length");
  }
  decoder.reset();
  frame[14]=3;
  for (size_t i=0;i<15;i++) decoder.push(frame[i]);
  expectTrue(decoder.count==0, "oversized binary frame resets without buffer overflow");
  frame[14]=2; frame[1]='X';
  for (size_t i=0;i<15;i++) decoder.push(frame[i]);
  expectTrue(decoder.count==0, "invalid firmware magic is rejected");
}

void testTerminalRenamePersistenceAndFailures() {
  Preferences::stored.clear();
  TerminalIdentity identity;
  identity.begin();
  const String originalId = identity.terminalId();
  FakeTripStorage storage;
  FakeBluetoothSerial serial;
  BluetoothSync sync(storage, serial, setBluetoothClock, onBluetoothClockSet,
    nullptr, nullptr, nullptr, nullptr, &identity);
  sync.begin();
  serial.connected = true;
  serial.input = "SET,TERMINAL_NAME,Room 204\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS_ACK,TERMINAL_NAME,Room 204"), "valid rename is acknowledged");
  TerminalIdentity restarted;
  restarted.begin();
  expectTrue(restarted.customName() == "Room 204", "rename survives reboot with NVS namespace limit");
  expectTrue(restarted.terminalId() == originalId, "rename retains stable device identity");

  Preferences::failWrite = true;
  serial.output.clear();
  serial.input = "SET,TERMINAL_NAME,Room 205\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS_ERROR,TERMINAL_NAME,STORAGE_FAILED"), "storage failures are not invalid names");
  expectTrue(identity.customName() == "Room 204" && serial.deviceName == "Room 204", "failed save restores discovery name");
  expectTrue(!contains(serial.output, "SETTINGS_ACK,TERMINAL_NAME,Room 205"), "failed save is never acknowledged");
  Preferences::failWrite = false;

  serial.renameSucceeds = false;
  serial.input = "SET,TERMINAL_NAME,Room 206\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS_ERROR,TERMINAL_NAME,BLE_UPDATE_FAILED"), "BLE failure has its own error");
  expectTrue(identity.customName() == "Room 204", "BLE failure does not persist a new name");
  serial.renameSucceeds = true;

  serial.input = "SET,TERMINAL_NAME,Room,207\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS_ERROR,TERMINAL_NAME,INVALID_VALUE"), "invalid name remains rejected");
  expectTrue(identity.customName() == "Room 204" && serial.deviceName == "Room 204", "invalid name does not change device");

  Preferences::failOpen = true;
  TerminalIdentity unavailable;
  unavailable.begin();
  expectTrue(unavailable.terminalId() == originalId && !unavailable.setCustomName("Room 208"), "unavailable storage keeps identity usable without false save success");
  Preferences::failOpen = false;
  Preferences::stored.clear();
}

void testBluetoothOwnerRelease() {
  FakeTripStorage storage;
  FakeBluetoothSerial serial;
  BluetoothSync sync(storage, serial, setBluetoothClock, onBluetoothClockSet, fakeGetActivePass);
  sync.begin();
  serial.connected = true;
  fakeHasActivePass = false;
  serial.input = "RELEASE_OWNER\n";
  sync.poll();
  expectTrue(contains(serial.output, "ERROR,UNSUPPORTED_COMMAND"), "release needs configured handler");
  sync.setOwnerReleaseHandler(fakeReleaseOwner);
  fakeHasActivePass = true;
  serial.input = "RELEASE_OWNER\n";
  sync.poll();
  expectTrue(ownerReleaseRequests == 0 && contains(serial.output, "ERROR,ACTIVE_PASS"), "release protects active passes");
  fakeHasActivePass = false;
  ownerReleaseSucceeds = false;
  serial.input = "RELEASE_OWNER\n";
  sync.poll();
  expectTrue(!contains(serial.output, "OWNER_RELEASED") && contains(serial.output, "ERROR,OWNER_RELEASE_FAILED"), "failed release never acknowledges success");
  ownerReleaseSucceeds = true;
  serial.input = "RELEASE_OWNER\n";
  sync.poll();
  expectTrue(ownerReleaseRequests == 2 && contains(serial.output, "OWNER_RELEASED"), "release acknowledges persisted owner removal");
}

void testBluetoothProtocolAndRecovery() {
  FakeTripStorage storage;
  storage.syncRecords = {
    {7, "7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"},
    {8, "8,ID2,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"}
  };
  FakeBluetoothSerial serial;
  BluetoothSync sync(storage, serial, setBluetoothClock, onBluetoothClockSet, fakeGetActivePass);
  sync.begin();
  expectTrue(serial.deviceName == "Hallzee" && serial.pin.empty(), "Bluetooth LE setup");

  serial.connected = true;
  sync.poll();
  expectTrue(contains(serial.output, "HALLZEE_READY,1"), "Bluetooth readiness handshake");

  // HELLO makes readiness deterministic even when the connect-time notification
  // happened before Windows finished enabling notifications.
  serial.output.clear();
  serial.input = "HEL";
  sync.poll();
  expectTrue(serial.output.empty(), "fragmented command waits for newline");
  serial.input = "LO,1\n";
  sync.poll();
  expectTrue(contains(serial.output, "HALLZEE_READY,1"), "HELLO repeats readiness");

  // Active pass query when available
  fakeHasActivePass = false;
  serial.output.clear();
  serial.input = "GET_ACTIVE_PASS\n";
  sync.poll();
  expectTrue(contains(serial.output, "ACTIVE_PASS,NONE"), "active pass query reports NONE when available");

  // Active pass query when occupied
  fakeHasActivePass = true;
  fakeActiveId = "10482";
  fakeActiveEpoch = 1725200000;
  serial.output.clear();
  serial.input = "GET_ACTIVE_PASS\n";
  sync.poll();
  expectTrue(contains(serial.output, "ACTIVE_PASS,10482,1725200000"), "active pass query reports occupied pass and epoch");

  // Real-time event notifications
  serial.output.clear();
  sync.notifyCheckout("10482", 1725200000);
  expectTrue(contains(serial.output, "EVENT,CHECKOUT,10482,1725200000"), "emits CHECKOUT event notification");

  serial.output.clear();
  sync.notifyCheckin("10482", 300);
  expectTrue(contains(serial.output, "EVENT,CHECKIN,10482,300"), "emits CHECKIN event notification");

  serial.output.clear();
  sync.notifyReset("10482", 0);
  expectTrue(contains(serial.output, "EVENT,RESET,10482,0"), "emits RESET event notification");

  serial.output.clear();
  serial.input = "GET_SETTINGS\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS,MAX_ID_LENGTH,10"),
    "current kiosk settings can be queried");

  serial.output.clear();
  const std::string settingsCommand = "SET,MAX_ID_LENGTH,16\n";
  serial.input = settingsCommand.substr(0, 20);
  sync.poll();
  expectTrue(serial.output.empty(), "long settings command waits for final BLE chunk");
  serial.input = settingsCommand.substr(20);
  sync.poll();
  expectTrue(storage.maxStudentIdLength == 16 &&
    contains(serial.output, "SETTINGS_ACK,MAX_ID_LENGTH,16"),
    "valid settings command persists and acknowledges the ID limit");

  serial.input = "SET,MAX_ID_LENGTH,3\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS_ERROR,MAX_ID_LENGTH,INVALID_VALUE") &&
    storage.maxStudentIdLength == 16, "out-of-range ID limit is rejected");

  storage.settingWriteResult = SettingWriteResult::ActiveCheckoutTooLong;
  serial.input = "SET,MAX_ID_LENGTH,8\n";
  sync.poll();
  expectTrue(contains(serial.output, "SETTINGS_ERROR,MAX_ID_LENGTH,ACTIVE_ID_TOO_LONG") &&
    storage.maxStudentIdLength == 16, "active checkout prevents an unsafe shorter limit");
  storage.settingWriteResult = SettingWriteResult::Saved;

  bluetoothClockSetCount = 0;
  serial.output.clear();
  const std::string cursorCommand = "TIME_CURSOR,2026-02-28,08:30:00,7\n";
  serial.input = cursorCommand.substr(0, 20);
  sync.poll();
  expectTrue(serial.output.empty(), "20-byte BLE command fragment is buffered");
  serial.input = cursorCommand.substr(20);
  sync.poll();
  expectTrue(bluetoothClockSetCount == 1 && bluetoothClockYear == 2026, "cursor command sets time");
  expectTrue(contains(serial.output, "TIME_ACK,OK") && contains(serial.output, "SYNC_BEGIN,1") &&
    contains(serial.output, "TRIP,8,ID2,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"),
    "cursor sync sends only newer records");

  serial.input = "ACK,8\n";
  sync.poll();
  expectTrue(storage.markedSynced.empty(), "cursor ACK avoids full flash-log rewrite");
  expectTrue(contains(serial.output, "SYNC_END"), "cursor ACK completes transfer");

  serial.input = "TIME,2026-02-28,08:30:00\n";
  sync.poll();
  expectTrue(contains(serial.output, "SYNC_BEGIN,2") &&
    contains(serial.output, "TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"),
    "legacy time command keeps unsynced compatibility");

  serial.input = "ACK,7\n";
  sync.poll();
  expectTrue(storage.markedSynced == std::vector<uint32_t>{7}, "legacy ACK marks record synced");
  expectTrue(contains(serial.output, "TRIP,8,ID2,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"), "ACK advances one record at a time");

  serial.input = "ACK,999\n";
  sync.poll();
  expectTrue(contains(serial.output, "ACK_ERROR,UNEXPECTED_ID"), "unexpected ACK rejected");

  serial.input = "TIME,2025-02-29,08:30:00\nWHAT\n";
  sync.poll();
  expectTrue(contains(serial.output, "TIME_ACK,ERROR") && contains(serial.output, "ERROR,UNKNOWN_COMMAND"), "invalid commands rejected");

  serial.input = "SYNC_";
  sync.poll();
  serial.connected = false;
  sync.poll();
  serial.connected = true;
  serial.output.clear();
  sync.poll();
  serial.input = "ALL\n";
  sync.poll();
  expectTrue(contains(serial.output, "ERROR,UNKNOWN_COMMAND"),
    "disconnect discards a fragmented command");

  serial.input = "SYNC_ALL\n";
  sync.poll();
  expectTrue(contains(serial.output, "TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"), "disconnect resets full history sync state");
}

void testBluetoothFailureAndValidationPaths() {
  FakeTripStorage storage;
  storage.syncRecords = {{9, "9,ID,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"}};
  FakeBluetoothSerial serial;
  BluetoothSync sync(storage, serial, setBluetoothClock, onBluetoothClockSet);
  sync.begin();
  serial.connected = true;
  sync.poll();

  serial.input = "ACK,\nACK,not-a-number\nACK,9\n";
  sync.poll();
  expectTrue(contains(serial.output, "ACK_ERROR,INVALID_ID"), "invalid ACK rejected");
  expectTrue(contains(serial.output, "ACK_ERROR,UNEXPECTED_ID"), "ACK without sync rejected");

  serial.input = std::string(MAX_BLUETOOTH_COMMAND_LENGTH + 1, 'X') + "\n";
  sync.poll();
  expectTrue(contains(serial.output, "ERROR,COMMAND_TOO_LONG"), "overlong command rejected");

  serial.input = "SYNC_START\n";
  sync.poll();
  storage.markSucceeds = false;
  serial.input = "ACK,9\n";
  sync.poll();
  expectTrue(contains(serial.output, "ACK_ERROR,MARK_FAILED"), "sync mark failure reported");

  FakeBluetoothSerial unavailableSerial;
  unavailableSerial.beginSucceeds = false;
  BluetoothSync unavailableSync(storage, unavailableSerial, setBluetoothClock, onBluetoothClockSet);
  unavailableSync.begin();
  unavailableSerial.connected = true;
  unavailableSerial.input = "SYNC_START\n";
  unavailableSync.poll();
  expectTrue(unavailableSerial.output.empty() && unavailableSerial.pin.empty(), "unavailable transport remains inactive");
}

void testBluetoothBellPolicyValidation() {
  FakeTripStorage storage;
  FakeBluetoothSerial serial;
  BellPolicy policy;
  BluetoothSync sync(
    storage, serial, setBluetoothClock, onBluetoothClockSet,
    nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, &policy
  );
  sync.begin();
  serial.connected = true;
  sync.poll();
  serial.output.clear();

  serial.input = "POLICY_BEGIN,1\nPOLICY_WINDOW,broken\nPOLICY_COMMIT,broken\n";
  sync.poll();
  expectTrue(contains(serial.output, "POLICY_ACK,BEGIN"),
    "valid policy begin is acknowledged");
  expectTrue(contains(serial.output, "POLICY_ERROR,INVALID_WINDOW"),
    "malformed policy window is rejected");
  expectTrue(contains(serial.output, "POLICY_ERROR,COMMIT_FAILED") && !policy.enabled(),
    "nonnumeric policy commit cannot replace the active policy");

  serial.output.clear();
  serial.input =
    "POLICY_BEGIN,1\n"
    "POLICY_WINDOW,20260908,480,600,490,590,2,1\n"
    "POLICY_COMMIT,1\n";
  sync.poll();
  expectTrue(policy.enabled() && policy.windowCount() == 1 &&
    contains(serial.output, "POLICY_ACK,COMMIT,1"),
    "valid policy transfer commits atomically");
}

void testTripRecordCodecPreservesAndRewritesRecords() {
  const String unsynced = "7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0";
  uint32_t tripID = 0;
  bool isSynced = true;
  expectTrue(TripRecordCodec::parse(unsynced, tripID, isSynced), "parse valid unsynced record");
  expectTrue(tripID == 7 && !isSynced, "parsed unsynced record values");

  String updated;
  expectTrue(TripRecordCodec::markSynced(unsynced, updated), "rewrite sync flag");
  expectTrue(updated == "7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,1", "only sync flag changes");

  const String syncedWithWhitespace = " 4294967295,ID,with,commas,1 ";
  expectTrue(TripRecordCodec::parse(syncedWithWhitespace, tripID, isSynced), "whitespace around fields accepted");
  expectTrue(tripID == UINT32_MAX && isSynced, "maximum ID and synced flag parsed");
  expectTrue(TripRecordCodec::markSynced(syncedWithWhitespace, updated), "already synced record can be rewritten");
  expectTrue(updated == " 4294967295,ID,with,commas,1", "rewrite preserves record body and removes trailing whitespace");

  expectTrue(!TripRecordCodec::parse("not,a,record", tripID, isSynced), "malformed record rejected");
  expectTrue(!TripRecordCodec::parse("7", tripID, isSynced), "record without commas rejected");
  expectTrue(!TripRecordCodec::parse(",ID,0", tripID, isSynced), "empty trip ID rejected");
  expectTrue(!TripRecordCodec::parse("0,ID,0", tripID, isSynced), "zero trip ID rejected");
  expectTrue(!TripRecordCodec::parse("7abc,ID,0", tripID, isSynced), "partial numeric trip ID rejected");
  expectTrue(!TripRecordCodec::parse("4294967296,ID,0", tripID, isSynced), "overflowing trip ID rejected");
  expectTrue(!TripRecordCodec::parse("7,ID,2", tripID, isSynced), "invalid sync flag rejected");
  expectTrue(!TripRecordCodec::markSynced("7,ID,2", updated), "invalid record is never rewritten");
}

void testCheckedOutScreenIncludesDurationInstruction() {
  RecordingDisplay display;
  TerminalDisplay terminal(display);
  terminal.showCheckedOut("AB12", "9:05 AM");

  expectEqual(display.commands, {
    "fillScreen:2016",
    "setTextColor:0",
    "setTextSize:2",
    "setCursor:10:20",
    "println:CHECKED",
    "setCursor:10:44",
    "println:OUT",
    "setTextSize:1",
    "setCursor:10:75",
    "print:ID: ",
    "println:AB12",
    "setCursor:10:95",
    "print:Time: ",
    "println:9:05 AM",
    "pause:2000"
  }, "checked out screen golden instructions");
}

}  // namespace

class NativeRecordingDisplay : public RecordingDisplay {
public:
  bool isNative320x240() const override { return true; }
};

void testTouchInput() {
  TouchCalibration calibration;
  // Deliberately swapped and reversed axes, as on a rotated panel.
  const TouchPoint raw[] = {{3800, 300}, {3800, 3700}, {400, 300}};
  expectTrue(calibration.fit(raw), "touch accepts reversed/swapped calibration");
  const auto middle = calibration.map({2100, 2000});
  expectTrue(middle.x == 160 && middle.y == 120, "touch maps calibrated center");
  const auto fourth = calibration.map({400, 3700});
  expectTrue(fourth.x == 296 && fourth.y == 216, "touch maps independent fourth corner");
  const auto outside = calibration.map({4100, 0});
  expectTrue(outside.x == 0 && outside.y > 0, "touch extrapolates without edge clamping");
  const TouchRect button{12, 204, 140, 34};
  expectTrue(button.contains({12, 204}) && !button.contains({152, 204}) &&
             !button.contains({12, 238}) && !button.contains({-1, 220}), "touch hit bounds exclude gaps/outside");
  const TouchPoint repeated[] = {{100, 100}, {101, 101}, {102, 102}};
  expectTrue(!calibration.fit(repeated) && calibration.map({100, 100}).x == -1,
             "touch refuses degenerate calibration");

  TouchTap tap;
  expectTrue(tap.update(0, true, 1) == -1 && tap.update(100, true, 1) == -1,
             "touch held across startup is suppressed");
  tap.update(110, false, -1); tap.update(180, false, -1);
  tap.update(200, true, 1); tap.update(240, true, 1);
  expectTrue(tap.pressed() == 1 && tap.update(5000, true, 1) == -1,
             "touch holding Submit never repeats or fires before release");
  tap.update(5010, false, -1);
  expectTrue(tap.update(5060, false, -1) == -1 && tap.update(5080, false, -1) == 1 &&
             tap.update(5200, false, -1) == -1, "touch fires exactly once after release debounce");
  tap.update(5300, true, 0); tap.update(5350, true, 0);
  tap.update(5360, true, -1); tap.update(5370, true, 0);
  tap.update(5400, false, -1);
  expectTrue(tap.update(5470, false, -1) == -1, "slide out and back cancels action");
  tap.update(5500, true, 1); tap.update(5550, true, 1);
  tap.update(5560, false, -1);
  expectTrue(tap.update(5600, true, 1) == -1, "brief pressure dropout does not fire");
  tap.suppress(); tap.update(5610, false, -1);
  expectTrue(tap.update(5680, false, -1) == -1, "screen change cancels pending submit");
  tap.update(5700, true, 1); tap.update(5720, false, -1);
  expectTrue(tap.update(5790, false, -1) == -1, "touch ignores short noise pulse");
  tap.update(UINT32_MAX - 20, true, 0); tap.update(30, true, 0);
  tap.update(40, false, -1);
  expectTrue(tap.update(110, false, -1) == 0, "touch debounce survives millis rollover");
}

int main() {
  testTouchInput();
  {
    NativeRecordingDisplay display;
    TerminalDisplay terminal(display);
    terminal.setFriendlyName("Room 204");
    terminal.drawClockSetupScreen(SET_MONTH, "");
    expectTrue(contains(display.commands, "println:Room 204"), "native setup header shows friendly name");
    expectTrue(contains(display.commands, "println:Pair: hold * + # for 5 seconds"), "native setup explains pairing chord");
    terminal.showPairing("HZ-A1B2C3D4E5F6", "Ms Rivera Room 204", 123456);
    expectTrue(contains(display.commands, "println:HZ-A1B2C3D4E5F6"), "native pairing shows full unique ID");
    expectTrue(contains(display.commands, "println:Ms Rivera Room 204"), "native pairing shows full friendly name");
  }
  {
    RecordingDisplay display;
    TerminalDisplay terminal(display);
    terminal.showPairing("HZ-A1B2C3D4E5F6", "Ms Rivera Room 204", 42);
    expectTrue(contains(display.commands, "println:HZ-A1B2C3D4E5F6"), "pairing shows full unique ID");
    expectTrue(contains(display.commands, "println:Ms Rivera Room 204"), "pairing shows friendly name");
    expectTrue(contains(display.commands, "println:000042"), "pairing pads all six passkey digits");
    display.commands.clear();
    terminal.setFriendlyName("Room 204");
    terminal.drawIdleScreen("", "");
    expectTrue(contains(display.commands, "print:Terminal: ") && contains(display.commands, "println:Room 204"), "header shows friendly name");
  }
  testEmptyIdEntryGoldenInstructions();
  testLongIdEntryUsesCompactText();
  testStudentIdLimitIsRecheckedAtSubmission();
  testLongOccupiedIdUsesCompactLabel();
  testStudentIdTooLongMessage();
  testOccupiedIdleScreenGoldenInstructions();
  testClockSetupStepUsesCorrectPromptAndHint();
  testCheckedOutScreenIncludesDurationInstruction();
  testEveryStatusViewEmitsItsContentAndExpectedPause();
  testPartialRedrawsAndAllClockSetupPrompts();
  testTerminalCheckoutAndCheckinAreDeterministic();
  testTerminalStorageFailuresDoNotLosePassState();
  testTerminalRestorationAndSubmissionClassification();
  testTerminalClampsBackwardTimeAndPersistsManualReset();
  testTerminalManualCheckInPersistsManualTrip();
  testTerminalEnforcesConfiguredMultiPassCapacity();
  testBellPolicyLocksWarnsAndFailsOpenOutsideCache();
  testKeypadControllerInterpretsKeysAndResetGesture();
  testFirmwareFrameBoundaries();
  testTerminalRenamePersistenceAndFailures();
  testBluetoothOwnerRelease();
  testBluetoothProtocolAndRecovery();
  testBluetoothFailureAndValidationPaths();
  testBluetoothBellPolicyValidation();
  testTripRecordCodecPreservesAndRewritesRecords();

  if (failures != 0) {
    std::cerr << failures << " test assertion group(s) failed.\n";
    return EXIT_FAILURE;
  }

  std::cout << "All native tests passed.\n";
  return EXIT_SUCCESS;
}
