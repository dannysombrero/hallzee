#include "BellPolicy.h"

namespace {
uint32_t dateKeyFor(time_t moment, uint16_t &minuteOfDay) {
  struct tm info;
  localtime_r(&moment, &info);
  minuteOfDay = static_cast<uint16_t>(info.tm_hour * 60 + info.tm_min);
  return static_cast<uint32_t>((info.tm_year + 1900) * 10000 + (info.tm_mon + 1) * 100 + info.tm_mday);
}
}

bool BellPolicy::begin() {
#ifdef ARDUINO
  if (!preferences.begin("bell_policy", false)) return false;
  preferencesReady = true;
  enforcementEnabled = preferences.getBool("enabled", false);
  const uint8_t savedCount = preferences.getUChar("count", 0);
  const size_t expectedBytes = static_cast<size_t>(savedCount) * sizeof(BellPolicyWindow);
  if (savedCount <= MAX_BELL_POLICY_WINDOWS && preferences.getBytesLength("windows") == expectedBytes) {
    activeCount = savedCount;
    if (expectedBytes > 0) preferences.getBytes("windows", activeWindows, expectedBytes);
  } else {
    activeCount = 0;
    enforcementEnabled = false;
  }
#endif
  return true;
}

void BellPolicy::beginUpdate(bool enabled) {
  stagedEnabled = enabled;
  stagedCount = 0;
}

bool BellPolicy::addStagedWindow(const BellPolicyWindow &window) {
  if (stagedCount >= MAX_BELL_POLICY_WINDOWS || window.dateKey < 20240101 ||
      window.startMinute >= 1440 || window.endMinute > 1440 ||
      window.endMinute <= window.startMinute ||
      window.firstWindowEndMinute < window.startMinute ||
      window.firstWindowEndMinute > window.endMinute ||
      window.lastWindowStartMinute < window.startMinute ||
      window.lastWindowStartMinute > window.endMinute) return false;
  stagedWindows[stagedCount++] = window;
  return true;
}

bool BellPolicy::commitUpdate(uint8_t expectedCount) {
  if (expectedCount != stagedCount) return false;
#ifdef ARDUINO
  if (!persist(stagedEnabled, stagedWindows, stagedCount)) return false;
#endif
  activeCount = stagedCount;
  enforcementEnabled = stagedEnabled;
  for (uint8_t index = 0; index < activeCount; index++) activeWindows[index] = stagedWindows[index];
  return true;
}

BellPolicyDecision BellPolicy::evaluate(time_t moment) const {
  if (!enforcementEnabled || moment <= 0) return BellPolicyDecision::Allow;
  uint16_t minute = 0;
  const uint32_t dateKey = dateKeyFor(moment, minute);
  for (uint8_t index = 0; index < activeCount; index++) {
    const BellPolicyWindow &window = activeWindows[index];
    if (window.dateKey != dateKey || minute < window.startMinute || minute >= window.endMinute) continue;
    BellPolicyDecision result = BellPolicyDecision::Allow;
    if (minute < window.firstWindowEndMinute) result = window.firstDecision;
    if (minute >= window.lastWindowStartMinute && static_cast<uint8_t>(window.lastDecision) > static_cast<uint8_t>(result)) {
      result = window.lastDecision;
    }
    return result;
  }
  return BellPolicyDecision::Allow;
}

#ifdef ARDUINO
bool BellPolicy::persist(bool enabled, const BellPolicyWindow *windows, uint8_t count) {
  if (!preferencesReady) return false;
  if (count == 0) {
    preferences.remove("windows");
  } else {
    const size_t bytes = static_cast<size_t>(count) * sizeof(BellPolicyWindow);
    if (preferences.putBytes("windows", windows, bytes) != bytes) return false;
  }
  if (preferences.putUChar("count", count) != sizeof(uint8_t)) return false;
  return preferences.putBool("enabled", enabled) == sizeof(bool);
}
#endif
