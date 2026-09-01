#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tools_dir="$project_root/.tools"
cli_dir="$tools_dir/arduino-cli"
cli="$cli_dir/bin/arduino-cli"
esp32_index="https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json"
port="${1:-}"

cat <<'REQUIREMENTS'
Bathroom Terminal firmware flasher

Before continuing, make sure:
  • An original ESP32 terminal is connected with a USB data cable.
  • The terminal is powered on.
  • No other USB serial device is connected, unless you provide its port.
  • You are ready to replace the firmware currently on that ESP32.

No Arduino software needs to be installed first; this script installs what it needs.
REQUIREMENTS

if [[ -z "$port" ]]; then
  candidates=()
  while IFS= read -r candidate; do candidates+=("$candidate"); done < <(find /dev -maxdepth 1 -type c \( -name 'cu.usb*' -o -name 'cu.wch*' -o -name 'cu.SLAB*' \) 2>/dev/null | sort)
  if [[ ${#candidates[@]} -eq 1 ]]; then
    port="${candidates[0]}"
  elif [[ ${#candidates[@]} -eq 0 ]]; then
    echo "No USB serial terminal was found. Plug in the ESP32, then run this command again."
    exit 1
  else
    echo "More than one possible ESP32 port was found:"
    printf '  %s\n' "${candidates[@]}"
    echo "Unplug other USB serial devices, then run this command again."
    exit 1
  fi
fi

if [[ ! -x "$cli" ]]; then
  mkdir -p "$cli_dir/bin"
  echo "Installing the Arduino command-line tools (one time only)…"
  curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | BINDIR="$cli_dir/bin" sh
fi

echo "Installing the ESP32 board support and required libraries if needed…"
"$cli" core update-index --additional-urls "$esp32_index"
"$cli" core install esp32:esp32 --additional-urls "$esp32_index"
"$cli" lib install "Adafruit GFX Library" "Adafruit ST7735 and ST7789 Library" Keypad

echo "Building and flashing Bathroom Terminal to $port…"
"$cli" compile --fqbn esp32:esp32:esp32 "$project_root"
"$cli" upload --fqbn esp32:esp32:esp32 --port "$port" "$project_root"
echo "Done. The Bathroom Terminal firmware is now on the ESP32."
