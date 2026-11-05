$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $projectRoot 'tools'
New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
$zipPath = Join-Path $toolsRoot 'nsis-3.11.zip'
# Portable, unmodified NSIS distributed by Tauri's build-tool mirror.
$url = 'https://github.com/tauri-apps/binary-releases/releases/download/nsis-3.11/nsis-3.11.zip'
$expected = 'C7D27F780DDB6CFFB4730138CD1591E841F4B7EDB155856901CDF5F214394FA1'
if (!(Test-Path -LiteralPath $zipPath)) { Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $zipPath }
if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash -ne $expected) { throw 'NSIS archive hash mismatch. Do not execute it.' }
Expand-Archive -LiteralPath $zipPath -DestinationPath $toolsRoot -Force
Write-Output 'Verified portable NSIS compiler is ready in tools.'
