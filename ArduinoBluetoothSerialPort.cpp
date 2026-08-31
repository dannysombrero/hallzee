#include "ArduinoBluetoothSerialPort.h"

namespace {
constexpr char SERVICE_UUID[] = "005924a2-c6e5-4340-9bb8-22d9dd37a283";
constexpr char TX_UUID[] = "44a359f3-9215-4189-a3cb-e7ce18ad40d6";
constexpr char RX_UUID[] = "e80f9559-49eb-47bc-af04-8e92e98ced56";
}

class ArduinoBluetoothSerialPort::ServerCallbacks : public BLEServerCallbacks {
public:
  explicit ServerCallbacks(ArduinoBluetoothSerialPort &owner) : owner(owner) {}
  void onConnect(BLEServer *) override { owner.connected = true; }
  void onDisconnect(BLEServer *) override {
    owner.connected = false;
    owner.restartAdvertising();
  }
private:
  ArduinoBluetoothSerialPort &owner;
};

class ArduinoBluetoothSerialPort::RxCallbacks : public BLECharacteristicCallbacks {
public:
  explicit RxCallbacks(ArduinoBluetoothSerialPort &owner) : owner(owner) {}
  void onWrite(BLECharacteristic *characteristic) override {
    const String value = characteristic->getValue();
    owner.enqueue(reinterpret_cast<const uint8_t *>(value.c_str()), value.length());
  }
private:
  ArduinoBluetoothSerialPort &owner;
};

bool ArduinoBluetoothSerialPort::begin(const char *deviceName) {
  BLEDevice::init(deviceName);
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
