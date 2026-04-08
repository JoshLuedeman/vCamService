#!/usr/bin/env pwsh
# Build script for vCamService MSI installer
# Run from the repo root on Windows

param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "=== vCamService Installer Build ===" -ForegroundColor Cyan
Write-Host "Version: $Version"

# Step 1: Publish the app
Write-Host "`nPublishing app..." -ForegroundColor Yellow
$publishDir = Join-Path $PSScriptRoot "publish"
dotnet publish src/vCamService.App/vCamService.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit 1 }

# Step 2: Build the installer
Write-Host "`nBuilding MSI..." -ForegroundColor Yellow
dotnet build src/vCamService.Installer/vCamService.Installer.wixproj `
    -c Release `
    -p:ProductVersion=$Version `
    -p:PublishDir="$publishDir\"

if ($LASTEXITCODE -ne 0) { Write-Error "Installer build failed"; exit 1 }

$msiPath = "src/vCamService.Installer/bin/Release/vCamService-Setup.msi"
Write-Host "`n=== Build complete ===" -ForegroundColor Green
Write-Host "MSI: $msiPath"
