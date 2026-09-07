#pragma once

#include <Arduino.h>
#include <time.h>

#ifdef ARDUINO
#include <Preferences.h>
#endif

constexpr uint8_t MAX_BELL_POLICY_WINDOWS = 96;

enum class BellPolicyDecision : uint8_t {
  Allow = 0,
  Warn = 1,
  Lock = 2
};

struct BellPolicyWindow {
  uint32_t dateKey = 0;
  uint16_t startMinute = 0;
  uint16_t endMinute = 0;
  uint16_t firstWindowEndMinute = 0;
  uint16_t lastWindowStartMinute = 0;
  BellPolicyDecision firstDecision = BellPolicyDecision::Allow;
  BellPolicyDecision lastDecision = BellPolicyDecision::Allow;
};

class BellPolicy {
public:
  bool begin();
  void beginUpdate(bool enabled);
  bool addStagedWindow(const BellPolicyWindow &window);
  bool commitUpdate(uint8_t expectedCount);
  BellPolicyDecision evaluate(time_t moment) const;
  bool enabled() const { return enforcementEnabled; }
  uint8_t windowCount() const { return activeCount; }

private:
  BellPolicyWindow activeWindows[MAX_BELL_POLICY_WINDOWS];
  BellPolicyWindow stagedWindows[MAX_BELL_POLICY_WINDOWS];
  uint8_t activeCount = 0;
  uint8_t stagedCount = 0;
  bool enforcementEnabled = false;
  bool stagedEnabled = false;

#ifdef ARDUINO
  Preferences preferences;
  bool preferencesReady = false;
  bool persist(bool enabled, const BellPolicyWindow *windows, uint8_t count);
#endif
};
