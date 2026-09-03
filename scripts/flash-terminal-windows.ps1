param(
  [string]$Port,
  [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
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

No Arduino software needs to be installed first; this script installs what it needs.
"@

if (-not $Port) {
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

Write-Host "Installing the ESP32 board support and required libraries if needed…"
& $cli core update-index --additional-urls $esp32Index
& $cli core install "esp32:esp32@$esp32Version" --additional-urls $esp32Index
& $cli lib install "Adafruit GFX Library" "Adafruit ST7735 and ST7789 Library" Keypad

# Arduino requires the sketch directory and its main .ino file to share a
# basename. The repository name is intentionally independent of that file.
$sketchFiles = @(Get-ChildItem -Path $ProjectRoot -Filter "*.ino" -File)
if ($sketchFiles.Count -ne 1) {
  throw "Expected exactly one .ino sketch in $ProjectRoot."
}
$sketchName = $sketchFiles[0].BaseName
$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("hallzee-" + [guid]::NewGuid().ToString())
$stagingSketch = Join-Path $stagingRoot $sketchName
New-Item -ItemType Directory -Force -Path $stagingSketch | Out-Null
Copy-Item -Path (Join-Path $ProjectRoot "*.ino") -Destination $stagingSketch
Copy-Item -Path (Join-Path $ProjectRoot "*.h") -Destination $stagingSketch
Copy-Item -Path (Join-Path $ProjectRoot "*.cpp") -Destination $stagingSketch

try {
  Write-Host "Building and flashing Hallzee to $Port…"
  & $cli compile --fqbn esp32:esp32:esp32 $stagingSketch
  & $cli upload --fqbn esp32:esp32:esp32 --port $Port $stagingSketch
  Write-Host "Done. The Hallzee firmware is now on the ESP32."
} finally {
  if (Test-Path $stagingRoot) {
    Remove-Item -Recurse -Force $stagingRoot
  }
}
