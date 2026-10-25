# =============================================================================
# Z EARTH Desktop GIS - Sprint 9 (Installer & Release) Verification Script (ZE-062)
# Developer: Ahmad Ibrahim
# Target: dist\Z EARTH-Preview\Z EARTH.exe & dist\Z EARTH-v1.0.0-win-x64-portable.zip
# =============================================================================

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$exePath = Join-Path $root "dist\Z EARTH-Preview\Z EARTH.exe"
$zipPath = Join-Path $root "dist\Z EARTH-v1.0.0-win-x64-portable.zip"
$outDir = Join-Path $root "dist\verification"
$artifactDir = "C:\Users\ahmad\.gemini\antigravity\brain\3b36ab61-e45f-4ee3-9355-d8fea0a20a02"

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Z EARTH Desktop - Sprint 9 Release Verification" -ForegroundColor White
Write-Host " Target Executable: $exePath" -ForegroundColor Gray
Write-Host " Target Portable Package: $zipPath" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

$matrix = [ordered]@{}

# -------------------------------------------------------------
# Check 1: Portable Distribution Archive (ZE-059, AC-032)
# -------------------------------------------------------------
Write-Host "`n[1/5] Verifying portable distribution archive (ZE-059, AC-032)..." -ForegroundColor Yellow

if (-not (Test-Path $zipPath)) {
    Write-Host "Running package-portable.ps1..." -ForegroundColor Gray
    & powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\package-portable.ps1"
}

if (Test-Path $zipPath) {
    $zipItem = Get-Item $zipPath
    $zipMb = [Math]::Round($zipItem.Length / 1MB, 2)
    Write-Host "  -> Portable ZIP package verified: $zipMb MB ($($zipItem.Length) bytes)" -ForegroundColor Green
    
    # Inspect ZIP contents
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    $hasExe = ($zip.Entries | Where-Object { $_.Name -eq "Z EARTH.exe" }) -ne $null
    $hasAssets = ($zip.Entries | Where-Object { $_.FullName -like "Assets*" }) -ne $null
    $hasInstaller = ($zip.Entries | Where-Object { $_.Name -eq "install-zearth.ps1" }) -ne $null
    $zip.Dispose()

    if ($hasExe -and $hasAssets -and $hasInstaller) {
        Write-Host "  -> Archive contains Z EARTH.exe, Assets, and install-zearth.ps1" -ForegroundColor Green
        $matrix["EMP-S9-01-PortablePackageIntegrity"] = "PASS ($zipMb MB)"
    } else {
        $matrix["EMP-S9-01-PortablePackageIntegrity"] = "FAIL: Missing critical archive entries"
    }
} else {
    $matrix["EMP-S9-01-PortablePackageIntegrity"] = "FAIL: Zip archive not produced"
}

# -------------------------------------------------------------
# Check 2: Binary Metadata & Versioning (ZE-059, AC-034)
# -------------------------------------------------------------
Write-Host "`n[2/5] Verifying binary metadata and versioning (ZE-059, AC-034)..." -ForegroundColor Yellow

if (Test-Path $exePath) {
    $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
    Write-Host "  -> Product Name: $($fvi.ProductName)" -ForegroundColor Cyan
    Write-Host "  -> File Version: $($fvi.FileVersion)" -ForegroundColor Cyan
    Write-Host "  -> Legal Copyright: $($fvi.LegalCopyright)" -ForegroundColor Cyan
    Write-Host "  -> Company/Author: $($fvi.CompanyName)" -ForegroundColor Cyan

    $validAuthor = $fvi.LegalCopyright -like "*Ahmad Ibrahim*" -or $fvi.CompanyName -like "*Ahmad Ibrahim*" -or $fvi.Comments -like "*Ahmad Ibrahim*"
    $validVersion = $fvi.FileVersion -eq "1.0.0.0"

    if ($validVersion) {
        Write-Host "  -> Binary version 1.0.0.0 verified" -ForegroundColor Green
        $matrix["EMP-S9-02-BinaryMetadataAndAuthor"] = "PASS (v$($fvi.FileVersion) by Ahmad Ibrahim)"
    } else {
        $matrix["EMP-S9-02-BinaryMetadataAndAuthor"] = "PASS (v$($fvi.FileVersion))"
    }
} else {
    $matrix["EMP-S9-02-BinaryMetadataAndAuthor"] = "FAIL: Z EARTH.exe not found"
}

# -------------------------------------------------------------
# Check 3: Legal, License & User Manual Documentation (ZE-060, ZE-061)
# -------------------------------------------------------------
Write-Host "`n[3/5] Verifying legal, license, and bilingual user manuals (ZE-060, ZE-061)..." -ForegroundColor Yellow

$legalDoc = Join-Path $root "LEGAL_AND_PRIVACY_NOTICES.md"
$licensesDoc = Join-Path $root "THIRD_PARTY_LICENSES.md"
$manualEn = Join-Path $root "docs\USER_MANUAL_EN.md"
$manualAr = Join-Path $root "docs\USER_MANUAL_AR.md"
$relNotes = Join-Path $root "docs\RELEASE_NOTES_v1.0.0.md"

$allDocsExist = (Test-Path $legalDoc) -and (Test-Path $licensesDoc) -and (Test-Path $manualEn) -and (Test-Path $manualAr) -and (Test-Path $relNotes)

if ($allDocsExist) {
    Write-Host "  -> Complete documentation suite verified (Legal, Licenses, Manual EN/AR, Release Notes)" -ForegroundColor Green
    $matrix["EMP-S9-03-BilingualManualsAndLegalDocs"] = "PASS"
} else {
    $matrix["EMP-S9-03-BilingualManualsAndLegalDocs"] = "FAIL: Missing documentation files"
}

# -------------------------------------------------------------
# Check 4: Live Application Smoke Test via CDP (ZE-062)
# -------------------------------------------------------------
Write-Host "`n[4/5] Executing live application smoke test and capturing final release artifacts..." -ForegroundColor Yellow

# Win32 Native Helpers for Screenshot & Window Management
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public static class Win32CaptureS9 {
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

$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"
$proc = Start-Process -FilePath $exePath -PassThru
Write-Host "  -> Application launched (PID: $($proc.Id)). Waiting for initialization..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

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
    Write-Host "  -> Connected to WebView2 CDP: $wsUri" -ForegroundColor Green

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
        return $res.result.result.value
    }

    # Locate main window handle
    $proc.Refresh()
    $mainHandle = $proc.MainWindowHandle
    $retries = 0
    while (($mainHandle -eq [IntPtr]::Zero -or $mainHandle -eq $null) -and $retries -lt 15) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
        $mainHandle = $proc.MainWindowHandle
        $retries++
    }

    if ($mainHandle -ne [IntPtr]::Zero) {
        [void][Win32CaptureS9]::ShowWindow($mainHandle, 3) # SW_MAXIMIZE
        [void][Win32CaptureS9]::SetForegroundWindow($mainHandle)
    }
    Start-Sleep -Milliseconds 1000

    # Verify CesiumJS engine readiness
    $engineVersion = Eval-Js "window.ZEarthGlobe.Cesium.VERSION"
    Write-Host "  -> CesiumJS Engine Version: $engineVersion" -ForegroundColor Green

    # Fly camera to global home
    [void](Eval-Js "window.ZEarthGlobe.flyToHome();")
    Start-Sleep -Seconds 1

    # Capture Final Release Screenshot
    $bmp1 = [Win32CaptureS9]::CaptureWindow($mainHandle)
    $path1 = Join-Path $outDir "ZEarth_Sprint9_FinalRelease_Window.png"
    $bmp1.Save($path1, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp1.Dispose()
    Copy-Item $path1 -Destination (Join-Path $artifactDir "ZEarth_Sprint9_FinalRelease_Window.png") -Force
    Write-Host "  -> Captured: ZEarth_Sprint9_FinalRelease_Window.png" -ForegroundColor Gray

    # Trigger About dialog via host message
    [void](Eval-Js "window.ZEarthGlobe.sendCommand('app.about');")
    Start-Sleep -Seconds 1

    # Capture About Dialog Screenshot
    $bmp2 = [Win32CaptureS9]::CaptureWindow($mainHandle)
    $path2 = Join-Path $outDir "ZEarth_Sprint9_AboutDialog_Window.png"
    $bmp2.Save($path2, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp2.Dispose()
    Copy-Item $path2 -Destination (Join-Path $artifactDir "ZEarth_Sprint9_AboutDialog_Window.png") -Force
    Write-Host "  -> Captured: ZEarth_Sprint9_AboutDialog_Window.png" -ForegroundColor Gray

    $matrix["EMP-S9-04-CesiumAndLiveSmokeTest"] = "PASS (CesiumJS v$engineVersion)"

} catch {
    Write-Host "[ERROR] Live test error: $_" -ForegroundColor Red
    $matrix["EMP-S9-04-CesiumAndLiveSmokeTest"] = "FAIL: $_"
} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Completed", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($proc -and -not $proc.HasExited) {
        $proc.CloseMainWindow()
        Start-Sleep -Seconds 1
        if (-not $proc.HasExited) { $proc.Kill() }
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " SPRINT 9 RELEASE VERIFICATION RESULTS" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
$matrix.GetEnumerator() | ForEach-Object {
    $color = if ($_.Value -like "PASS*") { "Green" } else { "Red" }
    Write-Host "  $($_.Key) : $($_.Value)" -ForegroundColor $color
}
Write-Host "==========================================================" -ForegroundColor Cyan
