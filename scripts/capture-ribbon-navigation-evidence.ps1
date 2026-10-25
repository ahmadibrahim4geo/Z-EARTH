# =============================================================================
# Automated Evidence Capture for Ribbon, Navigation & Explorer Addendum
# =============================================================================
$ErrorActionPreference = "Stop"

$solutionRoot = "D:\My Software\Z EARTH"
$exe = Join-Path $solutionRoot "src\ZEarth.App\bin\Debug\net10.0-windows\Z EARTH.exe"
$reportDir = Join-Path $solutionRoot "Development Reports\Ribbon_Navigation_Integration"
$outDir = Join-Path $reportDir "Evidence"
$buildDir = Join-Path $reportDir "build"
$sampleKmz = Join-Path $solutionRoot "Development Reports\Sidebar_Phase_01\sample_projects\cairo_heritage_5levels.kmz"

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

if (-not (Test-Path $buildDir)) {
    New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
}

# Copy build binaries to buildDir for runnable build delivery
Write-Host "Staging runnable build to: $buildDir..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $solutionRoot "src\ZEarth.App\bin\Debug\net10.0-windows\*") -Destination $buildDir -Recurse -Force

$tasks = @(
    @{ Name = "RN-01_saved_scenes_startup_ground_focus.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "places") },
    @{ Name = "RN-02_two_row_status_bar_live_readouts.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "search") },
    @{ Name = "RN-03_geographic_graticules_equator_tropics_prime_meridian.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "reference") },
    @{ Name = "RN-04_compact_provider_credits_and_lightbox.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "esri-imagery") },
    @{ Name = "RN-05_explorer_command_parity_and_hierarchy.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "temporary", "--kml", $sampleKmz) },
    @{ Name = "RN-06_compass_3modes_cardinal_heading_qibla.png"; Args = @("--no-splash", "--theme", "dark", "--compass", "qibla") },
    @{ Name = "RN-07_options_dialog_graticules_credits_zoom.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "options") },
    @{ Name = "RN-08_dark_theme_contrast_explorer_and_status.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "all") },
    @{ Name = "RN-09_light_theme_contrast_explorer_and_status.png"; Args = @("--no-splash", "--theme", "light", "--sidebar", "all") },
    @{ Name = "RN-10_arabic_rtl_ribbon_and_explorer.png"; Args = @("--no-splash", "--lang", "ar", "--theme", "dark", "--sidebar", "all") },
    @{ Name = "RN-11_projected_crs_utm36n_easting_northing.png"; Args = @("--no-splash", "--theme", "dark", "--crs", "32636") },
    @{ Name = "RN-12_2d_mode_graticules_and_readouts.png"; Args = @("--no-splash", "--mode", "2d", "--theme", "dark", "--sidebar", "all") },
    @{ Name = "RN-13_basemap_swipe_split_comparison.png"; Args = @("--no-splash", "--theme", "dark", "--swipe", "esri-imagery,esri-topo") },
    @{ Name = "RN-14_geodesic_measurement_circle_distance_area.png"; Args = @("--no-splash", "--theme", "dark", "--measure", "distance") }
)

Write-Host "Capturing $($tasks.Count) acceptance screenshots for Ribbon & Navigation integration..." -ForegroundColor Cyan

$successCount = 0
$failCount = 0

foreach ($t in $tasks) {
    $pngPath = Join-Path $outDir $t.Name
    Write-Host "Capturing: $($t.Name)..." -ForegroundColor Yellow
    $allArgs = $t.Args + @("--capture", "`"$pngPath`"")
    
    $proc = Start-Process -FilePath $exe -ArgumentList $allArgs -PassThru -Wait
    
    if (Test-Path $pngPath) {
        $len = (Get-Item $pngPath).Length
        Write-Host "  -> Generated: $len bytes" -ForegroundColor Green
        $successCount++
    } else {
        Write-Warning "  -> FAILED to generate $pngPath"
        $failCount++
    }
}

Write-Host "=== Capture Summary ===" -ForegroundColor Cyan
Write-Host "Successful captures: $successCount" -ForegroundColor Green
Write-Host "Failed captures:     $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
