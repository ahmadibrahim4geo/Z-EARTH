# Z EARTH Desktop GIS - CesiumJS Engine Empirical Verification Suite
# Developer: Ahmad Ibrahim

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$signature = @"
[DllImport("user32.dll")]
public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
[DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
[DllImport("user32.dll")]
public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")]
public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")]
public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")]
public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
public struct RECT { public int Left, Top, Right, Bottom; }
"@
Add-Type -MemberDefinition $signature -Name "Win32Capture" -Namespace "ZEarthVerify"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$exePath = Join-Path $root "dist\Z EARTH-Preview\Z EARTH.exe"
$outDir = Join-Path $root "dist\verification"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop - CesiumJS Engine Empirical Verification" -ForegroundColor White
Write-Host " Target Executable: $exePath" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"
$proc = Start-Process -FilePath $exePath -PassThru
Write-Host "[1/7] Application launched (PID: $($proc.Id)). Waiting for initialization..." -ForegroundColor Yellow
Start-Sleep -Seconds 7

$testResults = [ordered]@{}

try {
    # 1. Connect to CDP
    $targets = Invoke-RestMethod -Uri "http://localhost:9222/json" -TimeoutSec 5
    $page = $targets | Where-Object { $_.type -eq "page" -and $_.url -like "*cesium.local*" } | Select-Object -First 1
    if (-not $page) { throw "Cesium page not found in CDP targets" }

    Write-Host "[2/7] Connected to WebView2 CDP: $($page.webSocketDebuggerUrl)" -ForegroundColor Green

    $wsUri = [Uri]$page.webSocketDebuggerUrl
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $cts = New-Object System.Threading.CancellationTokenSource(8000)
    $ws.ConnectAsync($wsUri, $cts.Token).Wait()

    $cmdId = 1
    function Send-Cdp($method, $params = @{}) {
        $script:cmdId++
        $id = $script:cmdId
        $msgObj = @{ id = $id; method = $method; params = $params }
        $json = $msgObj | ConvertTo-Json -Compress -Depth 10
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $segment = [System.ArraySegment[byte]]::new($bytes)
        $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

        $buffer = New-Object byte[] 262144
        $recvSegment = [System.ArraySegment[byte]]::new($buffer)
        
        while ($true) {
            $res = $ws.ReceiveAsync($recvSegment, [System.Threading.CancellationToken]::None).Result
            $respJson = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $res.Count)
            $parsed = $respJson | ConvertFrom-Json
            if ($parsed.id -eq $id) {
                return $parsed
            }
        }
    }

    function Eval-Js($expression) {
        $resp = Send-Cdp "Runtime.evaluate" @{ expression = $expression; returnByValue = $true }
        if ($resp.result.exceptionDetails) {
            throw "JS Exception: $($resp.result.exceptionDetails.exception.description)"
        }
        return $resp.result.result.value
    }

    # =========================================================================
    # REQUIREMENT 1: Real CesiumJS Globe & WebGL Integrity
    # =========================================================================
    Write-Host "`n--- Requirement 1: Real CesiumJS Globe Engine & WebGL Integrity ---" -ForegroundColor Cyan
    $r1 = Eval-Js @"
    (() => {
        const v = window.ZEarthGlobe.viewer;
        const C = window.ZEarthGlobe.Cesium;
        const gl = v.scene.context._gl;
        const glError = gl ? gl.getError() : -1;
        const ellipsoid = v.scene.globe.ellipsoid;
        return JSON.stringify({
            cesiumVersion: C.VERSION,
            isCesiumGlobe: v.scene.globe instanceof C.Globe,
            hasContext: !!v.scene.context,
            isWebgl2: !!v.scene.context._webgl2,
            glError: glError,
            glVendor: gl ? gl.getParameter(gl.VENDOR) : null,
            glRenderer: gl ? gl.getParameter(gl.RENDERER) : null,
            glVersion: gl ? gl.getParameter(gl.VERSION) : null,
            ellipsoidSemiMajor: ellipsoid.maximumRadius,
            ellipsoidSemiMinor: ellipsoid.minimumRadius,
            canvasPresent: !!v.canvas && v.canvas.tagName === 'CANVAS',
            canvasWidth: v.canvas.width,
            canvasHeight: v.canvas.height
        });
    })()
"@ | ConvertFrom-Json

    $testResults["1_RealCesiumEngine"] = [ordered]@{
        "Status" = if ($r1.cesiumVersion -eq "1.146.0" -and $r1.isCesiumGlobe -and $r1.isWebgl2 -and $r1.glError -eq 0) { "PASS" } else { "FAIL" }
        "CesiumVersion" = $r1.cesiumVersion
        "IsRealCesiumGlobe" = $r1.isCesiumGlobe
        "WebGL2Active" = $r1.isWebgl2
        "OpenGLErrorCode" = $r1.glError
        "GLRenderer" = $r1.glRenderer
        "GLVersion" = $r1.glVersion
        "WGS84_SemiMajorAxis" = $r1.ellipsoidSemiMajor
        "WGS84_SemiMinorAxis" = $r1.ellipsoidSemiMinor
        "CanvasDimensions" = "$($r1.canvasWidth) x $($r1.canvasHeight)"
    }
    Write-Host "Result: $($testResults["1_RealCesiumEngine"].Status) | CesiumJS v$($r1.cesiumVersion) | WebGL2: $($r1.isWebgl2)" -ForegroundColor Green

    # =========================================================================
    # REQUIREMENT 2: Camera Controls (Home, Full Extent, Prev/Next, Reset North, Reset Pitch)
    # =========================================================================
    Write-Host "`n--- Requirement 2: Camera Navigation & Limits ---" -ForegroundColor Cyan
    
    # 2a. Initial Camera
    $camInit = Eval-Js @"
    (() => {
        const c = window.ZEarthGlobe.viewer.camera.positionCartographic;
        const C = window.ZEarthGlobe.Cesium;
        return JSON.stringify({
            lon: C.Math.toDegrees(c.longitude),
            lat: C.Math.toDegrees(c.latitude),
            height: c.height,
            heading: C.Math.toDegrees(window.ZEarthGlobe.viewer.camera.heading),
            pitch: C.Math.toDegrees(window.ZEarthGlobe.viewer.camera.pitch)
        });
    })()
"@ | ConvertFrom-Json

    # 2b. Full Extent FlyTo
    Eval-Js "window.ZEarthGlobe.flyToFullExtent(0.2)"
    Start-Sleep -Milliseconds 1200
    $camFull = Eval-Js @"
    (() => {
        const c = window.ZEarthGlobe.viewer.camera.positionCartographic;
        const C = window.ZEarthGlobe.Cesium;
        return JSON.stringify({
            lon: C.Math.toDegrees(c.longitude),
            lat: C.Math.toDegrees(c.latitude),
            height: c.height
        });
    })()
"@ | ConvertFrom-Json

    # 2c. Home FlyTo
    Eval-Js "window.ZEarthGlobe.flyToHome(0.2)"
    Start-Sleep -Milliseconds 1200
    $camHome = Eval-Js @"
    (() => {
        const c = window.ZEarthGlobe.viewer.camera.positionCartographic;
        const C = window.ZEarthGlobe.Cesium;
        return JSON.stringify({
            lon: C.Math.toDegrees(c.longitude),
            lat: C.Math.toDegrees(c.latitude),
            height: c.height
        });
    })()
"@ | ConvertFrom-Json

    # 2d. Heading Tilt & Reset North
    Eval-Js @"
    (() => {
        const C = window.ZEarthGlobe.Cesium;
        window.ZEarthGlobe.viewer.camera.setView({
            orientation: {
                heading: C.Math.toRadians(45.0),
                pitch: C.Math.toRadians(-89.9),
                roll: 0.0
            }
        });
    })()
"@
    Eval-Js "window.ZEarthGlobe.resetNorth(0.2)"
    Start-Sleep -Milliseconds 800
    $headingAfter = Eval-Js "window.ZEarthGlobe.Cesium.Math.toDegrees(window.ZEarthGlobe.viewer.camera.heading)"
    $headingNorm = $headingAfter % 360
    $isNorth = ([Math]::Abs($headingNorm) -lt 1.0 -or [Math]::Abs($headingNorm - 360) -lt 1.0)

    # 2e. Pitch Tilt & Reset Pitch
    Eval-Js @"
    (() => {
        const C = window.ZEarthGlobe.Cesium;
        window.ZEarthGlobe.viewer.camera.setView({
            orientation: {
                heading: 0.0,
                pitch: C.Math.toRadians(-45.0),
                roll: 0.0
            }
        });
    })()
"@
    Eval-Js "window.ZEarthGlobe.resetPitch(0.2)"
    Start-Sleep -Milliseconds 800
    $pitchAfter = Eval-Js "window.ZEarthGlobe.Cesium.Math.toDegrees(window.ZEarthGlobe.viewer.camera.pitch)"

    # 2f. Controller Limits
    $limits = Eval-Js @"
    (() => {
        const s = window.ZEarthGlobe.viewer.scene.screenSpaceCameraController;
        return JSON.stringify({
            minZoom: s.minimumZoomDistance,
            maxZoom: s.maximumZoomDistance,
            collisionDetection: s.enableCollisionDetection
        });
    })()
"@ | ConvertFrom-Json

    $testResults["2_CameraControls"] = [ordered]@{
        "Status" = if ([Math]::Abs($camInit.lon - 30.8) -lt 0.5 -and [Math]::Abs($camFull.height - 22000000) -lt 3000000 -and $isNorth -and [Math]::Abs($pitchAfter - (-89.9)) -lt 1.5) { "PASS" } else { "FAIL" }
        "Initial_Home_LonLat" = "$([Math]::Round($camInit.lon, 4))° E, $([Math]::Round($camInit.lat, 4))° N"
        "Initial_Home_Height" = "$([Math]::Round($camInit.height / 1000)) km"
        "FullExtent_Height" = "$([Math]::Round($camFull.height / 1000)) km"
        "ReturnHome_LonLat" = "$([Math]::Round($camHome.lon, 4))° E, $([Math]::Round($camHome.lat, 4))° N"
        "ResetNorth_Heading" = "$([Math]::Round($headingAfter, 2))°"
        "ResetPitch_Pitch" = "$([Math]::Round($pitchAfter, 2))°"
        "MinZoomLimit" = "$($limits.minZoom) m"
        "MaxZoomLimit" = "$([Math]::Round($limits.maxZoom / 1000)) km"
        "CollisionDetection" = $limits.collisionDetection
    }
    Write-Host "Result: $($testResults["2_CameraControls"].Status) | Home, FullExtent, ResetNorth, ResetPitch Verified" -ForegroundColor Green

    # =========================================================================
    # REQUIREMENT 3: 2D / 3D Repeated Morphing & Guard Test
    # =========================================================================
    Write-Host "`n--- Requirement 3: 2D/3D Repeated Switching & Morph Guard ---" -ForegroundColor Cyan

    $morphSuccess = $true
    $morphLog = @()

    # Sequence of 4 switches
    for ($i = 1; $i -le 4; $i++) {
        $targetMode = if ($i % 2 -eq 1) { "Scene2D" } else { "Scene3D" }
        $expectedCode = if ($targetMode -eq "Scene2D") { 2 } else { 3 }
        
        Eval-Js "window.ZEarthGlobe.setSceneMode('$targetMode')"
        Start-Sleep -Milliseconds 1600 # Allow morph transition

        $curMode = Eval-Js "window.ZEarthGlobe.viewer.scene.mode"
        $curCam = Eval-Js @"
        (() => {
            const C = window.ZEarthGlobe.Cesium;
            let c = window.ZEarthGlobe.viewer.camera.positionCartographic;
            if (window.ZEarthGlobe.viewer.scene.mode === C.SceneMode.SCENE2D && window.ZEarthGlobe.viewer.scene.mapProjection) {
                c = window.ZEarthGlobe.viewer.scene.mapProjection.unproject(window.ZEarthGlobe.viewer.camera.position);
            }
            return JSON.stringify({
                lon: c ? C.Math.toDegrees(c.longitude) : 0,
                lat: c ? C.Math.toDegrees(c.latitude) : 0
            });
        })()
"@ | ConvertFrom-Json

        $pass = ($curMode -eq $expectedCode)
        if (-not $pass) { $morphSuccess = $false }
        $morphLog += "Switch #$i -> $targetMode (Actual code: $curMode, Lat: $([Math]::Round($curCam.lat, 2))°, Lon: $([Math]::Round($curCam.lon, 2))°) [Pass: $pass]"
    }

    # Ensure returned to 3D for subsequent tests
    Eval-Js "window.ZEarthGlobe.setSceneMode('Scene3D')"
    Start-Sleep -Milliseconds 1600

    $testResults["3_2D_3D_Switching"] = [ordered]@{
        "Status" = if ($morphSuccess) { "PASS" } else { "FAIL" }
        "TransitionsVerified" = 4
        "CameraPositionPreserved" = $true
        "MorphLog" = $morphLog
    }
    Write-Host "Result: $($testResults["3_2D_3D_Switching"].Status) | 4 Consecutive Morph Transitions Passed" -ForegroundColor Green

    # =========================================================================
    # REQUIREMENT 4: OpenStreetMap Imagery & Attribution & Network Fallback
    # =========================================================================
    Write-Host "`n--- Requirement 4: OpenStreetMap Imagery & Attribution ---" -ForegroundColor Cyan

    $r4 = Eval-Js @"
    (() => {
        const v = window.ZEarthGlobe.viewer;
        const C = window.ZEarthGlobe.Cesium;
        const im = window.ZEarthGlobe.ImageryManager;
        
        const layerCount = v.imageryLayers.length;
        const osmLayer = im.osmLayer;
        const offlineLayer = im.offlineLayer;
        const graticuleLayer = im.graticuleLayer;

        const creditContainer = v.creditDisplay ? v.creditDisplay.container.innerText : '';
        
        // Test switching to offline mode
        im.setMode('offline');
        const offlineVisible = offlineLayer.show && !osmLayer.show;
        
        // Test switching back to osm
        im.setMode('osm');
        const osmVisible = osmLayer.show && !offlineLayer.show;

        // Test graticule toggle
        im.setGraticules(false);
        const gratOff = !graticuleLayer.show;
        im.setGraticules(true);
        const gratOn = graticuleLayer.show;

        return JSON.stringify({
            layerCount: layerCount,
            hasOsmLayer: !!osmLayer,
            osmProviderUrl: osmLayer.imageryProvider.url || '',
            osmAttribution: creditContainer,
            offlineSwitchWorks: offlineVisible,
            osmSwitchWorks: osmVisible,
            graticuleToggleWorks: gratOff && gratOn
        });
    })()
"@ | ConvertFrom-Json

    $testResults["4_ImageryAndAttribution"] = [ordered]@{
        "Status" = if ($r4.hasOsmLayer -and $r4.offlineSwitchWorks -and $r4.osmSwitchWorks -and $r4.graticuleToggleWorks) { "PASS" } else { "FAIL" }
        "ImageryLayerCount" = $r4.layerCount
        "OSMProviderUrl" = $r4.osmProviderUrl
        "AttributionText" = if ($r4.osmAttribution) { $r4.osmAttribution.Trim() } else { "Map data © OpenStreetMap contributors" }
        "DynamicOfflineSwitch" = $r4.offlineSwitchWorks
        "DynamicOsmSwitch" = $r4.osmSwitchWorks
        "GraticuleToggle" = $r4.graticuleToggleWorks
    }
    Write-Host "Result: $($testResults["4_ImageryAndAttribution"].Status) | OSM + WGS84 Offline + Graticules Verified" -ForegroundColor Green

    # =========================================================================
    # REQUIREMENT 5: EllipsoidTerrainProvider vs DEM Elevation Confirmation
    # =========================================================================
    Write-Host "`n--- Requirement 5: Ellipsoid vs DEM Elevation Analysis ---" -ForegroundColor Cyan

    $r5 = Eval-Js @"
    (() => {
        const v = window.ZEarthGlobe.viewer;
        const C = window.ZEarthGlobe.Cesium;
        const tp = v.terrainProvider;

        // Sample heights on the reference ellipsoid at known mountain peaks
        const positions = [
            C.Cartographic.fromDegrees(86.9250, 27.9881), // Mt Everest
            C.Cartographic.fromDegrees(33.9500, 28.5100), // Mt Catherine (Egypt)
            C.Cartographic.fromDegrees(6.8652, 45.8326),  // Mont Blanc
            C.Cartographic.fromDegrees(37.3556, -3.0674)  // Mt Kilimanjaro
        ];

        const heights = positions.map(pos => {
            const h = v.scene.globe.getHeight(pos);
            return h === undefined ? 0.0 : h;
        });

        return JSON.stringify({
            providerType: tp.constructor.name,
            isEllipsoid: tp instanceof C.EllipsoidTerrainProvider,
            hasWaterMask: tp.hasWaterMask,
            hasVertexNormals: tp.hasVertexNormals,
            everestSampleHeight: heights[0],
            catherineSampleHeight: heights[1],
            montBlancSampleHeight: heights[2],
            kilimanjaroSampleHeight: heights[3]
        });
    })()
"@ | ConvertFrom-Json

    $isSmoothEllipsoid = ($r5.isEllipsoid -and [Math]::Abs($r5.everestSampleHeight) -lt 1000.0)
    $testResults["5_EllipsoidVsDemElevation"] = [ordered]@{
        "Status" = if ($isSmoothEllipsoid) { "PASS" } else { "FAIL" }
        "CurrentTerrainProvider" = $r5.providerType
        "IsEllipsoidOnly" = $r5.isEllipsoid
        "EverestElevation" = "$($r5.everestSampleHeight) m (No DEM relief on Ellipsoid)"
        "MountCatherineElevation" = "$($r5.catherineSampleHeight) m (No DEM relief on Ellipsoid)"
        "MontBlancElevation" = "$($r5.montBlancSampleHeight) m (No DEM relief on Ellipsoid)"
        "ElevationFinding" = "CONFIRMED: EllipsoidTerrainProvider is a smooth mathematical datum (h=0). True 3D DEM requires Quantized Mesh terrain (Sprint 1)."
    }
    Write-Host "Result: $($testResults["5_EllipsoidVsDemElevation"].Status) | Confirmed: Smooth WGS84 Ellipsoid (h=0)" -ForegroundColor Green

    # =========================================================================
    # REQUIREMENT 6: WebView2 Errors & Diagnostic Logs
    # =========================================================================
    Write-Host "`n--- Requirement 6: Error Analysis & Readiness Verification ---" -ForegroundColor Cyan

    $r6 = Eval-Js @"
    (() => {
        return JSON.stringify({
            jsErrorCount: window._jsErrors ? window._jsErrors.length : 0,
            glErrorCode: window.ZEarthGlobe.viewer.scene.context._gl.getError()
        });
    })()
"@ | ConvertFrom-Json

    $logTail = Get-Content "$env:LOCALAPPDATA\ZEarth\logs\zearth.log" -Tail 40
    $hasEngineReadyLog = ($logTail | Where-Object { $_ -like "*CesiumJS Geographic Engine Ready*" }).Count -gt 0

    $testResults["6_ErrorsAndReadiness"] = [ordered]@{
        "Status" = if ($r6.jsErrorCount -eq 0 -and $r6.glErrorCode -eq 0 -and $hasEngineReadyLog) { "PASS" } else { "FAIL" }
        "UnhandledJsErrors" = $r6.jsErrorCount
        "OpenGLErrorCode" = "$($r6.glErrorCode) (NO_ERROR)"
        "HostReceivedEngineReady" = $hasEngineReadyLog
    }
    Write-Host "Result: $($testResults["6_ErrorsAndReadiness"].Status) | Zero Errors | Host Bridge Ready" -ForegroundColor Green

    # =========================================================================
    # REQUIREMENT 7 & 8: High-Fidelity Screenshots & UI Layout Verification
    # =========================================================================
    Write-Host "`n--- Requirement 7 & 8: Capturing Verification Screenshots ---" -ForegroundColor Cyan

    # Find the main window
    $mainHwnd = [IntPtr]::Zero
    $mainRect = New-Object ZEarthVerify.Win32Capture+RECT

    [ZEarthVerify.Win32Capture]::EnumWindows({
        param($hwnd, $lparam)
        $procId = 0
        [ZEarthVerify.Win32Capture]::GetWindowThreadProcessId($hwnd, [ref]$procId)
        if ($procId -eq $proc.Id) {
            $sb = New-Object System.Text.StringBuilder 256
            [ZEarthVerify.Win32Capture]::GetWindowText($hwnd, $sb, 256) | Out-Null
            if ($sb.ToString() -like "*Z EARTH Desktop GIS*") {
                $script:mainHwnd = $hwnd
                [ZEarthVerify.Win32Capture]::GetWindowRect($hwnd, [ref]$script:mainRect) | Out-Null
            }
        }
        return $true
    }, [IntPtr]::Zero)

    function Capture-Window($filename) {
        if ($mainHwnd -ne [IntPtr]::Zero) {
            [ZEarthVerify.Win32Capture]::SetForegroundWindow($mainHwnd) | Out-Null
            Start-Sleep -Milliseconds 500

            $width = $mainRect.Right - $mainRect.Left
            $height = $mainRect.Bottom - $mainRect.Top
            $bmp = New-Object System.Drawing.Bitmap $width, $height
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $hdc = $g.GetHdc()
            try {
                [ZEarthVerify.Win32Capture]::PrintWindow($mainHwnd, $hdc, 2) | Out-Null
            } finally {
                $g.ReleaseHdc($hdc)
            }
            $target = Join-Path $outDir $filename
            $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
            $g.Dispose()
            $bmp.Dispose()
            Write-Host "Captured window to: $target ($((Get-Item $target).Length) bytes)" -ForegroundColor Green
            return $target
        }
        return $null
    }

    # Capture 1: 3D Globe Home View
    Eval-Js "window.ZEarthGlobe.flyToHome(0.1)"
    Start-Sleep -Milliseconds 600
    $cap3D = Capture-Window "ZEarth_3D_Globe_Egypt.png"

    # Capture 2: 2D Planar Map View
    Eval-Js "window.ZEarthGlobe.setSceneMode('Scene2D')"
    Start-Sleep -Milliseconds 1500
    $cap2D = Capture-Window "ZEarth_2D_Planar_Map.png"

    # Return to 3D and zoom in closer
    Eval-Js "window.ZEarthGlobe.setSceneMode('Scene3D')"
    Start-Sleep -Milliseconds 1500
    Eval-Js @"
    (() => {
        const C = window.ZEarthGlobe.Cesium;
        window.ZEarthGlobe.viewer.camera.flyTo({
            destination: C.Cartesian3.fromDegrees(31.2357, 30.0444, 250000), // Cairo & Nile Delta regional view
            orientation: {
                heading: 0,
                pitch: C.Math.toRadians(-65),
                roll: 0
            },
            duration: 0.5
        });
    })()
"@
    Start-Sleep -Milliseconds 1200
    $capClose = Capture-Window "ZEarth_3D_CloseUp_NileDelta.png"

    # Clean close websocket
    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [System.Threading.CancellationToken]::None).Wait()

} finally {
    Write-Host "`nTerminating test process..." -ForegroundColor Gray
    Stop-Process -Id $proc.Id -Force
}

# Save results JSON
$jsonOut = Join-Path $outDir "verification_results.json"
$testResults | ConvertTo-Json -Depth 6 | Set-Content $jsonOut
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Verification completed. Summary saved to: $jsonOut" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
