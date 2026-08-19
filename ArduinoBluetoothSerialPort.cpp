#include "ArduinoBluetoothSerialPort.h"

bool ArduinoBluetoothSerialPort::begin(const char *deviceName) { return serial.begin(deviceName); }
void ArduinoBluetoothSerialPort::setPin(const char *pin, size_t length) { serial.setPin(pin, length); }
bool ArduinoBluetoothSerialPort::hasClient() { return serial.hasClient(); }
int ArduinoBluetoothSerialPort::available() { return serial.available(); }
int ArduinoBluetoothSerialPort::read() { return serial.read(); }
void ArduinoBluetoothSerialPort::print(const char *text) { serial.print(text); }
void ArduinoBluetoothSerialPort::print(const String &text) { serial.print(text); }
void ArduinoBluetoothSerialPort::println(const char *text) { serial.println(text); }
void ArduinoBluetoothSerialPort::println(const String &text) { serial.println(text); }
