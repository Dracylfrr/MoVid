param([switch]$AppOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework C# compiler not found. Build on Windows 10 or 11 with .NET Framework 4.8.' }
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'native') -Filter '*.cs' | ForEach-Object { $_.FullName }
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $outputDir 'MoVid.exe')),('/win32icon:' + (Join-Path $projectRoot 'build/icon.ico')),('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),('/resource:' + (Join-Path $projectRoot 'build/tmdb.png') + ',tmdb.png'),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Net.Http.dll','/reference:System.Web.Extensions.dll','/reference:System.Security.dll')
& $compilerPath @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Native app compilation failed.' }
if (!$AppOnly) {
  $nsis = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tools') -Filter makensis.exe -Recurse | Select-Object -First 1
  if (!$nsis) { throw 'Download and extract the portable NSIS compiler into tools, then build again (see README).' }
  & $nsis.FullName '/V2' (Join-Path $projectRoot 'build/installer.nsi')
  if ($LASTEXITCODE -ne 0) { throw 'Windows installer compilation failed.' }
}
Get-ChildItem -LiteralPath $outputDir -Filter '*.exe' | Select-Object Name,Length
