# Read-only diagnostic: identify the owner of a persistent blank window.
# Run AFTER exiting EyeGazeTrainer, while the blank white window is still visible.
# No windows are activated, moved, closed, hidden, or modified.
# Output includes window titles, which might contain private information.
$ErrorActionPreference = 'Stop'

if (-not ('EyeGazeTrainerWindowSurvey' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class EyeGazeTrainerWindowSurvey
{
    public delegate bool WindowCallback(IntPtr handle, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern int GetClassName(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out RECT rectangle);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
'@
}

$foreground = [EyeGazeTrainerWindowSurvey]::GetForegroundWindow()
$records = New-Object 'System.Collections.Generic.List[object]'

$callback = [EyeGazeTrainerWindowSurvey+WindowCallback] {
    param([IntPtr]$handle, [IntPtr]$unused)
    if (-not [EyeGazeTrainerWindowSurvey]::IsWindowVisible($handle)) {
        return $true
    }
    if ([EyeGazeTrainerWindowSurvey]::IsIconic($handle)) {
        return $true
    }

    $rect = New-Object 'EyeGazeTrainerWindowSurvey+RECT'
    if (-not [EyeGazeTrainerWindowSurvey]::GetWindowRect($handle, [ref]$rect)) {
        return $true
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -lt 350 -or $height -lt 220) {
        return $true
    }

    [uint32]$pid = 0
    [void][EyeGazeTrainerWindowSurvey]::GetWindowThreadProcessId($handle, [ref]$pid)
    $title = New-Object System.Text.StringBuilder 1024
    $className = New-Object System.Text.StringBuilder 256
    [void][EyeGazeTrainerWindowSurvey]::GetWindowText($handle, $title, $title.Capacity)
    [void][EyeGazeTrainerWindowSurvey]::GetClassName($handle, $className, $className.Capacity)

    $processName = '<unavailable>'
    try {
        $processName = (Get-Process -Id $pid -ErrorAction Stop).ProcessName
    } catch {}

    $records.Add([pscustomobject]@{
        Foreground = ($handle -eq $foreground)
        Process = $processName
        PID = $pid
        WindowClass = $className.ToString()
        Title = $title.ToString()
        Handle = ('0x{0:X}' -f $handle.ToInt64())
        X = $rect.Left
        Y = $rect.Top
        Width = $width
        Height = $height
    })
    return $true
}
[void][EyeGazeTrainerWindowSurvey]::EnumWindows($callback, [IntPtr]::Zero)

$relevant = @($records | Sort-Object -Property @{Expression='Foreground';Descending=$true}, @{Expression='Width';Descending=$true})
Write-Host 'EyeGazeTrainer blank-window survey (read-only):'
$relevant | Format-Table -AutoSize -Wrap | Out-Host
$path = Join-Path $env:TEMP 'EyeGazeTrainer-window-survey.csv'
$relevant | Export-Csv -Path $path -Encoding UTF8 -NoTypeInformation
Write-Host ('Saved: ' + $path)
Write-Host 'Note: titles can contain private information. Review/redact before sharing.'
