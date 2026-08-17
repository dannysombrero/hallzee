#!/bin/zsh
set -euo pipefail

clang -fobjc-arc -framework Foundation -framework IOBluetooth \
  BathroomReceiver.m -o bathroom-receiver
