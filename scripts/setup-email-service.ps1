param([string]$WorkerName='movid-email')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$serverDir = Join-Path $projectRoot 'server'
$config = Join-Path $serverDir 'wrangler.local.json'
$wrangler = 'wrangler@4.157.0'
if (!(Get-Command npx.cmd -ErrorAction SilentlyContinue)) { throw 'Install the free Node.js LTS from nodejs.org, then run this owner setup again.' }
if ($WorkerName -notmatch '^[a-z0-9-]{3,50}$') { throw 'Choose a simple lowercase worker name.' }
Write-Host 'MoVid owner setup. Everyday users will only enter and verify their email.'
Write-Host 'Use Cloudflare Workers Free and Brevo Free. Do not choose paid plans, buy credits, or enter payment details.'
if ((Read-Host 'Are both accounts on their FREE plans? Type YES') -cne 'YES') { throw 'Setup stopped before any service changes.' }
if (!(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tools') -Filter makensis.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1)) { & (Join-Path $PSScriptRoot 'get-installer-tool.ps1') }
& npx.cmd --yes $wrangler login
if ($LASTEXITCODE -ne 0) { throw 'Cloudflare sign-in did not finish.' }
if (!(Test-Path -LiteralPath $config)) {
  $initial = Get-Content -LiteralPath (Join-Path $serverDir 'wrangler.jsonc') -Raw | ConvertFrom-Json
  $initial.name = $WorkerName
  $initial.d1_databases = @()
  $initial | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $config
  & npx.cmd --yes $wrangler d1 create $WorkerName --config $config --binding DB --update-config
  if ($LASTEXITCODE -ne 0) { throw 'Database creation did not finish. See server/OWNER-SETUP.md for resuming an existing database.' }
}
$configured = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
if (!$configured.d1_databases -or !$configured.d1_databases[0].database_id) { throw 'The database binding is missing. Follow the resume instructions in server/OWNER-SETUP.md.' }
& npx.cmd --yes $wrangler d1 execute $configured.d1_databases[0].database_name --config $config --remote --file (Join-Path $serverDir 'schema.sql') --yes
if ($LASTEXITCODE -ne 0) { throw 'Email database preparation failed.' }
$deployLines = & npx.cmd --yes $wrangler deploy --config $config
$deployLines | Write-Host
if ($LASTEXITCODE -ne 0) { throw 'The email service was not deployed.' }
$urlMatch = [regex]::Match(($deployLines -join "`n"),'https://[a-z0-9-]+\.[a-z0-9-]+\.workers\.dev')
if (!$urlMatch.Success) { throw 'Could not identify the service address. Copy it from the Cloudflare dashboard and follow OWNER-SETUP.md.' }
$serviceUrl = $urlMatch.Value
function Put-PrivateSecret([string]$Name) {
  $secret = Read-Host "Paste $Name (hidden; never paste it into GitHub or a chat)" -AsSecureString
  $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($secret)
  try {
    $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($pointer)
    $plain | & npx.cmd --yes $wrangler secret put $Name --config $config
    if ($LASTEXITCODE -ne 0) { throw "Could not store $Name securely in Cloudflare." }
  } finally { $plain=$null;[System.Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($pointer);$secret.Dispose() }
}
Put-PrivateSecret 'TMDB_TOKEN'
Put-PrivateSecret 'BREVO_KEY'
$sender = Read-Host 'Enter the sender email address you verified with Brevo'
if ($sender -notmatch '^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$') { throw 'Enter a valid verified sender address.' }
$sender | & npx.cmd --yes $wrangler secret put MAIL_FROM --config $config
if ($LASTEXITCODE -ne 0) { throw 'Sender configuration failed.' }
$serviceUrl | & npx.cmd --yes $wrangler secret put PUBLIC_URL --config $config
if ($LASTEXITCODE -ne 0) { throw 'Public service URL configuration failed.' }
$health = Invoke-RestMethod -Uri ($serviceUrl + '/health')
if (!$health.ready) { throw 'The service reports incomplete configuration. No ready-to-send claim will be made.' }
Write-Host 'Service configuration is ready. Sender approval and a real test email still need to be verified.'
& (Join-Path $PSScriptRoot 'build.ps1') -ServiceUrl $serviceUrl
Write-Host 'Use the new installer in dist. Users can now verify an email instead of registering API accounts.'
