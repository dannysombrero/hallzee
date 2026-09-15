#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

# Ordinary terminal serial output can contain classroom records. Only exact,
# bounded diagnostic lines may reach stdout, including when the CLI fails.
filter_diagnostics() {
  local line
  local allowed='^BLE_DIAG (READY v1|CONNECTED|SECURITY_REQUEST|AUTH_OK|AUTH_FAILED 0x[0-9A-F]{2}|DISCONNECTED 0x[0-9A-F]{2,4}|READ_REQUEST|WRITE_REQUEST)$'
  while IFS= read -r line; do
    line="${line%$'\r'}"
    if [[ "$line" =~ $allowed ]]; then printf '%s\n' "$line"; fi
  done
}

if [[ "${1:-}" == "--filter-stdin" && $# -eq 1 ]]; then
  filter_diagnostics
  exit 0
fi
if [[ $# -gt 1 || "${1:-}" == -* ]]; then
  echo "Usage: bash scripts/monitor-terminal-macos.sh [serial-port]"
  echo "       bash scripts/monitor-terminal-macos.sh --filter-stdin"
  exit 1
fi

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cli_dir="$project_root/.tools/arduino-cli"
cli="${HALLZEE_ARDUINO_CLI:-$cli_dir/bin/arduino-cli}"
port="${1:-}"
if [[ -z "$port" ]]; then
  shopt -s nullglob
  candidates=(/dev/cu.usb* /dev/cu.wch* /dev/cu.SLAB*)
  if [[ ${#candidates[@]} -eq 0 ]]; then
    echo "No USB serial terminal found. Use a USB data cable connected to the Mac, then retry."
    exit 1
  elif [[ ${#candidates[@]} -gt 1 ]]; then
    echo "Multiple USB serial devices found. Unplug the others or pass the terminal's serial port."
    exit 1
  fi
  port="${candidates[0]}"
fi

if [[ ! -x "$cli" ]]; then
  if [[ -n "${HALLZEE_ARDUINO_CLI:-}" ]]; then
    echo "The configured Arduino CLI is unavailable."
    exit 1
  fi
  mkdir -p "$cli_dir/bin"
  echo "Installing Arduino CLI for USB monitoring (one time only)…"
  curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | BINDIR="$cli_dir/bin" sh
  "$cli" core update-index
fi

# Arduino CLI stops monitoring on stdin EOF. Hold an empty FIFO open instead:
# this keeps monitoring active and prevents keyboard input reaching the device.
monitor_state="$(mktemp -d)"
mkfifo "$monitor_state/input"
exec 3<>"$monitor_state/input"
trap 'exec 3>&-; rm -rf "$monitor_state"' EXIT
echo "Filtered Bluetooth diagnostics at 115200 baud. This script saves no raw serial log."
echo "Leave the terminal connected by USB to the Mac and retry Hallzee on the Chromebook."
echo "Copy only BLE_DIAG lines. If none appear, briefly press the ESP32 reset button once."
echo "Press Ctrl-C to stop."
trap 'exit 0' INT
set +e
"$cli" monitor --port "$port" --protocol serial --config baudrate=115200 --quiet <&3 2>&1 | filter_diagnostics
monitor_status=${PIPESTATUS[0]}
set -e
if [[ "$monitor_status" -ne 0 && "$monitor_status" -ne 130 ]]; then
  echo "USB monitor stopped. Check the data cable and close other serial monitors, then retry."
  exit "$monitor_status"
fi
