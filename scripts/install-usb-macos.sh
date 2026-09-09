#!/usr/bin/env bash
set -euo pipefail
bundle="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
variant="${1:?Usage: bash install-usb-macos.sh esp32-st7735-r1 /dev/cu.usbserial-XXXX}"
port="${2:?Supply the ESP32 USB serial port}"
case "$variant" in esp32-st7735-r1|esp32-ili9341-r[0-3]) ;; *) echo 'Unsupported hardware variant'; exit 1;; esac
chmod +x "$bundle/tool/FirmwareTool" "$bundle/tool/esptool" "$bundle/tool/mklittlefs"
"$bundle/tool/FirmwareTool" usb --esptool "$bundle/tool/esptool" --mklittlefs "$bundle/tool/mklittlefs" --port "$port" --build "$bundle/images/USB-$variant" --backup "$bundle/backups"
