# =============================================================================
# Z EARTH Desktop GIS - Windows Installation & Registration Script (ZE-059)
# Developer: Ahmad Ibrahim
# Product: Z EARTH Desktop GIS v1.0.0
# Target OS: Windows 10 / 11 (x64)
# =============================================================================

param(
    [string]$InstallPath = "$env:LOCALAPPDATA\Programs\ZEarth",
    [switch]$SkipShortcuts = $false,
    [switch]$Unattended = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop GIS - Installer v1.0.0" -ForegroundColor White
Write-Host " Developer: Ahmad Ibrahim" -ForegroundColor Gray
Write-Host " Destination: $InstallPath" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

$sourceDir = "$PSScriptRoot\..\dist\Z EARTH-Preview"
if (-not (Test-Path "$sourceDir\Z EARTH.exe")) {
    $sourceDir = "$PSScriptRoot\..\dist\staging"
}
if (-not (Test-Path "$sourceDir\Z EARTH.exe")) {
    $sourceDir = $PSScriptRoot
}

if (-not (Test-Path "$sourceDir\Z EARTH.exe")) {
    Write-Error "Could not locate source Z EARTH.exe to install. Build the project first."
    exit 1
}

# 1. Create Target Directory & Copy Files
Write-Host "`n[1/4] Copying application files..." -ForegroundColor Yellow
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

Copy-Item "$sourceDir\*" -Destination $InstallPath -Recurse -Force
Copy-Item "$PSScriptRoot\uninstall-zearth.ps1" -Destination $InstallPath -Force -ErrorAction SilentlyContinue

$installedExe = "$InstallPath\Z EARTH.exe"
$iconPath = "$InstallPath\Assets\zearth_icon.ico"
if (-not (Test-Path $iconPath)) { $iconPath = "$installedExe,0" }

# 2. Register Windows File Associations (.zearth, .kml, .kmz)
Write-Host "`n[2/4] Registering file associations (.zearth, .kml, .kmz)..." -ForegroundColor Yellow

function Register-FileExtension {
    param(
        [string]$Extension,
        [string]$ProgId,
        [string]$Description,
        [string]$IconPath,
        [string]$ExePath
    )

    $classesRoot = "HKCU:\Software\Classes"
    
    # Extension mapping
    New-Item -Path "$classesRoot\$Extension" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$Extension" -Name "(Default)" -Value $ProgId -Force

    # ProgId mapping
    New-Item -Path "$classesRoot\$ProgId" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$ProgId" -Name "(Default)" -Value $Description -Force

    # Icon
    New-Item -Path "$classesRoot\$ProgId\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$ProgId\DefaultIcon" -Name "(Default)" -Value $IconPath -Force

    # Shell Open Command
    New-Item -Path "$classesRoot\$ProgId\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$ProgId\shell\open\command" -Name "(Default)" -Value "`"$ExePath`" `"%1`"" -Force
}

Register-FileExtension -Extension ".zearth" -ProgId "ZEarth.Project" -Description "Z EARTH GIS Project" -IconPath $iconPath -ExePath $installedExe
Register-FileExtension -Extension ".kml" -ProgId "ZEarth.Kml" -Description "Keyhole Markup Language" -IconPath $iconPath -ExePath $installedExe
Register-FileExtension -Extension ".kmz" -ProgId "ZEarth.Kmz" -Description "Compressed Keyhole Markup Language" -IconPath $iconPath -ExePath $installedExe

# 3. Create Windows Shortcuts (Start Menu & Desktop)
if (-not $SkipShortcuts) {
    Write-Host "`n[3/4] Creating Start Menu & Desktop shortcuts..." -ForegroundColor Yellow
    $wsh = New-Object -ComObject WScript.Shell

    # Start Menu Shortcut
    $startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Z EARTH"
    if (-not (Test-Path $startMenuDir)) { New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null }
    $startShortcut = $wsh.CreateShortcut("$startMenuDir\Z EARTH Desktop GIS.lnk")
    $startShortcut.TargetPath = $installedExe
    $startShortcut.WorkingDirectory = $InstallPath
    $startShortcut.Description = "Z EARTH Desktop GIS - Open-source 3D Globe & Desktop GIS by Ahmad Ibrahim"
    $startShortcut.IconLocation = $iconPath
    $startShortcut.Save()

    # Desktop Shortcut
    $desktopDir = [Environment]::GetFolderPath("Desktop")
    $desktopShortcut = $wsh.CreateShortcut("$desktopDir\Z EARTH Desktop GIS.lnk")
    $desktopShortcut.TargetPath = $installedExe
    $desktopShortcut.WorkingDirectory = $InstallPath
    $desktopShortcut.Description = "Z EARTH Desktop GIS by Ahmad Ibrahim"
    $desktopShortcut.IconLocation = $iconPath
    $desktopShortcut.Save()
}

# 4. Register in Windows Add/Remove Programs (Clean Uninstaller support AC-032)
Write-Host "`n[4/4] Registering Windows uninstaller metadata..." -ForegroundColor Yellow
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ZEarth"
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Z EARTH Desktop GIS" -Force
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0" -Force
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "Ahmad Ibrahim" -Force
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value "$installedExe,0" -Force
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallPath -Force
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallPath\uninstall-zearth.ps1`"" -Force
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord -Force
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord -Force

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Z EARTH Desktop GIS installed successfully!" -ForegroundColor Green
Write-Host " Location: $installedExe" -ForegroundColor White
Write-Host " Developer: Ahmad Ibrahim" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Green
