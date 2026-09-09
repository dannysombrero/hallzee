param(
  [Parameter(Mandatory=$true)][ValidateSet('esp32-st7735-r1','esp32-ili9341-r0','esp32-ili9341-r1','esp32-ili9341-r2','esp32-ili9341-r3')][string]$Variant,
  [Parameter(Mandatory=$true)][string]$Port
)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/tool/FirmwareTool.exe" usb --esptool "$PSScriptRoot/tool/esptool.exe" --mklittlefs "$PSScriptRoot/tool/mklittlefs.exe" --port $Port --build "$PSScriptRoot/images/USB-$Variant" --backup "$PSScriptRoot/backups"
if ($LASTEXITCODE -ne 0) { throw 'USB setup failed. Keep the backup and diagnostic output.' }
