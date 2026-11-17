# Experimental TD Control gaze-aware foreground-window flag probe.
# OBSERVE is default and makes NO window changes.
# -Apply modifies ONLY the exact EyeGazeTrainer foreground window's EXSTYLE bit
# while this process runs. Requires supervised helper with keyboard/mouse.
# Does not open the gaze API, touch TD Control processes, or persist settings.
param(
    [switch]$Apply,
    [ValidateRange(10,300)][int]$DurationSeconds = 90
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows only.' }

# Documented by Tolt's draft interop specification as Tobii's gaze-aware
# indicator. On Windows bit 0x2 is also WS_EX_NOPARENTNOTIFY.
$TobiiGazeAware = 0x2
$GwlExStyle = -20

if (-not ('EyeGazeTrainerNativeWindowProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class EyeGazeTrainerNativeWindowProbe
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int count);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int count);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
'@
}

function Get-WindowIdentity([IntPtr]$h) {
    if ($h -eq [IntPtr]::Zero -or -not [EyeGazeTrainerNativeWindowProbe]::IsWindow($h)) {
        return $null
    }
    $titleBuffer = New-Object System.Text.StringBuilder 512
    $classBuffer = New-Object System.Text.StringBuilder 256
    [void][EyeGazeTrainerNativeWindowProbe]::GetWindowText($h, $titleBuffer, $titleBuffer.Capacity)
    [void][EyeGazeTrainerNativeWindowProbe]::GetClassName($h, $classBuffer, $classBuffer.Capacity)
    return [pscustomobject]@{ Title=$titleBuffer.ToString(); Class=$classBuffer.ToString(); Handle=$h }
}

function Test-OwnWindow($window) {
    if ($null -eq $window) { return $false }
    # The installed UWP package currently declares DisplayName "Eye Gaze API Viability".
    # A later package rename requires an explicit review of this narrow allowlist.
    return $window.Title -ceq 'Eye Gaze API Viability' -and
           $window.Class -ceq 'ApplicationFrameWindow'
}

$changed = [IntPtr]::Zero
$alreadySetWindow = [IntPtr]::Zero
$lastReported = ''
$lastForegroundDescriptor = ''
$deadline = [DateTime]::UtcNow.AddSeconds($DurationSeconds)
Write-Host "EyeGazeTrainer / TD Control overlay probe"
Write-Host ("Mode: " + $(if ($Apply) { 'APPLY (experimental window-style flag)' } else { 'OBSERVE ONLY (no changes)' }))
Write-Host "Target: foreground ApplicationFrameWindow titled exactly 'Eye Gaze API Viability'"
Write-Host "Maximum duration: $DurationSeconds seconds. Press Ctrl+C to stop."
Write-Host 'This does not hide TD Control directly; it only advertises gaze-awareness to compatible software.'
try {
    while ([DateTime]::UtcNow -lt $deadline) {
        $h = [EyeGazeTrainerNativeWindowProbe]::GetForegroundWindow()
        $identity = Get-WindowIdentity $h
        $isTarget = Test-OwnWindow $identity

        # If the target ceases to be foreground, remove ONLY the bit we set.
        if ($changed -ne [IntPtr]::Zero -and ($h -ne $changed -or -not $isTarget)) {
            if ([EyeGazeTrainerNativeWindowProbe]::IsWindow($changed)) {
                $style = [EyeGazeTrainerNativeWindowProbe]::GetWindowLong($changed, $GwlExStyle)
                if (($style -band $TobiiGazeAware) -ne 0) {
                    [void][EyeGazeTrainerNativeWindowProbe]::SetWindowLong(
                        $changed, $GwlExStyle, ($style -band (-bnot $TobiiGazeAware)))
                    Write-Host 'Restored original gaze-aware bit (focus lost).'
                }
            }
            $changed = [IntPtr]::Zero
        }

        if ($alreadySetWindow -ne [IntPtr]::Zero -and $alreadySetWindow -ne $h) {
            $alreadySetWindow = [IntPtr]::Zero
        }

        if ($isTarget) {
            if ($lastReported -ne 'target') {
                Write-Host ("Target active: " + $identity.Title + " [" + $identity.Class + "]")
                $lastReported = 'target'
            }
            $style = [EyeGazeTrainerNativeWindowProbe]::GetWindowLong($h, $GwlExStyle)
            if ($Apply -and $changed -eq [IntPtr]::Zero -and $alreadySetWindow -ne $h) {
                if (($style -band $TobiiGazeAware) -ne 0) {
                    $alreadySetWindow = $h
                    Write-Host 'Bit already set: leaving window style unchanged.'
                }
                else {
                    [void][EyeGazeTrainerNativeWindowProbe]::SetWindowLong(
                        $h, $GwlExStyle, ($style -bor $TobiiGazeAware))
                    $verify = [EyeGazeTrainerNativeWindowProbe]::GetWindowLong($h, $GwlExStyle)
                    if (($verify -band $TobiiGazeAware) -ne 0) {
                        $changed = $h
                        Write-Host 'Applied experimental gaze-aware bit to EyeGazeTrainer foreground window.'
                    }
                    else { Write-Warning 'Window-style write did not stick; no change to TD Control attempted.' }
                }
            }
        }
        else {
            # Read-only identification of the foreground UWP host. A strict
            # title mismatch is a common reason the experimental flag does
            # not get applied at all. Avoid logging unrelated window titles.
            $description = if ($null -eq $identity) { 'No foreground window' }
                elseif ($identity.Class -ceq 'ApplicationFrameWindow') {
                    "Foreground UWP window: title='" + $identity.Title +
                    "', class='" + $identity.Class + "'"
                }
                else { "Other foreground window class='" + $identity.Class + "'" }
            if ($description -ne $lastForegroundDescriptor) {
                Write-Host $description
                $lastForegroundDescriptor = $description
            }
            if ($lastReported -ne 'other') {
                Write-Host 'Waiting for EyeGazeTrainer title and class match. No window style changed.'
                $lastReported = 'other'
            }
        }
        Start-Sleep -Milliseconds 250
    }
}
finally {
    if ($changed -ne [IntPtr]::Zero -and [EyeGazeTrainerNativeWindowProbe]::IsWindow($changed)) {
        $style = [EyeGazeTrainerNativeWindowProbe]::GetWindowLong($changed, $GwlExStyle)
        if (($style -band $TobiiGazeAware) -ne 0) {
            [void][EyeGazeTrainerNativeWindowProbe]::SetWindowLong(
                $changed, $GwlExStyle, ($style -band (-bnot $TobiiGazeAware)))
            Write-Host 'Restored original gaze-aware bit (probe ended).'
        }
    }
    Write-Host 'Probe finished. TD Control processes and preferences were not changed.'
}
