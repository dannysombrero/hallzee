#include "TouchExperiment.h"
#include <algorithm>
#include <cassert>
#include <iostream>

uint32_t touchTestMillis = 0;
using Action = TouchExperiment::Action;

Action sample(TouchExperiment &experiment, int x, int y, bool down, uint32_t elapsed = 80,
              bool available = true) {
  touchTestMillis += elapsed;
  touchTestSample = {int16_t(x * 10 + 100), int16_t(y * 10 + 100), int16_t(down ? 1000 : 0)};
  return experiment.poll(available);
}
Action tap(TouchExperiment &experiment, int x, int y, bool available = true) {
  sample(experiment, x, y, false, 80, available);
  sample(experiment, x, y, false, 80, available);
  assert(sample(experiment, x, y, true, 80, available) == Action::None);
  assert(sample(experiment, x, y, true, 80, available) == Action::None);
  assert(sample(experiment, x, y, false, 10, available) == Action::None);
  return sample(experiment, x, y, false, 80, available);
}
void calibrate(TouchExperiment &experiment) {
  tap(experiment, 24, 24); tap(experiment, 296, 24);
  tap(experiment, 24, 216); tap(experiment, 296, 216);
}
bool printed(Adafruit_ILI9341 &screen, const char *text) {
  return std::find(screen.text.begin(), screen.text.end(), text) != screen.text.end();
}

int main() {
  Adafruit_ILI9341 screen;
  TouchExperiment experiment(screen);
  experiment.begin();
  assert(touchTestBus != &SPI && touchTestBus->bus == HSPI);
  assert(touchTestBus->sck == 16 && touchTestBus->mosi == 17 &&
         touchTestBus->miso == 19 && touchTestBus->cs == 4);
  assert(!SPI.initialized); // Touch initialization must not reconfigure TFT SPI.
  assert(experiment.pending());
  experiment.open();
  assert(experiment.active() && !experiment.pending());
  tap(experiment, 24, 24); tap(experiment, 296, 24); tap(experiment, 24, 216);
  experiment.close(); // Exiting before the fourth check must NOT enable Submit.
  assert(tap(experiment, 230, 220) == Action::None);
  experiment.open();
  tap(experiment, 24, 24); tap(experiment, 296, 24); tap(experiment, 24, 216);
  tap(experiment, 100, 100); // Incorrect fourth corner restarts calibration.
  assert(printed(screen, "Missed check. Start again."));
  calibrate(experiment);
  assert(experiment.active() && printed(screen, "NUM 88x34px"));
  assert(tap(experiment, 60, 100) == Action::None); // Sandbox digit, never Submit.
  assert(printed(screen, "1"));
  tap(experiment, 40, 20);
  assert(printed(screen, "NUM 28x26px"));
  tap(experiment, 40, 20);
  assert(printed(screen, "ABC 28x32px"));
  assert(tap(experiment, 15, 110) == Action::None);
  assert(printed(screen, "q"));
  assert(tap(experiment, 260, 20) == Action::Closed && !experiment.active());
  experiment.decorateIdle(true);
  assert(tap(experiment, 230, 220) == Action::Submit);
  assert(tap(experiment, 70, 220) == Action::Clear);
  assert(tap(experiment, 160, 220) == Action::None); // Gap between footer actions.
  assert(tap(experiment, 260, 130, false) == Action::None && !experiment.active());
  assert(tap(experiment, 260, 130) == Action::None && experiment.active());
  experiment.close();
  experiment.decorateIdle(true);
  sample(experiment, 230, 220, false); sample(experiment, 230, 220, false);
  sample(experiment, 230, 220, true); sample(experiment, 230, 220, true);
  experiment.suppress(); // Simulated redraw/update/setup transition during a press.
  sample(experiment, 230, 220, false);
  assert(sample(experiment, 230, 220, false) == Action::None);
  std::cout << "Touch experiment tests passed.\n";
}
