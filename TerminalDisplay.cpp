#include "TerminalDisplay.h"

#include "Config.h"
#include "DisplayColors.h"

TerminalDisplay::TerminalDisplay(DisplayPort &display) : display(display) {}

void TerminalDisplay::showBluetoothClockSynced(const String &date, const String &time) {
  display.fillScreen(DISPLAY_GREEN);
  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 24);
  display.println("CLOCK SYNCED");
  display.setTextSize(1);
  display.setCursor(10, 66);
  display.println(date);
  display.setCursor(10, 82);
  display.println(time);
  display.pause(1200);
}

void TerminalDisplay::showCheckedOut(const String &id, const String &time) {
  display.fillScreen(DISPLAY_GREEN);
  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 20);
  display.println("CHECKED");
  display.setCursor(10, 44);
  display.println("OUT");
  display.setTextSize(1);
  display.setCursor(10, 75);
  display.print("ID: ");
  display.println(id);
  display.setCursor(10, 95);
  display.print("Time: ");
  display.println(time);
  display.pause(2000);
}

void TerminalDisplay::showCheckedIn(unsigned long elapsedSeconds) {
  display.fillScreen(DISPLAY_BLUE);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 12);
  display.println("CHECKED");
  display.setCursor(10, 36);
  display.println("IN");

  const unsigned long hours = elapsedSeconds / 3600;
  const unsigned long minutes = (elapsedSeconds % 3600) / 60;
  const unsigned long seconds = elapsedSeconds % 60;

  display.setTextSize(1);
  display.setCursor(10, 68);
  display.println("Time away:");
  display.setTextSize(2);
  display.setCursor(10, 85);
  if (hours > 0) {
    display.print(hours);
    display.print("h ");
  }
  display.print(minutes);
  display.print("m ");
  if (seconds < 10) {
    display.print("0");
  }
  display.print(seconds);
  display.print("s");
  display.pause(3000);
}

void TerminalDisplay::showPassOccupied() {
  display.fillScreen(DISPLAY_RED);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 20);
  display.println("PASS");
  display.setCursor(10, 44);
  display.println("OCCUPIED");
  display.setTextSize(1);
  display.setCursor(10, 80);
  display.println("Waiting for current");
  display.setCursor(10, 95);
  display.println("student to return.");
  display.pause(2000);
}

void TerminalDisplay::showEnterId() {
  display.fillScreen(DISPLAY_RED);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 30);
  display.println("ENTER ID");
  display.setTextSize(1);
  display.setCursor(10, 72);
  display.println("Type your student ID");
  display.setCursor(10, 87);
  display.println("before submitting.");
  display.pause(1500);
}

void TerminalDisplay::showStudentIdTooLong(uint8_t maximumLength) {
  display.fillScreen(DISPLAY_RED);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 22);
  display.println("ID TOO LONG");
  display.setTextSize(1);
  display.setCursor(10, 68);
  display.print("Current limit: ");
  display.println(String(maximumLength));
  display.setCursor(10, 84);
  display.println("Enter the ID again.");
  display.pause(1800);
}

void TerminalDisplay::showStorageError() {
  display.fillScreen(DISPLAY_RED);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 20);
  display.println("NOT SAVED");
  display.setTextSize(1);
  display.setCursor(10, 62);
  display.println("Trip log unavailable.");
  display.setCursor(10, 77);
  display.println("Pass remains occupied.");
  display.pause(2200);
}

void TerminalDisplay::showTripLogSummary(
  bool logReady,
  uint32_t recordCount,
  uint32_t latestTripID
) {
  display.fillScreen(DISPLAY_BLACK);
  display.setTextColor(DISPLAY_YELLOW);
  display.setTextSize(2);
  display.setCursor(10, 12);
  display.println("TRIP LOG");
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(1);

  if (!logReady) {
    display.setCursor(10, 58);
    display.println("Storage unavailable");
  } else {
    display.setCursor(10, 52);
    display.print("Saved records: ");
    display.println(String(recordCount));
    display.setCursor(10, 70);
    if (latestTripID == 0) {
      display.println("No trips recorded yet.");
    } else {
      display.print("Latest trip ID: ");
      display.println(String(latestTripID));
    }
  }

  display.setCursor(10, 110);
  display.println("Returning...");
  display.pause(2500);
}

void TerminalDisplay::showManualReset(const String &id) {
  display.fillScreen(DISPLAY_YELLOW);
  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 22);
  display.println("PASS");
  display.setCursor(10, 46);
  display.println("RESET");
  display.setTextSize(1);
  display.setCursor(10, 80);
  display.print("Cleared ID: ");
  display.println(id);
  display.pause(1800);
}

void TerminalDisplay::drawIdEntry(const String &entry) {
  display.fillRect(13, 79, 134, 17, UI_PANEL_DARK);
  display.setTextColor(UI_TEXT);
  const bool useCompactText = entry.length() > 10;
  display.setTextSize(useCompactText ? 1 : 2);
  display.setCursor(16, useCompactText ? 83 : 79);
  display.print(entry.length() == 0 ? "_" : entry);
}

void TerminalDisplay::drawClock(const String &time) {
  display.fillRect(102, 7, 53, 11, UI_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(1);
  display.setCursor(104, 8);
  display.print(time);
}

void TerminalDisplay::drawIdleScreen(const String &currentOutId, const String &entry) {
  display.fillScreen(UI_BACKGROUND);
  display.setTextWrap(false);
  display.fillRect(0, 0, 160, 24, UI_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(1);
  display.setCursor(9, 3);
  display.println("HALLZEE");
  display.setTextColor(UI_BACKGROUND);
  display.setCursor(9, 14);
  display.println("TERMINAL");

  const uint16_t stateColor = currentOutId.length() == 0 ? UI_GREEN : UI_RED;
  display.fillRoundRect(8, 28, 144, 31, 6, UI_PANEL);
  display.fillRoundRect(8, 28, 5, 31, 3, stateColor);
  if (currentOutId.length() == 0) {
    display.setTextColor(UI_GREEN);
    display.setTextSize(2);
    display.setCursor(20, 34);
    display.println("OPEN");
    display.setTextColor(UI_MUTED);
    display.setTextSize(1);
    display.setCursor(21, 50);
    display.println("Bathroom is open");
  } else {
    display.setTextColor(UI_RED);
    display.setTextSize(2);
    display.setCursor(20, 34);
    display.println("OCCUPIED");
    display.setTextColor(UI_MUTED);
    display.setTextSize(1);
    display.setCursor(21, 50);
    display.print(currentOutId.length() > 10 ? "OUT ID " : "OUT WITH ID ");
    display.println(currentOutId);
  }

  display.setTextColor(UI_MUTED);
  display.setTextSize(1);
  display.setCursor(10, 66);
  display.println("STUDENT ID");
  display.drawRoundRect(9, 75, 142, 25, 5, UI_MUTED);
  display.fillRoundRect(10, 76, 140, 23, 4, UI_PANEL_DARK);
  display.setCursor(10, 114);
  display.print("* CLEAR");
  display.setCursor(103, 114);
  display.print("# SUBMIT");
  drawIdEntry(entry);
}

void TerminalDisplay::drawClockSetupEntry(const String &entry) {
  display.fillRect(13, 80, 134, 17, UI_PANEL_DARK);
  display.setTextColor(UI_TEXT);
  display.setTextSize(2);
  display.setCursor(16, 80);
  display.print(entry.length() == 0 ? "_" : entry);
}

void TerminalDisplay::drawClockSetupScreen(ClockSetupStep step, const String &entry) {
  display.fillScreen(UI_BACKGROUND);
  display.setTextWrap(false);
  display.fillRect(0, 0, 160, 24, UI_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(1);
  display.setCursor(9, 3);
  display.println("SET TIME");
  display.setTextColor(UI_BACKGROUND);
  display.setCursor(112, 8);
  display.print("STEP ");
  display.print(static_cast<int>(step) + 1);
  display.print("/6");

  display.fillRoundRect(8, 28, 144, 31, 6, UI_PANEL);
  display.setTextColor(UI_MUTED);
  display.setTextSize(1);
  display.setCursor(20, 33);
  display.println("SET DATE & TIME");

  const char *prompts[] = {"MONTH", "DAY", "YEAR", "HOUR", "MINUTE", "AM / PM"};
  const char *hints[] = {"1-12", "1-31", "YYYY", "1-12", "0-59", "1 or 2"};
  const int index = static_cast<int>(step);
  display.setTextColor(UI_TEXT);
  display.setTextSize(2);
  display.setCursor(20, 42);
  display.println(prompts[index]);
  display.setTextColor(UI_MUTED);
  display.setTextSize(1);
  display.setCursor(112, 46);
  display.println(hints[index]);

  display.setCursor(10, 67);
  display.println("ENTER VALUE");
  display.drawRoundRect(9, 76, 142, 25, 5, UI_MUTED);
  display.fillRoundRect(10, 77, 140, 23, 4, UI_PANEL_DARK);
  display.setCursor(10, 114);
  display.print("* CLEAR");
  display.setCursor(106, 114);
  display.print("# NEXT");
  drawClockSetupEntry(entry);
}

void TerminalDisplay::showInvalidClockValue(const String &message) {
  display.fillScreen(UI_BACKGROUND);
  display.fillRect(0, 0, 160, 26, UI_NAVY);
  display.fillRoundRect(8, 37, 144, 52, 7, UI_PANEL);
  display.fillRoundRect(8, 37, 5, 52, 3, UI_RED);
  display.setTextColor(UI_RED);
  display.setTextSize(2);
  display.setCursor(20, 51);
  display.println("TRY AGAIN");
  display.setTextColor(UI_TEXT);
  display.setTextSize(1);
  display.setCursor(20, 79);
  display.println(message);
  display.pause(1200);
}

void TerminalDisplay::showClockSet(const String &date, const String &time) {
  display.fillScreen(DISPLAY_GREEN);
  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 18);
  display.println("CLOCK SET");
  display.setTextSize(1);
  display.setCursor(10, 60);
  display.println(date);
  display.setCursor(10, 80);
  display.println(time);
  display.pause(1800);
}

void TerminalDisplay::showPairing(
  const String &suffix,
  uint32_t passkey
) {
  display.fillScreen(UI_BACKGROUND);
  display.setTextWrap(false);
  display.setTextColor(UI_TEXT);
  display.setTextSize(2);
  display.setCursor(10, 8);
  display.println("PAIRING");
  display.setTextSize(1);
  display.setCursor(10, 29);
  display.print("Hallzee-");
  display.println(suffix);
  display.setCursor(10, 49);
  display.println("ENTER THIS PASSKEY");
  display.setTextSize(2);
  display.setCursor(10, 68);
  if (passkey < 100000) display.print("0");
  display.println(String(passkey));
  display.setCursor(10, 101);
  display.setTextSize(1);
  display.println("Enter passkey in app");
}

void TerminalDisplay::showPairingComplete(const String &suffix) {
  display.fillScreen(DISPLAY_GREEN);
  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 22);
  display.println("CLAIMED");
  display.setTextSize(1);
  display.setCursor(10, 62);
  display.print("Hallzee-");
  display.println(suffix);
  display.setCursor(10, 82);
  display.println("This terminal is linked.");
  display.pause(1800);
}

void TerminalDisplay::showPairingError(const String &message) {
  display.fillScreen(DISPLAY_RED);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 22);
  display.println("PAIR ERROR");
  display.setTextSize(1);
  display.setCursor(10, 64);
  display.println(message);
  display.pause(1800);
}

void TerminalDisplay::showOwnerReset() {
  display.fillScreen(DISPLAY_YELLOW);
  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 22);
  display.println("OWNER RESET");
  display.setTextSize(1);
  display.setCursor(10, 64);
  display.println("Pairing is cleared.");
  display.setCursor(10, 80);
  display.println("Hold * + # for 5 sec");
  display.setCursor(10, 96);
  display.println("to pair again.");
  display.pause(2200);
}
