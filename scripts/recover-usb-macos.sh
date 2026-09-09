#!/usr/bin/env bash
set -euo pipefail
bundle="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
backup="${1:?Usage: bash recover-usb-macos.sh backups/<backup-folder> /dev/cu.usbserial-XXXX}"
port="${2:?Supply the ESP32 USB port}"
chmod +x "$bundle/tool/FirmwareTool" "$bundle/tool/esptool"
"$bundle/tool/FirmwareTool" recover --esptool "$bundle/tool/esptool" --port "$port" --backup "$backup"
