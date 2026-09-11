#include "TerminalDisplay.h"

#include "Config.h"
#include "DisplayColors.h"

TerminalDisplay::TerminalDisplay(DisplayPort &display) : display(display) {}

void TerminalDisplay::prepareScreenTransition(uint16_t backgroundColor) {
  clockSetupActive = false;
  display.fillScreen(backgroundColor);
  display.setTextWrap(false);
  display.setTextSize(1);
  display.setFont(DisplayFont::BuiltIn);
}

void TerminalDisplay::showBluetoothClockSynced(const String &date, const String &time) {
  prepareScreenTransition(DISPLAY_GREEN);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_BLACK);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("CLOCK SYNCED");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println(date);
    display.setCursor(24, 135);
    display.println(time);
    display.pause(1200);
    return;
  }

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
  if (display.isNative320x240()) {
    prepareScreenTransition(DISPLAY_GREEN);
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("CHECKED OUT");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.print("ID: ");
    display.setFont(DisplayFont::DMSansBold18);
    display.println(id);

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 150);
    display.print("Time: ");
    display.setFont(DisplayFont::DMSansBold18);
    display.println(time);
    display.pause(2000);
    return;
  }

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

void TerminalDisplay::showCheckedOutWarning(const String &id, const String &time) {
  prepareScreenTransition(UI_AMBER);
  display.setTextColor(DISPLAY_BLACK);
  if (display.isNative320x240()) {
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 52);
    display.println("CHECKOUT WARNING");
    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 102);
    display.println("Bell-time window is active.");
    display.setCursor(24, 137);
    display.print("ID: ");
    display.println(id);
    display.setCursor(24, 165);
    display.print("Time: ");
    display.println(time);
  } else {
    display.setTextSize(2);
    display.setCursor(10, 18);
    display.println("WARNING");
    display.setTextSize(1);
    display.setCursor(10, 55);
    display.println("Bell window active.");
    display.setCursor(10, 75);
    display.print("ID: ");
    display.println(id);
  }
  display.pause(2400);
}

void TerminalDisplay::showCheckedIn(unsigned long elapsedSeconds) {
  prepareScreenTransition(DISPLAY_BLUE);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("CHECKED IN");

    const unsigned long hours = elapsedSeconds / 3600;
    const unsigned long minutes = (elapsedSeconds % 3600) / 60;
    const unsigned long seconds = elapsedSeconds % 60;

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println("Time away:");

    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 150);
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
    display.println("s");
    display.pause(3000);
    return;
  }

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

void TerminalDisplay::showPolicyLocked() {
  prepareScreenTransition(DISPLAY_RED);
  display.setTextColor(DISPLAY_WHITE);
  if (display.isNative320x240()) {
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("PASS WINDOW CLOSED");
    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 115);
    display.println("Try again after the bell window.");
  } else {
    display.setTextSize(2);
    display.setCursor(10, 20);
    display.println("PASS CLOSED");
    display.setTextSize(1);
    display.setCursor(10, 65);
    display.println("Try again after bell window.");
  }
  display.pause(2200);
}

void TerminalDisplay::showPassOccupied() {
  prepareScreenTransition(DISPLAY_RED);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("PASS OCCUPIED");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println("Waiting for current");
    display.setCursor(24, 135);
    display.println("student to return.");
    display.pause(2000);
    return;
  }

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
  prepareScreenTransition(DISPLAY_RED);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("ENTER ID");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println("Type your student ID");
    display.setCursor(24, 135);
    display.println("before submitting.");
    display.pause(1500);
    return;
  }

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
  prepareScreenTransition(DISPLAY_RED);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("ID TOO LONG");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.print("Current limit: ");
    display.println(String(maximumLength));
    display.setCursor(24, 135);
    display.println("Enter the ID again.");
    display.pause(1800);
    return;
  }

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
  prepareScreenTransition(DISPLAY_RED);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("NOT SAVED");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println("Trip log unavailable.");
    display.setCursor(24, 135);
    display.println("Pass remains occupied.");
    display.pause(2200);
    return;
  }

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
  prepareScreenTransition(DISPLAY_BLACK);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_YELLOW);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 50);
    display.println("TRIP LOG");

    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansRegular12);
    if (!logReady) {
      display.setCursor(24, 95);
      display.println("Storage unavailable");
    } else {
      display.setCursor(24, 95);
      display.print("Saved records: ");
      display.println(String(recordCount));
      display.setCursor(24, 125);
      if (latestTripID == 0) {
        display.println("No trips recorded yet.");
      } else {
        display.print("Latest trip ID: ");
        display.println(String(latestTripID));
      }
    }
    display.setCursor(24, 180);
    display.println("Returning...");
    display.pause(2500);
    return;
  }

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
  prepareScreenTransition(DISPLAY_YELLOW);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_BLACK);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("PASS RESET");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.print("Cleared ID: ");
    display.println(id);
    display.pause(1800);
    return;
  }

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
  if (display.isNative320x240()) {
  // This is deliberately a flat readout: it must not resemble a touch control.
  display.fillRoundRect(12, 146, 296, 44, 4, UI_FIELD);
  display.drawRoundRect(12, 146, 296, 44, 4, UI_FIELD_BORDER);
  display.setTextColor(UI_TEXT);
  if (entry.length() == 0) {
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(22, 185);
    display.print("-");
  } else {
    display.setFont(entry.length() > 12 ? DisplayFont::DMSansBold12 : DisplayFont::DMSansBold18);
    display.setCursor(22, entry.length() > 12 ? 174 : 179);
    display.print(entry);
  }
  return;
  }
  display.fillRect(13, 79, 134, 17, UI_PANEL_DARK);
  display.setTextColor(UI_TEXT);
  const bool useCompactText = entry.length() > 10;
  display.setTextSize(useCompactText ? 1 : 2);
  display.setCursor(16, useCompactText ? 83 : 79);
  display.print(entry.length() == 0 ? "_" : entry);
}

void TerminalDisplay::drawClock(const String &time) {
  if (display.isNative320x240()) {
  display.fillRect(215, 3, 105, 36, UI_HEADER_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setFont(DisplayFont::DMSansBold9);
  int16_t timeWidth = 0;
  for (unsigned int i = 0; i < time.length(); i++) {
    char c = time.charAt(i);
    const bool isLast = (i == time.length() - 1);
    if (isLast && c == 'M') {
      timeWidth += 15;
    } else {
      switch (c) {
        case ' ': timeWidth += 4; break;
        case ':': timeWidth += 5; break;
        case '0': timeWidth += 13; break;
        case '1': timeWidth += 7; break;
        case '2': timeWidth += 10; break;
        case '3': timeWidth += 11; break;
        case '4': timeWidth += 12; break;
        case '5': timeWidth += 11; break;
        case '6': timeWidth += 11; break;
        case '7': timeWidth += 10; break;
        case '8': timeWidth += 11; break;
        case '9': timeWidth += 11; break;
        case 'A': timeWidth += 13; break;
        case 'P': timeWidth += 11; break;
        case 'M': timeWidth += 16; break;
        default:  timeWidth += 9; break;
      }
    }
  }
  const int16_t cursorX = 308 - timeWidth;
  display.setCursor(cursorX, 24);
  display.print(time);
  return;
  }
  display.fillRect(102, 1, 53, 11, UI_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(1);
  display.setCursor(104, 2);
  display.print(time);
}

void TerminalDisplay::drawBluetoothStatus(bool connected) {
  if (display.isNative320x240()) {
  const uint16_t color = connected ? UI_HALLZEE_BLUE : UI_BLUETOOTH_MUTED;
  display.fillRect(174, 3, 46, 36, UI_HEADER_NAVY);
  // Authentic Bluetooth rune geometry with double-stroke lines for crisp presence
  const int16_t xc = 186;
  const int16_t ytop = 8;
  const int16_t ybot = 32;
  const int16_t w = 6;
  const int16_t ytop_lobe = 14;
  const int16_t ybot_lobe = 26;

  // Central spine
  display.drawLine(xc, ytop, xc, ybot, color);
  display.drawLine(xc + 1, ytop, xc + 1, ybot, color);

  // Top lobe & diagonal down-left
  display.drawLine(xc, ytop, xc + w, ytop_lobe, color);
  display.drawLine(xc + 1, ytop, xc + w + 1, ytop_lobe, color);
  display.drawLine(xc + w, ytop_lobe, xc - w, ybot_lobe, color);
  display.drawLine(xc + w + 1, ytop_lobe, xc - w + 1, ybot_lobe, color);

  // Bottom lobe & diagonal up-left
  display.drawLine(xc, ybot, xc + w, ybot_lobe, color);
  display.drawLine(xc + 1, ybot, xc + w + 1, ybot_lobe, color);
  display.drawLine(xc + w, ybot_lobe, xc - w, ytop_lobe, color);
  display.drawLine(xc + w + 1, ybot_lobe, xc - w + 1, ytop_lobe, color);

  if (connected) {
    display.fillRoundRect(201, 17, 6, 6, 2, UI_HALLZEE_BLUE);
  }
  return;
  }
  (void)connected;
}

void TerminalDisplay::drawIdleScreen(const String &currentOutId, const String &entry) {
  if (display.isNative320x240()) {
  prepareScreenTransition(UI_BACKGROUND);
  display.fillRect(0, 0, 320, 40, UI_HEADER_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setFont(DisplayFont::DMSansBold8);
  display.setCursor(12, 19);
  display.println("HALLZEE");
  display.setTextColor(UI_FIELD);
  display.setFont(DisplayFont::DMSansRegular6);
  display.setCursor(12, 29);
  drawTerminalLabel();
  display.setTextColor(UI_TEXT);

  const bool available = currentOutId.length() == 0;
  const uint16_t accent = available ? UI_STATUS_GREEN : UI_STATUS_RED;
  const uint16_t panel = available ? UI_PALE_GREEN : UI_PALE_RED;
  display.fillRoundRect(12, 54, 296, 54, 8, panel);
  display.fillRoundRect(18, 60, 5, 42, 3, accent);
  display.setTextColor(accent);
  display.setFont(DisplayFont::DMSansBold12);
  display.setCursor(32, 80);
  display.println(available ? "AVAILABLE" : "OCCUPIED");
  display.setTextColor(UI_MUTED);
  display.setFont(DisplayFont::DMSansRegular9);
  display.setCursor(32, 98);
  if (available) display.println("READY FOR STUDENT ID");
  else {
    display.print("OUT WITH ID ");
    display.println(currentOutId);
  }

  display.setTextColor(UI_TEXT);
  display.setFont(DisplayFont::DMSansBold9);
  display.setCursor(12, 138);
  display.println("STUDENT ID");
  drawIdEntry(entry);

  display.setTextColor(UI_TEXT);
  display.setFont(DisplayFont::DMSansBold8);
  display.setCursor(51, 224);
  display.print("*");
  display.setCursor(64, 224);
  display.print("CLEAR");
  display.drawLine(160, 212, 160, 230, UI_FIELD_BORDER);
  display.setCursor(205, 224);
  display.print("#");
  display.setCursor(224, 224);
  display.print("SUBMIT");
  return;
  }
  display.fillScreen(UI_BACKGROUND);
  display.setTextWrap(false);
  display.fillRect(0, 0, 160, 24, UI_NAVY);
  display.setTextColor(DISPLAY_WHITE);
  display.setTextSize(1);
  display.setCursor(9, 3);
  display.println("HALLZEE");
  display.setTextColor(UI_BACKGROUND);
  display.setCursor(9, 14);
  drawTerminalLabel();

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
  if (display.isNative320x240()) {
    display.fillRoundRect(12, 154, 296, 44, 4, UI_FIELD);
    display.drawRoundRect(12, 154, 296, 44, 4, UI_FIELD_BORDER);
    display.setTextColor(UI_TEXT);
    if (entry.length() == 0) {
      display.setFont(DisplayFont::DMSansBold18);
      display.setCursor(22, 196);
      display.print("-");
    } else {
      display.setFont(DisplayFont::DMSansBold18);
      display.setCursor(22, 187);
      display.print(entry);
    }
    return;
  }
  display.fillRect(13, 80, 134, 17, UI_PANEL_DARK);
  display.setTextColor(UI_TEXT);
  display.setTextSize(2);
  display.setCursor(16, 80);
  display.print(entry.length() == 0 ? "_" : entry);
}

void TerminalDisplay::drawClockSetupScreen(ClockSetupStep step, const String &entry) {
  if (display.isNative320x240()) {
    const char *prompts[] = {"MONTH", "DAY", "YEAR", "HOUR", "MINUTE", "AM / PM"};
    const char *hints[] = {"1-12", "1-31", "YYYY", "1-12", "0-59", "1 or 2"};
    const int index = static_cast<int>(step);

    if (!clockSetupActive) {
      prepareScreenTransition(UI_BACKGROUND);
      clockSetupActive = true;

      display.fillRect(0, 0, 320, 40, UI_HEADER_NAVY);
      display.setTextColor(DISPLAY_WHITE);
      display.setFont(DisplayFont::DMSansBold8);
      display.setCursor(12, 19);
      display.println("HALLZEE");
      display.setTextColor(UI_FIELD);
      display.setFont(DisplayFont::DMSansRegular6);
      display.setCursor(12, 29);
      drawTerminalLabel();
      display.setTextColor(DISPLAY_WHITE);
      display.setFont(DisplayFont::DMSansBold9);
      display.setCursor(187, 24);
      display.print("SET TIME");

      display.setTextColor(UI_TEXT);
      display.setFont(DisplayFont::DMSansBold9);
      display.setCursor(12, 56);
      display.println("SET DATE & TIME");

      display.setFont(DisplayFont::DMSansRegular6);
      display.setCursor(12, 120);
      display.println("Pair: hold * + # for 5 seconds");
      display.setFont(DisplayFont::DMSansBold9);
      display.setCursor(12, 146);
      display.println("ENTER VALUE");

      display.setFont(DisplayFont::DMSansBold8);
      display.setCursor(51, 229);
      display.print("*");
      display.setCursor(64, 229);
      display.print("CLEAR");
      display.drawLine(160, 217, 160, 232, UI_FIELD_BORDER);
      display.setCursor(213, 229);
      display.print("#");
      display.setCursor(232, 229);
      display.print("NEXT");
    }

    display.fillRoundRect(12, 63, 296, 44, 8, UI_PANEL);
    display.fillRoundRect(18, 69, 5, 32, 3, UI_HALLZEE_BLUE);
    display.setTextColor(UI_HALLZEE_BLUE);
    display.setFont(DisplayFont::DMSansBold12);
    display.setCursor(36, 90);
    display.println(prompts[index]);
    display.setTextColor(UI_MUTED);
    display.setFont(DisplayFont::DMSansRegular9);
    display.setCursor(250, 90);
    display.println(hints[index]);

    drawClockSetupEntry(entry);
    return;
  }
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
  display.setCursor(10, 103);
  display.println("Pair: * + # for 5s");
  display.setCursor(106, 114);
  display.print("# NEXT");
  drawClockSetupEntry(entry);
}

void TerminalDisplay::showInvalidClockValue(const String &message) {
  clockSetupActive = false;
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
  prepareScreenTransition(DISPLAY_GREEN);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_BLACK);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("CLOCK SET");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println(date);
    display.setCursor(24, 135);
    display.println(time);
    display.pause(1800);
    return;
  }

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
  const String &terminalId,
  const String &name,
  uint32_t passkey,
  bool bondRepair
) {
  prepareScreenTransition(UI_BACKGROUND);
  display.setFont(DisplayFont::BuiltIn);
  display.setTextWrap(false);
  const bool large = display.isNative320x240();
  display.setTextColor(UI_TEXT);
  display.setTextSize(2);
  display.setCursor(10, large ? 16 : 6);
  display.println(bondRepair ? "BT REPAIR" : "PAIRING");
  display.setTextSize(1);
  display.setCursor(10, large ? 52 : 28);
  display.println(name);
  display.setCursor(10, large ? 72 : 43);
  display.print("ID: ");
  display.println(terminalId);
  display.setCursor(10, large ? 105 : 62);
  display.println("ENTER THIS PASSKEY");
  display.setTextSize(large ? 3 : 2);
  display.setCursor(10, large ? 130 : 78);
  String digits = String(passkey);
  while (digits.length() < 6) digits = String("0") + digits.c_str();
  display.println(digits);
  display.setTextSize(1);
  display.setCursor(10, large ? 192 : 108);
  display.println(bondRepair ? "Code in OS prompt" : "Enter passkey in app");
}

void TerminalDisplay::drawTerminalLabel() {
  display.setFont(DisplayFont::BuiltIn);
  display.setTextSize(1);
  // The full name is visible in pairing; abbreviate only the narrow header.
  display.print("Terminal: ");
  const unsigned int limit = 15;
  display.println(friendlyName.length() > limit ? friendlyName.substring(0, limit - 3) + "..." : friendlyName);
}

void TerminalDisplay::showBondRepairWaiting() {
  prepareScreenTransition(UI_BACKGROUND);
  display.setFont(DisplayFont::BuiltIn);
  display.setTextColor(UI_TEXT);
  display.setTextSize(2);
  display.setCursor(10, 16);
  display.println("BT REPAIR");
  display.setTextSize(1);
  display.setCursor(10, 52);
  display.println("Disconnecting...");
  display.setCursor(10, 72);
  display.println("Please wait");
}

void TerminalDisplay::showPairingComplete(const String &suffix, bool bondRepair) {
  prepareScreenTransition(DISPLAY_GREEN);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_BLACK);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println(bondRepair ? "REPAIRED" : "CLAIMED");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.print("Hallzee-");
    display.println(suffix);
    display.setCursor(24, 135);
    display.println("This terminal is linked.");
    display.pause(1800);
    return;
  }

  display.setTextColor(DISPLAY_BLACK);
  display.setTextSize(2);
  display.setCursor(10, 22);
  display.println(bondRepair ? "REPAIRED" : "CLAIMED");
  display.setTextSize(1);
  display.setCursor(10, 62);
  display.print("Hallzee-");
  display.println(suffix);
  display.setCursor(10, 82);
  display.println("This terminal is linked.");
  display.pause(1800);
}

void TerminalDisplay::showPairingError(const String &message) {
  prepareScreenTransition(DISPLAY_RED);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_WHITE);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("PAIR ERROR");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 110);
    display.println(message);
    display.pause(1800);
    return;
  }

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
  prepareScreenTransition(DISPLAY_YELLOW);
  if (display.isNative320x240()) {
    display.setTextColor(DISPLAY_BLACK);
    display.setFont(DisplayFont::DMSansBold18);
    display.setCursor(24, 60);
    display.println("OWNER RESET");

    display.setFont(DisplayFont::DMSansRegular12);
    display.setCursor(24, 100);
    display.println("Pairing is cleared.");
    display.setCursor(24, 125);
    display.println("Hold * + # for 5 sec");
    display.setCursor(24, 150);
    display.println("to pair again.");
    display.pause(2200);
    return;
  }

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
