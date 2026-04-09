#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Full development deployment: build, register COM, launch app.
.DESCRIPTION
    1. Builds the entire solution
    2. Runs register-com.ps1 (self-elevates for admin)
    3. Launches the app (non-elevated)
#>
param(
    [string]$Configuration = "Debug",
    [string]$StreamUrl = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

Write-Host "=== vCamService Dev Deploy ===" -ForegroundColor Cyan

# Step 1: Build everything
Write-Host "`nBuilding solution..." -ForegroundColor Yellow
dotnet build (Join-Path $repoRoot "vCamService.slnx") -c $Configuration
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
Write-Host "  Build succeeded" -ForegroundColor Green

# Step 2: Register COM (elevates itself)
Write-Host "`nRegistering COM server..." -ForegroundColor Yellow
$registerScript = Join-Path $PSScriptRoot "register-com.ps1"
& $registerScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { Write-Error "COM registration failed"; exit 1 }

# Step 3: Launch app (non-elevated)
$appExe = Join-Path $repoRoot "src\vCamService.App\bin\$Configuration\net10.0-windows10.0.22621.0\vCamService.App.exe"
if (-not (Test-Path $appExe)) {
    Write-Error "App not found at $appExe"
    exit 1
}

Write-Host "`nLaunching vCamService..." -ForegroundColor Yellow
Start-Process $appExe
Write-Host "  App launched" -ForegroundColor Green

Write-Host "`n=== Deploy Complete ===" -ForegroundColor Green
