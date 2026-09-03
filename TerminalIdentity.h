#pragma once

#include <Arduino.h>
#include <Preferences.h>

class TerminalIdentity {
public:
  bool begin();

  const String &terminalId() const { return id; }
  String terminalSuffix() const;
  String advertisedName() const;
  const String &customName() const { return name; }

  bool setCustomName(const String &requestedName);
  static bool isValidCustomName(const String &requestedName);

private:
  Preferences preferences;
  String id;
  String name;
};
