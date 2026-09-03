#pragma once

#include <Arduino.h>

// Product behavior
constexpr char CLOCK_CODE[] = "1234";
constexpr char LOG_SUMMARY_CODE[] = "9999";
constexpr char BLUETOOTH_DEVICE_NAME[] = "Hallzee";
constexpr int MAX_BLUETOOTH_COMMAND_LENGTH = 192;
constexpr uint8_t MIN_STUDENT_ID_LENGTH = 4;
constexpr uint8_t DEFAULT_STUDENT_ID_LENGTH = 10;
constexpr uint8_t MAX_STUDENT_ID_LENGTH = 16;
constexpr unsigned long RESET_HOLD_MS = 2000;
constexpr unsigned long PAIRING_HOLD_MS = 5000;
constexpr unsigned long OWNER_RESET_HOLD_MS = 10000;

// Display palette
constexpr uint16_t UI_NAVY = 0x1A4D;
constexpr uint16_t UI_BACKGROUND = 0xBDF7;
constexpr uint16_t UI_PANEL = 0xFFFF;
constexpr uint16_t UI_PANEL_DARK = 0xE73C;
constexpr uint16_t UI_TEXT = 0x2124;
constexpr uint16_t UI_MUTED = 0x6B6D;
constexpr uint16_t UI_GREEN = 0x3E8E;
constexpr uint16_t UI_AMBER = 0xFD20;
constexpr uint16_t UI_RED = 0xE986;

// TFT wiring
constexpr int TFT_CS = 5;
constexpr int TFT_RST = 22;
constexpr int TFT_DC = 21;
constexpr int TFT_MOSI = 23;
constexpr int TFT_SCLK = 18;

// Keypad wiring
constexpr byte KEYPAD_ROWS = 4;
constexpr byte KEYPAD_COLS = 3;
