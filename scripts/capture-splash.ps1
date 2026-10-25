Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$signature = @'
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
public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
public struct RECT { public int Left, Top, Right, Bottom; }
'@
Add-Type -MemberDefinition $signature -Name "Win32CaptureSplash" -Namespace "Win32_Splash"

$proc = Start-Process -FilePath "D:\My Software\Z EARTH\dist\Z EARTH-Preview\Z EARTH.exe" -PassThru
Start-Sleep -Milliseconds 800

$splashHwnd = [IntPtr]::Zero
$splashRect = New-Object Win32_Splash.Win32CaptureSplash+RECT

[Win32_Splash.Win32CaptureSplash]::EnumWindows({
    param($hwnd, $lparam)
    $procId = 0
    [Win32_Splash.Win32CaptureSplash]::GetWindowThreadProcessId($hwnd, [ref]$procId)
    if ($procId -eq $proc.Id) {
        $sb = New-Object System.Text.StringBuilder 256
        [Win32_Splash.Win32CaptureSplash]::GetWindowText($hwnd, $sb, 256) | Out-Null
        if ($sb.ToString() -like "*Loading*") {
            $script:splashHwnd = $hwnd
            [Win32_Splash.Win32CaptureSplash]::GetWindowRect($hwnd, [ref]$script:splashRect) | Out-Null
        }
    }
    return $true
}, [IntPtr]::Zero)

if ($splashHwnd -ne [IntPtr]::Zero) {
    $width = $splashRect.Right - $splashRect.Left
    $height = $splashRect.Bottom - $splashRect.Top
    Write-Host "Capturing splash window via PrintWindow: ${width}x${height}"

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try {
        $ok = [Win32_Splash.Win32CaptureSplash]::PrintWindow($splashHwnd, $hdc, 2)
        Write-Host "PrintWindow returned: $ok"
    } finally {
        $graphics.ReleaseHdc($hdc)
    }

    $outPath = "D:\My Software\Z EARTH\dist\ZEarth_Splash_Capture.png"
    $bitmap.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Saved splash capture to: $outPath (Size: $((Get-Item $outPath).Length) bytes)"
} else {
    Write-Host "Splash window not found."
}

Stop-Process -Id $proc.Id -Force
