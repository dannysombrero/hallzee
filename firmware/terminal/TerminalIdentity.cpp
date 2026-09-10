#include "TerminalIdentity.h"

#include <esp_system.h>

namespace {
constexpr char ID_PREFIX[] = "HZ-";
constexpr char DEFAULT_NAME[] = "Hallzee";
// ESP32 NVS namespaces allow at most 15 characters (excluding the terminator).
constexpr char PREFERENCES_NAMESPACE[] = "hallzee_id";
static_assert(sizeof(PREFERENCES_NAMESPACE) <= 16, "NVS namespace exceeds 15 characters");
constexpr char CUSTOM_NAME_KEY[] = "custom_name";
}

bool TerminalIdentity::begin() {
  const uint64_t eFuseMac = ESP.getEfuseMac();
  char idBuffer[sizeof(ID_PREFIX) + 12] = {};
  snprintf(idBuffer, sizeof(idBuffer), "%s%012llX", ID_PREFIX,
           static_cast<unsigned long long>(eFuseMac));
  id = String(idBuffer);

  // The eFuse-derived identity remains usable even if the optional NVS
  // custom-name namespace is unavailable. Owner credentials use LittleFS.
  preferencesReady = preferences.begin(PREFERENCES_NAMESPACE, false);
  if (!preferencesReady) {
    name = DEFAULT_NAME;
    return true;
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

String TerminalIdentity::advertisedName(bool inUse) const {
  String baseName;
  if (name.length() > 0 && name != DEFAULT_NAME) {
    baseName = name;
  } else {
    baseName = String(DEFAULT_NAME) + "-" + terminalSuffix();
  }
  // A name AD field has 29 bytes; reserve all six bytes of the status suffix.
  if (inUse && baseName.length() > 23) baseName = baseName.substring(0, 23);
  return inUse ? baseName + "-INUSE" : baseName;
}

bool TerminalIdentity::setCustomName(const String &requestedName) {
  if (!isValidCustomName(requestedName)) return false;
  const String normalized = requestedName;
  if (!preferencesReady) return false;
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
    const unsigned char character = static_cast<unsigned char>(normalized.charAt(index));
    if (character < 0x20 || character > 0x7E || character == ',') return false;
  }
  return true;
}
