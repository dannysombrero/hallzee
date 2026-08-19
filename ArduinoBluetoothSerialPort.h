#pragma once

#include <BluetoothSerial.h>

#include "BluetoothSerialPort.h"

class ArduinoBluetoothSerialPort : public BluetoothSerialPort {
public:
  bool begin(const char *deviceName) override;
  void setPin(const char *pin, size_t length) override;
  bool hasClient() override;
  int available() override;
  int read() override;
  void print(const char *text) override;
  void print(const String &text) override;
  void println(const char *text) override;
  void println(const String &text) override;

private:
  BluetoothSerial serial;
};
