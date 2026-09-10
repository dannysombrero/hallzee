#include "TouchExperiment.h"
#if defined(HALLZEE_TOUCH_TEST)
#include "Config.h"
#include <SPI.h>

namespace {
constexpr TouchPoint targets[] = {{24, 24}, {296, 24}, {24, 216}, {296, 216}};
constexpr TouchRect clearRect = {12, 204, 140, 34};
constexpr TouchRect submitRect = {168, 204, 140, 34};
constexpr TouchRect testRect = {224, 114, 84, 32};
constexpr TouchRect toolbar[] = {{4, 4, 96, 30}, {112, 4, 96, 30}, {220, 4, 96, 30}};
}

void TouchExperiment::begin() {
  // Dedicated bus: no shared wires or default HSPI pins (the keypad uses
  // GPIO13/14). The sensor library's subsequent begin() preserves these pins.
  touchSpi.begin(TOUCH_SCLK, TOUCH_MISO, TOUCH_MOSI, TOUCH_CS);
  sensor.begin(touchSpi);
  sensor.setRotation(0); // Calibration maps raw axes to the actual visible screen.
  tap.suppress();
}

void TouchExperiment::button(TouchRect r, const char *label) {
  screen.fillRoundRect(r.x, r.y, r.w, r.h, 3, UI_PANEL);
  screen.drawRoundRect(r.x, r.y, r.w, r.h, 3, UI_FIELD_BORDER);
  screen.setFont(nullptr); screen.setTextSize(1); screen.setTextColor(UI_TEXT);
  screen.setCursor(r.x + (r.w - strlen(label) * 6) / 2, r.y + (r.h - 8) / 2);
  screen.print(label);
}

void TouchExperiment::open() {
  bootPending = false;
  if (calibrating || !calibration.valid) { startCalibration(); return; }
  testing = true; calibrating = false;
  entry = ""; taps = corrections = 0;
  tap.suppress(); highlighted = -1;
  drawTest();
}

void TouchExperiment::close() {
  if (calibrating) calibration.valid = false;
  bootPending = false; calibrating = testing = false;
  highlighted = -1; tap.suppress();
}

void TouchExperiment::startCalibration() {
  calibration.valid = false;
  calibrating = true; testing = false; step = 0;
  highlighted = -1; tap.suppress();
  drawCalibration();
}

void TouchExperiment::drawCalibration(const char *message) {
  screen.fillScreen(UI_BACKGROUND);
  screen.setFont(nullptr); screen.setTextWrap(false);
  screen.setTextSize(1); screen.setTextColor(UI_TEXT);
  screen.setCursor(36, 86); screen.print("TOUCH CALIBRATION "); screen.print(step + 1);
  screen.setCursor(36, 106); screen.print(message);
  screen.setCursor(36, 126); screen.print("Use fingertip or blunt stylus.");
  screen.setCursor(36, 146); screen.print("Physical *: skip / exit");
  const auto p = targets[step];
  screen.drawCircle(p.x, p.y, 10, UI_STATUS_RED);
  screen.drawFastHLine(p.x - 14, p.y, 29, UI_STATUS_RED);
  screen.drawFastVLine(p.x, p.y - 14, 29, UI_STATUS_RED);
}

TouchRect TouchExperiment::keyRect(int index) const {
  if (layout == 2) {
    const int row = index < 10 ? 0 : (index < 19 ? 1 : 2);
    const int col = index - (row == 0 ? 0 : row == 1 ? 10 : 19);
    return {int16_t(4 + row * 10 + col * 31), int16_t(100 + row * 37), 28, 32};
  }
  const int w = layout == 0 ? 88 : 28, h = layout == 0 ? 34 : 26;
  const int gap = layout == 0 ? 8 : 4;
  return {int16_t((320 - (3 * w + 2 * gap)) / 2 + (index % 3) * (w + gap)),
          int16_t(84 + (index / 3) * (h + 4)), int16_t(w), int16_t(h)};
}

char TouchExperiment::keyChar(int index) const {
  return layout == 2 ? "qwertyuiopasdfghjklzxcvbnm"[index] : "123456789<0#"[index];
}

void TouchExperiment::drawReadout() {
  screen.fillRect(0, 38, 320, 44, UI_BACKGROUND);
  screen.setFont(nullptr); screen.setTextSize(1); screen.setTextColor(UI_TEXT);
  screen.setCursor(8, 39);
  screen.print(layout == 0 ? "NUM 88x34px" : layout == 1 ? "NUM 28x26px" : "ABC 28x32px");
  screen.print("  Taps:"); screen.print(taps);
  screen.print("  Back:"); screen.print(corrections);
  screen.setCursor(8, 54); screen.print("> "); screen.print(entry);
  screen.setCursor(8, 69); screen.print("Pressure: "); screen.print(lastPressure);
  screen.print("  Physical * exits");
}

void TouchExperiment::drawTest() {
  screen.fillScreen(UI_BACKGROUND); screen.setTextWrap(false);
  button(toolbar[0], "MODE"); button(toolbar[1], "CALIBRATE"); button(toolbar[2], "EXIT");
  drawReadout();
  for (int i = 0; i < keyCount(); ++i) {
    const char key = keyChar(i);
    char label[] = {key, 0};
    button(keyRect(i), key == '#' ? "CLR" : label);
  }
  if (layout == 2) {
    button({8, 212, 96, 26}, "BACKSPACE");
    button({112, 212, 96, 26}, "SPACE");
    button({216, 212, 96, 26}, "CLEAR");
  }
}

void TouchExperiment::decorateIdle(bool available) {
  tap.suppress(); highlighted = -1;
  if (calibrating) { drawCalibration(); return; }
  if (testing) { drawTest(); return; }
  if (!calibration.valid) return;
  screen.drawRoundRect(clearRect.x, clearRect.y, clearRect.w, clearRect.h, 3, UI_FIELD_BORDER);
  screen.drawRoundRect(submitRect.x, submitRect.y, submitRect.w, submitRect.h, 3, UI_FIELD_BORDER);
  if (available) button(testRect, "TOUCH TEST");
}

int TouchExperiment::hit(TouchPoint p, bool available) const {
  if (calibrating) return 0;
  if (!calibration.valid) return -1;
  if (!testing) {
    if (clearRect.contains(p)) return 0;
    if (submitRect.contains(p)) return 1;
    if (available && testRect.contains(p)) return 2;
    return -1;
  }
  for (int i = 0; i < 3; ++i) if (toolbar[i].contains(p)) return i;
  for (int i = 0; i < keyCount(); ++i) if (keyRect(i).contains(p)) return 10 + i;
  if (layout == 2) {
    if (TouchRect{8, 212, 96, 26}.contains(p)) return 40;
    if (TouchRect{112, 212, 96, 26}.contains(p)) return 41;
    if (TouchRect{216, 212, 96, 26}.contains(p)) return 42;
  }
  return -1;
}

void TouchExperiment::highlight(int target, bool enabled) {
  if (target < 0 || calibrating) return;
  TouchRect r;
  if (!testing) r = target == 0 ? clearRect : target == 1 ? submitRect : testRect;
  else if (target < 3) r = toolbar[target];
  else if (target >= 40) r = {int16_t(8 + (target - 40) * 104), 212, 96, 26};
  else r = keyRect(target - 10);
  screen.drawRoundRect(r.x, r.y, r.w, r.h, 3, enabled ? UI_STATUS_GREEN : UI_FIELD_BORDER);
}

TouchExperiment::Action TouchExperiment::poll(bool available) {
  const uint32_t now = millis();
  if (uint32_t(now - lastPoll) < 10) return Action::None;
  lastPoll = now;
  const auto raw = sensor.getPoint();
  const bool touching = raw.z >= 400 && raw.x > 0 && raw.x < 4095 && raw.y > 0 && raw.y < 4095;
  if (touching) { lastRaw = {raw.x, raw.y}; lastPressure = raw.z; }
  const TouchPoint point = calibration.map({raw.x, raw.y});
  const int action = tap.update(now, touching, hit(point, available));
  const int pressed = tap.pressed();
  if (highlighted != pressed) {
    highlight(highlighted, false); highlight(pressed, true); highlighted = pressed;
  }
  if (action < 0) return Action::None;
  if (calibrating) {
    if (step < 3) samples[step] = lastRaw;
    ++step;
    if (step == 3 && !calibration.fit(samples)) {
      step = 0; drawCalibration("Points too close. Try again.");
    } else if (step == 4) {
      const auto check = calibration.map(lastRaw);
      if (abs(check.x - 296) > 18 || abs(check.y - 216) > 18) {
        calibration.valid = false; step = 0;
        drawCalibration("Missed check. Start again.");
      } else {
        calibrating = false; open();
      }
    } else drawCalibration();
    tap.suppress();
    return Action::None;
  }
  if (!testing) {
    if (action == 0) return Action::Clear;
    if (action == 1) return Action::Submit;
    if (action == 2) open();
    return Action::None;
  }
  if (action == 0) {
    layout = (layout + 1) % 3; entry = ""; taps = corrections = 0;
    tap.suppress(); drawTest();
  } else if (action == 1) startCalibration();
  else if (action == 2) { close(); return Action::Closed; }
  else {
    const char key = action == 40 ? '<' : action == 41 ? ' ' : action == 42 ? '#' : keyChar(action - 10);
    ++taps;
    if (key == '<') { if (entry.length()) entry.remove(entry.length() - 1); ++corrections; }
    else if (key == '#') { entry = ""; taps = corrections = 0; }
    else if (entry.length() < 48) entry += key;
    drawReadout();
  }
  return Action::None;
}
#endif
