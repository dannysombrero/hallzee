#include "TerminalIdentity.h"

#include <esp_system.h>

namespace {
constexpr char ID_PREFIX[] = "HZ-";
constexpr char DEFAULT_NAME[] = "Hallzee";
constexpr char PREFERENCES_NAMESPACE[] = "hallzee_identity";
constexpr char CUSTOM_NAME_KEY[] = "custom_name";
}

bool TerminalIdentity::begin() {
  const uint64_t eFuseMac = ESP.getEfuseMac();
  char idBuffer[sizeof(ID_PREFIX) + 12] = {};
  snprintf(idBuffer, sizeof(idBuffer), "%s%012llX", ID_PREFIX,
           static_cast<unsigned long long>(eFuseMac));
  id = String(idBuffer);

  if (!preferences.begin(PREFERENCES_NAMESPACE, false)) {
    return false;
  }

  name = preferences.getString(CUSTOM_NAME_KEY, "");
  if (!isValidCustomName(name)) {
    name = advertisedName();
  }
  return true;
}

String TerminalIdentity::terminalSuffix() const {
  return id.length() >= 4 ? id.substring(id.length() - 4) : id;
}

String TerminalIdentity::advertisedName() const {
  if (name.length() > 0 && name != DEFAULT_NAME) return name;
  return String(DEFAULT_NAME) + "-" + terminalSuffix();
}

bool TerminalIdentity::setCustomName(const String &requestedName) {
  if (!isValidCustomName(requestedName)) return false;
  const String normalized = requestedName;
  if (preferences.putString(CUSTOM_NAME_KEY, normalized) == 0) return false;
  name = normalized;
  return true;
}

bool TerminalIdentity::isValidCustomName(const String &requestedName) {
  const String normalized = requestedName;
  if (normalized.length() < 1 || normalized.length() > 24 ||
      normalized.charAt(0) == ' ' ||
      normalized.charAt(normalized.length() - 1) == ' ') {
    return false;
  }

  for (unsigned int index = 0; index < normalized.length(); index++) {
    const char character = normalized.charAt(index);
    const bool allowed =
      (character >= 'a' && character <= 'z') ||
      (character >= 'A' && character <= 'Z') ||
      (character >= '0' && character <= '9') ||
      character == ' ' || character == '-' || character == '_' ||
      character == '(' || character == ')';
    if (!allowed) return false;
  }
  return true;
}
