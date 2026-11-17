# Experimental TD Control gaze-aware foreground-window flag probe.
# OBSERVE is default and makes NO window changes.
# -Apply modifies ONLY the exact EyeGazeTrainer foreground window's EXSTYLE bit
# while this process runs. Requires supervised helper with keyboard/mouse.
# Does not open the gaze API, touch TD Control processes, or persist settings.
param(
    [switch]$Apply,
    [switch]$Launch,
    [ValidateRange(10,28800)][int]$DurationSeconds = 90
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

# -Launch converts the validated interop probe into a single-command
# launcher. It must be explicitly paired with -Apply; no gaze APIs are opened
# here and the UWP app retains its own controlled Exit sequence.
if ($Launch -and -not $Apply) { throw '-Launch requires -Apply.' }
if ($Launch) {
    $packages = @(Get-AppxPackage -Name 'EyeGazeApiViability')
    if ($packages.Count -ne 1) {
        throw "Expected one installed EyeGazeApiViability UWP package, found $($packages.Count). No app launched."
    }
    $appId = $packages[0].PackageFamilyName + '!App'
    Write-Host ("Launching installed EyeGazeTrainer package: " + $appId)
    # Shell AppsFolder activation uses the normal app startup and does not
    # require elevated permissions or modify package registration.
    Start-Process -FilePath explorer.exe -ArgumentList ("shell:AppsFolder\\" + $appId)
}

$changed = [IntPtr]::Zero
$alreadySetWindow = [IntPtr]::Zero
$lastReported = ''
$lastForegroundDescriptor = ''
$seenApp = $false
$seenHwnd = [IntPtr]::Zero
$start = [DateTime]::UtcNow
$lostSince = [DateTime]::MinValue
$deadline = [DateTime]::UtcNow.AddSeconds($DurationSeconds)
Write-Host "EyeGazeTrainer / TD Control overlay probe"
Write-Host ("Mode: " + $(if ($Apply) { 'APPLY (experimental window-style flag)' } else { 'OBSERVE ONLY (no changes)' }))
Write-Host "Target: foreground ApplicationFrameWindow titled exactly 'Eye Gaze API Viability'"
Write-Host "Maximum duration: $DurationSeconds seconds. Press Ctrl+C to stop."
Write-Host 'This does not hide TD Control directly; it only advertises gaze-awareness to compatible software.'
if ($Launch) {
    Write-Host 'Automatic launch mode: overlay flag follows app focus; launcher ends after app window closes.'
}
try {
    while ([DateTime]::UtcNow -lt $deadline) {
        $h = [EyeGazeTrainerNativeWindowProbe]::GetForegroundWindow()
        $identity = Get-WindowIdentity $h
        $isTarget = Test-OwnWindow $identity

        if ($Launch) {
            if ($isTarget) {
                $seenApp = $true
                $seenHwnd = $h
                $lostSince = [DateTime]::MinValue
            }
            elseif ($seenApp) {
                # Ordinary focus changes do not end the helper: the UWP window
                # is still alive even while another window is foreground.
                if (-not [EyeGazeTrainerNativeWindowProbe]::IsWindow($seenHwnd)) {
                    if ($lostSince -eq [DateTime]::MinValue) {
                        $lostSince = [DateTime]::UtcNow
                    }
                    elseif (([DateTime]::UtcNow - $lostSince).TotalSeconds -ge 2) {
                        Write-Host 'EyeGazeTrainer window closed; ending overlay helper.'
                        break
                    }
                }
                else {
                    $lostSince = [DateTime]::MinValue
                }
            }
            elseif (([DateTime]::UtcNow - $start).TotalSeconds -ge 40) {
                Write-Warning 'The UWP window was not detected within 40 seconds. No change to TD Control.'
                break
            }
        }

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
