#!/bin/bash
# CI/developer packaging; Actions installs the SDK and macOS workload first.
set -euo pipefail
export AVALONIA_TELEMETRY_OPTOUT=1
cd "$(dirname "$0")/.."
version="${1:?Usage: package-client-macos.sh VERSION RID RELEASE_REPOSITORY OUTPUT}"
rid="${2:?Expected osx-arm64 or osx-x64}"
repository="${3:?Expected owner/repository}"
output="${4:?Expected a new output directory}"
[[ "$version" =~ ^(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})$ ]]
[[ "$rid" == osx-arm64 || "$rid" == osx-x64 ]]
[[ "$repository" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]
[[ ! -e "$output" ]]
mkdir -p "$output"
output="$(cd "$output" && pwd)"
app="$output/Hallzee.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp receiver/universal/Assets/hallzee.icns "$app/Contents/Resources/hallzee.icns"
dotnet publish receiver/universal/BathroomSync.Universal.csproj -c Release -r "$rid" --self-contained true -p:PublishTrimmed=false -p:Version="$version" -p:ReleaseRepository="$repository" -o "$app/Contents/MacOS"
# The helper is a native .NET macOS app, not part of the Avalonia publish output.
dotnet build receiver/MacBLEAgent/BathroomSync.MacBLEAgent.csproj -c Release -r "$rid" -p:Version="$version" -p:EnableCodeSigning=false
helper="receiver/MacBLEAgent/bin/Release/net8.0-macos/$rid/BathroomSync.MacBLEAgent.app"
test -x "$helper/Contents/MacOS/BathroomSync.MacBLEAgent"
ditto "$helper" "$app/Contents/MacOS/BathroomSync.MacBLEAgent.app"
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.hallzee.desktop</string>
<key>CFBundleName</key><string>Hallzee</string>
<key>CFBundleExecutable</key><string>HallzeeSync.Universal</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleIconFile</key><string>hallzee.icns</string>
<key>CFBundleShortVersionString</key><string>$version</string>
<key>CFBundleVersion</key><string>$version</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSBluetoothAlwaysUsageDescription</key><string>Hallzee uses Bluetooth to connect to your classroom terminal.</string>
</dict></plist>
PLIST
# Ad-hoc signing permits local execution; it is not Developer ID notarization.
# --deep does not discover the helper's native libraries in Contents/MonoBundle.
# The macOS build rewrites their load paths, invalidating their original signatures.
# Sign these explicitly before sealing the helper and enclosing application.
while IFS= read -r -d '' library; do
  codesign --force --sign - "$library"
done < <(find "$app" -type f -name '*.dylib' -print0)
codesign --force --sign - "$app/Contents/MacOS/BathroomSync.MacBLEAgent.app"
codesign --force --deep --sign - "$app"
while IFS= read -r -d '' library; do
  codesign --verify --strict "$library"
done < <(find "$app" -type f -name '*.dylib' -print0)
codesign --verify --deep --strict "$app"
# With no arguments the helper loads its runtime and exits before using Bluetooth.
# An Intel package built on ARM (or vice versa) needs a matching-host launch check.
if [[ ( "$rid" == osx-arm64 && "$(uname -m)" == arm64 ) ||
      ( "$rid" == osx-x64 && "$(uname -m)" == x86_64 ) ]]; then
  "$app/Contents/MacOS/BathroomSync.MacBLEAgent.app/Contents/MacOS/BathroomSync.MacBLEAgent"
else
  echo "Helper launch check skipped: validate $rid on a matching Mac before release."
fi
ditto -c -k --sequesterRsrc --keepParent "$app" "$output/Hallzee-Mac-$rid.zip"
