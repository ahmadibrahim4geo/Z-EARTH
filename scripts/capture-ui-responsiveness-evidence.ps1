# Captures genuine screenshots of the running Z EARTH desktop application for the
# UI-responsiveness acceptance evidence. Unlike the built-in --capture switch (which uses
# CopyFromScreen and can photograph whatever window happens to be foreground), this script
# explicitly brings the Z EARTH main window to the foreground before capturing, and records
# the window title/process that was actually foreground at capture time.
param(
    [string]$ExePath = "src\ZEarth.App\bin\Release\net10.0-windows\Z EARTH.exe",
    [string]$OutDir = "Development Reports\UI_Responsiveness_Fix\screenshots",
    [int]$StartupSeconds = 20,
    [int]$RenderSeconds = 12,
    [string]$InvokeAutomationName = "",
    [int]$PostInvokeSeconds = 6,
    [string]$OutputName = "zearth_running_window.png"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Win32Capture {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static string Title(IntPtr h) {
        var sb = new StringBuilder(512);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }
}
"@

if (-not (Test-Path -LiteralPath $ExePath)) { throw "Executable not found: $ExePath" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$exe = (Resolve-Path -LiteralPath $ExePath).Path
$proc = Start-Process -FilePath $exe -ArgumentList @('--no-splash') -PassThru

try {
    $deadline = (Get-Date).AddSeconds($StartupSeconds)
    while ((Get-Date) -lt $deadline -and $proc.MainWindowHandle -eq 0) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
    }

    if ($proc.MainWindowHandle -eq 0) { throw "Z EARTH main window never appeared." }

    $hwnd = $proc.MainWindowHandle
    if ([Win32Capture]::IsIconic($hwnd)) { [Win32Capture]::ShowWindow($hwnd, 9) | Out-Null }  # SW_RESTORE
    [Win32Capture]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Seconds 2
    [Win32Capture]::SetForegroundWindow($hwnd) | Out-Null

    Write-Output "Waiting ${RenderSeconds}s for the globe to render..."
    Start-Sleep -Seconds $RenderSeconds

    if ($InvokeAutomationName -ne "") {
        Write-Output "Invoking UI Automation element named '$InvokeAutomationName'..."
        Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $InvokeAutomationName)
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -eq $element) { throw "No automation element named '$InvokeAutomationName' was found." }
        $invoked = $false
        foreach ($pat in @([System.Windows.Automation.InvokePattern]::Pattern, [System.Windows.Automation.TogglePattern]::Pattern, [System.Windows.Automation.SelectionItemPattern]::Pattern)) {
            if ($element.TryGetCurrentPattern($pat, [ref]$null)) {
                $p = $element.GetCurrentPattern($pat)
                if ($pat -eq [System.Windows.Automation.InvokePattern]::Pattern) { $p.Invoke() }
                elseif ($pat -eq [System.Windows.Automation.TogglePattern]::Pattern) { $p.Toggle() }
                else { $p.Select() }
                $invoked = $true
                break
            }
        }
        if (-not $invoked) {
            Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Input {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
"@
            $pt = $element.GetClickablePoint()
            [Win32Input]::SetCursorPos([int]$pt.X, [int]$pt.Y) | Out-Null
            Start-Sleep -Milliseconds 300
            [Win32Input]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)  # LEFTDOWN
            [Win32Input]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)  # LEFTUP
            Write-Output "Clicked at $($pt.X),$($pt.Y) (no automation pattern available)."
        }
        else {
            Write-Output "Invoked via automation pattern."
        }
        Write-Output "Waiting ${PostInvokeSeconds}s..."
        Start-Sleep -Seconds $PostInvokeSeconds
    }

    $fg = [Win32Capture]::GetForegroundWindow()
    $fgTitle = [Win32Capture]::Title($fg)
    Write-Output "Foreground window at capture: '$fgTitle'"

    $rect = New-Object Win32Capture+RECT
    [Win32Capture]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    Write-Output "Z EARTH window rect: $($rect.Left),$($rect.Top) ${width}x${height}"

    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $target = Join-Path $OutDir $OutputName
    $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose()
    $bmp.Dispose()

    Write-Output "Saved: $target"
    Write-Output "Foreground matches Z EARTH window: $($fg -eq $hwnd)"
}
finally {
    if (-not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 3
    }
    if (-not $proc.HasExited) { $proc.Kill() }
}
