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
sqlite_arch="${rid#osx-}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
app="$output/Hallzee.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp receiver/universal/Assets/hallzee.icns "$app/Contents/Resources/hallzee.icns"
dotnet publish receiver/universal/BathroomSync.Universal.csproj -c Release -r "$rid" --self-contained true -p:PublishTrimmed=false -p:Version="$version" -p:ReleaseRepository="$repository" -o "$app/Contents/MacOS"
# The macOS workload emits an app bundle whose executable still names the
# development-time Avalonia native library path. The library is included in the
# bundle's MonoBundle directory as libAvaloniaNative.dylib, and the executable
# already has that directory in its rpath. Point the load command at the
# bundled library so a clean Mac never needs /usr/local/lib populated.
install_name_tool -change \
  /usr/local/lib/libAvalonia.Native.OSX.dylib \
  @rpath/libAvaloniaNative.dylib \
  "$app/Contents/MacOS/HallzeeSync.Universal"
install_name_tool -change \
  "./bin/e_sqlite3/mac/$sqlite_arch/libe_sqlite3.dylib" \
  @rpath/libe_sqlite3.dylib \
  "$app/Contents/MacOS/HallzeeSync.Universal"
# The helper is a native .NET macOS app, not part of the Avalonia publish output.
dotnet build receiver/MacBLEAgent/BathroomSync.MacBLEAgent.csproj -c Release -r "$rid" -p:Version="$version" -p:EnableCodeSigning=false
helper="receiver/MacBLEAgent/bin/Release/net8.0-macos/$rid/BathroomSync.MacBLEAgent.app"
test -x "$helper/Contents/MacOS/BathroomSync.MacBLEAgent"
ditto "$helper" "$app/Contents/MacOS/BathroomSync.MacBLEAgent.app"
# Notices are resources, not executable code. In Contents/MacOS, codesign
# treats dotted package directories as nested bundles and rejects the app.
for notice in LICENSE.txt COPYRIGHT.txt THIRD-PARTY-NOTICES.md licenses; do
  mv "$app/Contents/MacOS/$notice" "$app/Contents/Resources/$notice"
done
# Workload framework packs are not ordinary NuGet package entries. Ask MSBuild
# which runtime packs it actually selected rather than guessing installed versions.
dotnet msbuild receiver/MacBLEAgent/BathroomSync.MacBLEAgent.csproj \
  -p:RuntimeIdentifier="$rid" -p:Configuration=Release \
  -target:ResolveFrameworkReferences -getItem:ResolvedFrameworkReference \
  > "$output/helper-framework-references.json"
# Capture only this release's restored graphs, including its native helper.
# The publication checksum covers the final ZIP after codesign changes binaries.
python3 scripts/generate-dependency-inventory.py --no-npm --rid "$rid" \
  --assets receiver/universal/obj/project.assets.json \
  --assets receiver/MacBLEAgent/obj/project.assets.json \
  --framework-references "$output/helper-framework-references.json" \
  --notices "$app/Contents/Resources/licenses/nuget" \
  --output "$app/Contents/Resources/licenses/dependency-inventory.json"
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
# Use Rosetta when it is already available to exercise Intel packages on ARM.
if [[ ( "$rid" == osx-arm64 && "$(uname -m)" == arm64 ) ||
      ( "$rid" == osx-x64 && "$(uname -m)" == x86_64 ) ]]; then
  "$app/Contents/MacOS/BathroomSync.MacBLEAgent.app/Contents/MacOS/BathroomSync.MacBLEAgent"
elif [[ "$rid" == osx-x64 && "$(uname -m)" == arm64 ]] && /usr/bin/arch -x86_64 /usr/bin/true 2>/dev/null; then
  /usr/bin/arch -x86_64 "$app/Contents/MacOS/BathroomSync.MacBLEAgent.app/Contents/MacOS/BathroomSync.MacBLEAgent"
else
  echo "Helper launch check skipped: validate $rid on a matching Mac before release."
fi
ditto -c -k --sequesterRsrc --keepParent "$app" "$output/Hallzee-Mac-$rid.zip"
