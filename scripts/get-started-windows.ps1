$ErrorActionPreference = "Stop"

$owner = "dannysombrero"
$repository = "bathroom-signin"
$ref = if ($env:BATHROOM_TERMINAL_REF) { $env:BATHROOM_TERMINAL_REF } else { "codex/web-preview-site" }
$destination = if ($env:BATHROOM_TERMINAL_HOME) { $env:BATHROOM_TERMINAL_HOME } else { Join-Path $HOME "Bathroom-Terminal" }

if (Test-Path $destination) {
  Write-Host "$destination already exists. To protect your files, it was not changed."
  Write-Host "Run $destination\scripts\build-windows-client.ps1 to build the Windows app."
  exit 0
}

$temporaryDir = Join-Path ([System.IO.Path]::GetTempPath()) ("bathroom-terminal-" + [guid]::NewGuid())
$archive = Join-Path $temporaryDir "source.zip"
New-Item -ItemType Directory -Force -Path $temporaryDir | Out-Null

try {
  Write-Host "Downloading Bathroom Terminal ($ref)…"
  Invoke-WebRequest "https://github.com/$owner/$repository/archive/refs/heads/$ref.zip" -OutFile $archive
  Expand-Archive -Path $archive -DestinationPath $temporaryDir
  $sourceDir = Get-ChildItem -Path $temporaryDir -Directory | Where-Object { $_.Name -notlike ".*" } | Select-Object -First 1
  Move-Item -Path $sourceDir.FullName -Destination $destination
  & (Join-Path $destination "scripts\build-windows-client.ps1") -ProjectRoot $destination
} finally {
  Remove-Item -Recurse -Force $temporaryDir -ErrorAction SilentlyContinue
}
