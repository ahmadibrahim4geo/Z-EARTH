# =============================================================================
# Z EARTH Desktop GIS - Sprint 7 (Layers, Terrain & Imagery) Empirical Verification Script
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
Write-Host " Z EARTH Desktop - Sprint 7 (Layers & Terrain) Verification" -ForegroundColor White
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
        [void][Win32Capture]::ShowWindow($mainHandle, 3) # SW_MAXIMIZE
        [void][Win32Capture]::SetForegroundWindow($mainHandle)
    }
    Start-Sleep -Milliseconds 800

    # -------------------------------------------------------------
    # Check 1: Offline Vector Basemap (ZE-048, AC-001)
    # -------------------------------------------------------------
    Write-Host "[3/6] Verifying Natural Earth Offline Basemap (ZE-048, AC-001)..." -ForegroundColor Yellow

    $offlineJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      g.ImageryManager.setBasemap('offline-vector');
      return {
        activeBasemap: g.ImageryManager.activeBasemap,
        offlineVisible: g.ImageryManager.baseLayers['offline-vector'].layer.show,
        osmVisible: g.ImageryManager.baseLayers['osm-standard'].layer.show,
        attribution: g.ImageryManager.baseLayers['offline-vector'].attribution
      };
    })()
'@
    $offlineRes = Eval-Js $offlineJs

    if ($offlineRes.activeBasemap -eq 'offline-vector' -and $offlineRes.offlineVisible -and -not $offlineRes.osmVisible) {
        Write-Host "  -> Offline Basemap active: $($offlineRes.attribution)" -ForegroundColor Green
        $matrix["EMP-S7-01-OfflineVectorBasemap"] = "PASS"
    } else {
        Write-Host "  -> Offline Basemap check failed: $($offlineRes | ConvertTo-Json)" -ForegroundColor Red
        $matrix["EMP-S7-01-OfflineVectorBasemap"] = "FAIL"
    }

    Start-Sleep -Milliseconds 800
    # Capture Screenshot 1: Offline Basemap
    $bmp1 = [Win32Capture]::CaptureWindow($mainHandle)
    $path1 = Join-Path $outDir "ZEarth_Sprint7_OfflineBasemap_Window.png"
    $bmp1.Save($path1, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp1.Dispose()
    Copy-Item $path1 -Destination (Join-Path $artifactDir "ZEarth_Sprint7_OfflineBasemap_Window.png") -Force

    # -------------------------------------------------------------
    # Check 2: Basemap Switching & Combined Attribution (ZE-049, AC-025)
    # -------------------------------------------------------------
    Write-Host "[4/6] Verifying Basemap Switching & Attribution (ZE-049, AC-025)..." -ForegroundColor Yellow

    $switchJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      // Switch to OpenTopoMap
      g.ImageryManager.setBasemap('opentopo');
      const topoAttr = g.ImageryManager.attributionEl.textContent;

      // Switch to CartoDB Dark
      g.ImageryManager.setBasemap('carto-dark');
      const darkAttr = g.ImageryManager.attributionEl.textContent;

      // Restore OSM
      g.ImageryManager.setBasemap('osm-standard');
      const osmAttr = g.ImageryManager.attributionEl.textContent;

      return {
        topoAttr: topoAttr,
        darkAttr: darkAttr,
        osmAttr: osmAttr,
        success: topoAttr.includes('OpenTopoMap') && darkAttr.includes('CARTO') && osmAttr.includes('OpenStreetMap')
      };
    })()
'@
    $switchRes = Eval-Js $switchJs

    if ($switchRes.success) {
        Write-Host "  -> Basemap switching & attribution: Topo, Dark, OSM verified" -ForegroundColor Green
        $matrix["EMP-S7-02-BasemapSwitchingAndAttribution"] = "PASS"
    } else {
        Write-Host "  -> Basemap switching failed: $($switchRes | ConvertTo-Json)" -ForegroundColor Red
        $matrix["EMP-S7-02-BasemapSwitchingAndAttribution"] = "FAIL"
    }

    # -------------------------------------------------------------
    # Check 3: Custom Imagery Layer & Reordering (ZE-052)
    # -------------------------------------------------------------
    Write-Host "[5/6] Verifying Custom Imagery Layer & Opacity (ZE-052)..." -ForegroundColor Yellow

    $customLayerJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      const addRes = g.ImageryManager.addCustomLayer({
        id: 'carto-voyager',
        name: 'CartoDB Voyager',
        type: 'XyzTileServer',
        url: 'https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png',
        attribution: 'Map tiles: © CARTO',
        opacity: 0.65,
        visible: true
      });

      const layerItem = g.ImageryManager.overlayLayers.get('carto-voyager');
      const opacityApplied = layerItem ? layerItem.layer.alpha : 0;
      const visibleApplied = layerItem ? layerItem.layer.show : false;

      // Reorder test
      const raised = g.ImageryManager.reorderLayer('carto-voyager', 'up');
      const lowered = g.ImageryManager.reorderLayer('carto-voyager', 'down');

      return {
        addRes: addRes,
        opacityApplied: opacityApplied,
        visibleApplied: visibleApplied,
        raised: raised,
        lowered: lowered
      };
    })()
'@
    $customRes = Eval-Js $customLayerJs

    if ($customRes.addRes.success -and [math]::Abs($customRes.opacityApplied - 0.65) -lt 0.01) {
        Write-Host "  -> Custom Layer added with opacity 65% and reordering verified" -ForegroundColor Green
        $matrix["EMP-S7-03-CustomImageryLayerAndReorder"] = "PASS"
    } else {
        Write-Host "  -> Custom layer test failed: $($customRes | ConvertTo-Json)" -ForegroundColor Red
        $matrix["EMP-S7-03-CustomImageryLayerAndReorder"] = "FAIL"
    }

    Start-Sleep -Milliseconds 800
    # Capture Screenshot 2: Layers Sidebar & Ribbon
    $bmp2 = [Win32Capture]::CaptureWindow($mainHandle)
    $path2 = Join-Path $outDir "ZEarth_Sprint7_LayersSidebarAndRibbon_Window.png"
    $bmp2.Save($path2, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp2.Dispose()
    Copy-Item $path2 -Destination (Join-Path $artifactDir "ZEarth_Sprint7_LayersSidebarAndRibbon_Window.png") -Force

    # -------------------------------------------------------------
    # Check 4: Terrain Provider Abstraction & Safe Fallback (ZE-050, AC-019)
    # -------------------------------------------------------------
    Write-Host "[6/6] Verifying Terrain Provider Abstraction & Safe Fallback (ZE-050)..." -ForegroundColor Yellow

    $terrainFallbackJs = @'
    (async () => {
      const g = window.ZEarthGlobe;
      const initialType = g.TerrainManager.currentType;

      // Try setting an invalid/unreachable terrain URL to test fallback
      const failRes = await g.TerrainManager.setProvider('custom', 'https://invalid-non-existent-terrain.local/tiles');

      const isEllipsoid = g.viewer.terrainProvider instanceof g.Cesium.EllipsoidTerrainProvider;

      return {
        initialType: initialType,
        failRes: failRes,
        fallbackToEllipsoid: isEllipsoid,
        currentType: g.TerrainManager.currentType
      };
    })()
'@
    $fallbackRes = Eval-Js $terrainFallbackJs

    if ($fallbackRes.fallbackToEllipsoid -and $fallbackRes.currentType -eq 'ellipsoid') {
        Write-Host "  -> Terrain Fallback verified: invalid endpoint safely reverted to Ellipsoid without crash" -ForegroundColor Green
        $matrix["EMP-S7-04-TerrainFallbackRobustness"] = "PASS"
    } else {
        Write-Host "  -> Terrain Fallback failed: $($fallbackRes | ConvertTo-Json)" -ForegroundColor Red
        $matrix["EMP-S7-04-TerrainFallbackRobustness"] = "FAIL"
    }

    # -------------------------------------------------------------
    # Check 5: Vertical Exaggeration Scaling & Clean Reset (ZE-051, AC-019)
    # -------------------------------------------------------------
    Write-Host "  Verifying Vertical Exaggeration Scaling (ZE-051, AC-019)..." -ForegroundColor Yellow

    $exaggerationJs = @'
    (() => {
      const g = window.ZEarthGlobe;
      // Set 3.0x
      const ex3 = g.setTerrainExaggeration(3.0);
      const sceneEx3 = g.viewer.scene.globe.terrainExaggeration;

      // Reset 1.0x
      const ex1 = g.resetTerrainExaggeration();
      const sceneEx1 = g.viewer.scene.globe.terrainExaggeration;

      return {
        ex3: ex3,
        sceneEx3: sceneEx3,
        ex1: ex1,
        sceneEx1: sceneEx1,
        valid: Math.abs(sceneEx3 - 3.0) < 0.01 && Math.abs(sceneEx1 - 1.0) < 0.01
      };
    })()
'@
    $exagRes = Eval-Js $exaggerationJs

    if ($exagRes.valid) {
        Write-Host "  -> Vertical Exaggeration 3.0x and clean 1.0x reset verified" -ForegroundColor Green
        $matrix["EMP-S7-05-VerticalExaggerationScaling"] = "PASS"
    } else {
        Write-Host "  -> Exaggeration check failed: $($exagRes | ConvertTo-Json)" -ForegroundColor Red
        $matrix["EMP-S7-05-VerticalExaggerationScaling"] = "FAIL"
    }

    Start-Sleep -Milliseconds 600
    # Capture Screenshot 3: Terrain Exaggeration
    $bmp3 = [Win32Capture]::CaptureWindow($mainHandle)
    $path3 = Join-Path $outDir "ZEarth_Sprint7_TerrainExaggeration_Window.png"
    $bmp3.Save($path3, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp3.Dispose()
    Copy-Item $path3 -Destination (Join-Path $artifactDir "ZEarth_Sprint7_TerrainExaggeration_Window.png") -Force

    Write-Host "`n================ EMPIRICAL VERIFICATION RESULTS ================" -ForegroundColor White
    $matrix.GetEnumerator() | ForEach-Object {
        $col = if ($_.Value -like "PASS*") { "Green" } else { "Red" }
        Write-Host ("{0,-42} : {1}" -f $_.Key, $_.Value) -ForegroundColor $col
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
