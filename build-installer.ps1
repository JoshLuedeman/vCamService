#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build vCamService MSI installer.
    Publishes the app (framework-dependent), then builds the WiX MSI.
.PARAMETER Version
    Version number for the installer (e.g. 1.0.0).
.PARAMETER FfmpegDir
    Path to directory containing ffmpeg.exe and ffprobe.exe.
    Defaults to the directory of ffmpeg.exe found on PATH.
#>
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$FfmpegDir = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== vCamService Installer Build ===" -ForegroundColor Cyan
Write-Host "Version: $Version"

# Locate ffmpeg
if (-not $FfmpegDir) {
    $ffmpegExe = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($ffmpegExe) {
        $FfmpegDir = Split-Path $ffmpegExe.Source -Parent
    } else {
        Write-Error "ffmpeg not found on PATH. Provide -FfmpegDir parameter."
        exit 1
    }
}
Write-Host "ffmpeg: $FfmpegDir"

if (-not (Test-Path (Join-Path $FfmpegDir "ffmpeg.exe"))) {
    Write-Error "ffmpeg.exe not found in $FfmpegDir"
    exit 1
}

# Step 1: Publish the app (framework-dependent)
Write-Host "`nPublishing app (framework-dependent)..." -ForegroundColor Yellow
$publishDir = Join-Path $PSScriptRoot "publish"
dotnet publish src/vCamService.App/vCamService.App.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit 1 }

# Copy COM host runtimeconfig (needed for comhost.dll to find the runtime)
$appRuntimeConfig = Join-Path $publishDir "vCamService.App.runtimeconfig.json"
$vcamRuntimeConfig = Join-Path $publishDir "vCamService.VCam.runtimeconfig.json"
if ((Test-Path $appRuntimeConfig) -and -not (Test-Path $vcamRuntimeConfig)) {
    Copy-Item $appRuntimeConfig $vcamRuntimeConfig
    Write-Host "  Copied runtimeconfig for COM host"
}

Write-Host "  Published to $publishDir" -ForegroundColor Green

# Step 2: Build the MSI
Write-Host "`nBuilding MSI..." -ForegroundColor Yellow
dotnet build src/vCamService.Installer/vCamService.Installer.wixproj `
    -c $Configuration `
    -p:PublishDir="$publishDir\" `
    -p:FfmpegPath="$FfmpegDir\" `
    -p:ProductVersion=$Version

if ($LASTEXITCODE -ne 0) { Write-Error "Installer build failed"; exit 1 }

$msiPath = Get-ChildItem "src/vCamService.Installer/bin/$Configuration" -Filter "*.msi" -Recurse | Select-Object -First 1
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
if ($msiPath) {
    Write-Host "MSI: $($msiPath.FullName)"
} else {
    Write-Host "MSI output in: src/vCamService.Installer/bin/$Configuration/"
}
