param([Parameter(Mandatory=$true)][string]$BackupFolder,[Parameter(Mandatory=$true)][string]$Port)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/tool/FirmwareTool.exe" recover --esptool "$PSScriptRoot/tool/esptool.exe" --port $Port --backup $BackupFolder
if ($LASTEXITCODE -ne 0) { throw 'USB recovery failed. Preserve the original backup.' }
