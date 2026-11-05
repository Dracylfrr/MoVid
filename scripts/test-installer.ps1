$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectRoot ('test-output/installer-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
$installTarget = [System.IO.Path]::GetFullPath((Join-Path $testRoot 'installed'))
if (!$installTarget.StartsWith([System.IO.Path]::GetFullPath((Join-Path $projectRoot 'test-output')) + [System.IO.Path]::DirectorySeparatorChar,[System.StringComparison]::OrdinalIgnoreCase)) { throw 'Test install must stay inside this project.' }
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$startupKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
$startupBefore = (Get-ItemProperty -Path $startupKey -Name MoVid -ErrorAction SilentlyContinue).MoVid
$installer = Join-Path $projectRoot 'dist/MoVid-Setup-0.2.1.exe'
$proc = Start-Process -FilePath $installer -ArgumentList @('/S','/TESTMODE',('/D=' + $installTarget)) -WindowStyle Hidden -PassThru -Wait
if ($proc.ExitCode -ne 0) { throw "Installer failed: $($proc.ExitCode)" }
foreach ($name in @('MoVid.exe','Uninstall.exe','LICENSE.txt','README.txt','test-install.txt')) { if (!(Test-Path -LiteralPath (Join-Path $installTarget $name))) { throw "Missing installed file: $name" } }
$installedHash = (Get-FileHash -LiteralPath (Join-Path $installTarget 'MoVid.exe')).Hash
$sourceHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'bin/MoVid.exe')).Hash
if ($installedHash -ne $sourceHash) { throw 'Installed executable differs from the tested build.' }
$startupAfterInstall = (Get-ItemProperty -Path $startupKey -Name MoVid -ErrorAction SilentlyContinue).MoVid
if ($startupBefore -ne $startupAfterInstall) { throw 'Isolated install unexpectedly changed startup registration.' }
$testProfile = Join-Path $testRoot 'profile'
New-Item -ItemType Directory -Force -Path $testProfile | Out-Null
$probe = Start-Process -FilePath (Join-Path $installTarget 'MoVid.exe') -ArgumentList @('--self-test','--test-dir',('"' + $testProfile + '"')) -WindowStyle Hidden -PassThru -Wait
if ($probe.ExitCode -ne 0) { throw 'The installed app failed its checks.' }
$uninstaller = Start-Process -FilePath (Join-Path $installTarget 'Uninstall.exe') -ArgumentList '/S' -WindowStyle Hidden -PassThru -Wait
if ($uninstaller.ExitCode -ne 0) { throw 'Uninstaller failed.' }
$deadline = [DateTime]::UtcNow.AddSeconds(12)
while ((Test-Path -LiteralPath (Join-Path $installTarget 'MoVid.exe')) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
if (Test-Path -LiteralPath (Join-Path $installTarget 'MoVid.exe')) { throw 'Uninstall left the app executable.' }
$startupAfter = (Get-ItemProperty -Path $startupKey -Name MoVid -ErrorAction SilentlyContinue).MoVid
if ($startupBefore -ne $startupAfter) { throw 'Isolated uninstall changed the user startup registration.' }
@{passed=$true;checks=@('Silent isolated installation','Installed executable integrity','Installed app self-tests','Uninstall removes app','Isolated test preserves user startup state')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'installer-test.json')
Get-Content -LiteralPath (Join-Path $testRoot 'installer-test.json')
