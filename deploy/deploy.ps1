<#
.SYNOPSIS
    Builds Web Builders (website + Domains API) and deploys it to the server over FTPS.

.DESCRIPTION
    1. dotnet publish (Release) into artifacts/publish; the website is included in wwwroot.
    2. Writes appsettings.Production.json into the build output, with the Stripe and Namecheap keys taken from
       your local .NET user-secrets and the server's IP from deploy.settings.json. Keys never enter the repository.
    3. Uploads app_offline.htm so IIS stops the app and releases its files.
    4. Uploads every published file over FTPS.
    5. Removes app_offline.htm (the app starts again) and checks the site responds.

    The FTP password is read from the WB_FTP_PASSWORD environment variable, or asked for when the script runs.

.EXAMPLE
    ./deploy/deploy.ps1              # build and deploy
.EXAMPLE
    ./deploy/deploy.ps1 -BuildOnly   # build into artifacts/publish without uploading
#>
[CmdletBinding()]
param(
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 may default to old TLS versions for the final site check.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$repoRoot     = Split-Path -Parent $PSScriptRoot
$apiProject   = Join-Path $repoRoot 'src\WebBuilders.Domains.Api'
$publishDir   = Join-Path $repoRoot 'artifacts\publish'
$settingsFile = Join-Path $PSScriptRoot 'deploy.settings.json'
$offlinePage  = Join-Path $PSScriptRoot 'app_offline.htm'

function Write-Step([string]$message) { Write-Host "`n==> $message" -ForegroundColor Cyan }

$settings = Get-Content $settingsFile -Raw | ConvertFrom-Json

# ---------------------------------------------------------------- server settings from user-secrets
function Get-ServerSettings {
    $ErrorActionPreference = 'Continue'   # see Invoke-Curl: keep native stderr from becoming an exception
    $raw = & dotnet user-secrets list --json --project $apiProject 2>$null
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) { throw 'Could not read user-secrets. See docs/local-development.md to set the keys.' }

    $json    = ($raw | Where-Object { $_ -notmatch '^//(BEGIN|END)' }) -join "`n"
    $secrets = $json | ConvertFrom-Json

    $required = 'Stripe:PublishableKey', 'Stripe:SecretKey', 'Namecheap:ApiUser', 'Namecheap:ApiKey'
    $missing  = $required | Where-Object { -not $secrets.$_ }
    if ($missing) { throw "Missing user-secrets: $($missing -join ', '). See docs/local-development.md." }

    return [ordered]@{
        Stripe = [ordered]@{
            PublishableKey = $secrets.'Stripe:PublishableKey'
            SecretKey      = $secrets.'Stripe:SecretKey'
        }
        Namecheap = [ordered]@{
            ApiUser  = $secrets.'Namecheap:ApiUser'
            ApiKey   = $secrets.'Namecheap:ApiKey'
            # The server calls Namecheap from its own IP, not from your PC's.
            ClientIp = $settings.namecheap.serverClientIp
        }
    }
}

# ---------------------------------------------------------------- build
Write-Step 'Reading server keys from user-secrets'
$serverSettings = Get-ServerSettings

Write-Step 'Publishing Release build'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $apiProject -c Release -o $publishDir -p:UseAppHost=false --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$serverSettings | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $publishDir 'appsettings.Production.json') -Encoding utf8
$files = @(Get-ChildItem $publishDir -Recurse -File)
Write-Host ("Build ready: {0} ({1} files)" -f $publishDir, $files.Count) -ForegroundColor Green

if ($BuildOnly) { return }

# ---------------------------------------------------------------- upload
$ftp        = $settings.ftp
$remotePath = ([string]$ftp.remotePath).Trim('/')
$baseUrl    = "ftp://$($ftp.host)/" + $(if ($remotePath) { "$remotePath/" } else { '' })
$siteUrl    = ([string]$settings.siteUrl).TrimEnd('/')

$password = $env:WB_FTP_PASSWORD
if (-not $password) {
    $secure   = Read-Host "FTP password for $($ftp.user)@$($ftp.host)" -AsSecureString
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

# Credentials go in a temporary curl config file so they never appear on a command line.
$curlConfig = New-TemporaryFile
@(
    "user = `"$($ftp.user):$($password -replace '\\', '\\' -replace '"', '\"')`""
    'ssl-reqd'
    'ftp-create-dirs'
    'silent'
    'show-error'
    'fail-early'
) | Set-Content $curlConfig -Encoding ascii

# Runs curl and returns its exit code. Error messages curl prints never become PowerShell exceptions:
# Windows PowerShell 5.1 turns a native program's redirected stderr into a terminating error under
# ErrorActionPreference=Stop, which would skip the retry logic below. -Quiet hides curl's messages.
function Invoke-Curl([string[]]$arguments, [switch]$Quiet) {
    $ErrorActionPreference = 'Continue'
    if ($Quiet) { & curl.exe -K $curlConfig.FullName @arguments 2>&1 | Out-Null }
    else        { & curl.exe -K $curlConfig.FullName @arguments | Out-Host }
    return $LASTEXITCODE
}

# Names of the files in the remote site folder.
function Get-RemoteListing {
    $ErrorActionPreference = 'Continue'
    $lines = & curl.exe -K $curlConfig.FullName --list-only $baseUrl 2>$null
    return @($lines | ForEach-Object { $_.Trim() })
}

function Get-RemoteUrl($file) {
    $relative = $file.FullName.Substring($publishDir.Length + 1).Replace('\', '/')
    $encoded  = ($relative -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    return "$baseUrl$encoded"
}

# Uploads a batch over one connection; if anything fails, retries each file of the batch on its own.
# Returns the files that still failed after 3 attempts.
function Send-Batch($batch) {
    $arguments = foreach ($file in $batch) { '-T'; $file.FullName; (Get-RemoteUrl $file) }
    if ((Invoke-Curl $arguments -Quiet) -eq 0) { return @() }

    $failed = @()
    foreach ($file in $batch) {
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            if ((Invoke-Curl @('-T', $file.FullName, (Get-RemoteUrl $file)) -Quiet) -eq 0) { break }
            if ($attempt -eq 3) { $failed += $file } else { Start-Sleep -Seconds (2 * $attempt) }
        }
    }
    return $failed
}

# Deletes app_offline.htm and checks the folder listing until it is really gone.
function Remove-AppOffline {
    $target = if ($remotePath) { "$remotePath/app_offline.htm" } else { 'app_offline.htm' }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        Invoke-Curl @('-Q', "DELE $target", "ftp://$($ftp.host)/", '--list-only', '-o', 'NUL') -Quiet | Out-Null
        if ((Get-RemoteListing) -notcontains 'app_offline.htm') { return $true }
        Start-Sleep -Seconds (3 * $attempt)
    }
    return $false
}

$failedFiles = @()
$wentOffline = $false
try {
    Write-Step "Taking $siteUrl offline (app_offline.htm)"
    if ((Invoke-Curl @('-T', $offlinePage, "${baseUrl}app_offline.htm")) -ne 0) {
        throw 'Could not connect to the FTP server (check the password and deploy.settings.json).'
    }
    $wentOffline = $true
    Start-Sleep -Seconds 5   # give IIS time to stop the app and release the DLLs

    Write-Step "Uploading $($files.Count) files to $($ftp.host)"
    $batchSize = 40   # one curl call per batch reuses the FTPS connection
    for ($i = 0; $i -lt $files.Count; $i += $batchSize) {
        $last = [Math]::Min($i + $batchSize, $files.Count) - 1
        $failedFiles += @(Send-Batch $files[$i..$last])
        Write-Host ("  {0}/{1}" -f ($last + 1), $files.Count)
    }
}
finally {
    # Always bring the site back, even when an upload failed, so it never stays on the "updating" page.
    if ($wentOffline) {
        Write-Step 'Bringing the site back online'
        if (-not (Remove-AppOffline)) {
            Write-Warning 'Could not remove app_offline.htm. Delete it from the site root over FTP, or run the deploy again.'
        }
    }
    Remove-Item $curlConfig -Force -ErrorAction SilentlyContinue
}

# Website files the host refuses (its FTP content filter rejects a few sample images) are reported, not fatal.
# Anything else (DLLs, config) must arrive, or the deployed app would be inconsistent.
$wwwrootPrefix = Join-Path $publishDir 'wwwroot'
$failedApp     = @($failedFiles | Where-Object { -not $_.FullName.StartsWith($wwwrootPrefix) })
$failedWebsite = @($failedFiles | Where-Object { $_.FullName.StartsWith($wwwrootPrefix) })
foreach ($file in $failedWebsite) {
    Write-Warning ("Server refused website file: {0}" -f $file.FullName.Substring($publishDir.Length + 1))
}
if ($failedApp.Count -gt 0) {
    $names = $failedApp | ForEach-Object { $_.FullName.Substring($publishDir.Length + 1) }
    throw "These application files were not uploaded; run the deploy again: $($names -join ', ')"
}

# ---------------------------------------------------------------- smoke test
Write-Step "Checking $siteUrl"
Start-Sleep -Seconds 3
try {
    $page   = Invoke-WebRequest "$siteUrl/domains.html" -UseBasicParsing -TimeoutSec 90
    $config = Invoke-RestMethod "$siteUrl/api/store/config" -TimeoutSec 60
    Write-Host ("  domains.html  : HTTP {0}" -f $page.StatusCode)
    Write-Host ("  Registrar     : {0}" -f $config.registrar)
    Write-Host ("  Card payments : {0}" -f $(if ($config.paymentsEnabled) { 'enabled (Stripe test mode)' } else { 'NOT configured' }))
    Write-Host "`nDeployed: $siteUrl/domains.html" -ForegroundColor Green
}
catch {
    Write-Warning "Upload finished but the site did not answer correctly: $($_.Exception.Message)"
    Write-Warning 'See docs/server-deployment.md > Troubleshooting.'
    exit 1
}
