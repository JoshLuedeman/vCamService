#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds and registers the vCamService COM server for Frame Server.
    Must be run as Administrator.
.DESCRIPTION
    1. Builds vCamService.VCam project
    2. Stops the Windows Camera Frame Server service
    3. Copies COM host files to C:\ProgramData\vCamService\com\
    4. Writes HKLM CLSID registry keys
    5. Restarts Frame Server
#>
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

# Self-elevate if not admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting elevation..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Configuration $Configuration"
    exit $LASTEXITCODE
}

Write-Host "=== vCamService COM Registration ===" -ForegroundColor Cyan

$repoRoot = Split-Path $PSScriptRoot -Parent
$vcamProj = Join-Path $repoRoot "src\vCamService.VCam\vCamService.VCam.csproj"

# Step 1: Build
Write-Host "`nBuilding VCam project ($Configuration)..." -ForegroundColor Yellow
dotnet build $vcamProj -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

# Locate build output
# VCam project has <Platforms>x64</Platforms>, so output goes to bin\x64\<Config>
$buildDir = Join-Path $repoRoot "src\vCamService.VCam\bin\x64\$Configuration\net10.0-windows10.0.22621.0"
if (-not (Test-Path $buildDir)) {
    # Fallback to non-platform path for AnyCPU builds
    $buildDir = Join-Path $repoRoot "src\vCamService.VCam\bin\$Configuration\net10.0-windows10.0.22621.0"
}
if (-not (Test-Path $buildDir)) {
    Write-Error "Build output not found"
    exit 1
}
Write-Host "  Build output: $buildDir"

# Step 2: Stop Frame Server
Write-Host "`nStopping Frame Server..." -ForegroundColor Yellow
$svc = Get-Service -Name "FrameServer" -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") {
    Stop-Service "FrameServer" -Force
    Start-Sleep -Seconds 1
    Write-Host "  Frame Server stopped"
} else {
    Write-Host "  Frame Server not running"
}

# Step 3: Copy COM files
$comDir = "C:\ProgramData\vCamService\com"
$baseDir = "C:\ProgramData\vCamService"
Write-Host "`nDeploying COM files to $comDir..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $comDir -Force | Out-Null

# Grant Users modify access so the non-elevated app can write stream-config.json
$acl = Get-Acl $baseDir
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "Users", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl $baseDir $acl
Write-Host "  Granted Users modify access to $baseDir"

$comFiles = @(
    "vCamService.VCam.comhost.dll",
    "vCamService.VCam.dll",
    "vCamService.VCam.runtimeconfig.json",
    "vCamService.VCam.deps.json",
    "vCamService.Core.dll",
    "DirectNCore.dll",
    "Microsoft.Windows.SDK.NET.dll",
    "WinRT.Runtime.dll"
)

foreach ($file in $comFiles) {
    $src = Join-Path $buildDir $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $comDir $file) -Force
        Write-Host "  Copied $file"
    } else {
        Write-Warning "  Missing: $file"
    }
}

# Step 4: Register COM class
Write-Host "`nRegistering COM class..." -ForegroundColor Yellow
$clsid = "{B5823E0D-4D72-4B3F-A9B8-C12F5E7D9A3E}"
$comhostPath = Join-Path $comDir "vCamService.VCam.comhost.dll"

$clsidPath = "HKLM:\Software\Classes\CLSID\$clsid"
New-Item -Path $clsidPath -Force | Out-Null
Set-ItemProperty -Path $clsidPath -Name "(Default)" -Value "vCamService Virtual Camera Source"

$inprocPath = "$clsidPath\InProcServer32"
New-Item -Path $inprocPath -Force | Out-Null
Set-ItemProperty -Path $inprocPath -Name "(Default)" -Value $comhostPath
Set-ItemProperty -Path $inprocPath -Name "ThreadingModel" -Value "Both"
Write-Host "  CLSID $clsid registered"

# Step 5: Restart Frame Server
Write-Host "`nStarting Frame Server..." -ForegroundColor Yellow
Start-Service "FrameServer"
Start-Sleep -Seconds 1
Write-Host "  Frame Server started"

Write-Host "`n=== COM Registration Complete ===" -ForegroundColor Green
