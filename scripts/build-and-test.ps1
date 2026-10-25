# Z EARTH Desktop GIS — fail-closed build, tests, and single-preview publication.
# Developer: Ahmed Ibrahim
param([switch]$SkipTests = $false)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$solution = Join-Path $root "ZEarth.sln"

function Invoke-DotNet([string]$Label, [string[]]$Arguments) {
    Write-Host "[Z EARTH] $Label" -ForegroundColor Cyan
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed (dotnet exit code $LASTEXITCODE); Preview was NOT published."
    }
}

Invoke-DotNet "Restore dependencies" @("restore", $solution)
Invoke-DotNet "Compile Release" @("build", $solution, "-c", "Release", "--no-restore")

if ($SkipTests) {
    Write-Warning "Tests were skipped: this build is NOT a verified release. Existing Preview was left untouched."
    return
}

Invoke-DotNet "Run .NET automated tests" @("test", (Join-Path $root "tests/ZEarth.Tests/ZEarth.Tests.csproj"), "-c", "Release", "--no-build")
$dist = Join-Path $root "dist"
$staging = Join-Path $dist ".pending-Z-EARTH-build"
$preview = Join-Path $dist "Z EARTH-Preview"
$previous = Join-Path $dist ".previous-preview-recovery"

New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path $previous) {
    throw "Recovery backup exists at $previous; restore or remove it explicitly before republishing."
}
if (Test-Path $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
Invoke-DotNet "Publish to staging only" @("publish", (Join-Path $root "src/ZEarth.App/ZEarth.App.csproj"), "-c", "Release", "-o", $staging, "--no-build", "--no-self-contained")

$assetsSource = Join-Path $root "src/ZEarth.Globe/Assets"
$assetsTarget = Join-Path $staging "Assets"
if (-not (Test-Path $assetsSource)) { throw "Cesium source assets missing; Preview not replaced." }
New-Item -ItemType Directory -Force -Path $assetsTarget | Out-Null
Copy-Item (Join-Path $assetsSource "*") -Destination $assetsTarget -Recurse -Force

foreach ($required in @(
    "Z EARTH.exe", "Assets/cesium-engine.js", "Assets/rectangle-geometry.js", "Assets/cesium/Cesium.js", "Assets/index.html"
)) {
    if (-not (Test-Path (Join-Path $staging $required))) {
        throw "Staging incomplete: $required is missing; Preview not replaced."
    }
}

$backedUp = $false
try {
    if (Test-Path $preview) {
        Move-Item -LiteralPath $preview -Destination $previous -ErrorAction Stop
        $backedUp = $true
    }
    Move-Item -LiteralPath $staging -Destination $preview -ErrorAction Stop
} catch {
    if ($backedUp -and -not (Test-Path $preview) -and (Test-Path $previous)) {
        Move-Item -LiteralPath $previous -Destination $preview -ErrorAction Stop
    }
    throw
}

if (Test-Path $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
Write-Host "[SUCCESS] Exactly one current Preview folder: $preview" -ForegroundColor Green
Write-Host "NOTE: Source/CI tests do NOT replace a real Windows WebView2/GPU functional acceptance run."
