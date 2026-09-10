param(
  [string]$Port,
  [ValidateSet("st7735", "ili9341")]
  [string]$Display = "st7735",
  [ValidateRange(0, 3)]
  [int]$Rotation = 1,
  [switch]$CompileOnly,
  [switch]$Fast,
  [switch]$TouchTest,
  [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
if ($TouchTest -and ($Display -ne "ili9341" -or $Rotation -notin @(1, 3))) {
  throw "-TouchTest requires -Display ili9341 and landscape rotation 1 or 3."
}
$toolsDir = Join-Path $ProjectRoot ".tools"
$cliDir = Join-Path $toolsDir "arduino-cli"
$cli = Join-Path $cliDir "arduino-cli.exe"
$esp32Index = "https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json"
$esp32Version = "3.3.11"

Write-Host @"
Hallzee firmware flasher

Before continuing, make sure:
  - An original ESP32 terminal is connected with a USB data cable.
  - The terminal is powered on.
  - No other USB serial device is connected, unless you provide its port.
  - You are ready to replace the firmware currently on that ESP32.

Regular mode installs required Arduino software; fast mode reuses installed dependencies.
"@

if (-not $Port -and -not $CompileOnly) {
  $ports = [System.IO.Ports.SerialPort]::GetPortNames() | Sort-Object
  if ($ports.Count -eq 1) {
    $Port = $ports[0]
  } elseif ($ports.Count -eq 0) {
    throw "No USB serial terminal was found. Plug in the ESP32, then run this command again."
  } else {
    $joinedPorts = $ports -join ", "
    throw "More than one serial device was found: $joinedPorts. Unplug the others, then run this command again."
  }
}

if (-not (Test-Path $cli)) {
  New-Item -ItemType Directory -Force -Path $cliDir | Out-Null
  $archive = Join-Path $toolsDir "arduino-cli.zip"
  Write-Host "Installing the Arduino command-line tools (one time only)…"
  Invoke-WebRequest "https://downloads.arduino.cc/arduino-cli/arduino-cli_latest_Windows_64bit.zip" -OutFile $archive
  Expand-Archive -Path $archive -DestinationPath $cliDir -Force
  Remove-Item -Force $archive
}

if (-not $Fast) {
  Write-Host "Installing the ESP32 board support and required libraries if needed…"
  & $cli core update-index --additional-urls $esp32Index
  if ($LASTEXITCODE -ne 0) { throw "Could not update the Arduino package index." }
  & $cli core install "esp32:esp32@$esp32Version" --additional-urls $esp32Index
  if ($LASTEXITCODE -ne 0) { throw "Could not install the pinned ESP32 core." }
  $libraries = @(Get-Content (Join-Path $ProjectRoot "firmware/arduino-libraries.txt") | Where-Object { $_ -and -not $_.StartsWith('#') })
  & $cli lib install --no-deps @libraries
  if ($LASTEXITCODE -ne 0) { throw "Could not install firmware libraries." }

  if ($TouchTest) {
    & $cli lib install "XPT2046_Touchscreen@1.4"
    if ($LASTEXITCODE -ne 0) { throw "Could not install the touch experiment library." }
  }
} else {
  Write-Host "Fast mode: using installed board support/libraries. If compilation reports missing tools, run once without -Fast (optionally -CompileOnly)."
}

# Arduino requires the sketch directory and its main .ino file to share a
# basename. The repository name is intentionally independent of that file.
$sketchFiles = @(Get-ChildItem -Path $ProjectRoot -Filter "*.ino" -File)
if ($sketchFiles.Count -ne 1) {
  throw "Expected exactly one .ino sketch in $ProjectRoot."
}
$sketchName = $sketchFiles[0].BaseName
$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("hallzee-" + [guid]::NewGuid().ToString())
$stagingSketch = Join-Path $stagingRoot $sketchName
$buildDir = Join-Path $stagingRoot "build"
New-Item -ItemType Directory -Force -Path $stagingSketch | Out-Null
Copy-Item -Path (Join-Path $ProjectRoot "*.ino") -Destination $stagingSketch
Copy-Item -Path (Join-Path $ProjectRoot "*.h") -Destination $stagingSketch
Copy-Item -Path (Join-Path $ProjectRoot "*.cpp") -Destination $stagingSketch
Copy-Item -Path (Join-Path $ProjectRoot "firmware/partitions.csv") -Destination $stagingSketch
if (Test-Path (Join-Path $ProjectRoot "fonts")) {
  Copy-Item -Path (Join-Path $ProjectRoot "fonts") -Destination $stagingSketch -Recurse
}

try {
  Write-Host "Building Hallzee…"
  $buildProperties = @("--build-property", "upload.maximum_size=1572864")
  if ($Display -eq "ili9341") {
    $extraFlags = "-DHALLZEE_ILI9341 -DHALLZEE_DISPLAY_ROTATION=$Rotation"
    if ($TouchTest) { $extraFlags += " -DHALLZEE_TOUCH_TEST" }
    $buildProperties += @("--build-property", "compiler.cpp.extra_flags=$extraFlags")
  }
  & $cli compile --fqbn esp32:esp32:esp32 $stagingSketch --build-path $buildDir @buildProperties
  if ($LASTEXITCODE -ne 0) { throw "Firmware compilation failed." }
  if ($CompileOnly) {
    Write-Host "Compile-only check passed; the ESP32 was not changed."
  } else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    $hasDotnet8 = $dotnetCommand -and ((& $dotnetCommand.Source --list-sdks) -match '^8\.')
    $dotnet = if ($hasDotnet8) { $dotnetCommand.Source } else { Join-Path $toolsDir "dotnet/dotnet.exe" }
    if (-not (Test-Path $dotnet) -or -not ((& $dotnet --list-sdks) -match '^8\.')) {
      $installer = Join-Path $toolsDir "dotnet-install.ps1"
      Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
      & $installer -Channel "8.0" -InstallDir (Join-Path $toolsDir "dotnet") -NoPath
    }
    $arduinoData = if ($env:ARDUINO_DIRECTORIES_DATA) { $env:ARDUINO_DIRECTORIES_DATA } else { Join-Path $env:LOCALAPPDATA "Arduino15" }
    $esptool = Join-Path $arduinoData "packages/esp32/tools/esptool_py/5.3.1/esptool.exe"
    $mklittlefs = Join-Path $arduinoData "packages/esp32/tools/mklittlefs/4.0.2-db0513a/mklittlefs.exe"
    Copy-Item (Join-Path $arduinoData "packages/esp32/hardware/esp32/$esp32Version/tools/partitions/boot_app0.bin") (Join-Path $buildDir "boot_app0.bin")
    if ($Fast) {
      & $dotnet run --project (Join-Path $ProjectRoot "tools/FirmwareTool/FirmwareTool.csproj") -- usb-fast --esptool $esptool --port $Port --build $buildDir
    } else {
      & $dotnet run --project (Join-Path $ProjectRoot "tools/FirmwareTool/FirmwareTool.csproj") -- usb --esptool $esptool --mklittlefs $mklittlefs --port $Port --build $buildDir --backup (Join-Path $toolsDir "terminal-backups")
    }
    if ($LASTEXITCODE -ne 0) { throw "USB flash failed. Preserve any existing terminal backup; see the tool error above." }
  }
} finally {
  if (Test-Path $stagingRoot) {
    Remove-Item -Recurse -Force $stagingRoot
  }
}
