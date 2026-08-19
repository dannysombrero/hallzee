#pragma once

#include <cstdint>
#include <cstdlib>
#include <sstream>
#include <string>
#include <type_traits>

using byte = uint8_t;

class SerialStub {
public:
  template <typename T>
  void print(const T &) {}
  template <typename T>
  void println(const T &) {}
};

inline SerialStub Serial;

class String {
public:
  String() = default;
  String(const char *value) : value(value == nullptr ? "" : value) {}
  String(const std::string &value) : value(value) {}

  template <typename T, typename = std::enable_if_t<std::is_arithmetic_v<T>>>
  String(T number) : value(std::to_string(number)) {}

  size_t length() const { return value.length(); }
  const char *c_str() const { return value.c_str(); }
  bool startsWith(const char *prefix) const { return value.rfind(prefix, 0) == 0; }
  String substring(size_t start) const { return value.substr(start); }
  char charAt(size_t index) const { return value.at(index); }
  long toInt() const { return std::strtol(value.c_str(), nullptr, 10); }
  void trim() {
    const auto first = value.find_first_not_of(" \t\r\n");
    const auto last = value.find_last_not_of(" \t\r\n");
    value = first == std::string::npos ? "" : value.substr(first, last - first + 1);
  }
  String &operator+=(char character) { value += character; return *this; }
  operator std::string() const { return value; }

  bool operator==(const char *other) const { return value == (other == nullptr ? "" : other); }
  bool operator!=(const char *other) const { return !(*this == other); }
  bool operator==(const String &other) const { return value == other.value; }
  bool operator!=(const String &other) const { return !(*this == other); }

private:
  std::string value;
};
