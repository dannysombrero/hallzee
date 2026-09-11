param([ValidateSet('dev','check','build','preview')][string]$Action = 'dev')
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Manifest = Get-Content -Raw (Join-Path $RepoRoot 'web-client/toolchain.json') | ConvertFrom-Json
if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This bootstrap supports Windows x64.' }
$Version = $Manifest.node
if ($Version -notmatch '^22\.\d+\.\d+$') { throw 'Invalid Node version in manifest.' }
$ToolRoot = Join-Path $RepoRoot '.local/web-client'
$NodeDir = Join-Path $ToolRoot "node-v$Version-win-x64"
New-Item -ItemType Directory -Force $ToolRoot | Out-Null
if (-not (Test-Path (Join-Path $NodeDir 'node.exe'))) {
  $Archive = Join-Path $ToolRoot "node-$([guid]::NewGuid()).zip"
  try {
    Invoke-WebRequest -UseBasicParsing "https://nodejs.org/dist/v$Version/node-v$Version-win-x64.zip" -OutFile $Archive
    if ((Get-FileHash $Archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Manifest.'win-x64') { throw 'Node checksum mismatch. Installation stopped.' }
    Expand-Archive $Archive -DestinationPath $ToolRoot -Force
  } finally { Remove-Item $Archive -ErrorAction SilentlyContinue }
}
$env:PATH = "$NodeDir;$env:PATH"
$env:npm_config_cache = Join-Path $RepoRoot '.local/npm-cache'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $RepoRoot '.local/playwright'
Push-Location (Join-Path $RepoRoot 'web-client')
try {
  & npm.cmd ci
  if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
  if ($Action -eq 'check') {
    & npx.cmd --no-install playwright install chromium
    if ($LASTEXITCODE -ne 0) { throw 'Browser installation failed.' }
  }
  if ($Action -eq 'preview') {
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
  }
  & npm.cmd run $Action
  if ($LASTEXITCODE -ne 0) { throw "Web client $Action failed." }
} finally { Pop-Location }
