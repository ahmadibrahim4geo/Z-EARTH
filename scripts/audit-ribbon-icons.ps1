# Z EARTH: native WPF UI Automation icon audit.
# Evidence levels: discovered, invoked, and *separately* verified scene effect.
# Native graphical Windows only. Never claim that InvokePattern proves a Cesium render.
param(
    [string]$ExePath = "dist/Z EARTH-Preview/Z EARTH.exe",
    [string]$OutputDir = "Development Reports/Icon_UIA_Evidence",
    [int]$StartupTimeoutSeconds = 45,
    [switch]$InvokeSafe,
    [switch]$CaptureScreenshots
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
$script:summary = [ordered]@{
    ReportKind = "Real Windows UI Automation - WPF main window"
    StartedUtc = [DateTime]::UtcNow.ToString("o")
    Result = "BLOCKED"
    Reason = ""
    Machine = $env:COMPUTERNAME
    InteractiveSession = [Environment]::UserInteractive
    SourceControls = 0
    DiscoveredControls = 0
    InvokedControls = 0
    FunctionalEffectsConfirmed = 0
    Controls = @()
    Tabs = @()
    Screenshots = @()
}
$sourcePath = "src/ZEarth.App/Views/MainWindow.xaml"
if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Run from Z EARTH repository root." }
$xaml = Get-Content -LiteralPath $sourcePath -Raw
$sourceMatches = [regex]::Matches($xaml, '(?s)<(?:Button|ToggleButton|RadioButton)\b[^>]*?\bAutomationProperties\.AutomationId="(ZE\.Main\.[^"]+)"[^>]*>')
$source = [ordered]@{}
foreach ($m in $sourceMatches) {
    $id = $m.Groups[1].Value
    if ($source.Contains($id)) { throw "Duplicate AutomationId: $id" }
    $source[$id] = [ordered]@{
        Id = $id
        Command = ($id -split '\.')[-1]
        Found = $false
        Name = ""
        Tab = ""
        Status = "NOT_FOUND_IN_UIA"
        Proof = "NONE"
        Error = ""
        Screenshot = ""
    }
}
$script:summary.SourceControls = $source.Count
$proc = $null
try {
    if (-not [Environment]::UserInteractive) { throw "Windows runner has no interactive desktop session." }
    if (-not (Test-Path -LiteralPath $ExePath)) { throw "Executable not found: $ExePath" }
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    Add-Type -AssemblyName System.Drawing
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ZEarthUIWin32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT value);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
    $resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
    $proc = Start-Process -FilePath $resolvedExe -ArgumentList @('--no-splash') -PassThru
    $script:summary.ProcessId = $proc.Id
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $hwnd = [IntPtr]::Zero
    while ((Get-Date) -lt $deadline) {
        $proc.Refresh()
        if ($proc.HasExited) { throw "Program exited before opening a window. ExitCode=$($proc.ExitCode)" }
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $proc.MainWindowHandle; break }
        Start-Sleep -Milliseconds 400
    }
    if ($hwnd -eq [IntPtr]::Zero) { throw "Main WPF window did not appear within timeout." }
    Start-Sleep -Seconds 5
    $main = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    if ($null -eq $main) { throw "Windows UIA could not access the WPF main window." }
    $script:summary.WindowTitle = $main.Current.Name
    # Verify that the actual launched window fits the physical Windows work area.
    # A few pixels of DWM invisible resize border are tolerated.
    Add-Type -AssemblyName System.Windows.Forms
    $monitor = [System.Windows.Forms.Screen]::FromHandle($hwnd)
    $windowRect = New-Object ZEarthUIWin32+RECT
    if ([ZEarthUIWin32]::GetWindowRect($hwnd, [ref]$windowRect)) {
        $area = $monitor.WorkingArea
        $script:summary.WindowBoundsPx = [ordered]@{
            Left = $windowRect.Left; Top = $windowRect.Top
            Right = $windowRect.Right; Bottom = $windowRect.Bottom
        }
        $script:summary.WorkAreaPx = [ordered]@{
            Left = $area.Left; Top = $area.Top
            Right = $area.Right; Bottom = $area.Bottom
        }
        $tolerance = 10
        $script:summary.WindowFullyVisible =
            ($windowRect.Left -ge ($area.Left - $tolerance)) -and
            ($windowRect.Top -ge ($area.Top - $tolerance)) -and
            ($windowRect.Right -le ($area.Right + $tolerance)) -and
            ($windowRect.Bottom -le ($area.Bottom + $tolerance))
    }
    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $radioCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::RadioButton)
    $winCondition = [System.Windows.Automation.OrCondition]::new($buttonCondition, $radioCondition)
    $tabCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TabItem)
    $safeCommands = @(
        "HomeCommand", "FullExtentCommand", "PreviousExtentCommand", "NextExtentCommand",
        "ZoomInCommand", "ZoomOutCommand", "ResetNorthCommand", "ResetPitchCommand",
        "Set2DModeCommand", "Set3DModeCommand", "ToggleGraticulesCommand",
        "ToggleSunlightCommand", "ToggleAtmosphereCommand", "SetCompassModeCommand",
        "ToggleThemeCommand", "ClearMeasurementCommand", "Is3DMode", "Is2DMode",
        "ToggleSidebarDockCommand", "ExpandAllSectionsCommand", "CollapseAllSectionsCommand",
        "ClearSearchCommand", "ToggleCoordinateFormatCommand",
        "SetVerticalExaggerationCommand", "ResetVerticalExaggerationCommand",
        "MeasureDistanceCommand", "MeasureAreaCommand", "ToggleTerrainCommand",
        "ToggleLanguageCommand"
    )
    $screenshotCommands = @(
        "Set2DModeCommand", "Set3DModeCommand", "ToggleGraticulesCommand",
        "ToggleSunlightCommand", "ToggleAtmosphereCommand", "ToggleThemeCommand",
        "ToggleTerrainCommand", "ToggleLanguageCommand", "SetVerticalExaggerationCommand"
    )
    $invokedIds = New-Object 'System.Collections.Generic.HashSet[string]'
    function Get-AllControls {
        param([string]$TabName)
        $items = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, $winCondition)
        for ($k = 0; $k -lt $items.Count; $k++) {
            $el = $items.Item($k)
            try {
                $id = $el.Current.AutomationId
                if (-not $source.Contains($id)) { continue }
                $entry = $source[$id]
                $entry.Found = $true
                $entry.Name = $el.Current.Name
                $entry.Tab = $TabName
                if ($entry.Status -eq "NOT_FOUND_IN_UIA") { $entry.Status = "FOUND_NOT_INVOKED" }
                if (-not $InvokeSafe) { continue }
                $cmd = $entry.Command
                if (-not ($safeCommands -contains $cmd)) { continue }
                if ($invokedIds.Contains($id)) { continue }
                if (-not $el.Current.IsEnabled -or $el.Current.IsOffscreen) { continue }
                $null = $invokedIds.Add($id)
                try {
                    $pattern = $null
                    if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
                        $pattern.Invoke()
                    }
                    elseif ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
                        $pattern.Select()
                    }
                    elseif ($el.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
                        $pattern.Toggle()
                    }
                    else {
                        $entry.Status = "NO_INVOKE_PATTERN"
                        continue
                    }
                    $entry.Status = "INVOKED_EFFECT_UNVERIFIED"
                    $entry.Proof = "WINDOWS_UIA_PATTERN_INVOKED"
                    if ($screenshotCommands -contains $cmd) {
                        Start-Sleep -Milliseconds 700
                        Save-WindowScreenshot -name ("action-" + ($id -replace '[^a-zA-Z0-9_-]', '_'))
                    }
                    else {
                        Start-Sleep -Milliseconds 300
                    }
                }
                catch {
                    $entry.Status = "INVOKE_FAILED"
                    $entry.Error = $_.Exception.Message
                }
            }
            catch {
                # Stale UIA peers may disappear during scene/tab changes.
                # Preserve already-collected evidence and continue.
            }
        }
    }
    function Save-WindowScreenshot {
        param([string]$name)
        if (-not $CaptureScreenshots) { return }
        try {
            $null = [ZEarthUIWin32]::SetForegroundWindow($hwnd)
            Start-Sleep -Milliseconds 350
            if ([ZEarthUIWin32]::GetForegroundWindow() -ne $hwnd) { return }
            $rect = New-Object ZEarthUIWin32+RECT
            if (-not [ZEarthUIWin32]::GetWindowRect($hwnd, [ref]$rect)) { return }
            $w = $rect.Right - $rect.Left
            $h = $rect.Bottom - $rect.Top
            if ($w -lt 300 -or $h -lt 200) { return }
            $bitmap = New-Object System.Drawing.Bitmap($w, $h)
            $gfx = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
                $filename = "$name.png"
                $bitmap.Save((Join-Path $OutputDir $filename), [System.Drawing.Imaging.ImageFormat]::Png)
                $script:summary.Screenshots += $filename
            }
            finally { $gfx.Dispose(); $bitmap.Dispose() }
        }
        catch { }
    }
    Get-AllControls -TabName "MainWindow"
    Save-WindowScreenshot -name "00-main"
    $tabs = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition)
    for ($i = 0; $i -lt $tabs.Count; $i++) {
        try {
            $tab = $tabs.Item($i)
            $tabName = $tab.Current.Name
            $pattern = $null
            if (-not $tab.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { continue }
            if (-not $tab.Current.IsEnabled) { continue }
            $pattern.Select()
            Start-Sleep -Milliseconds 450
            $script:summary.Tabs += $tabName
            Get-AllControls -TabName $tabName
            Save-WindowScreenshot -name ("tab-{0:00}" -f $i)
        }
        catch { }
    }
    $script:summary.Result = "UIA_COMPLETED_NOT_FULL_FUNCTIONAL_CERTIFICATION"
    $script:summary.Reason = "Only safe UIA actions are invoked; globe rendering, dialogs and destructive workflows require separate evidence."
}
catch {
    $script:summary.Result = "UIA_BLOCKED_OR_FAILED"
    $script:summary.Reason = $_.Exception.Message
}
finally {
    if ($null -ne $proc) {
        try { $proc.Refresh(); if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } } catch { }
    }
    $script:summary.Controls = @($source.Values)
    $script:summary.DiscoveredControls = @($source.Values | Where-Object { $_.Found }).Count
    $script:summary.InvokedControls = @($source.Values | Where-Object { $_.Status -eq 'INVOKED_EFFECT_UNVERIFIED' }).Count
    $script:summary.CompletedUtc = [DateTime]::UtcNow.ToString("o")
    $reportPath = Join-Path $OutputDir "icon-uia-results.json"
    $script:summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Output "UIA audit: $($script:summary.Result). Found $($script:summary.DiscoveredControls)/$($script:summary.SourceControls), invoked $($script:summary.InvokedControls); native functional effects confirmed $($script:summary.FunctionalEffectsConfirmed)."
    Write-Output "Evidence: $reportPath"
}
if ($script:summary.Result -eq "UIA_BLOCKED_OR_FAILED") { exit 3 }
