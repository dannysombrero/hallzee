#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tools_dir="$project_root/.tools"
cli_dir="$tools_dir/arduino-cli"
cli="$cli_dir/bin/arduino-cli"
esp32_index="https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json"
esp32_version="3.3.11"
compile_only=false
port=""
display="st7735"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --compile-only) compile_only=true ;;
    --display) display="${2:?--display requires st7735 or ili9341}"; shift ;;
    --display=*) display="${1#*=}" ;;
    --) shift; break ;;
    -*) echo "Unknown option: $1"; exit 1 ;;
    *) if [[ -n "$port" && "$port" != "${1}" ]]; then echo "Only one serial port may be provided."; exit 1; fi; port="$1" ;;
  esac
  shift
done

if [[ "$display" != "st7735" && "$display" != "ili9341" ]]; then
  echo "Display must be st7735 or ili9341."
  exit 1
fi

# Arduino sketches must live in a directory with the same name as their .ino
# file. The repository directory is named independently, so stage the sketch
# sources in a short-lived Arduino-compatible directory before compiling.
shopt -s nullglob
sketch_files=("$project_root"/*.ino)
source_files=("$project_root"/*.ino "$project_root"/*.h "$project_root"/*.cpp)
if [[ ${#sketch_files[@]} -ne 1 ]]; then
  echo "Expected exactly one .ino sketch in $project_root."
  exit 1
fi
sketch_name="$(basename "${sketch_files[0]}" .ino)"
staging_root="$(mktemp -d)"
staging_sketch="$staging_root/$sketch_name"
mkdir -p "$staging_sketch"
cp "${source_files[@]}" "$staging_sketch/"
trap 'rm -rf "$staging_root"' EXIT

cat <<'REQUIREMENTS'
Hallzee firmware flasher

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
"$cli" core install "esp32:esp32@$esp32_version" --additional-urls "$esp32_index"
"$cli" lib install "Adafruit GFX Library" "Adafruit ST7735 and ST7789 Library" "Adafruit ILI9341" Keypad

echo "Building and flashing Hallzee to ${port}…"
compile_args=(--fqbn esp32:esp32:esp32 "$staging_sketch")
if [[ "$display" == "ili9341" ]]; then
  compile_args+=(--build-property compiler.cpp.extra_flags=-DHALLZEE_ILI9341)
fi
"$cli" compile "${compile_args[@]}"
if $compile_only; then
  echo "Compile-only check passed; the ESP32 was not changed."
  exit 0
fi
upload_args=(--fqbn esp32:esp32:esp32 --port "$port" "$staging_sketch")
if [[ "$display" == "ili9341" ]]; then
  upload_args+=(--build-property compiler.cpp.extra_flags=-DHALLZEE_ILI9341)
fi
"$cli" upload "${upload_args[@]}"
echo "Done. The Hallzee firmware is now on the ESP32."
