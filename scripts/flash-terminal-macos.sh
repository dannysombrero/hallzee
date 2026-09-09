#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tools_dir="$project_root/.tools"
cli_dir="$tools_dir/arduino-cli"
cli="$cli_dir/bin/arduino-cli"
esp32_index="https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json"
esp32_version="3.3.11"
compile_only=false
fast=false
port=""
display="st7735"
rotation="1"
touch_test=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --fast) fast=true ;;
    --touch-test) touch_test=true ;;
    --compile-only) compile_only=true ;;
    --display) display="${2:?--display requires st7735 or ili9341}"; shift ;;
    --display=*) display="${1#*=}" ;;
    --rotation) rotation="${2:?--rotation requires 0, 1, 2, or 3}"; shift ;;
    --rotation=*) rotation="${1#*=}" ;;
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
if [[ "$rotation" != "0" && "$rotation" != "1" && "$rotation" != "2" && "$rotation" != "3" ]]; then
  echo "Rotation must be 0, 1, 2, or 3."
  exit 1
fi

if $touch_test && { [[ "$display" != "ili9341" ]] || [[ "$rotation" != "1" && "$rotation" != "3" ]]; }; then
  echo "--touch-test requires --display ili9341 and landscape rotation 1 or 3."
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
build_dir="$staging_root/build"
mkdir -p "$staging_sketch"
cp "${source_files[@]}" "$staging_sketch/"
cp "$project_root/firmware/partitions.csv" "$staging_sketch/partitions.csv"
if [[ -d "$project_root/fonts" ]]; then
  cp -R "$project_root/fonts" "$staging_sketch/"
fi
trap 'rm -rf "$staging_root"' EXIT

cat <<'REQUIREMENTS'
Hallzee firmware flasher

Before continuing, make sure:
  • An original ESP32 terminal is connected with a USB data cable.
  • The terminal is powered on.
  • No other USB serial device is connected, unless you provide its port.
  • You are ready to replace the firmware currently on that ESP32.

Regular mode installs required Arduino software; fast mode reuses installed dependencies.
REQUIREMENTS

if [[ -z "$port" && "$compile_only" == false ]]; then
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

if ! $fast; then
  echo "Installing the ESP32 board support and required libraries if needed…"
  "$cli" core update-index --additional-urls "$esp32_index"
  "$cli" core install "esp32:esp32@$esp32_version" --additional-urls "$esp32_index"
  "$cli" lib install "Adafruit GFX Library" "Adafruit ST7735 and ST7789 Library" "Adafruit ILI9341" Keypad

  if $touch_test; then "$cli" lib install "XPT2046_Touchscreen@1.4"; fi
else
  echo "Fast mode: using installed board support/libraries. If compilation reports missing tools, run once without --fast (optionally --compile-only)."
fi

echo "Building Hallzee…"
compile_args=(--fqbn esp32:esp32:esp32 "$staging_sketch" --build-path "$build_dir" --build-property upload.maximum_size=1572864)
if [[ "$display" == "ili9341" ]]; then
  extra_flags="-DHALLZEE_ILI9341 -DHALLZEE_DISPLAY_ROTATION=$rotation"
  if $touch_test; then extra_flags+=" -DHALLZEE_TOUCH_TEST"; fi
  compile_args+=(--build-property "compiler.cpp.extra_flags=$extra_flags")
fi
"$cli" compile "${compile_args[@]}"
if $compile_only; then
  echo "Compile-only check passed; the ESP32 was not changed."
  exit 0
fi
# The first OTA install moves LittleFS; fast mode checks the installed layout.
dotnet_cli="$(command -v dotnet || true)"
if [[ -z "$dotnet_cli" ]] || ! "$dotnet_cli" --list-sdks | grep -q '^8\.'; then
  dotnet_cli="$tools_dir/dotnet/dotnet"
  if [[ ! -x "$dotnet_cli" ]] || ! "$dotnet_cli" --list-sdks | grep -q '^8\.'; then
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tools_dir/dotnet-install.sh"
    bash "$tools_dir/dotnet-install.sh" --channel 8.0 --install-dir "$tools_dir/dotnet" --no-path
  fi
fi
arduino_data="${ARDUINO_DIRECTORIES_DATA:-$HOME/Library/Arduino15}"
esptool="$arduino_data/packages/esp32/tools/esptool_py/5.3.1/esptool"
mklittlefs="$arduino_data/packages/esp32/tools/mklittlefs/4.0.2-db0513a/mklittlefs"
cp "$arduino_data/packages/esp32/hardware/esp32/$esp32_version/tools/partitions/boot_app0.bin" "$build_dir/boot_app0.bin"
if $fast; then
  "$dotnet_cli" run --project "$project_root/tools/FirmwareTool/FirmwareTool.csproj" -- usb-fast \
    --esptool "$esptool" --port "$port" --build "$build_dir"
else
  "$dotnet_cli" run --project "$project_root/tools/FirmwareTool/FirmwareTool.csproj" -- usb \
    --esptool "$esptool" --mklittlefs "$mklittlefs" --port "$port" --build "$build_dir" --backup "$tools_dir/terminal-backups"
fi
