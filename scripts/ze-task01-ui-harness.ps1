# Z EARTH - Task 01 UI responsiveness / command reliability harness
#
# Provides two genuinely-executed test families:
#   1) -Mode matrix : runs the real application once per command group using the app's own
#                     automation flags, while a "hung application" probe
#                     (SendMessageTimeout(WM_NULL) with SMTO_ABORTIFHUNG - the same criterion
#                     Windows itself uses for "not responding") samples UI-thread message-pump
#                     latency for the whole run.
#   2) -Mode stress : 3D<->2D transitions x N (app's own --e2e-morph driver), theme toggle x N via
#                     real keystrokes, and a sustained synthetic globe drag/hover.
#
# Probes are bucketed:
#   cold   = before the process has finished its own WebView2 settle window (excluded from the
#            headline responsiveness numbers; reported separately as a one-time startup cost)
#   steady = after the settle window: the user-perceived interactive responsiveness
#
# Every run records: exit code, per-command log slice, steady-state latency p50/p95/p99/max,
# hang counts, and presence of engine.ready / errors / timeouts / unsupported actions.

param(
    [string]$ExePath = "dist\Z EARTH_Win_x64\Z EARTH.exe",
    [string]$EvidenceDir = "Development Reports\ZE_Task_01_evidence",
    [ValidateSet("matrix", "stress", "all")]
    [string]$Mode = "all",
    [int]$MorphCycles = 20,
    [int]$ThemeToggles = 20,
    [int]$DragSeconds = 60,
    [int]$ProbeIntervalMs = 40,
    [int]$ProbeTimeoutMs = 1000,
    [int]$ColdCutoffMs = 6000,
    [string]$ScenarioFilter = "",
    [switch]$TraceProbes
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Add-Type -AssemblyName System.Drawing

if (-not ("ZE.Win32" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
namespace ZE {
  public static class Win32 {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
  }
}
"@
}

$WM_NULL = 0
$SMTO_ABORTIFHUNG = 0x0002
$MOUSEEVENTF_LEFTDOWN = 0x0002
$MOUSEEVENTF_LEFTUP = 0x0004
$VK_CONTROL = 0x11
$VK_T = 0x54
$KEYEVENTF_KEYUP = 0x0002

$logPath = Join-Path $env:LOCALAPPDATA "ZEarth\logs\zearth.log"
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
$shotsDir = Join-Path $EvidenceDir "screenshots"
New-Item -ItemType Directory -Force -Path $shotsDir | Out-Null
$runLogDir = Join-Path $EvidenceDir "run-logs"
New-Item -ItemType Directory -Force -Path $runLogDir | Out-Null

if (-not (Test-Path -LiteralPath $ExePath)) { throw "Executable not found: $ExePath" }
$exe = (Resolve-Path -LiteralPath $ExePath).Path

# ---------------------------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------------------------
function Get-LogLength {
    if (Test-Path -LiteralPath $logPath) { return (Get-Item -LiteralPath $logPath).Length }
    return 0
}

function Get-LogDelta {
    param([long]$From)
    if (-not (Test-Path -LiteralPath $logPath)) { return "" }
    $fs = [System.IO.File]::Open($logPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        if ($fs.Length -le $From) { return "" }
        $fs.Seek($From, [System.IO.SeekOrigin]::Begin) | Out-Null
        $sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)
        return $sr.ReadToEnd()
    }
    finally { $fs.Dispose() }
}

function Wait-ForWindow {
    param($Process, [int]$TimeoutSeconds = 25)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) { return [IntPtr]::Zero }
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { return $Process.MainWindowHandle }
        Start-Sleep -Milliseconds 200
    }
    return [IntPtr]::Zero
}

function Focus-Window {
    param([IntPtr]$Hwnd)
    if ($Hwnd -eq [IntPtr]::Zero) { return }
    if ([ZE.Win32]::IsIconic($Hwnd)) { [ZE.Win32]::ShowWindow($Hwnd, 9) | Out-Null }
    [ZE.Win32]::SetForegroundWindow($Hwnd) | Out-Null
}

function New-Probe {
    param([IntPtr]$Hwnd, [double]$Ms, [int]$TimeoutMs)
    if ($Hwnd -eq [IntPtr]::Zero -or -not [ZE.Win32]::IsWindow($Hwnd)) { return $null }
    $result = [IntPtr]::Zero
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $ok = [ZE.Win32]::SendMessageTimeout($Hwnd, $WM_NULL, [IntPtr]::Zero, [IntPtr]::Zero, $SMTO_ABORTIFHUNG, [uint32]$TimeoutMs, [ref]$result)
    $sw.Stop()
    return [pscustomobject]@{
        Ms = $Ms
        Ok = ($ok -ne [IntPtr]::Zero)
        Us = [math]::Round($sw.Elapsed.TotalMilliseconds * 1000.0, 1)
    }
}

function Get-Stats {
    param([double[]]$Samples, [int]$Hangs)
    if (-not $Samples -or $Samples.Count -eq 0) {
        return [pscustomobject]@{ Count = 0; P50 = 0; P95 = 0; P99 = 0; Max = 0; Mean = 0; Hangs = $Hangs }
    }
    $sorted = $Samples | Sort-Object
    $n = $sorted.Count
    function Pct([double]$p) { $idx = [math]::Ceiling($p / 100.0 * $n) - 1; if ($idx -lt 0) { $idx = 0 }; if ($idx -ge $n) { $idx = $n - 1 }; return $sorted[$idx] }
    return [pscustomobject]@{
        Count = $n
        P50   = [math]::Round((Pct 50), 1)
        P95   = [math]::Round((Pct 95), 1)
        P99   = [math]::Round((Pct 99), 1)
        Max   = [math]::Round($sorted[$n - 1], 1)
        Mean  = [math]::Round(($Samples | Measure-Object -Average).Average, 1)
        Hangs = $Hangs
    }
}

function Get-BucketStats {
    param($Probes, [double]$FromMs, [double]$ToMs)
    $bucket = @($Probes | Where-Object { $_.Ms -ge $FromMs -and $_.Ms -lt $ToMs })
    $ok = @($bucket | Where-Object { $_.Ok } | ForEach-Object { $_.Us })
    $hang = @($bucket | Where-Object { -not $_.Ok }).Count
    return Get-Stats -Samples $ok -Hangs $hang
}

function Analyze-LogDelta {
    param([string]$Text)
    $lines = @()
    if ($Text) { $lines = $Text -split "`r?`n" | Where-Object { $_ -ne "" } }
    $unsupported = @()
    foreach ($m in ($lines | Select-String -Pattern "Unsupported action: ([a-zA-Z0-9_\.]+)" -AllMatches)) {
        foreach ($mm in $m.Matches) { $unsupported += $mm.Groups[1].Value }
    }
    $distinctUnsupported = ($unsupported | Sort-Object -Unique) -join ";"
    return [pscustomobject]@{
        Lines         = $lines.Count
        EngineReady   = (($lines | Select-String -SimpleMatch "engine.ready").Count)
        Http200       = (($lines | Select-String -SimpleMatch "HTTP: 200").Count)
        Errors        = (($lines | Select-String -SimpleMatch "] [ERROR]").Count)
        Warnings      = (($lines | Select-String -SimpleMatch "] [WARN]").Count)
        Timeouts      = (($lines | Select-String -SimpleMatch "timed out").Count)
        Unsupported   = $unsupported.Count
        UnsupportedList = $distinctUnsupported
        NotCompleted  = (($lines | Select-String -SimpleMatch "not completed").Count)
        Dropped       = (($lines | Select-String -SimpleMatch "dropped").Count)
        Telemetry     = (($lines | Select-String -SimpleMatch "Globe telemetry").Count)
        JsFatal       = (($lines | Select-String -SimpleMatch "FATAL_JS_ERROR").Count)
        PackUri       = (($lines | Select-String -SimpleMatch "URI prefix is not recognized").Count)
        SqliteLock    = (($lines | Select-String -SimpleMatch "being used by another process").Count)
        ReferenceFail = (($lines | Select-String -SimpleMatch "reference layer").Count)
    }
}

function Save-ProcessScreenshot {
    param([IntPtr]$Hwnd, [string]$Path)
    try {
        $rect = New-Object ZE.Win32+RECT
        if (-not [ZE.Win32]::GetWindowRect($Hwnd, [ref]$rect)) { return $false }
        $w = $rect.Right - $rect.Left
        $h = $rect.Bottom - $rect.Top
        if ($w -lt 100 -or $h -lt 100) { return $false }
        $bmp = New-Object System.Drawing.Bitmap($w, $h)
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        $gfx.Dispose(); $bmp.Dispose()
        return $true
    }
    catch { return $false }
}

function Start-ZeApp {
    param([string[]]$ArgList)
    $argLine = ($ArgList | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    return Start-Process -FilePath $exe -ArgumentList $argLine -PassThru
}

function New-ResultRow {
    param($Id, $Expect, $ExitCode, $Screenshot, $Stats, $ColdHangs, $ElapsedMs, $log, $logFile)
    return [pscustomobject]@{
        Id             = $Id
        Expect         = $Expect
        ExitCode       = $ExitCode
        Screenshot     = $Screenshot
        ProbeSamples   = $Stats.Count
        P50us          = $Stats.P50
        P95us          = $Stats.P95
        P99us          = $Stats.P99
        Maxus          = $Stats.Max
        Hangs          = $Stats.Hangs
        ColdHangs      = $ColdHangs
        RunMs          = [math]::Round($ElapsedMs)
        LogLines       = $log.Lines
        EngineReady    = $log.EngineReady
        Http200        = $log.Http200
        Errors         = $log.Errors
        Timeouts       = $log.Timeouts
        Unsupported    = $log.Unsupported
        UnsupportedList = $log.UnsupportedList
        NotCompleted   = $log.NotCompleted
        JsFatal        = $log.JsFatal
        PackUriErrors  = $log.PackUri
        SqliteLocks    = $log.SqliteLock
        RunLog         = $logFile
    }
}

# ---------------------------------------------------------------------------------------------
# command matrix
# ---------------------------------------------------------------------------------------------
$matrix = @(
    @{ Id = "NAV-3D";           Args = @("--no-splash");                                       Expect = "3D scene ready" }
    @{ Id = "NAV-2D";           Args = @("--no-splash", "--mode", "2d");                       Expect = "2D scene morph" }
    @{ Id = "CMP-QIBLA";        Args = @("--no-splash", "--compass", "qibla");                 Expect = "compass mode" }
    @{ Id = "CMP-HEADING";      Args = @("--no-splash", "--compass", "heading");               Expect = "compass mode" }
    @{ Id = "CRS-3857";         Args = @("--no-splash", "--crs", "3857");                      Expect = "projected CRS" }
    @{ Id = "CRS-32636";        Args = @("--no-splash", "--crs", "32636");                     Expect = "UTM projected CRS" }
    @{ Id = "LYR-BASEMAP-ESRI"; Args = @("--no-splash", "--basemap", "esri-imagery");          Expect = "basemap switch" }
    @{ Id = "LYR-BASEMAP-OSM";  Args = @("--no-splash", "--basemap", "osm-standard");          Expect = "basemap switch" }
    @{ Id = "LYR-BASEMAP-BAD";  Args = @("--no-splash", "--basemap", "not-a-real-basemap");    Expect = "invalid basemap id handled gracefully" }
    @{ Id = "LYR-SWIPE";        Args = @("--no-splash", "--swipe", "esri-imagery,osm-standard"); Expect = "imagery swipe" }
    @{ Id = "TRN-TOGGLE";       Args = @("--no-splash", "--terrain", "dem");                   Expect = "terrain toggle" }
    @{ Id = "TRN-EXAG";         Args = @("--no-splash", "--exaggeration", "3");                Expect = "vertical exaggeration" }
    @{ Id = "DRW-CIRCLE";       Args = @("--no-splash", "--draw", "circle");                   Expect = "draw circle" }
    @{ Id = "DRW-POLYGON";      Args = @("--no-splash", "--draw", "polygon");                  Expect = "draw polygon" }
    @{ Id = "DRW-PATH";         Args = @("--no-splash", "--draw", "path");                     Expect = "draw path" }
    @{ Id = "MSR-DISTANCE";     Args = @("--no-splash", "--measure", "distance");              Expect = "measure distance" }
    @{ Id = "MSR-AREA";         Args = @("--no-splash", "--measure", "area");                  Expect = "measure area" }
    @{ Id = "MSR-CIRCLE";       Args = @("--no-splash", "--measure", "circle");                Expect = "measure circle" }
    @{ Id = "ENV-SUNLIGHT";     Args = @("--no-splash", "--sunlight", "true");                 Expect = "sunlight enable" }
    @{ Id = "ENV-ATMOSPHERE";   Args = @("--no-splash", "--atmosphere", "true");               Expect = "atmosphere enable" }
    @{ Id = "ENV-TIME";         Args = @("--no-splash", "--time", "15.5");                     Expect = "time of day" }
    @{ Id = "SRH-NAME";         Args = @("--no-splash", "--search-query", "Cairo");            Expect = "search by name (network)" }
    @{ Id = "SRH-COORDS";       Args = @("--no-splash", "--search-mode", "coords", "--search-coords", "31.2357,30.0444,4326"); Expect = "locate coordinates" }
    @{ Id = "DLG-OPTIONS";      Args = @("--no-splash", "--dialog", "options");                Expect = "options dialog" }
    @{ Id = "DLG-PROPERTIES";   Args = @("--no-splash", "--dialog", "properties");             Expect = "properties dialog" }
    @{ Id = "DLG-BASEMAPS";     Args = @("--no-splash", "--dialog", "basemaps");               Expect = "basemap gallery dialog" }
    @{ Id = "DLG-COORDS";       Args = @("--no-splash", "--dialog", "coords");                 Expect = "coordinate converter dialog" }
    @{ Id = "DLG-CRS";          Args = @("--no-splash", "--dialog", "crs");                    Expect = "CRS dialog" }
    @{ Id = "DLG-ADDIMAGERY";   Args = @("--no-splash", "--dialog", "addimagery");             Expect = "add imagery dialog" }
    @{ Id = "DLG-ELEVATION";    Args = @("--no-splash", "--dialog", "elevation");              Expect = "elevation profile dialog" }
    @{ Id = "SIDEBAR-REF";      Args = @("--no-splash", "--sidebar", "reference");             Expect = "reference layers section" }
    @{ Id = "SIDEBAR-ALL";      Args = @("--no-splash", "--sidebar", "all");                   Expect = "all sections expanded" }
    @{ Id = "TAB-DRAWING";      Args = @("--no-splash", "--ribbon-tab", "2");                  Expect = "ribbon tab switch" }
    @{ Id = "TAB-LAYERS";       Args = @("--no-splash", "--ribbon-tab", "4");                  Expect = "ribbon tab switch" }
    @{ Id = "THM-LIGHT";        Args = @("--no-splash", "--theme", "light");                   Expect = "light theme apply" }
    @{ Id = "THM-DARK";         Args = @("--no-splash", "--theme", "dark");                    Expect = "dark theme apply" }
    @{ Id = "OFFLINE-START";    Args = @("--no-splash", "--basemap", "offline-vector");        Expect = "offline startup with bundled vector basemap" }
    @{ Id = "OFFLINE-PROVIDER-FAIL"; Args = @("--no-splash", "--basemap", "esri-imagery");     Expect = "online provider unreachable: precise error, no hang" }
)

function Invoke-MatrixRun {
    param($Scenario)
    $id = $Scenario.Id
    $png = Join-Path $shotsDir "$id.png"
    if (Test-Path -LiteralPath $png) { Remove-Item -LiteralPath $png -Force }
    $runArgs = @($Scenario.Args) + @("--capture", $png)

    $logFrom = Get-LogLength
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-ZeApp -ArgList $runArgs
    $hwnd = Wait-ForWindow -Process $proc -TimeoutSeconds 25

    $probes = New-Object System.Collections.Generic.List[object]
    while (-not $proc.HasExited) {
        $ms = $watch.Elapsed.TotalMilliseconds
        if ($hwnd -ne [IntPtr]::Zero) { $pr = New-Probe -Hwnd $hwnd -Ms $ms -TimeoutMs $ProbeTimeoutMs; if ($pr) { $probes.Add($pr) | Out-Null } }
        Start-Sleep -Milliseconds $ProbeIntervalMs
        if ($watch.Elapsed.TotalSeconds -gt 180) { break }
    }
    if (-not $proc.HasExited) { $proc.Kill(); Start-Sleep -Milliseconds 500 }
    $proc.WaitForExit()
    $watch.Stop()
    Start-Sleep -Milliseconds 600

    $text = Get-LogDelta -From $logFrom
    $logFile = Join-Path $runLogDir "$id.log"
    Set-Content -LiteralPath $logFile -Value $text -Encoding UTF8
    if ($TraceProbes) { $probes | Export-Csv -LiteralPath (Join-Path $runLogDir "$id.probes.csv") -NoTypeInformation -Encoding UTF8 }
    $steady = Get-BucketStats -Probes $probes -FromMs $ColdCutoffMs -ToMs ([double]::MaxValue)
    $cold = Get-BucketStats -Probes $probes -FromMs 0 -ToMs $ColdCutoffMs
    $log = Analyze-LogDelta -Text $text
    return New-ResultRow -Id $id -Expect $Scenario.Expect -ExitCode $proc.ExitCode -Screenshot (Test-Path -LiteralPath $png) `
        -Stats $steady -ColdHangs $cold.Hangs -ElapsedMs $watch.Elapsed.TotalMilliseconds -log $log -logFile $logFile
}

function Run-Matrix {
    $results = New-Object System.Collections.Generic.List[object]
    foreach ($scenario in $matrix) {
        if ($ScenarioFilter -and $scenario.Id -notlike "*$ScenarioFilter*") { continue }
        Write-Host ("[matrix] {0} ..." -f $scenario.Id)
        $r = Invoke-MatrixRun -Scenario $scenario
        $results.Add($r) | Out-Null
        Write-Host ("         exit={0} png={1} steady n={2} p50={3} p95={4} p99={5} max={6}us hangs={7} coldHangs={8} errors={9} ready={10} ms={11}" -f `
            $r.ExitCode, $r.Screenshot, $r.ProbeSamples, $r.P50us, $r.P95us, $r.P99us, $r.Maxus, $r.Hangs, $r.ColdHangs, $r.Errors, $r.EngineReady, $r.RunMs)
    }
    return $results
}

# ---------------------------------------------------------------------------------------------
# stress
# ---------------------------------------------------------------------------------------------
function Invoke-StressInteractive {
    param(
        [string]$Id,
        [scriptblock]$DuringRun,
        [int]$Seconds,
        [string]$ScreenshotName
    )
    $logFrom = Get-LogLength
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-ZeApp -ArgList @("--no-splash")
    $hwnd = Wait-ForWindow -Process $proc -TimeoutSeconds 25
    if ($hwnd -eq [IntPtr]::Zero) { throw "The application window never appeared for $Id" }
    Focus-Window -Hwnd $hwnd
    Start-Sleep -Seconds 9
    Focus-Window -Hwnd $hwnd

    $rect = New-Object ZE.Win32+RECT
    [ZE.Win32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

    $probes = New-Object System.Collections.Generic.List[object]
    $iteration = 0
    $endMs = $watch.Elapsed.TotalMilliseconds + ($Seconds * 1000)
    while ($watch.Elapsed.TotalMilliseconds -lt $endMs -and -not $proc.HasExited) {
        & $DuringRun $hwnd $rect $iteration
        $ms = $watch.Elapsed.TotalMilliseconds
        $pr = New-Probe -Hwnd $hwnd -Ms $ms -TimeoutMs $ProbeTimeoutMs; if ($pr) { $probes.Add($pr) | Out-Null }
        $iteration++
        Start-Sleep -Milliseconds 12
    }

    $shot = $false
    if ($ScreenshotName) { $shot = Save-ProcessScreenshot -Hwnd $hwnd -Path (Join-Path $shotsDir $ScreenshotName) }

    [ZE.Win32]::SetForegroundWindow($hwnd) | Out-Null
    $proc.CloseMainWindow() | Out-Null
    $closed = $proc.WaitForExit(20000)
    if (-not $closed) { $proc.Kill() }
    $watch.Stop()
    Start-Sleep -Milliseconds 600

    $text = Get-LogDelta -From $logFrom
    $logFile = Join-Path $runLogDir "$Id.log"
    Set-Content -LiteralPath $logFile -Value $text -Encoding UTF8
    $steady = Get-BucketStats -Probes $probes -FromMs $ColdCutoffMs -ToMs ([double]::MaxValue)
    $cold = Get-BucketStats -Probes $probes -FromMs 0 -ToMs $ColdCutoffMs
    $log = Analyze-LogDelta -Text $text
    $row = New-ResultRow -Id $Id -Expect "interactive stress" -ExitCode $proc.ExitCode -Screenshot $shot `
        -Stats $steady -ColdHangs $cold.Hangs -ElapsedMs $watch.Elapsed.TotalMilliseconds -log $log -logFile $logFile
    $row | Add-Member -NotePropertyName Iterations -NotePropertyValue $iteration
    $row | Add-Member -NotePropertyName GracefulClose -NotePropertyValue $closed
    return $row
}

function Run-Morph {
    param([int]$Cycles)
    $morphLog = Join-Path $EvidenceDir ("e2e_morph_{0}_cycles.json" -f $Cycles)
    if (Test-Path -LiteralPath $morphLog) { Remove-Item -LiteralPath $morphLog -Force }
    $logFrom = Get-LogLength
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-ZeApp -ArgList @("--no-splash", "--e2e-morph", "$Cycles", "--morph-log", $morphLog)
    $hwnd = Wait-ForWindow -Process $proc -TimeoutSeconds 25
    $probes = New-Object System.Collections.Generic.List[object]
    while (-not $proc.HasExited -and $watch.Elapsed.TotalSeconds -lt (60 + $Cycles * 6)) {
        $ms = $watch.Elapsed.TotalMilliseconds
        if ($hwnd -ne [IntPtr]::Zero) { $pr = New-Probe -Hwnd $hwnd -Ms $ms -TimeoutMs $ProbeTimeoutMs; if ($pr) { $probes.Add($pr) | Out-Null } }
        Start-Sleep -Milliseconds $ProbeIntervalMs
    }
    $completed = $proc.HasExited
    if (-not $proc.HasExited) { $proc.Kill() }
    $proc.WaitForExit()
    $watch.Stop()
    Start-Sleep -Milliseconds 600
    $text = Get-LogDelta -From $logFrom
    $logFile = Join-Path $runLogDir "STRESS-MORPH.log"
    Set-Content -LiteralPath $logFile -Value $text -Encoding UTF8
    if ($TraceProbes) { $probes | Export-Csv -LiteralPath (Join-Path $runLogDir "STRESS-MORPH.probes.csv") -NoTypeInformation -Encoding UTF8 }
    $steady = Get-BucketStats -Probes $probes -FromMs $ColdCutoffMs -ToMs ([double]::MaxValue)
    $cold = Get-BucketStats -Probes $probes -FromMs 0 -ToMs $ColdCutoffMs
    $log = Analyze-LogDelta -Text $text
    $row = New-ResultRow -Id "STRESS-MORPH-$Cycles" -Expect "$Cycles rapid 3D<->2D transitions, no hang, valid final mode" `
        -ExitCode $proc.ExitCode -Screenshot $false -Stats $steady -ColdHangs $cold.Hangs -ElapsedMs $watch.Elapsed.TotalMilliseconds -log $log -logFile $logFile
    $row | Add-Member -NotePropertyName Iterations -NotePropertyValue $Cycles
    $row | Add-Member -NotePropertyName GracefulClose -NotePropertyValue $completed
    $morphJson = ""
    if (Test-Path -LiteralPath $morphLog) { $morphJson = Get-Content -LiteralPath $morphLog -Raw }
    $row | Add-Member -NotePropertyName MorphJson -NotePropertyValue $morphJson
    return $row
}

function Test-Selected {
    param([string]$Id)
    return (-not $ScenarioFilter) -or ($Id -like "*$ScenarioFilter*")
}

function Run-Stress {
    $results = New-Object System.Collections.Generic.List[object]

    if (Test-Selected "STRESS-MORPH") {
        Write-Host ("[stress] morph x{0} ..." -f $MorphCycles)
        $m = Run-Morph -Cycles $MorphCycles
        $results.Add($m) | Out-Null
        Write-Host ("         exit={0} p95={1}us p99={2}us max={3}us hangs={4} coldHangs={5} errors={6}" -f $m.ExitCode, $m.P95us, $m.P99us, $m.Maxus, $m.Hangs, $m.ColdHangs, $m.Errors)
    }

    $VK_HOME = 0x24
    $VK_F = 0x46
    $VK_LEFT = 0x25
    $VK_RIGHT = 0x27
    $VK_MENU = 0x12

    if (Test-Selected "STRESS-THEME") {
        $themeBlock = {
            param($h, $r, $i)
            [ZE.Win32]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
            [ZE.Win32]::keybd_event($VK_T, 0, 0, [UIntPtr]::Zero)
            [ZE.Win32]::keybd_event($VK_T, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            [ZE.Win32]::keybd_event($VK_CONTROL, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 260
        }
        Write-Host ("[stress] theme toggle x{0} ..." -f $ThemeToggles)
        $t = Invoke-StressInteractive -Id "STRESS-THEME-$ThemeToggles" -DuringRun $themeBlock `
            -Seconds ([math]::Ceiling($ThemeToggles * 0.32) + 2) -ScreenshotName "zearth_theme_toggle_live.png"
        $results.Add($t) | Out-Null
        Write-Host ("         p95={0} p99={1} max={2} hangs={3} errors={4} gracefulClose={5}" -f $t.P95us, $t.P99us, $t.Maxus, $t.Hangs, $t.Errors, $t.GracefulClose)
    }

    if (Test-Selected "STRESS-NAVKEYS") {
        $navBlock = {
            param($h, $r, $i)
            $step = $i % 4
            if ($step -eq 0) {
                [ZE.Win32]::keybd_event($VK_HOME, 0, 0, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_HOME, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            }
            elseif ($step -eq 1) {
                [ZE.Win32]::keybd_event($VK_F, 0, 0, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_F, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            }
            elseif ($step -eq 2) {
                [ZE.Win32]::keybd_event($VK_MENU, 0, 0, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_LEFT, 0, 0, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_LEFT, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_MENU, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            }
            else {
                [ZE.Win32]::keybd_event($VK_MENU, 0, 0, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_RIGHT, 0, 0, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_RIGHT, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
                [ZE.Win32]::keybd_event($VK_MENU, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            }
        }
        Write-Host "[stress] rapid nav keys (Home/FlyTo/Back/Forward) 30s ..."
        $n = Invoke-StressInteractive -Id "STRESS-NAVKEYS-30" -DuringRun $navBlock -Seconds 30 -ScreenshotName "zearth_after_navkeys.png"
        $results.Add($n) | Out-Null
        Write-Host ("         p95={0} p99={1} max={2} hangs={3} errors={4} gracefulClose={5}" -f $n.P95us, $n.P99us, $n.Maxus, $n.Hangs, $n.Errors, $n.GracefulClose)
    }

    if (Test-Selected "STRESS-DRAG") {
        # Genuine held-button drag: press, keep the button down across a sweep of moves, release.
        $dragBlock = {
            param($h, $r, $i)
            $cx = [int](($r.Left + $r.Right) / 2)
            $cy = [int](($r.Top + $r.Bottom) / 2)
            $phase = $i % 70
            $x = $cx + [int](300 * [math]::Sin($phase / 9.0))
            $y = $cy + [int](170 * [math]::Cos($phase / 11.0))
            if ($phase -eq 0) {
                [ZE.Win32]::SetCursorPos($cx, $cy) | Out-Null
                [ZE.Win32]::mouse_event($MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
            }
            elseif ($phase -lt 55) {
                [ZE.Win32]::SetCursorPos($x, $y) | Out-Null
            }
            elseif ($phase -eq 55) {
                [ZE.Win32]::mouse_event($MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
            }
        }
        Write-Host ("[stress] globe drag/hover {0}s ..." -f $DragSeconds)
        $d = Invoke-StressInteractive -Id "STRESS-DRAG-$DragSeconds" -DuringRun $dragBlock `
            -Seconds $DragSeconds -ScreenshotName "zearth_after_drag.png"
        $results.Add($d) | Out-Null
        Write-Host ("         p95={0} p99={1} max={2} hangs={3} errors={4} gracefulClose={5}" -f $d.P95us, $d.P99us, $d.Maxus, $d.Hangs, $d.Errors, $d.GracefulClose)
    }

    return $results
}

# ---------------------------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------------------------
$all = New-Object System.Collections.Generic.List[object]
if ($Mode -eq "matrix" -or $Mode -eq "all") { Run-Matrix | ForEach-Object { $all.Add($_) } }
if ($Mode -eq "stress" -or $Mode -eq "all") { Run-Stress | ForEach-Object { $all.Add($_) } }

$stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$suffix = if ($ScenarioFilter) { "-$ScenarioFilter" } else { "-$Mode" }
$jsonPath = Join-Path $EvidenceDir "task01-results$suffix.json"
$all | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$csvPath = Join-Path $EvidenceDir "task01-matrix$suffix.csv"
$all | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host ("Results written to {0} and {1}" -f $jsonPath, $csvPath)
Write-Host ("Run at {0}" -f $stamp)
