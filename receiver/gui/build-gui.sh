#!/bin/zsh
set -euo pipefail
script_dir="$(cd "$(dirname "$0")" && pwd)"
receiver_dir="$(cd "$script_dir/.." && pwd)"
app_dir="$receiver_dir/Bathroom Sync.app"
macos_dir="$app_dir/Contents/MacOS"
(cd "$receiver_dir" && zsh build.sh)
mkdir -p "$macos_dir"
cp "$script_dir/Info.plist" "$app_dir/Contents/Info.plist"
cp "$receiver_dir/bathroom-receiver" "$macos_dir/bathroom-receiver"
clang -fobjc-arc -framework Cocoa "$script_dir/BathroomSyncApp.m" -o "$macos_dir/BathroomSync"
echo "Built: $app_dir"
