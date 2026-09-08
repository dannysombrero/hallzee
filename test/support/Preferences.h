#pragma once

#include <Arduino.h>
#include <cstring>
#include <map>

// Model the NVS namespace limit and durable values across identity instances.
class Preferences {
public:
  inline static bool failOpen = false;
  inline static bool failWrite = false;
  inline static std::map<std::string, std::map<std::string, String>> stored;

  bool begin(const char *name, bool) {
    if (failOpen || !name || std::strlen(name) > 15) return false;
    openedNamespace = name;
    return true;
  }
  String getString(const char *key, const char *fallback) {
    const auto &values = stored[openedNamespace];
    const auto found = values.find(key);
    return found == values.end() ? String(fallback) : found->second;
  }
  size_t putString(const char *key, const String &value) {
    if (failWrite || openedNamespace.empty()) return 0;
    stored[openedNamespace][key] = value;
    return value.length();
  }

private:
  std::string openedNamespace;
};
