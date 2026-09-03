#include "ArduinoBluetoothSerialPort.h"
#include <BLESecurity.h>
#if defined(CONFIG_BLUEDROID_ENABLED)
#include <esp_gap_ble_api.h>
#elif defined(CONFIG_NIMBLE_ENABLED)
#include <host/ble_hs.h>
#include <services/gap/ble_svc_gap.h>
#endif

#if __has_include(<esp_arduino_version.h>)
#include <esp_arduino_version.h>
#endif

namespace {
constexpr char SERVICE_UUID[] = "005924a2-c6e5-4340-9bb8-22d9dd37a283";
constexpr char TX_UUID[] = "44a359f3-9215-4189-a3cb-e7ce18ad40d6";
constexpr char RX_UUID[] = "e80f9559-49eb-47bc-af04-8e92e98ced56";
}

class ArduinoBluetoothSerialPort::ServerCallbacks : public BLEServerCallbacks {
public:
  explicit ServerCallbacks(ArduinoBluetoothSerialPort &owner) : owner(owner) {}
#if defined(CONFIG_BLUEDROID_ENABLED)
  void onConnect(BLEServer *) override {}
  void onDisconnect(BLEServer *) override {}
  void onConnect(BLEServer *, esp_ble_gatts_cb_param_t *param) override {
    owner.handleConnect(param->connect.conn_id);
  }
  void onDisconnect(BLEServer *, esp_ble_gatts_cb_param_t *param) override {
    owner.handleDisconnect(param->disconnect.conn_id);
  }
#elif defined(CONFIG_NIMBLE_ENABLED)
  void onConnect(BLEServer *, ble_gap_conn_desc *desc) override {
    owner.handleConnect(desc->conn_handle);
  }
  void onDisconnect(BLEServer *, ble_gap_conn_desc *desc) override {
    owner.handleDisconnect(desc->conn_handle);
  }
#else
  void onConnect(BLEServer *) override { owner.handleConnect(0xFFFF); }
  void onDisconnect(BLEServer *) override { owner.handleDisconnect(0xFFFF); }
#endif
private:
  ArduinoBluetoothSerialPort &owner;
};

class ArduinoBluetoothSerialPort::RxCallbacks : public BLECharacteristicCallbacks {
public:
  explicit RxCallbacks(ArduinoBluetoothSerialPort &owner) : owner(owner) {}
  void onWrite(BLECharacteristic *characteristic) override {
#if defined(ESP_ARDUINO_VERSION_MAJOR) && ESP_ARDUINO_VERSION_MAJOR >= 3
    const String value = characteristic->getValue();
    owner.enqueue(reinterpret_cast<const uint8_t *>(value.c_str()), value.length());
#else
    const std::string value = characteristic->getValue();
    owner.enqueue(reinterpret_cast<const uint8_t *>(value.data()), value.size());
#endif
  }
private:
  ArduinoBluetoothSerialPort &owner;
};

bool ArduinoBluetoothSerialPort::begin(const char *deviceName) {
  BLEDevice::init(deviceName);
  BLESecurity::setAuthenticationMode(ESP_LE_AUTH_REQ_SC_MITM_BOND);
  BLESecurity::setCapability(ESP_IO_CAP_OUT);
  BLESecurity::setKeySize(16);
  server = BLEDevice::createServer();
  if (!server) return false;
  server->setCallbacks(new ServerCallbacks(*this));

  BLEService *service = server->createService(SERVICE_UUID);
  if (!service) return false;

  txCharacteristic = service->createCharacteristic(
    TX_UUID,
    BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_NOTIFY
  );
  rxCharacteristic = service->createCharacteristic(
    RX_UUID,
    BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR
  );
  if (!txCharacteristic || !rxCharacteristic) return false;
  txCharacteristic->setAccessPermissions(ESP_GATT_PERM_READ_ENC_MITM);
  rxCharacteristic->setAccessPermissions(ESP_GATT_PERM_WRITE_ENC_MITM);
  // Windows enables notifications by writing the standard Client Characteristic
  // Configuration Descriptor (CCCD). The ESP32 library does not add it for us.
  txCharacteristic->addDescriptor(new BLE2902());
  rxCharacteristic->setCallbacks(new RxCallbacks(*this));

  service->start();
  BLEAdvertising *advertising = BLEDevice::getAdvertising();
  advertising->addServiceUUID(SERVICE_UUID);
  advertising->setScanResponse(true);
  advertising->start();
  return true;
}

void ArduinoBluetoothSerialPort::setPin(const char *, size_t) {
  // BLE GATT is intentionally connection-without-legacy-PIN for the kiosk sync flow.
}

void ArduinoBluetoothSerialPort::disconnectClient() {
  if (server && connected && activeConnectionId != 0xFFFF) {
    server->disconnect(activeConnectionId);
  }
}

bool ArduinoBluetoothSerialPort::clearBondedDevices() {
#if defined(CONFIG_BLUEDROID_ENABLED)
  int deviceCount = esp_ble_get_bond_device_num();
  if (deviceCount <= 0) return true;

  auto *devices = new esp_ble_bond_dev_t[deviceCount];
  if (esp_ble_get_bond_device_list(&deviceCount, devices) != ESP_OK) {
    delete[] devices;
    return false;
  }

  bool removedAll = true;
  for (int index = 0; index < deviceCount; index++) {
    if (esp_ble_remove_bond_device(devices[index].bd_addr) != ESP_OK) {
      removedAll = false;
    }
  }
  delete[] devices;
  return removedAll;
#else
  // The currently supported original ESP32 target uses Bluedroid. Do not
  // report success on another BLE stack until its bond-store reset is wired.
  return false;
#endif
}

void ArduinoBluetoothSerialPort::setPairingPasskey(uint32_t passkey) {
  BLESecurity::setPassKey(true, passkey);
}

bool ArduinoBluetoothSerialPort::setDeviceName(const char *deviceName) {
  if (!deviceName || !server) return false;
#if defined(CONFIG_BLUEDROID_ENABLED)
  return esp_ble_gap_set_device_name(const_cast<char *>(deviceName)) == ESP_OK;
#elif defined(CONFIG_NIMBLE_ENABLED)
  return ble_svc_gap_device_name_set(deviceName) == 0;
#else
  return false;
#endif
}

bool ArduinoBluetoothSerialPort::hasClient() { return connected; }
int ArduinoBluetoothSerialPort::available() { return static_cast<int>(receiveBuffer.size()); }

int ArduinoBluetoothSerialPort::read() {
  if (receiveBuffer.empty()) return -1;
  const int value = receiveBuffer.front();
  receiveBuffer.pop_front();
  return value;
}

void ArduinoBluetoothSerialPort::enqueue(const uint8_t *data, size_t length) {
  for (size_t i = 0; i < length; ++i) receiveBuffer.push_back(data[i]);
}

void ArduinoBluetoothSerialPort::handleConnect(uint16_t connectionId) {
  if (connected) {
    if (server && connectionId != 0xFFFF) server->disconnect(connectionId);
    return;
  }
  connected = true;
  activeConnectionId = connectionId;
}

void ArduinoBluetoothSerialPort::handleDisconnect(uint16_t connectionId) {
  if (!connected || activeConnectionId == 0xFFFF ||
      connectionId == activeConnectionId || connectionId == 0xFFFF) {
    connected = false;
    activeConnectionId = 0xFFFF;
    restartAdvertising();
  }
}

void ArduinoBluetoothSerialPort::send(const String &text) {
  if (!connected || !txCharacteristic) return;
  // Keep notifications below the default 20-byte ATT payload so this works
  // immediately without depending on MTU negotiation.
  constexpr size_t CHUNK_SIZE = 20;
  for (size_t offset = 0; offset < text.length(); offset += CHUNK_SIZE) {
    const size_t length = min(CHUNK_SIZE, text.length() - offset);
    txCharacteristic->setValue(
      reinterpret_cast<const uint8_t *>(text.c_str() + offset), length
    );
    txCharacteristic->notify();
  }
}

void ArduinoBluetoothSerialPort::print(const char *text) { send(String(text)); }
void ArduinoBluetoothSerialPort::print(const String &text) { send(text); }
void ArduinoBluetoothSerialPort::println(const char *text) { send(String(text) + "\n"); }
void ArduinoBluetoothSerialPort::println(const String &text) { send(text + "\n"); }

void ArduinoBluetoothSerialPort::restartAdvertising() {
  if (server) server->getAdvertising()->start();
}
