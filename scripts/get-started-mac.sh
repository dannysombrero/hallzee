#!/usr/bin/env bash
set -euo pipefail

owner="dannysombrero"
repository="bathroom-signin"
ref="${BATHROOM_TERMINAL_REF:-codex/web-preview-site}"
destination="${BATHROOM_TERMINAL_HOME:-$HOME/Bathroom-Terminal}"

if [[ -e "$destination" ]]; then
  echo "$destination already exists. To protect your files, it was not changed."
  echo "Run $destination/scripts/flash-terminal-macos.sh to flash the firmware."
  exit 0
fi

temporary_dir="$(mktemp -d)"
trap 'rm -rf "$temporary_dir"' EXIT
archive="$temporary_dir/source.zip"

echo "Downloading Bathroom Terminal ($ref)…"
curl -fsSL "https://github.com/$owner/$repository/archive/refs/heads/$ref.zip" -o "$archive"
unzip -q "$archive" -d "$temporary_dir"
source_dir="$(find "$temporary_dir" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
mv "$source_dir" "$destination"
bash "$destination/scripts/flash-terminal-macos.sh"
