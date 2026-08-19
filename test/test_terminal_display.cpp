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
#include "TerminalController.h"
#include "TerminalDisplay.h"
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
  bool cleared = false;
  String savedId;
  time_t savedTime = 0;
  std::vector<Record> records;
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
  bool appendTripRecord(const String &studentID, time_t outTime, time_t inTime,
                        long durationSeconds, const char *status) override {
    records.push_back({studentID, outTime, inTime, durationSeconds, status});
    return appendSucceeds;
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
    return true;
  }
};

class FakeBluetoothSerial : public BluetoothSerialPort {
public:
  bool beginSucceeds = true;
  bool connected = false;
  std::string input;
  std::vector<std::string> output;
  std::string deviceName;
  std::string pin;
  std::string partialLine;

  bool begin(const char *name) override { deviceName = name; return beginSucceeds; }
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
std::vector<char> setupKeys;
std::vector<char> numberKeys;
int clearCount = 0;
int submitCount = 0;
int resetCount = 0;

bool isKeypadSetupMode() { return keypadSetupMode; }
bool isKeypadResetAllowed() { return keypadResetAllowed; }
void onSetupKey(char key) { setupKeys.push_back(key); }
void onNumberKey(char key) { numberKeys.push_back(key); }
void onClear() { clearCount++; }
void onSubmit() { submitCount++; }
void onReset() { resetCount++; }

void resetKeypadCallbacks() {
  keypadSetupMode = false;
  keypadResetAllowed = false;
  setupKeys.clear();
  numberKeys.clear();
  clearCount = 0;
  submitCount = 0;
  resetCount = 0;
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
    "println:BATHROOM",
    "setTextColor:48631",
    "setCursor:9:14",
    "println:TERMINAL",
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
  expectTrue(setupKeys == std::vector<char>{'#'}, "setup mode delegates every key");
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
}

void testBluetoothProtocolAndRecovery() {
  FakeTripStorage storage;
  storage.syncRecords = {
    {7, "7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"},
    {8, "8,ID2,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"}
  };
  FakeBluetoothSerial serial;
  BluetoothSync sync(storage, serial, setBluetoothClock, onBluetoothClockSet);
  sync.begin();
  expectTrue(serial.deviceName == "Bathroom-Terminal" && serial.pin == "1234", "Bluetooth setup");

  serial.connected = true;
  sync.poll();
  expectTrue(contains(serial.output, "BATHROOM_TERMINAL_READY"), "Bluetooth readiness handshake");

  bluetoothClockSetCount = 0;
  serial.input = "TIME,2026-02-28,08:30:00\n";
  sync.poll();
  expectTrue(bluetoothClockSetCount == 1 && bluetoothClockYear == 2026, "valid time command");
  expectTrue(contains(serial.output, "TIME_ACK,OK") && contains(serial.output, "TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"), "time starts sync");

  serial.input = "ACK,7\n";
  sync.poll();
  expectTrue(storage.markedSynced == std::vector<uint32_t>{7}, "matching ACK marks record synced");
  expectTrue(contains(serial.output, "TRIP,8,ID2,2026-01-01,09:00:00,09:10:00,600,COMPLETE,0"), "ACK advances one record at a time");

  serial.input = "ACK,999\n";
  sync.poll();
  expectTrue(contains(serial.output, "ACK_ERROR,UNEXPECTED_ID"), "unexpected ACK rejected");

  serial.input = "TIME,2025-02-29,08:30:00\nWHAT\n";
  sync.poll();
  expectTrue(contains(serial.output, "TIME_ACK,ERROR") && contains(serial.output, "ERROR,UNKNOWN_COMMAND"), "invalid commands rejected");

  serial.connected = false;
  sync.poll();
  serial.connected = true;
  serial.input = "SYNC_ALL\n";
  sync.poll();
  expectTrue(contains(serial.output, "TRIP,7,ID1,2026-01-01,08:00:00,08:10:00,600,COMPLETE,0"), "disconnect resets full history sync state");
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

int main() {
  testEmptyIdEntryGoldenInstructions();
  testOccupiedIdleScreenGoldenInstructions();
  testClockSetupStepUsesCorrectPromptAndHint();
  testCheckedOutScreenIncludesDurationInstruction();
  testEveryStatusViewEmitsItsContentAndExpectedPause();
  testPartialRedrawsAndAllClockSetupPrompts();
  testTerminalCheckoutAndCheckinAreDeterministic();
  testTerminalStorageFailuresDoNotLosePassState();
  testTerminalRestorationAndSubmissionClassification();
  testTerminalClampsBackwardTimeAndPersistsManualReset();
  testKeypadControllerInterpretsKeysAndResetGesture();
  testBluetoothProtocolAndRecovery();

  if (failures != 0) {
    std::cerr << failures << " test assertion group(s) failed.\n";
    return EXIT_FAILURE;
  }

  std::cout << "All native tests passed.\n";
  return EXIT_SUCCESS;
}
