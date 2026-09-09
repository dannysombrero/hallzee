#pragma once

#include <BLEDevice.h>
#include <BLE2902.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <atomic>

#include "BluetoothSerialPort.h"

// BLE UART-style transport. The higher-level sync protocol remains newline-delimited
// text, so BluetoothSync does not need to know whether the radio is Classic or BLE.
class ArduinoBluetoothSerialPort : public BluetoothSerialPort {
public:
  bool begin(const char *deviceName) override;
  void setPin(const char *pin, size_t length) override;
  bool hasClient() override;
  bool isReady() const { return server && txCharacteristic && rxCharacteristic; }
  void disconnectClient() override;
  bool clearBondedDevices() override;
  void setPairingPasskey(uint32_t passkey) override;
  bool setDeviceName(const char *deviceName) override;
  int available() override;
  int read() override;
  void print(const char *text) override;
  void print(const String &text) override;
  void println(const char *text) override;
  void println(const String &text) override;

private:
  class ServerCallbacks;
  class RxCallbacks;
  friend class ServerCallbacks;
  friend class RxCallbacks;

  void enqueue(const uint8_t *data, size_t length);
  void send(const String &text);
  void restartAdvertising();
  void handleConnect(uint16_t connectionId);
  void handleDisconnect(uint16_t connectionId);

  BLEServer *server = nullptr;
  BLECharacteristic *txCharacteristic = nullptr;
  BLECharacteristic *rxCharacteristic = nullptr;
  QueueHandle_t receiveQueue = nullptr;
  std::atomic<bool> connected{false};
  String advertisedName;
  uint16_t activeConnectionId = 0xFFFF;
};
