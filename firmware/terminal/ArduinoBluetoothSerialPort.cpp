#include "ArduinoBluetoothSerialPort.h"
#include "TerminalAdvertisement.h"
#include <BLESecurity.h>
#if defined(CONFIG_BLUEDROID_ENABLED)
#include <esp_gap_ble_api.h>
#include <esp_gatts_api.h>
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
#if defined(CONFIG_BLUEDROID_ENABLED)
// BLEServer keeps its GATT interface private. Capture the one Hallzee server's
// registration through the supported event hook for addressed notifications.
std::atomic<esp_gatt_if_t> transportGattInterface{ESP_GATT_IF_NONE};
void onGattEvent(esp_gatts_cb_event_t event, esp_gatt_if_t interface,
                 esp_ble_gatts_cb_param_t *param) {
  if (event == ESP_GATTS_REG_EVT && param && param->reg.status == ESP_GATT_OK) {
    transportGattInterface = interface;
  }
}
#endif
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
#if defined(CONFIG_BLUEDROID_ENABLED)
  void onWrite(BLECharacteristic *characteristic, esp_ble_gatts_cb_param_t *param) override {
    if (!param || !owner.connected || param->write.conn_id != owner.activeConnectionId) return;
    receive(characteristic);
  }
#elif defined(CONFIG_NIMBLE_ENABLED)
  void onWrite(BLECharacteristic *characteristic, ble_gap_conn_desc *desc) override {
    if (!desc || !owner.connected || desc->conn_handle != owner.activeConnectionId) return;
    receive(characteristic);
  }
#else
  void onWrite(BLECharacteristic *characteristic) override {
    receive(characteristic);
  }
#endif
private:
  void receive(BLECharacteristic *characteristic) {
#if defined(ESP_ARDUINO_VERSION_MAJOR) && ESP_ARDUINO_VERSION_MAJOR >= 3
    const String value = characteristic->getValue();
    owner.enqueue(reinterpret_cast<const uint8_t *>(value.c_str()), value.length());
#else
    const std::string value = characteristic->getValue();
    owner.enqueue(reinterpret_cast<const uint8_t *>(value.data()), value.size());
#endif
  }
  ArduinoBluetoothSerialPort &owner;
};

bool ArduinoBluetoothSerialPort::begin(const char *deviceName) {
  receiveQueue = xQueueCreate(4096, sizeof(uint8_t));
  if (!receiveQueue) return false;
  BLEDevice::init(deviceName);
  BLEDevice::setMTU(247);
  // The physical six-digit code belongs to the application claim protocol.
  // Secure Connections Just Works encrypts and bonds the BLE link without a
  // second OS passkey prompt. Use the boolean overload: on ESP32 3.3.11 it
  // also enables security and selects the matching encryption level.
  BLESecurity::setAuthenticationMode(true, false, true);
  BLESecurity::setCapability(ESP_IO_CAP_NONE);
  BLESecurity::setKeySize(16);
  BLESecurity::setInitEncryptionKey(ESP_BLE_ENC_KEY_MASK | ESP_BLE_ID_KEY_MASK);
  BLESecurity::setRespEncryptionKey(ESP_BLE_ENC_KEY_MASK | ESP_BLE_ID_KEY_MASK);
#if defined(CONFIG_BLUEDROID_ENABLED)
  BLEDevice::setCustomGattsHandler(onGattEvent);
#endif
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
  txCharacteristic->setAccessPermissions(ESP_GATT_PERM_READ_ENCRYPTED);
  rxCharacteristic->setAccessPermissions(ESP_GATT_PERM_WRITE_ENCRYPTED);
  // Windows enables notifications by writing the standard Client Characteristic
  // Configuration Descriptor (CCCD). The ESP32 library does not add it for us.
  txCharacteristic->addDescriptor(new BLE2902());
  rxCharacteristic->setCallbacks(new RxCallbacks(*this));

  service->start();
  BLEAdvertising *advertising = BLEDevice::getAdvertising();
  // Keep the service UUID in the advertisement and the whole name in the
  // scan response. Ownership is manufacturer metadata, never a name suffix.
  advertising->setScanResponse(true);
  advertisedName = deviceName;
  BLEAdvertisementData nameData;
  nameData.setName(advertisedName);
  advertising->setScanResponseData(nameData);
  updateAdvertisement();
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

void ArduinoBluetoothSerialPort::setPairingStatus(bool isClaimed, uint16_t suffix) {
  if (claimed == isClaimed && terminalSuffix == suffix) return;
  claimed = isClaimed;
  terminalSuffix = suffix;
  if (server && !connected) {
    server->getAdvertising()->stop();
    restartAdvertising();
  }
}

void ArduinoBluetoothSerialPort::updateAdvertisement() {
  if (!server) return;
  BLEAdvertisementData serviceData;
  serviceData.setFlags(0x06);
  serviceData.setCompleteServices(BLEUUID(SERVICE_UUID));
  const auto metadata = terminalAdvertisement(claimed, terminalSuffix);
  serviceData.setManufacturerData(String(
    reinterpret_cast<const char *>(metadata.data()), metadata.size()));
  server->getAdvertising()->setAdvertisementData(serviceData);
}

bool ArduinoBluetoothSerialPort::setDeviceName(const char *deviceName) {
  if (!deviceName || !server || strlen(deviceName) > 29) return false;
#if defined(CONFIG_BLUEDROID_ENABLED)
  if (esp_ble_gap_set_device_name(const_cast<char *>(deviceName)) != ESP_OK) return false;
#elif defined(CONFIG_NIMBLE_ENABLED)
  if (ble_svc_gap_device_name_set(deviceName) != 0) return false;
#else
  return false;
#endif
  advertisedName = deviceName;
  // An authorized connection remains exclusive. Update its next advertisement
  // when it disconnects; otherwise refresh discovery immediately.
  if (!connected) {
    server->getAdvertising()->stop();
    restartAdvertising();
  }
  return true;
}

bool ArduinoBluetoothSerialPort::hasClient() { return connected; }
int ArduinoBluetoothSerialPort::available() { return receiveQueue ? uxQueueMessagesWaiting(receiveQueue) : 0; }

int ArduinoBluetoothSerialPort::read() {
  uint8_t value;
  return receiveQueue && xQueueReceive(receiveQueue, &value, 0) == pdTRUE ? value : -1;
}

void ArduinoBluetoothSerialPort::enqueue(const uint8_t *data, size_t length) {
  if (!receiveQueue) return;
  for (size_t i = 0; i < length; ++i) {
    if (xQueueSend(receiveQueue, data + i, 0) != pdTRUE) {
      xQueueReset(receiveQueue);
      disconnectClient();
      return;
    }
  }
}

void ArduinoBluetoothSerialPort::handleConnect(uint16_t connectionId) {
  if (connected) {
    if (server && connectionId != 0xFFFF) server->disconnect(connectionId);
    return;
  }
  // TX is readable to trigger link encryption. Never retain an earlier
  // owner's notification value for a newly connected, unauthenticated peer.
  if (txCharacteristic) txCharacteristic->setValue("");
  if (receiveQueue) xQueueReset(receiveQueue);
  generation++;
  activeConnectionId = connectionId;
  connected = true;
}

void ArduinoBluetoothSerialPort::handleDisconnect(uint16_t connectionId) {
  if (!connected || activeConnectionId == 0xFFFF ||
      connectionId == activeConnectionId || connectionId == 0xFFFF) {
    connected = false;
    if (txCharacteristic) txCharacteristic->setValue("");
    if (receiveQueue) xQueueReset(receiveQueue);
    activeConnectionId = 0xFFFF;
    restartAdvertising();
  }
}

void ArduinoBluetoothSerialPort::send(const String &text) {
  if (!connected || !txCharacteristic) return;
  // An application response can use multiple print() calls. Do not let a
  // subsequent fragment adopt a new peer before the application clears the
  // previous session and explicitly observes that connection.
  const uint32_t sendingGeneration = observedGeneration;
  if (sendingGeneration != generation) return;
  const uint16_t sendingConnection = activeConnectionId;
  auto *notifications = static_cast<BLE2902 *>(
    txCharacteristic->getDescriptorByUUID(static_cast<uint16_t>(0x2902)));
  if (notifications && !notifications->getNotifications()) return;
  // Keep notifications below the default 20-byte ATT payload so this works
  // immediately without depending on MTU negotiation.
  constexpr size_t CHUNK_SIZE = 20;
  for (size_t offset = 0; offset < text.length(); offset += CHUNK_SIZE) {
    if (!connected || generation != sendingGeneration) return;
    const size_t length = min(CHUNK_SIZE, text.length() - offset);
#if defined(CONFIG_BLUEDROID_ENABLED)
    // Address the admitted peer, not every peer in the BLE server's map.
    // Keep the readable TX value empty: a pre-auth read is only used to
    // establish encryption and must never expose previous notification data.
    const esp_err_t result = esp_ble_gatts_send_indicate(
      transportGattInterface, sendingConnection, txCharacteristic->getHandle(),
      length, reinterpret_cast<uint8_t *>(const_cast<char *>(text.c_str() + offset)), false);
    if (result != ESP_OK) return;
#else
    (void)sendingConnection;
    txCharacteristic->setValue(
      reinterpret_cast<const uint8_t *>(text.c_str() + offset), length
    );
    txCharacteristic->notify();
    txCharacteristic->setValue("");
#endif
  }
}

void ArduinoBluetoothSerialPort::print(const char *text) { send(String(text)); }
void ArduinoBluetoothSerialPort::print(const String &text) { send(text); }
void ArduinoBluetoothSerialPort::println(const char *text) { send(String(text) + "\n"); }
void ArduinoBluetoothSerialPort::println(const String &text) { send(text + "\n"); }

void ArduinoBluetoothSerialPort::restartAdvertising() {
  if (!server) return;
  updateAdvertisement();
  BLEAdvertisementData nameData;
  nameData.setName(advertisedName);
  auto *advertising = server->getAdvertising();
  advertising->setScanResponseData(nameData);
  advertising->start();
}
