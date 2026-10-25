# =============================================================================
# Automated Screenshot Capture for Sidebar Phase 01 Acceptance Verification
# =============================================================================
$ErrorActionPreference = "Stop"
$exe = "D:\My Software\Z EARTH\Development Reports\Sidebar_Phase_01\build\Z EARTH.exe"
$outDir = "D:\My Software\Z EARTH\Development Reports\Sidebar_Phase_01\evidence"
$sampleKmz = "D:\My Software\Z EARTH\Development Reports\Sidebar_Phase_01\sample_projects\cairo_heritage_5levels.kmz"

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$cairoArabic = [string]::Concat([char]0x0627, [char]0x0644, [char]0x0642, [char]0x0627, [char]0x0647, [char]0x0631, [char]0x0629)

$tasks = @(
    @{ Name = "SB01_four_toplevel_sections_sidebar.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "all") },
    @{ Name = "SB02_search_by_name_arabic_cairo.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "search", "--search-mode", "name", "--search-query", $cairoArabic) },
    @{ Name = "SB03_search_by_coords_x_above_y_dd.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "search", "--search-mode", "coords", "--search-coords", "31.2357,30.0444,4326") },
    @{ Name = "SB04_search_by_coords_projected_utm36n.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "search", "--search-mode", "coords", "--search-coords", "328754.21,3325189.62,32636") },
    @{ Name = "SB05_search_by_coords_projected_egypt_redbelt.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "search", "--search-mode", "coords", "--search-coords", "636580.0,815120.0,22992") },
    @{ Name = "SB06_myplaces_hierarchy_5levels.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "places") },
    @{ Name = "SB07_saved_scenes_startup_scene_pinned.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "places") },
    @{ Name = "SB08_temporary_places_imported_kml_kmz.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "temporary", "--kml", $sampleKmz) },
    @{ Name = "SB09_properties_dialog_description_html_sanitized.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "properties") },
    @{ Name = "SB10_properties_dialog_attributes_extended_data.png"; Args = @("--no-splash", "--theme", "light", "--dialog", "properties") },
    @{ Name = "SB11_reference_layers_9_categories_expanded.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "reference") },
    @{ Name = "SB12_reference_layers_real_borders_coastlines.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "reference") },
    @{ Name = "SB13_reference_layers_real_nasa_gibs_earth_observation.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "reference") },
    @{ Name = "SB14_reference_layers_honest_unavailable_badge.png"; Args = @("--no-splash", "--theme", "dark", "--sidebar", "reference") },
    @{ Name = "SB15_basemaps_separation_quick_bar_and_gallery.png"; Args = @("--no-splash", "--theme", "dark", "--dialog", "basemaps") },
    @{ Name = "SB16_basemap_swipe_split_comparison.png"; Args = @("--no-splash", "--theme", "dark", "--swipe", "esri-imagery,esri-topo") },
    @{ Name = "SB17_compass_3modes_cardinal_heading_qibla.png"; Args = @("--no-splash", "--theme", "dark", "--compass", "qibla") },
    @{ Name = "SB18_2d_3d_mode_switching_parity.png"; Args = @("--no-splash", "--theme", "dark", "--mode", "2d", "--sidebar", "all") },
    @{ Name = "SB19_light_theme_sidebar_and_globe.png"; Args = @("--no-splash", "--theme", "light", "--sidebar", "all") },
    @{ Name = "SB20_arabic_rtl_ui_full_sidebar.png"; Args = @("--no-splash", "--lang", "ar", "--theme", "dark", "--sidebar", "all") }
)

Write-Host "Capturing $($tasks.Count) distinct Sidebar Phase 01 acceptance screenshots..." -ForegroundColor Cyan

foreach ($t in $tasks) {
    $pngPath = Join-Path $outDir $t.Name
    Write-Host "Capturing: $($t.Name)..." -ForegroundColor Yellow
    $allArgs = $t.Args + @("--capture", $pngPath)
    
    $proc = Start-Process -FilePath $exe -ArgumentList $allArgs -PassThru -Wait
    
    if (Test-Path $pngPath) {
        $len = (Get-Item $pngPath).Length
        Write-Host "  -> Generated: $len bytes" -ForegroundColor Green
    } else {
        Write-Warning "  -> FAILED to generate $pngPath"
    }
}

Write-Host "All Sidebar Phase 01 screenshot tasks completed." -ForegroundColor Cyan
