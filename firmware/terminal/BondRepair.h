#pragma once

#include "BluetoothSerialPort.h"

// This class only changes OS bonds. It has no access to ownership or trip storage.
class BondRepair {
public:
  enum class State { Idle, Disconnecting, Ready, Complete, Failed, Expired };
  explicit BondRepair(BluetoothSerialPort &port) : port(port) {}
  bool start(uint32_t now, bool allowed) {
    if (!allowed || active()) return false;
    started = now;
    state = State::Disconnecting;
    port.disconnectClient();
    return true;
  }
  void poll(uint32_t now, bool authorized) {
    if (state == State::Disconnecting) {
      if (port.hasClient()) {
        if (uint32_t(now - started) >= 5000) finish(State::Failed);
        return;
      }
      if (!port.clearBondedDevices()) { finish(State::Failed); return; }
      started = now;
      state = State::Ready;
    } else if (state == State::Ready) {
      if (authorized) finish(State::Complete);
      else if (uint32_t(now - started) >= 120000) {
        port.disconnectClient();
        finish(State::Expired);
      }
    }
  }
  bool active() const { return state == State::Disconnecting || state == State::Ready; }
  State status() const { return state; }
  void reset() { finish(State::Idle); }
private:
  BluetoothSerialPort &port;
  State state = State::Idle;
  uint32_t started = 0;
  void finish(State next) { state = next; }
};
