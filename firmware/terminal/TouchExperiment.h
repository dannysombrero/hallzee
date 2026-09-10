#pragma once

#if defined(HALLZEE_TOUCH_TEST)
#if !defined(HALLZEE_ILI9341) || (HALLZEE_DISPLAY_ROTATION != 1 && HALLZEE_DISPLAY_ROTATION != 3)
#error "Touch experiment requires ILI9341 landscape rotation 1 or 3"
#endif
#include <Adafruit_ILI9341.h>
#include <XPT2046_Touchscreen.h>
#include "TouchInput.h"
#include "Config.h"
#include <SPI.h>

class TouchExperiment {
public:
  enum class Action { None, Clear, Submit, Closed };
  explicit TouchExperiment(Adafruit_ILI9341 &screen) : screen(screen), touchSpi(HSPI), sensor(TOUCH_CS) {}
  void begin();
  bool active() const { return calibrating || testing; }
  bool pending() const { return bootPending; }
  void open();
  void close();
  void suppress() { tap.suppress(); }
  void decorateIdle(bool available);
  Action poll(bool available);
private:
  Adafruit_ILI9341 &screen;
  SPIClass touchSpi;
  XPT2046_Touchscreen sensor;
  TouchCalibration calibration;
  TouchTap tap;
  TouchPoint samples[3], lastRaw;
  bool bootPending = true, calibrating = false, testing = false;
  uint8_t step = 0, layout = 0;
  uint32_t lastPoll = 0;
  int highlighted = -1, lastPressure = 0;
  unsigned int taps = 0, corrections = 0;
  String entry;
  void startCalibration();
  void drawCalibration(const char *message = "Tap crosshair, then lift finger");
  void drawTest();
  void drawReadout();
  void button(TouchRect rect, const char *label);
  TouchRect keyRect(int index) const;
  int keyCount() const { return layout == 2 ? 26 : 12; }
  char keyChar(int index) const;
  int hit(TouchPoint point, bool available) const;
  void highlight(int target, bool enabled);
};
#endif
