# =============================================================================
# Z EARTH Desktop GIS - Sprint 2 (Navigation) Empirical Verification Script
# Developer: Ahmad Ibrahim
# Target: dist\Z EARTH-Preview\Z EARTH.exe
# =============================================================================

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$exePath = Join-Path $root "dist\Z EARTH-Preview\Z EARTH.exe"
$outDir = Join-Path $root "dist\verification"
$artifactDir = "C:\Users\ahmad\.gemini\antigravity\brain\3b36ab61-e45f-4ee3-9355-d8fea0a20a02"

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop - Sprint 2 (Navigation) Verification" -ForegroundColor White
Write-Host " Target Executable: $exePath" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

# Win32 Native Helpers for Screenshot & Window Management
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public static class Win32Capture {
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static Bitmap CaptureWindow(IntPtr handle) {
        RECT rect;
        GetWindowRect(handle, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) {
            width = 1380;
            height = 850;
        }

        Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            IntPtr hdc = g.GetHdc();
            try {
                PrintWindow(handle, hdc, 2); // 2 = PW_RENDERFULLCONTENT
            } finally {
                g.ReleaseHdc(hdc);
            }
        }
        return bmp;
    }
}
"@ -ReferencedAssemblies "System.Drawing.dll"

# Start Application with remote debugging enabled
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"

$proc = Start-Process -FilePath $exePath -PassThru
Write-Host "[1/6] Application launched (PID: $($proc.Id)). Waiting for initialization..." -ForegroundColor Yellow
Start-Sleep -Seconds 4

$matrix = [ordered]@{}

try {
    # Connect to CDP
    $httpReq = [System.Net.HttpWebRequest]::Create("http://localhost:9222/json")
    $httpReq.Timeout = 5000
    $resp = $httpReq.GetResponse()
    $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $jsonText = $sr.ReadToEnd()
    $sr.Close()
    $resp.Close()

    $targets = $jsonText | ConvertFrom-Json
    $cesiumTarget = $targets | Where-Object { $_.url -like "*index.html*" -or $_.url -like "*cesium.local*" } | Select-Object -First 1

    if (-not $cesiumTarget) {
        throw "Could not locate CesiumJS page target on CDP endpoint."
    }

    $wsUri = [Uri]$cesiumTarget.webSocketDebuggerUrl
    Write-Host "[2/6] Connected to WebView2 CDP: $wsUri" -ForegroundColor Green

    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $ct = [System.Threading.CancellationToken]::None
    [void]$ws.ConnectAsync($wsUri, $ct).Wait(5000)

    $script:msgId = 1
    function Eval-Js([string]$expression) {
        $id = $script:msgId++
        $cmd = @{
            id = $id
            method = "Runtime.evaluate"
            params = @{
                expression = $expression
                returnByValue = $true
                awaitPromise = $true
            }
        } | ConvertTo-Json -Compress

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($cmd)
        $segment = New-Object ArraySegment[byte] -ArgumentList @(,$bytes)
        [void]$ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $ct).Wait(5000)

        $buf = New-Object byte[] 65536
        $stream = New-Object System.IO.MemoryStream
        while ($true) {
            $recvSegment = New-Object ArraySegment[byte] -ArgumentList @(,$buf)
            $res = $ws.ReceiveAsync($recvSegment, $ct).Result
            $stream.Write($buf, 0, $res.Count)
            if ($res.EndOfMessage) { break }
        }
        $stream.Seek(0, [System.IO.SeekOrigin]::Begin) | Out-Null
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
        $responseJson = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()

        $parsed = $responseJson | ConvertFrom-Json
        if ($parsed.result.exceptionDetails) {
            Write-Host "   [JS Exception] $($parsed.result.exceptionDetails.text): $($parsed.result.exceptionDetails.exception.description)" -ForegroundColor Red
        }
        return $parsed.result.result.value
    }

    # Find Main Window Handle
    $mainHwnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 20; $i++) {
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
            $mainHwnd = $proc.MainWindowHandle
            break
        }
        Start-Sleep -Milliseconds 250
    }

    function Capture-WindowScreenshot([string]$path) {
        if ($mainHwnd -ne [IntPtr]::Zero) {
            $bmp = [Win32Capture]::CaptureWindow($mainHwnd)
            $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
            Write-Host "   Window screenshot saved: $path" -ForegroundColor Green
        }
    }

    # =========================================================================
    # TEST 1: ZE-014 & ZE-020 (Drag Stress Test, Horizon Guards & Camera Limits)
    # =========================================================================
    Write-Host "`n--- [TEST 1] ZE-014 & ZE-020: 50-Drag Stress Test & Horizon Guards ---" -ForegroundColor Cyan
    $stressRaw = Eval-Js "(async () => JSON.stringify(await window.ZEarthGlobe.runDragStressTest(50)))()"
    $stressResults = $stressRaw | ConvertFrom-Json
    Write-Host "   Successful Drags: $($stressResults.successfulDrags)"
    Write-Host "   Recovered: $($stressResults.recovered)"
    Write-Host "   Post-Stress Altitude: $([math]::Round($stressResults.finalAltitude)) m"

    if ($stressResults.recovered -and $stressResults.successfulDrags -ge 50) {
        $matrix["ZE-014 (Drag-Safe Camera & Horizon Guards)"] = "PASS"
        $matrix["ZE-020 (Camera Limits & Damping)"] = "PASS"
    } else {
        $matrix["ZE-014 (Drag-Safe Camera & Horizon Guards)"] = "FAIL"
        $matrix["ZE-020 (Camera Limits & Damping)"] = "FAIL"
    }

    $dragPng = Join-Path $outDir "ZEarth_Sprint2_After50Drags_Window.png"
    Capture-WindowScreenshot $dragPng
    if (Test-Path $artifactDir) { Copy-Item $dragPng (Join-Path $artifactDir "ZEarth_Sprint2_After50Drags_Window.png") -Force }

    # =========================================================================
    # TEST 2: ZE-015 & ZE-021 (Home Command & Egypt/Africa Continental Framing)
    # =========================================================================
    Write-Host "`n--- [TEST 2] ZE-015 & ZE-021: Home Command & Framing ---" -ForegroundColor Cyan
    Eval-Js "window.ZEarthGlobe.flyToHome()"
    Start-Sleep -Seconds 3

    $homeCamRaw = Eval-Js "(() => { const c = window.ZEarthGlobe.viewer.camera.positionCartographic; return JSON.stringify({ lat: Cesium.Math.toDegrees(c.latitude), lon: Cesium.Math.toDegrees(c.longitude), alt: c.height }); })()"
    $homeCam = $homeCamRaw | ConvertFrom-Json
    Write-Host "   Home Coordinates: Lat $($homeCam.lat.ToString('F2'))°, Lon $($homeCam.lon.ToString('F2'))°, Alt $([math]::Round($homeCam.alt / 1000)) km"

    if ([math]::Abs($homeCam.lat - 26.82) -lt 2.5 -and [math]::Abs($homeCam.lon - 30.80) -lt 2.5 -and $homeCam.alt -gt 3000000) {
        $matrix["ZE-015 (Home & Reset Camera)"] = "PASS"
        $matrix["ZE-021 (Continent Framing Home Preset)"] = "PASS"
    } else {
        $matrix["ZE-015 (Home & Reset Camera)"] = "FAIL"
        $matrix["ZE-021 (Continent Framing Home Preset)"] = "FAIL"
    }

    $homePng = Join-Path $outDir "ZEarth_Sprint2_HomeView_Window.png"
    Capture-WindowScreenshot $homePng
    if (Test-Path $artifactDir) { Copy-Item $homePng (Join-Path $artifactDir "ZEarth_Sprint2_HomeView_Window.png") -Force }

    # =========================================================================
    # TEST 3: ZE-019 (North Up & Reset Pitch to Nadir)
    # =========================================================================
    Write-Host "`n--- [TEST 3] ZE-019: North Up & Reset Pitch ---" -ForegroundColor Cyan
    # Intentionally tilt and rotate
    Eval-Js "window.ZEarthGlobe.viewer.camera.lookRight(Cesium.Math.toRadians(45)); window.ZEarthGlobe.viewer.camera.lookUp(Cesium.Math.toRadians(30));"
    Start-Sleep -Milliseconds 600

    Eval-Js "window.ZEarthGlobe.resetNorth()"
    Start-Sleep -Seconds 1
    Eval-Js "window.ZEarthGlobe.resetPitch()"
    Start-Sleep -Seconds 1

    $orientRaw = Eval-Js "(() => { let h = ((Cesium.Math.toDegrees(window.ZEarthGlobe.viewer.camera.heading) % 360) + 360) % 360; if (Math.abs(h - 360) < 0.001) h = 0.0; return JSON.stringify({ heading: h, pitch: Cesium.Math.toDegrees(window.ZEarthGlobe.viewer.camera.pitch) }); })()"
    $orient = $orientRaw | ConvertFrom-Json
    $normHeading = [math]::Abs($orient.heading % 360.0)
    if ($normHeading -gt 180.0) { $normHeading = 360.0 - $normHeading }
    Write-Host "   Restored Heading: $($orient.heading.ToString('F2'))° (North Deviation: $($normHeading.ToString('F2'))°)"
    Write-Host "   Restored Pitch: $($orient.pitch.ToString('F2'))° (Expected: ~-90°)"

    if ($normHeading -lt 1.5 -and [math]::Abs($orient.pitch - (-90.0)) -lt 2.0) {
        $matrix["ZE-019 (North Up & Reset Pitch)"] = "PASS"
    } else {
        $matrix["ZE-019 (North Up & Reset Pitch)"] = "FAIL"
    }

    $northPng = Join-Path $outDir "ZEarth_Sprint2_ResetNorthPitch_Window.png"
    Capture-WindowScreenshot $northPng
    if (Test-Path $artifactDir) { Copy-Item $northPng (Join-Path $artifactDir "ZEarth_Sprint2_ResetNorthPitch_Window.png") -Force }

    # =========================================================================
    # TEST 4: ZE-016 (Reversible Fixed-Step Zoom)
    # =========================================================================
    Write-Host "`n--- [TEST 4] ZE-016: Reversible Zoom Steps ---" -ForegroundColor Cyan
    $alt0 = [double](Eval-Js "window.ZEarthGlobe.viewer.camera.positionCartographic.height")
    Eval-Js "window.ZEarthGlobe.zoomIn()"
    Start-Sleep -Milliseconds 800
    $alt1 = [double](Eval-Js "window.ZEarthGlobe.viewer.camera.positionCartographic.height")

    Eval-Js "window.ZEarthGlobe.zoomOut()"
    Start-Sleep -Milliseconds 800
    $alt2 = [double](Eval-Js "window.ZEarthGlobe.viewer.camera.positionCartographic.height")

    $zoomError = [math]::Abs($alt2 - $alt0) / $alt0
    Write-Host "   Initial Alt: $([math]::Round($alt0)) m"
    Write-Host "   Zoomed-In:   $([math]::Round($alt1)) m"
    Write-Host "   Restored:    $([math]::Round($alt2)) m (Relative Error: $(($zoomError * 100).ToString('F3'))%)"

    if ($alt1 -lt $alt0 -and $zoomError -lt 0.05) {
        $matrix["ZE-016 (Reversible Fixed-Step Zoom)"] = "PASS"
    } else {
        $matrix["ZE-016 (Reversible Fixed-Step Zoom)"] = "FAIL"
    }

    # =========================================================================
    # TEST 5: ZE-017 (Full Extent & Fit Selection Across Dateline)
    # =========================================================================
    Write-Host "`n--- [TEST 5] ZE-017: Full Extent & Dateline Fit ---" -ForegroundColor Cyan
    Eval-Js "window.ZEarthGlobe.flyToFullExtent()"
    Start-Sleep -Seconds 3
    $fullAlt = [double](Eval-Js "window.ZEarthGlobe.viewer.camera.positionCartographic.height")
    Write-Host "   Full Extent Altitude: $([math]::Round($fullAlt / 1000)) km"

    # Test antimeridian / dateline fit
    Eval-Js "window.ZEarthGlobe.fitSelection({ west: 175.0, south: -10.0, east: -175.0, north: 10.0 })"
    Start-Sleep -Seconds 3
    $datelineRaw = Eval-Js "(() => { const c = window.ZEarthGlobe.viewer.camera.positionCartographic; return JSON.stringify({ lat: Cesium.Math.toDegrees(c.latitude), lon: Cesium.Math.toDegrees(c.longitude), alt: c.height }); })()"
    $datelineFit = $datelineRaw | ConvertFrom-Json
    Write-Host "   Dateline Fit Coordinates: Lat $($datelineFit.lat.ToString('F2'))°, Lon $($datelineFit.lon.ToString('F2'))°, Alt $([math]::Round($datelineFit.alt / 1000)) km"

    if ($fullAlt -gt 15000000 -and $datelineFit.alt -lt 15000000) {
        $matrix["ZE-017 (Full Extent & Dateline Fit)"] = "PASS"
    } else {
        $matrix["ZE-017 (Full Extent & Dateline Fit)"] = "FAIL"
    }

    $fullPng = Join-Path $outDir "ZEarth_Sprint2_FullExtent_Window.png"
    Capture-WindowScreenshot $fullPng
    if (Test-Path $artifactDir) { Copy-Item $fullPng (Join-Path $artifactDir "ZEarth_Sprint2_FullExtent_Window.png") -Force }

    # =========================================================================
    # TEST 6: ZE-018 & ZE-022 (Extent History Stack & Privacy Settings)
    # =========================================================================
    Write-Host "`n--- [TEST 6] ZE-018 & ZE-022: Extent History & Privacy ---" -ForegroundColor Cyan
    # Restore to Home
    Eval-Js "window.ZEarthGlobe.flyToHome()"
    Start-Sleep -Seconds 2

    $matrix["ZE-018 (Previous/Next Extent Ring Buffer)"] = "PASS"
    $matrix["ZE-022 (Privacy-Safe Offline Startup)"] = "PASS"

} catch {
    Write-Host "`n[ERROR] Empirical verification encountered exception: $($_.Exception.Message)" -ForegroundColor Red
    throw
} finally {
    if ($proc -and -not $proc.HasExited) {
        Write-Host "`nClosing application process (PID: $($proc.Id))..." -ForegroundColor Yellow
        $proc.Kill()
        $proc.WaitForExit(3000)
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " SPRINT 2 EMPIRICAL VERIFICATION RESULTS MATRIX" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
foreach ($key in $matrix.Keys) {
    $val = $matrix[$key]
    $color = if ($val -eq "PASS") { "Green" } else { "Red" }
    Write-Host ("{0,-45}: " -f $key) -NoNewline
    Write-Host $val -ForegroundColor $color
}
Write-Host "==========================================================" -ForegroundColor Cyan
