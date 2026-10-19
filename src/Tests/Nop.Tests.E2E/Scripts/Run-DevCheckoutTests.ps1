<#
.SYNOPSIS
    Runs local End-to-End Checkout Sandbox Tests against nopCommerce.

.DESCRIPTION
    1. Validates dev database safeguards (Sandbox mode enabled, email queue task disabled).
    2. Verifies test product (system-verification-item) is active.
    3. Checks if local store is already running on https://localhost:58552; if not, launches Kestrel in background.
    4. Executes Playwright NUnit checkout tests.
    5. Tears down the background server if it was started by this script.
    6. Reports test results and failure screenshots.

.PARAMETER Filter
    NUnit test filter (e.g. "Category=Square", "Category=PayPal", "Category=Manual", or specific test name).
    Default runs all tests with Category=CheckoutE2E.

.PARAMETER Headless
    Switch to force headless mode (defaults to setting in testsettings.json, typically false for visual debug).

.PARAMETER NoServerLaunch
    Switch to prevent launching Kestrel automatically if the store isn't already running.

.PARAMETER DatabaseServer
    SQL Server instance name (default: "DERRICK-SURFACE\SQLEXPRESS").

.PARAMETER DatabaseName
    SQL Database catalog name (default: "rcc_azure_20260801_2").
#>
[CmdletBinding()]
param (
    [string]$Filter = "Category=CheckoutE2E",
    [switch]$Headless,
    [switch]$NoServerLaunch,
    [string]$DatabaseServer = "DERRICK-SURFACE\SQLEXPRESS",
    [string]$DatabaseName = "rcc_azure_20260801_2",
    [int]$Port = 58552
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$E2eProjectDir = Split-Path -Parent $ScriptDir
$SrcDir = Split-Path -Parent (Split-Path -Parent $E2eProjectDir)
$WebProjectDir = Join-Path $SrcDir "Presentation\Nop.Web"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  nopCommerce Local E2E Sandbox Checkout Test Runner      " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------
# Step 1: Dev Database Safeguards
# ------------------------------------------------------------------
Write-Host "`n[1/4] Applying dev database safeguards and verifying test SKU..." -ForegroundColor Yellow
$SqlScript = Join-Path $ScriptDir "Apply-DevSandboxSettings.sql"
if (Test-Path $SqlScript) {
    try {
        & sqlcmd -S $DatabaseServer -d $DatabaseName -E -i $SqlScript
        Write-Host "Database safeguards verified successfully." -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not run sqlcmd to apply database safeguards. Proceeding with caution: $_"
    }
} else {
    Write-Warning "Apply-DevSandboxSettings.sql not found at $SqlScript"
}

# ------------------------------------------------------------------
# Step 2: Check or Launch Kestrel Server
# ------------------------------------------------------------------
$TargetUrl = "https://localhost:$Port"
Write-Host "`n[2/4] Checking store connectivity at $TargetUrl..." -ForegroundColor Yellow

function Test-StoreEndpoint {
    param ([string]$url)
    try {
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $response = Invoke-WebRequest -Uri $url -SkipCertificateCheck -TimeoutSec 3 -ErrorAction SilentlyContinue
        } else {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3 -ErrorAction SilentlyContinue
        }
        return ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400)
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -lt 500) {
            return $true
        }
        return $false
    }
}

$serverProcess = $null
$startedByScript = $false

if (Test-StoreEndpoint $TargetUrl) {
    Write-Host "Store is already running and accessible at $TargetUrl." -ForegroundColor Green
}
else {
    if ($NoServerLaunch) {
        throw "Store is not running at $TargetUrl and -NoServerLaunch was specified."
    }

    $dllPath = Join-Path $WebProjectDir "bin\Debug\net10.0\Nop.Web.dll"
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "dotnet"
    if (Test-Path $dllPath) {
        $startInfo.Arguments = "`"$dllPath`""
    } else {
        $startInfo.Arguments = "run --project `"$WebProjectDir\Nop.Web.csproj`""
    }
    $startInfo.WorkingDirectory = $WebProjectDir
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "https://localhost:$Port;http://localhost:$($Port + 1)"
    $startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development"

    $serverProcess = [System.Diagnostics.Process]::Start($startInfo)
    $startedByScript = $true

    Write-Host "Waiting for store to warm up at $TargetUrl (up to 60s)..." -NoNewline
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        Write-Host "." -NoNewline
        if (Test-StoreEndpoint $TargetUrl) {
            $ready = $true
            break
        }
    }
    Write-Host ""

    if (-not $ready) {
        if ($serverProcess -and -not $serverProcess.HasExited) {
            Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        }
        throw "Timed out waiting for store to become healthy at $TargetUrl."
    }

    Write-Host "Store is healthy and listening on $TargetUrl." -ForegroundColor Green
}

# ------------------------------------------------------------------
# Step 3: Run Playwright NUnit Tests
# ------------------------------------------------------------------
Write-Host "`n[3/4] Executing checkout tests..." -ForegroundColor Yellow

$TestCsProj = Join-Path $E2eProjectDir "Nop.Tests.E2E.csproj"
$TestArgs = @(
    "test",
    "`"$TestCsProj`"",
    "--logger", "console;verbosity=normal"
)

if ($Filter) {
    $TestArgs += @("--filter", "$Filter")
}

if ($Headless) {
    $env:CHECKOUT_HEADLESS = "true"
}

$exitCode = 0
try {
    & dotnet @TestArgs
    $exitCode = $LASTEXITCODE
}
finally {
    if ($Headless) {
        Remove-Item Env:\CHECKOUT_HEADLESS -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------
# Step 4: Teardown & Summary
# ------------------------------------------------------------------
Write-Host "`n[4/4] Completing test run and cleaning up..." -ForegroundColor Yellow

if ($startedByScript -and $serverProcess -and -not $serverProcess.HasExited) {
    Write-Host "Stopping background Kestrel process (PID $($serverProcess.Id))..." -ForegroundColor Gray
    try {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        $serverProcess.WaitForExit(5000)
    }
    catch {
        Write-Warning "Failed to stop background server: $_"
    }
}

$ScreenshotsDir = Join-Path $E2eProjectDir "bin\Debug\net10.0\Screenshots"
if (Test-Path $ScreenshotsDir) {
    $recentScreenshots = Get-ChildItem -Path $ScreenshotsDir -Filter "*.png" | Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-10) }
    if ($recentScreenshots.Count -gt 0) {
        Write-Host "`nFailure screenshots captured:" -ForegroundColor Red
        foreach ($shot in $recentScreenshots) {
            Write-Host "  - $($shot.FullName)" -ForegroundColor Red
        }
    }
}

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "  ALL CHECKOUT TESTS PASSED SUCCESSFULLY!                 " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "  CHECKOUT TESTS FAILED (Exit Code $exitCode)             " -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Red
}

exit $exitCode
