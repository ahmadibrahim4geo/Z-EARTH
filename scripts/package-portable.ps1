# =============================================================================
# Z EARTH Desktop GIS - Portable Packaging Script (ZE-059, AC-032)
# Developer: Ahmad Ibrahim
# Target: dist\Z EARTH-v1.0.0-win-x64-portable.zip
# =============================================================================

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$previewDir = Join-Path $root "dist\Z EARTH-Preview"
$distDir = Join-Path $root "dist"
$zipPath = Join-Path $distDir "Z EARTH-v1.0.0-win-x64-portable.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop GIS - Portable Packaging" -ForegroundColor White
Write-Host " Developer: Ahmad Ibrahim" -ForegroundColor Gray
Write-Host " Output Archive: $zipPath" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# Ensure build is fresh
Write-Host "`n[1/3] Verifying preview binaries..." -ForegroundColor Yellow
if (-not (Test-Path "$previewDir\Z EARTH.exe")) {
    Write-Host "Running build-and-test.ps1 to prepare release binaries..." -ForegroundColor Gray
    & powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\build-and-test.ps1"
}

# Copy installer and uninstaller into staging package
Copy-Item "$PSScriptRoot\install-zearth.ps1" -Destination $previewDir -Force
Copy-Item "$PSScriptRoot\uninstall-zearth.ps1" -Destination $previewDir -Force

# Remove previous zip if exists
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Create zip archive
Write-Host "`n[2/3] Compressing portable distribution archive..." -ForegroundColor Yellow
Compress-Archive -Path "$previewDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

# Verify archive
Write-Host "`n[3/3] Validating portable archive..." -ForegroundColor Yellow
$zipSize = (Get-Item $zipPath).Length
$zipSizeMb = [Math]::Round($zipSize / 1MB, 2)

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Portable Package Generated!" -ForegroundColor Green
Write-Host " Archive: $zipPath" -ForegroundColor White
Write-Host " Size: $zipSizeMb MB ($zipSize bytes)" -ForegroundColor Cyan
Write-Host " Developer: Ahmad Ibrahim" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Green
