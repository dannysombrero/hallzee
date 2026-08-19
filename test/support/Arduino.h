#pragma once

#include <cstdint>
#include <sstream>
#include <string>
#include <type_traits>

using byte = uint8_t;

class String {
public:
  String() = default;
  String(const char *value) : value(value == nullptr ? "" : value) {}
  String(const std::string &value) : value(value) {}

  template <typename T, typename = std::enable_if_t<std::is_arithmetic_v<T>>>
  String(T number) : value(std::to_string(number)) {}

  size_t length() const { return value.length(); }
  const char *c_str() const { return value.c_str(); }
  operator std::string() const { return value; }

  bool operator==(const char *other) const { return value == (other == nullptr ? "" : other); }
  bool operator!=(const char *other) const { return !(*this == other); }
  bool operator==(const String &other) const { return value == other.value; }
  bool operator!=(const String &other) const { return !(*this == other); }

private:
  std::string value;
};
