# =============================================================================
# Z EARTH Desktop GIS - Clean Windows Uninstallation Script (ZE-059, AC-032)
# Developer: Ahmad Ibrahim
# Product: Z EARTH Desktop GIS v1.0.0
# =============================================================================

param(
    [string]$InstallPath = "$env:LOCALAPPDATA\Programs\ZEarth",
    [switch]$Force = $false
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop GIS - Clean Uninstaller" -ForegroundColor White
Write-Host " Developer: Ahmad Ibrahim" -ForegroundColor Gray
Write-Host " Target Directory: $InstallPath" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Unregister File Associations (.zearth, .kml, .kmz)
Write-Host "`n[1/4] Unregistering file associations..." -ForegroundColor Yellow
$classesRoot = "HKCU:\Software\Classes"

@(".zearth", ".kml", ".kmz", "ZEarth.Project", "ZEarth.Kml", "ZEarth.Kmz") | ForEach-Object {
    $keyPath = "$classesRoot\$_"
    if (Test-Path $keyPath) {
        Remove-Item -Path $keyPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  -> Removed registry key: $_" -ForegroundColor Gray
    }
}

# 2. Remove Windows Shortcuts
Write-Host "`n[2/4] Removing Start Menu & Desktop shortcuts..." -ForegroundColor Yellow
$startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Z EARTH"
if (Test-Path $startMenuDir) {
    Remove-Item -Path $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Start Menu shortcut" -ForegroundColor Gray
}

$desktopShortcut = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "Z EARTH Desktop GIS.lnk")
if (Test-Path $desktopShortcut) {
    Remove-Item -Path $desktopShortcut -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Desktop shortcut" -ForegroundColor Gray
}

# 3. Remove Windows Add/Remove Programs Entry
Write-Host "`n[3/4] Removing uninstaller registry entry..." -ForegroundColor Yellow
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ZEarth"
if (Test-Path $uninstallKey) {
    Remove-Item -Path $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Add/Remove Programs entry" -ForegroundColor Gray
}

# 4. Remove Application Files
Write-Host "`n[4/4] Removing application files..." -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    # Check if currently running inside $InstallPath
    if ($PSScriptRoot -eq $InstallPath) {
        # Schedule removal of folder on reboot or spawn a detached cmd process
        Start-Process -FilePath "cmd.exe" -ArgumentList "/c ping 127.0.0.1 -n 2 > nul & rmdir /s /q `"$InstallPath`"" -WindowStyle Hidden
        Write-Host "  -> Directory removal scheduled asynchronously" -ForegroundColor Gray
    } else {
        Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  -> Removed application files: $InstallPath" -ForegroundColor Gray
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Z EARTH Desktop GIS uninstalled cleanly." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
