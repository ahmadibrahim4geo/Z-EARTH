# Z EARTH Windows x64 self-contained PORTABLE PREVIEW (not a production release).
# Default local use checks build/tests. CI may use -TestsAlreadyPassed after its full test job.
[CmdletBinding()]
param([switch]$TestsAlreadyPassed)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src/ZEarth.App/ZEarth.App.csproj'
[xml]$projxml = Get-Content -LiteralPath $project -Raw
$version = [string]$projxml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected version: $version" }
$dist = Join-Path $root 'dist'
$staging = Join-Path $dist '.pending-portable-publish'
$preview = Join-Path $dist 'Z EARTH-Preview'
$previous = Join-Path $dist '.previous-preview-recovery'
$zip = Join-Path $dist ("Z-EARTH-v{0}-win-x64-portable-preview.zip" -f $version)
New-Item -ItemType Directory -Path $dist -Force | Out-Null

if (-not $TestsAlreadyPassed) {
    & (Join-Path $PSScriptRoot 'build-and-test.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Build/test gate failed: $LASTEXITCODE" }
}
if (Test-Path -LiteralPath $previous) { throw "Recovery backup exists: $previous" }
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

# RID publish must restore RID runtime dependencies even if a non-RID build passed.
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $staging
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed: $LASTEXITCODE" }

# Copy all Cesium/WebView assets, regardless of transitive MSBuild Content behavior.
$assetsSource = Join-Path $root 'src/ZEarth.Globe/Assets'
$assetsTarget = Join-Path $staging 'Assets'
if (-not (Test-Path -LiteralPath $assetsSource)) { throw 'Missing Cesium source assets' }
New-Item -ItemType Directory -Path $assetsTarget -Force | Out-Null
Copy-Item -Path (Join-Path $assetsSource '*') -Destination $assetsTarget -Recurse -Force
$required = @('Z EARTH.exe', 'Z EARTH.dll', 'Z EARTH.runtimeconfig.json',
    'Assets/index.html', 'Assets/cesium-engine.js', 'Assets/historical-timeline.js',
    'Assets/rectangle-geometry.js', 'Assets/cesium/Cesium.js')
foreach ($item in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $staging $item) -PathType Leaf)) {
        throw "Incomplete publish, missing $item"
    }
}
$exe = Join-Path $staging 'Z EARTH.exe'
$stream = [System.IO.File]::OpenRead($exe)
try {
    if ($stream.Length -lt 10240 -or $stream.ReadByte() -ne 77 -or $stream.ReadByte() -ne 90) {
        throw 'Invalid Windows PE executable'
    }
} finally { $stream.Dispose() }

foreach ($doc in @('THIRD_PARTY_LICENSES.md', 'LEGAL_AND_PRIVACY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $root $doc) -Destination $staging -Force
}
$sourceSha = [string]$env:GITHUB_SHA
if ([string]::IsNullOrWhiteSpace($sourceSha)) {
    $sourceSha = (& git -C $root rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) { $sourceSha = 'unknown-local-commit' }
}
$notes = @"
Z EARTH v$version | Windows x64 PORTABLE PREVIEW | Source commit $sourceSha

1. Extract the full downloaded ZIP to one directory, then launch Z EARTH.exe.
2. KEEP the Assets folder, DLLs, .json files and all other dependencies beside EXE.
3. .NET Windows Desktop runtime is included. Windows 10/11 x64, WebView2 Runtime
   and a WebGL-capable GPU are still required. Online map/terrain services vary.
4. UNSIGNED DEVELOPMENT PREVIEW. Verify GitHub source and SHA-256; do not ignore
   your computer's security warnings or bypass organizational security rules.
5. Wayback imagery dates reflect Esri basemap RELEASES, not satellite capture dates.
6. Native live WebView2/GPU and third-party tile service acceptance remain pending.
7. Extracting this archive changes no Windows file associations or registry entries.

Source: https://github.com/ahmadibrahim4geo/Z-EARTH
Issues: https://github.com/ahmadibrahim4geo/Z-EARTH/issues
"@
Set-Content -LiteralPath (Join-Path $staging 'READ-ME-FIRST.txt') -Value $notes -Encoding utf8
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $staging 'Z-EARTH.exe.sha256') -Value "$hash  Z EARTH.exe" -Encoding ascii

# One canonical Preview directory, with recovery on failed replacement.
$backedUp = $false
try {
    if (Test-Path -LiteralPath $preview) {
        Move-Item -LiteralPath $preview -Destination $previous -ErrorAction Stop
        $backedUp = $true
    }
    Move-Item -LiteralPath $staging -Destination $preview -ErrorAction Stop
} catch {
    if ($backedUp -and -not (Test-Path -LiteralPath $preview) -and (Test-Path -LiteralPath $previous)) {
        Move-Item -LiteralPath $previous -Destination $preview -ErrorAction Stop
    }
    throw
}
if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $preview '*') -DestinationPath $zip -CompressionLevel Optimal
if (-not (Test-Path -LiteralPath $zip) -or (Get-Item -LiteralPath $zip).Length -lt 10240) {
    throw "Portable archive not generated: $zip"
}
Write-Host "[SUCCESS] Portable ZIP: $zip"
Write-Host "[SUCCESS] Z EARTH.exe SHA256: $hash"
Write-Warning 'Package validation is NOT real WebView2/GPU runtime acceptance.'
