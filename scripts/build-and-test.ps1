# Z EARTH Desktop GIS - Build, Test & Preview Script
# Developer: Ahmad Ibrahim

param(
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = "C:\Program Files\dotnet\dotnet.exe"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop GIS - Automated Build & Test" -ForegroundColor White
Write-Host " Developer: Ahmad Ibrahim" -ForegroundColor Gray
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Restore
Write-Host "`n[1/4] Restoring NuGet dependencies..." -ForegroundColor Yellow
& $dotnet restore "$root\ZEarth.sln"

# 2. Build Solution
Write-Host "`n[2/4] Compiling ZEarth solution (Release)..." -ForegroundColor Yellow
& $dotnet build "$root\ZEarth.sln" -c Release --no-restore

# 3. Run Automated Tests
if (-not $SkipTests) {
    Write-Host "`n[3/4] Running automated tests..." -ForegroundColor Yellow
    & $dotnet test "$root\ZEarth.sln" -c Release --no-build
}

# 4. Publish Preview Build (Single Preview Build Policy)
Write-Host "`n[4/4] Publishing verified preview build..." -ForegroundColor Yellow
$staging = Join-Path $root "dist\staging"
$preview = Join-Path $root "dist\Z EARTH-Preview"

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
& $dotnet publish "$root\src\ZEarth.App\ZEarth.App.csproj" -c Release -o $staging --no-build --no-self-contained

# Ensure all Globe assets (HTML, CSS, JS, CesiumJS runtime) are present in staging
$globeAssetsSrc = Join-Path $root "src\ZEarth.Globe\Assets"
$globeAssetsDst = Join-Path $staging "Assets"
if (Test-Path $globeAssetsSrc) {
    if (-not (Test-Path $globeAssetsDst)) { New-Item -ItemType Directory -Path $globeAssetsDst -Force | Out-Null }
    Copy-Item (Join-Path $globeAssetsSrc "*") -Destination $globeAssetsDst -Recurse -Force
}

$stagingExe = Join-Path $staging "Z EARTH.exe"
if (Test-Path $stagingExe) {
    if (-not (Test-Path $preview)) { New-Item -ItemType Directory -Path $preview -Force }
    Copy-Item (Join-Path $staging "*") -Destination $preview -Recurse -Force
    Remove-Item $staging -Recurse -Force
    Write-Host "[SUCCESS] Verified Preview published to: $preview\Z EARTH.exe" -ForegroundColor Green
} else {
    Write-Error "Build verification failed: Z EARTH.exe was not produced."
}
