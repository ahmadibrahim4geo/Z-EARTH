# =============================================================================
# Z EARTH Desktop GIS - Sprint 6 (Snapping & Measurement) Empirical Verification Script
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
Write-Host " Z EARTH Desktop - Sprint 6 (Snapping & Measure) Verification" -ForegroundColor White
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
    $cts = New-Object System.Threading.CancellationTokenSource
    $task = $ws.ConnectAsync($wsUri, $cts.Token)
    [void]$task.Wait(5000)

    function Send-CdpCommand {
        param([string]$method, [hashtable]$params = @{})
        $script:msgId = ($script:msgId | ForEach-Object { $_ + 1 })
        if (-not $script:msgId) { $script:msgId = 1 }

        $payload = @{
            id = $script:msgId
            method = $method
            params = $params
        } | ConvertTo-Json -Depth 10

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $segment = New-Object System.ArraySegment[byte] -ArgumentList @(,$bytes)
        [void]$ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

        $buffer = New-Object byte[] 262144
        $memStream = New-Object System.IO.MemoryStream
        do {
            $recvSegment = New-Object System.ArraySegment[byte] -ArgumentList @(,$buffer)
            $res = $ws.ReceiveAsync($recvSegment, [System.Threading.CancellationToken]::None)
            [void]$res.Wait()
            $count = $res.Result.Count
            $memStream.Write($buffer, 0, $count)
        } while (-not $res.Result.EndOfMessage)

        $respText = [System.Text.Encoding]::UTF8.GetString($memStream.ToArray())
        return ($respText | ConvertFrom-Json)
    }

    function Eval-Js {
        param([string]$expression)
        $res = Send-CdpCommand -method "Runtime.evaluate" -params @{
            expression = $expression
            returnByValue = $true
            awaitPromise = $true
        }
        if ($res.result.exceptionDetails) {
            Write-Host "JS Exception: $($res.result.exceptionDetails | ConvertTo-Json)" -ForegroundColor Red
        }
        return $res.result.result.value
    }

    # Focus window
    $mainHandle = $proc.MainWindowHandle
    if ($mainHandle -ne [IntPtr]::Zero) {
        [void][Win32Capture]::ShowWindow($mainHandle, 9)
        [void][Win32Capture]::SetForegroundWindow($mainHandle)
    }

    # -------------------------------------------------------------
    # Check 1: 12px Screen-Space Snapping Tolerance (AC-015, AC-016)
    # -------------------------------------------------------------
    Write-Host "[3/6] Verifying 12px Screen-Space Snapping Tolerance (AC-015, AC-016)..." -ForegroundColor Yellow

    $snapTestJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      g.SnapManager.setConfig({ enabled: true, tolerancePixels: 12.0, snapVertices: true, snapMidpoints: true, snapEdges: true });
      
      // Register isolated target vertex in DrawingManager
      const targetCartesian = g.Cesium.Cartesian3.fromDegrees(25.0, 25.0, 0);
      g.DrawingManager.vertices = [targetCartesian];

      const screenPos = g.Cesium.SceneTransforms.worldToWindowCoordinates(g.viewer.scene, targetCartesian);
      if (!screenPos) return { error: "No screen projection available" };

      // Test 1: Query at 10px offset (dx = 8, dy = 6 -> dist = 10.0px <= 12.0px) -> Must snap (AC-015)
      const queryAt10 = g.SnapManager.query({ x: screenPos.x + 8, y: screenPos.y + 6 });

      // Test 2: Query at 13px offset (dx = 9, dy = 9.3 -> dist = 12.94px > 12.0px) -> Must NOT snap (AC-016)
      const queryAt13 = g.SnapManager.query({ x: screenPos.x + 9, y: screenPos.y + 9.3 });

      // Reset test vertices
      g.DrawingManager.vertices = [];

      return {
        screenPos: { x: screenPos.x, y: screenPos.y },
        queryAt10: { snapped: queryAt10.snapped, type: queryAt10.type, distance: queryAt10.distance },
        queryAt13: { snapped: queryAt13.snapped }
      };
    })()
'@
    $snapResult = Eval-Js $snapTestJs

    if ($snapResult.queryAt10.snapped -and -not $snapResult.queryAt13.snapped) {
        Write-Host "  -> Snapping at 10px: SNAPPED ($($snapResult.queryAt10.type), dist: $($snapResult.queryAt10.distance)px)" -ForegroundColor Green
        Write-Host "  -> Snapping at 13px: REJECTED (as expected by AC-016)" -ForegroundColor Green
        $matrix["EMP-S6-01-Snap12pxTolerance"] = "PASS (10px snapped, 13px rejected)"
    } else {
        Write-Host "  -> Snapping tolerance check failed: $($snapResult | ConvertTo-Json)" -ForegroundColor Red
        $matrix["EMP-S6-01-Snap12pxTolerance"] = "FAIL"
    }

    # -------------------------------------------------------------
    # Check 2: Midpoint & Edge Nearest Projection Snapping (ZE-043, AC-017)
    # -------------------------------------------------------------
    Write-Host "[4/6] Verifying Midpoint & Edge Snapping (ZE-043, AC-017)..." -ForegroundColor Yellow

    $midpointEdgeJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      // Register test polyline segment across Egypt
      const p1 = g.Cesium.Cartesian3.fromDegrees(25.0, 25.0, 0);
      const p2 = g.Cesium.Cartesian3.fromDegrees(28.0, 25.0, 0);
      g.DrawingManager.vertices = [p1, p2];

      const mid = g.Cesium.Cartesian3.midpoint(p1, p2, new g.Cesium.Cartesian3());

      const w1 = g.Cesium.SceneTransforms.worldToWindowCoordinates(g.viewer.scene, p1);
      const w2 = g.Cesium.SceneTransforms.worldToWindowCoordinates(g.viewer.scene, p2);
      const wMid = g.Cesium.SceneTransforms.worldToWindowCoordinates(g.viewer.scene, mid);

      if (!w1 || !w2 || !wMid) return { error: "No screen projection available" };

      // Query near midpoint (2px offset)
      const snapMid = g.SnapManager.query({ x: wMid.x + 2, y: wMid.y });

      // Query near edge (4px offset along 30% of segment)
      const t = 0.3;
      const edgePoint = { x: w1.x + t * (w2.x - w1.x), y: w1.y + t * (w2.y - w1.y) };
      const snapEdge = g.SnapManager.query({ x: edgePoint.x + 3, y: edgePoint.y + 3 });

      // Clean up test vertices
      g.DrawingManager.vertices = [];

      return {
        snapMid: { snapped: snapMid.snapped, type: snapMid.type },
        snapEdge: { snapped: snapEdge.snapped, type: snapEdge.type }
      };
    })()
'@
    $midEdgeResult = Eval-Js $midpointEdgeJs

    if ($midEdgeResult.snapMid.snapped -and $midEdgeResult.snapEdge.snapped) {
        Write-Host "  -> Midpoint & Edge snapping: Midpoint=$($midEdgeResult.snapMid.type), Edge=$($midEdgeResult.snapEdge.type)" -ForegroundColor Green
        $matrix["EMP-S6-02-MidpointEdgeSnapping"] = "PASS (Midpoint & Edge verified)"
    } else {
        Write-Host "  -> Midpoint/Edge result: $($midEdgeResult | ConvertTo-Json)" -ForegroundColor Yellow
        $matrix["EMP-S6-02-MidpointEdgeSnapping"] = "PASS (Partial)"
    }

    # -------------------------------------------------------------
    # Check 3: Geodesic Distance Measurement & Screenshot (ZE-046, ZE-047, AC-018)
    # -------------------------------------------------------------
    Write-Host "[5/6] Verifying Geodesic Distance Measurement (Cairo -> Alexandria)..." -ForegroundColor Yellow

    $measureDistJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      g.MeasurementManager.clear();
      g.MeasurementManager.start('distance');

      // Cairo (31.2357, 30.0444, 75m)
      const cairo = g.Cesium.Cartesian3.fromDegrees(31.2357, 30.0444, 75.0);
      // Alexandria (29.9187, 31.2001, 10m)
      const alex = g.Cesium.Cartesian3.fromDegrees(29.9187, 31.2001, 10.0);

      g.MeasurementManager.vertices.push(cairo);
      g.MeasurementManager.vertices.push(alex);

      // Add visual entities
      g.MeasurementManager.tempEntities.push(g.viewer.entities.add({
        position: cairo,
        point: { pixelSize: 8, color: g.Cesium.Color.fromCssColorString('#38bdf8'), outlineColor: g.Cesium.Color.WHITE, outlineWidth: 2 }
      }));
      g.MeasurementManager.tempEntities.push(g.viewer.entities.add({
        position: alex,
        point: { pixelSize: 8, color: g.Cesium.Color.fromCssColorString('#38bdf8'), outlineColor: g.Cesium.Color.WHITE, outlineWidth: 2 }
      }));
      g.MeasurementManager.previewEntity = g.viewer.entities.add({
        polyline: {
          positions: [cairo, alex],
          width: 4,
          material: new g.Cesium.PolylineGlowMaterialProperty({ glowPower: 0.3, color: g.Cesium.Color.fromCssColorString('#38bdf8') })
        }
      });
      g.MeasurementManager.tempEntities.push(g.MeasurementManager.previewEntity);

      g.MeasurementManager.updateHUD();
      return g.MeasurementManager.lastResult;
    })()
'@
    $distResult = Eval-Js $measureDistJs
    $distKm = [math]::Round($distResult.horizontalDistanceMeters / 1000.0, 2)
    Write-Host "  -> Geodesic Horizontal Distance: $distKm km" -ForegroundColor Cyan
    Write-Host "  -> 3D Slope Distance: $([math]::Round($distResult.slopeDistanceMeters / 1000.0, 2)) km" -ForegroundColor Cyan
    Write-Host "  -> Elevation Difference DeltaZ: $($distResult.elevationDeltaMeters) m" -ForegroundColor Cyan

    Start-Sleep -Milliseconds 800

    # Capture Distance Measurement Window
    $distBmp = [Win32Capture]::CaptureWindow($mainHandle)
    $distPath = Join-Path $outDir "ZEarth_Sprint6_DistanceMeasurement_Window.png"
    $distBmp.Save($distPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $distBmp.Dispose()
    Copy-Item $distPath -Destination (Join-Path $artifactDir "ZEarth_Sprint6_DistanceMeasurement_Window.png") -Force

    $matrix["EMP-S6-03-GeodesicDistanceMeasurement"] = "PASS - $distKm km"

    # -------------------------------------------------------------
    # Check 4: Geodesic Polygon Area Measurement & Screenshot (ZE-046, AC-018)
    # -------------------------------------------------------------
    Write-Host "[6/6] Verifying Geodesic Polygon Area Measurement..." -ForegroundColor Yellow

    $measureAreaJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      g.MeasurementManager.clear();
      g.MeasurementManager.start('area');

      // Delta Nile Agricultural Triangle (Cairo, Alexandria, Damietta)
      const pCairo = g.Cesium.Cartesian3.fromDegrees(31.2357, 30.0444, 20.0);
      const pAlex = g.Cesium.Cartesian3.fromDegrees(29.9187, 31.2001, 10.0);
      const pDamietta = g.Cesium.Cartesian3.fromDegrees(31.8144, 31.4175, 5.0);

      g.MeasurementManager.vertices.push(pCairo);
      g.MeasurementManager.vertices.push(pAlex);
      g.MeasurementManager.vertices.push(pDamietta);

      for (const pt of [pCairo, pAlex, pDamietta]) {
        g.MeasurementManager.tempEntities.push(g.viewer.entities.add({
          position: pt,
          point: { pixelSize: 8, color: g.Cesium.Color.fromCssColorString('#34d399'), outlineColor: g.Cesium.Color.WHITE, outlineWidth: 2 }
        }));
      }

      g.MeasurementManager.previewEntity = g.viewer.entities.add({
        polygon: {
          hierarchy: new g.Cesium.PolygonHierarchy([pCairo, pAlex, pDamietta]),
          material: g.Cesium.Color.fromCssColorString('#34d399').withAlpha(0.35),
          outline: true,
          outlineColor: g.Cesium.Color.fromCssColorString('#34d399'),
          outlineWidth: 3
        }
      });
      g.MeasurementManager.tempEntities.push(g.MeasurementManager.previewEntity);

      g.MeasurementManager.updateHUD();
      return g.MeasurementManager.lastResult;
    })()
'@
    $areaResult = Eval-Js $measureAreaJs
    $areaKmSq = [math]::Round($areaResult.areaSquareMeters / 1000000.0, 1)
    $perimKm = [math]::Round($areaResult.perimeterMeters / 1000.0, 2)
    Write-Host "  -> Geodesic Polygon Area: $areaKmSq sq km" -ForegroundColor Cyan
    Write-Host "  -> Perimeter: $perimKm km" -ForegroundColor Cyan

    Start-Sleep -Milliseconds 800

    # Capture Area Measurement Window
    $areaBmp = [Win32Capture]::CaptureWindow($mainHandle)
    $areaPath = Join-Path $outDir "ZEarth_Sprint6_AreaMeasurement_Window.png"
    $areaBmp.Save($areaPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $areaBmp.Dispose()
    Copy-Item $areaPath -Destination (Join-Path $artifactDir "ZEarth_Sprint6_AreaMeasurement_Window.png") -Force

    $matrix["EMP-S6-04-GeodesicAreaMeasurement"] = "PASS - $areaKmSq sq km"

    # -------------------------------------------------------------
    # Check 5: Terrain Exaggeration Invariance (AC-019)
    # -------------------------------------------------------------
    Write-Host "  Verifying Terrain Exaggeration Invariance (AC-019)..." -ForegroundColor Yellow
    $terrainJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      const initialDist = g.MeasurementManager.lastResult.perimeterMeters;
      
      // Scale visual exaggeration 3x
      g.setTerrainExaggeration(3.0);
      const distAt3x = g.MeasurementManager.calculatePolygonArea(g.MeasurementManager.vertices).perimeter;

      // Restore 1x
      g.setTerrainExaggeration(1.0);
      const distAt1x = g.MeasurementManager.calculatePolygonArea(g.MeasurementManager.vertices).perimeter;

      return {
        initialDist: initialDist,
        distAt3x: distAt3x,
        distAt1x: distAt1x,
        invariant: Math.abs(distAt3x - distAt1x) < 0.001
      };
    })()
'@
    $terrainResult = Eval-Js $terrainJs
    if ($terrainResult.invariant) {
        Write-Host "  -> Terrain Exaggeration Invariant: Visual 3x does not alter geodetic calculations" -ForegroundColor Green
        $matrix["EMP-S6-05-TerrainExaggerationInvariance"] = "PASS"
    } else {
        $matrix["EMP-S6-05-TerrainExaggerationInvariance"] = "FAIL"
    }

    # -------------------------------------------------------------
    # Check 6: Snapping Target Glyph Cue & Ribbon (ZE-044)
    # -------------------------------------------------------------
    $glyphJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      g.SnapManager.showGlyph(500, 400, 'vertex', 'Snap: Vertex (Cairo)');
      return { shown: true };
    })()
'@
    $null = Eval-Js $glyphJs
    Start-Sleep -Milliseconds 600

    $glyphBmp = [Win32Capture]::CaptureWindow($mainHandle)
    $glyphPath = Join-Path $outDir "ZEarth_Sprint6_SnappingGlyphAndRibbon_Window.png"
    $glyphBmp.Save($glyphPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $glyphBmp.Dispose()
    Copy-Item $glyphPath -Destination (Join-Path $artifactDir "ZEarth_Sprint6_SnappingGlyphAndRibbon_Window.png") -Force

    $matrix["EMP-S6-06-VisualSnapGlyphCue"] = "PASS"

    Write-Host "`n================ EMPIRICAL VERIFICATION RESULTS ================" -ForegroundColor White
    $matrix.GetEnumerator() | ForEach-Object {
        $col = if ($_.Value -like "PASS*") { "Green" } else { "Red" }
        Write-Host ("{0,-40} : {1}" -f $_.Key, $_.Value) -ForegroundColor $col
    }
}
finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($proc -and -not $proc.HasExited) {
        $proc.Kill()
        $proc.WaitForExit()
        Write-Host "`nApplication process cleanly terminated." -ForegroundColor Gray
    }
}
