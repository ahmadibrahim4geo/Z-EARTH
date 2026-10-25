# =============================================================================
# Automated Screenshot Capture for Addendum 07 & CRS Acceptance
# =============================================================================
$ErrorActionPreference = "Stop"
$exe = "D:\My Software\Z EARTH\Development Reports\Z_Earth_RC2_Closure_Fixes\build\Z EARTH.exe"
$outDir = "D:\My Software\Z EARTH\Development Reports\Z_Earth_RC2_Closure_Fixes\screenshots"

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# Remove temporary test file if present
$testFile = Join-Path $outDir "test_capture.png"
if (Test-Path $testFile) { Remove-Item $testFile -Force }

$tasks = @(
    @{ Name = "A07-01_dark_add_imagery_dialog.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "addimagery") },
    @{ Name = "A07-02_light_add_imagery_dialog.png"; Args = @("--no-splash", "--theme", "light", "--dialog", "addimagery") },
    @{ Name = "A07-03_light_basemap_gallery.png"; Args = @("--no-splash", "--theme", "light", "--dialog", "basemaps") },
    @{ Name = "A07-04_dark_basemap_gallery.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "basemaps") },
    @{ Name = "A07-05_layers_sidebar_basemap_and_overlays.png"; Args = @("--no-splash", "--theme", "dark", "--tab", "layers") },
    @{ Name = "A07-06_layers_sidebar_light_theme.png"; Args = @("--no-splash", "--theme", "light", "--tab", "layers") },
    @{ Name = "A07-07_offline_vector_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "offline-vector") },
    @{ Name = "A07-08_esri_imagery_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "esri-imagery") },
    @{ Name = "A07-09_esri_hybrid_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "esri-hybrid") },
    @{ Name = "A07-10_carto_dark_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "carto-dark") },
    @{ Name = "A07-11_osm_streets_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "osm-standard") },
    @{ Name = "A07-12_opentopo_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "opentopo") },
    @{ Name = "A07-13_esri_topo_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "esri-topo") },
    @{ Name = "A07-14_swipe_split_comparison.png"; Args = @("--no-splash", "--theme", "dark", "--swipe", "esri-imagery,esri-topo") },
    @{ Name = "A07-15_swipe_exit_single_basemap.png"; Args = @("--no-splash", "--theme", "dark", "--basemap", "esri-imagery") },
    @{ Name = "A07-16_compass_cardinal_mode.png"; Args = @("--no-splash", "--theme", "dark", "--compass", "cardinal") },
    @{ Name = "A07-17_compass_heading_mode.png"; Args = @("--no-splash", "--theme", "dark", "--compass", "heading") },
    @{ Name = "A07-18_compass_qibla_cairo.png"; Args = @("--no-splash", "--theme", "dark", "--compass", "qibla") },
    @{ Name = "A07-19_compass_qibla_riyadh.png"; Args = @("--no-splash", "--theme", "light", "--compass", "qibla") },
    @{ Name = "A07-20_qibla_2d_wgs84.png"; Args = @("--no-splash", "--mode", "2d", "--compass", "qibla", "--crs", "4326") },
    @{ Name = "A07-21_arabic_light_ui.png"; Args = @("--no-splash", "--lang", "ar", "--theme", "light", "--dialog", "basemaps") },
    @{ Name = "A07-21_arabic_dark_ui.png"; Args = @("--no-splash", "--lang", "ar", "--theme", "dark", "--dialog", "basemaps") },
    @{ Name = "A07-22_options_persisted_settings.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "options") },
    @{ Name = "CRS-01_new_blank_2d_wgs84.png"; Args = @("--no-splash", "--mode", "2d", "--crs", "4326", "--theme", "dark") },
    @{ Name = "CRS-02_crs_browser_dialog.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "crs") },
    @{ Name = "CRS-04_projected_egypt_red_belt.png"; Args = @("--no-splash", "--mode", "2d", "--crs", "22992", "--theme", "dark") },
    @{ Name = "CRS-05_projected_utm_36n.png"; Args = @("--no-splash", "--mode", "2d", "--crs", "32636", "--theme", "dark") },
    @{ Name = "CRS-24_crs_arabic_light.png"; Args = @("--no-splash", "--lang", "ar", "--theme", "light", "--dialog", "crs") }
)

Write-Host "Capturing $($tasks.Count) distinct acceptance screenshots..." -ForegroundColor Cyan

foreach ($t in $tasks) {
    $pngPath = Join-Path $outDir $t.Name
    Write-Host "Capturing: $($t.Name)..." -ForegroundColor Yellow
    $allArgs = $t.Args + @("--capture", "`"$pngPath`"")
    
    $proc = Start-Process -FilePath $exe -ArgumentList $allArgs -PassThru -Wait
    
    if (Test-Path $pngPath) {
        $len = (Get-Item $pngPath).Length
        Write-Host "  -> Generated: $len bytes" -ForegroundColor Green
    } else {
        Write-Warning "  -> FAILED to generate $pngPath"
    }
}

Write-Host "All capture tasks completed." -ForegroundColor Cyan
