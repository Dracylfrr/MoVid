$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1') -AppOnly
$reportDir = Join-Path $projectRoot ('test-output/run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$appExe = Join-Path $projectRoot 'bin/MoVid.exe'
foreach ($mode in @('--self-test','--smoke-test')) {
  $testDir = Join-Path $reportDir $mode.Substring(2)
  New-Item -ItemType Directory -Force -Path $testDir | Out-Null
  $proc = Start-Process -FilePath $appExe -ArgumentList @($mode,'--test-dir',('"' + $testDir + '"')) -WindowStyle Hidden -PassThru -Wait
  if ($proc.ExitCode -ne 0) { Get-ChildItem -LiteralPath $testDir -Filter '*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName }; throw "$mode failed." }
}
Get-ChildItem -LiteralPath $reportDir -Filter '*test.json' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName }
Write-Output "Reports and screenshots: $reportDir"
