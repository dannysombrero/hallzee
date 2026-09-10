#pragma once

#include <Arduino.h>

class ClockService {
public:
  bool isSet() const;
  void set24Hour(int year, int month, int day, int hour, int minute, int second);
  void set12Hour(int year, int month, int day, int hour, int minute, bool pm);
  String timeString() const;
  String dateString() const;

  static int daysInMonth(int month, int year);

private:
  bool hasBeenSet = false;

  static bool isLeapYear(int year);
};
