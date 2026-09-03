param(
  [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$toolsDir = Join-Path $ProjectRoot ".tools"
$dotnetDir = Join-Path $toolsDir "dotnet"
$dotnet = Join-Path $dotnetDir "dotnet.exe"
$output = Join-Path $ProjectRoot "artifacts\BathroomSync-Windows"

if (-not (Test-Path $dotnet)) {
  New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
  $installer = Join-Path $toolsDir "dotnet-install.ps1"
  Write-Host "Installing the .NET build tools (one time only)…"
  Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
  & $installer -Channel "8.0" -InstallDir $dotnetDir -NoPath
}

Write-Host "Building the Windows Hallzee Sync app…"
& $dotnet publish (Join-Path $ProjectRoot "receiver\universal\BathroomSync.Universal.csproj") `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -o $output

Write-Host "Done. Open this folder to run the app: $output"
