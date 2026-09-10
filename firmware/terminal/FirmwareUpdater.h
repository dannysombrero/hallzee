#pragma once
#ifdef ARDUINO
#include <Arduino.h>
#include <esp_ota_ops.h>
#include <mbedtls/sha256.h>
#include "BluetoothSerialPort.h"

class FirmwareUpdater {
public:
  using Allowed = bool (*)();
  FirmwareUpdater(BluetoothSerialPort &port, Allowed authorized, Allowed idle);
  bool command(const String &text);
  void frame(const uint8_t *bytes, size_t size);
  void poll();
  bool busy() const { return session != 0; }
  unsigned int progress() const { return imageSize ? (written * 100 / imageSize) : 0; }
  void confirmBoot(bool healthy);
private:
  BluetoothSerialPort &port;
  Allowed authorized, idle;
  uint32_t session = 0, written = 0, imageSize = 0, metadataSize = 0;
  unsigned long lastActivity = 0, rebootAt = 0;
  String metadata, signature, digest;
  esp_ota_handle_t handle = 0;
  const esp_partition_t *partition = nullptr;
  mbedtls_sha256_context sha;
  bool receivingImage = false;
  bool bootChecked = false, bootHealthy = false;
  void abort();
  void error(const char *reason);
  bool verifyMetadata();
  void info();
};
#endif
