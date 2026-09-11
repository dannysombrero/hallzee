#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
action="${1:-dev}"
case "$action" in dev|check|build|preview) ;; *) echo 'Usage: web-client-macos.sh [dev|check|build|preview]' >&2; exit 2;; esac
[[ "$(uname -s)" == Darwin ]] || { echo 'This bootstrap is for macOS.' >&2; exit 1; }
case "$(uname -m)" in arm64) arch=arm64;; x86_64) arch=x64;; *) echo 'Supported architectures: arm64, x64.' >&2; exit 1;; esac
manifest="$repo_root/web-client/toolchain.json"
version="$(sed -n 's/.*"node": "\([^"]*\)".*/\1/p' "$manifest")"
checksum="$(sed -n "s/.*\"darwin-$arch\": \"\([^\"]*\)\".*/\1/p" "$manifest")"
[[ "$version" =~ ^22\.[0-9]+\.[0-9]+$ && "$checksum" =~ ^[0-9a-f]{64}$ ]] || { echo 'Invalid toolchain manifest.' >&2; exit 1; }
tool_root="$repo_root/.local/web-client"
node_dir="$tool_root/node-v$version-darwin-$arch"
mkdir -p "$tool_root"
if [[ ! -x "$node_dir/bin/node" ]]; then
  archive="$(mktemp "$tool_root/node-download.XXXXXX")"
  trap 'rm -f "$archive"' EXIT
  curl --fail --location --retry 3 "https://nodejs.org/dist/v$version/node-v$version-darwin-$arch.tar.gz" -o "$archive"
  actual="$(shasum -a 256 "$archive" | awk '{print $1}')"
  [[ "$actual" == "$checksum" ]] || { echo 'Node checksum mismatch. Installation stopped.' >&2; exit 1; }
  tar -xzf "$archive" -C "$tool_root"
  rm -f "$archive"
  trap - EXIT
fi
export PATH="$node_dir/bin:$PATH"
export npm_config_cache="$repo_root/.local/npm-cache"
export PLAYWRIGHT_BROWSERS_PATH="$repo_root/.local/playwright"
cd "$repo_root/web-client"
npm ci
if [[ "$action" == check ]]; then npm exec -- playwright install chromium; fi
if [[ "$action" == preview ]]; then npm run build; fi
exec npm run "$action"
