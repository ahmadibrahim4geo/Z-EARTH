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
public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
public struct RECT { public int Left, Top, Right, Bottom; }
"@
Add-Type -MemberDefinition $signature -Name "Win32CapturePW" -Namespace "Win32_PW"

$proc = Start-Process -FilePath "D:\My Software\Z EARTH\dist\Z EARTH-Preview\Z EARTH.exe" -PassThru
Start-Sleep -Seconds 7

$mainHwnd = [IntPtr]::Zero
$mainRect = New-Object Win32_PW.Win32CapturePW+RECT

[Win32_PW.Win32CapturePW]::EnumWindows({
    param($hwnd, $lparam)
    $procId = 0
    [Win32_PW.Win32CapturePW]::GetWindowThreadProcessId($hwnd, [ref]$procId)
    if ($procId -eq $proc.Id) {
        $sb = New-Object System.Text.StringBuilder 256
        [Win32_PW.Win32CapturePW]::GetWindowText($hwnd, $sb, 256) | Out-Null
        if ($sb.ToString() -like "*Z EARTH Desktop GIS*") {
            $script:mainHwnd = $hwnd
            [Win32_PW.Win32CapturePW]::GetWindowRect($hwnd, [ref]$script:mainRect) | Out-Null
        }
    }
    return $true
}, [IntPtr]::Zero)

if ($mainHwnd -ne [IntPtr]::Zero) {
    $width = $mainRect.Right - $mainRect.Left
    $height = $mainRect.Bottom - $mainRect.Top
    Write-Host "Capturing window via PrintWindow: ${width}x${height}"

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try {
        $ok = [Win32_PW.Win32CapturePW]::PrintWindow($mainHwnd, $hdc, 2)
        Write-Host "PrintWindow returned: $ok"
    } finally {
        $graphics.ReleaseHdc($hdc)
    }

    $outPath = "D:\My Software\Z EARTH\dist\ZEarth_Window_Capture.png"
    $bitmap.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Saved window capture to: $outPath (Size: $((Get-Item $outPath).Length) bytes)"
} else {
    Write-Host "Main window not found."
}

Stop-Process -Id $proc.Id -Force
