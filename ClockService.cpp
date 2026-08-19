#include "ClockService.h"

#include <sys/time.h>
#include <time.h>

bool ClockService::isSet() const {
  return hasBeenSet;
}

void ClockService::set24Hour(
  int year,
  int month,
  int day,
  int hour,
  int minute,
  int second
) {
  struct tm timeInfo = {};
  timeInfo.tm_year = year - 1900;
  timeInfo.tm_mon = month - 1;
  timeInfo.tm_mday = day;
  timeInfo.tm_hour = hour;
  timeInfo.tm_min = minute;
  timeInfo.tm_sec = second;

  // Treat entered time directly as classroom local time.
  setenv("TZ", "UTC0", 1);
  tzset();

  const time_t newTime = mktime(&timeInfo);
  struct timeval now = {newTime, 0};
  settimeofday(&now, nullptr);

  hasBeenSet = true;
  Serial.println("Clock updated.");
}

void ClockService::set12Hour(
  int year,
  int month,
  int day,
  int hour,
  int minute,
  bool pm
) {
  int hour24 = hour;
  if (pm && hour != 12) {
    hour24 += 12;
  } else if (!pm && hour == 12) {
    hour24 = 0;
  }

  set24Hour(year, month, day, hour24, minute, 0);
}

String ClockService::timeString() const {
  if (!hasBeenSet) {
    return "--:--";
  }

  time_t now;
  time(&now);
  struct tm timeInfo;
  localtime_r(&now, &timeInfo);

  int hour = timeInfo.tm_hour;
  const bool pm = hour >= 12;
  hour %= 12;
  if (hour == 0) {
    hour = 12;
  }

  char buffer[16];
  snprintf(
    buffer, sizeof(buffer), "%d:%02d %s", hour, timeInfo.tm_min,
    pm ? "PM" : "AM"
  );
  return String(buffer);
}

String ClockService::dateString() const {
  if (!hasBeenSet) {
    return "--/--/----";
  }

  time_t now;
  time(&now);
  struct tm timeInfo;
  localtime_r(&now, &timeInfo);

  char buffer[16];
  snprintf(
    buffer, sizeof(buffer), "%02d/%02d/%04d", timeInfo.tm_mon + 1,
    timeInfo.tm_mday, timeInfo.tm_year + 1900
  );
  return String(buffer);
}

bool ClockService::isLeapYear(int year) {
  if (year % 400 == 0) return true;
  if (year % 100 == 0) return false;
  return year % 4 == 0;
}

int ClockService::daysInMonth(int month, int year) {
  switch (month) {
    case 4:
    case 6:
    case 9:
    case 11:
      return 30;
    case 2:
      return isLeapYear(year) ? 29 : 28;
    default:
      return 31;
  }
}
