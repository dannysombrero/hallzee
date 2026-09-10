#pragma once

#include <stdint.h>

struct TouchPoint { int32_t x = 0, y = 0; };
struct TouchRect {
  int16_t x, y, w, h;
  bool contains(TouchPoint p) const {
    return p.x >= x && p.x < x + w && p.y >= y && p.y < y + h;
  }
};

// Three measured corners establish both axis direction and rotation. Do not
// clamp coordinates: an off-screen sample must never become an edge-button tap.
class TouchCalibration {
public:
  bool fit(const TouchPoint *raw) {
    origin = raw[0];
    u = {raw[1].x - origin.x, raw[1].y - origin.y};
    v = {raw[2].x - origin.x, raw[2].y - origin.y};
    determinant = u.x * v.y - u.y * v.x;
    valid = determinant > 500000 || determinant < -500000;
    return valid;
  }
  TouchPoint map(TouchPoint raw) const {
    if (!valid) return {-1, -1};
    const int64_t dx = raw.x - origin.x, dy = raw.y - origin.y;
    return {int32_t(24 + 272 * (dx * v.y - dy * v.x) / determinant),
            int32_t(24 + 192 * (u.x * dy - u.y * dx) / determinant)};
  }
  bool valid = false;
private:
  TouchPoint origin, u, v;
  int32_t determinant = 0;
};

// One action on release after a stable press. Sliding off the original target
// cancels the entire gesture. Short pressure dropouts cannot generate repeats.
class TouchTap {
public:
  void suppress() { blocked = true; down = false; releasing = false; target = -1; }
  int pressed() const { return down && accepted && !cancelled ? target : -1; }
  int update(uint32_t now, bool touching, int hit) {
    if (touching) {
      releasing = false;
      if (blocked) return -1;
      if (!down) {
        down = true; accepted = false; cancelled = hit < 0;
        target = hit; started = now;
      }
      if (hit != target) cancelled = true;
      if (uint32_t(now - started) >= 40) accepted = true;
      return -1;
    }
    if (!releasing) { releasing = true; released = now; }
    if (uint32_t(now - released) < 70) return -1;
    const int result = !blocked && down && accepted && !cancelled ? target : -1;
    blocked = false; down = false; accepted = false; target = -1;
    return result;
  }
private:
  bool blocked = true, down = false, accepted = false;
  bool cancelled = false, releasing = false;
  int target = -1;
  uint32_t started = 0, released = 0;
};
