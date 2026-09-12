#pragma once

#include <Arduino.h>

class BluetoothSerialPort {
public:
  virtual ~BluetoothSerialPort() = default;

  virtual bool begin(const char *deviceName) = 0;
  virtual void setPin(const char *pin, size_t length) = 0;
  virtual bool hasClient() = 0;
  virtual uint32_t connectionGeneration() const { return 0; }
  virtual void observeConnection(uint32_t) {}
  virtual void disconnectClient() {}
  virtual bool clearBondedDevices() { return true; }
  virtual void setPairingStatus(bool, uint16_t) {}
  virtual bool setDeviceName(const char *) { return false; }
  virtual int available() = 0;
  virtual int read() = 0;
  virtual void print(const char *text) = 0;
  virtual void print(const String &text) = 0;
  virtual void println(const char *text) = 0;
  virtual void println(const String &text) = 0;
};
