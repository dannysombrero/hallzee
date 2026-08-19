#include "TimeProvider.h"

time_t SystemTimeProvider::now() const {
  time_t currentTime;
  time(&currentTime);
  return currentTime;
}
