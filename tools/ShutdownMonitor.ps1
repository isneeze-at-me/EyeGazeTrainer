# Read-only independent observer; run before opening the trainer.
[CmdletBinding()]
param([int]$PostExitSeconds = 60, [int]$WaitForLaunchSeconds = 300)
$ErrorActionPreference = 'Stop'
$directory = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'EyeGazeTrainer\Diagnostics'
[IO.Directory]::CreateDirectory($directory) | Out-Null
$log = Join-Path $directory ('Shutdown-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.txt')
function Write-Trace([string]$message) {
    $line = [DateTimeOffset]::Now.ToString('o') + ' | MONITOR | ' + $message + [Environment]::NewLine
    $bytes = [Text.Encoding]::UTF8.GetBytes($line)
    $file = [IO.FileStream]::new($log,[IO.FileMode]::Append,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite)
    try { $file.Write($bytes,0,$bytes.Length); $file.Flush($true) } finally { $file.Dispose() }
}
$offsets = @{}
$started = [DateTime]::UtcNow
$packageFolders = @(Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter 'EyeGazeApiViability_*' -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'LocalState' })
function Copy-NewTraceLines {
    $sources = @()
    foreach ($folder in $packageFolders) {
        $sources += @(Get-ChildItem $folder -Filter 'shutdown-*.txt' -File -ErrorAction SilentlyContinue)
    }
    $sources += @(Get-ChildItem $directory -Filter 'overlay-*.txt' -File -ErrorAction SilentlyContinue)
    foreach ($source in $sources) {
        if ($source.LastWriteTimeUtc -lt $started.AddSeconds(-2)) { continue }
        try {
            $f = [IO.FileStream]::new($source.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
            try {
                $position = if ($offsets.ContainsKey($source.FullName)) { [long]$offsets[$source.FullName] } else { 0L }
                if ($position -gt $f.Length) { $position = 0 }
                [void]$f.Seek($position,[IO.SeekOrigin]::Begin)
                # Consume complete lines only; a concurrent writer may be mid-write.
                $buffer = [IO.MemoryStream]::new()
                try {
                    $f.CopyTo($buffer)
                    $bytes = $buffer.ToArray()
                    $lastNewline = [Array]::LastIndexOf($bytes,[byte]10)
                    if ($lastNewline -ge 0) {
                        $chunk = [Text.Encoding]::UTF8.GetString($bytes,0,$lastNewline+1)
                        foreach ($line in ($chunk -split '\r?\n')) {
                            if ($line) { Write-Trace ('SOURCE ' + $source.Name + ' | ' + $line) }
                        }
                        $offsets[$source.FullName] = $position + $lastNewline + 1
                    }
                } finally { $buffer.Dispose() }
            } finally { $f.Dispose() }
        } catch { Write-Trace ('Trace read error: ' + $_.Exception.Message) }
    }
}
# Read-only Win32 snapshots. No titles, input injection, or window changes.
$windowProbeReady = $false
try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class TrainerWindowProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    public delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    public static IntPtr[] TopLevelWindows() {
        var windows = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h,d) => { windows.Add(h); return true; }, IntPtr.Zero);
        return windows.ToArray();
    }
}
'@
    $windowProbeReady = $true
} catch { Write-Trace ('Window probe unavailable: ' + $_.Exception.Message) }

function Get-WindowSnapshot([IntPtr]$handle) {
    if ($handle -eq [IntPtr]::Zero) { return 'none' }
    [uint32]$ownerId = 0
    [void][TrainerWindowProbe]::GetWindowThreadProcessId($handle,[ref]$ownerId)
    $owner = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
    $name = if ($owner) { $owner.ProcessName } else { 'unknown' }
    if ($owner) { $owner.Dispose() }
    $class = [Text.StringBuilder]::new(256)
    [void][TrainerWindowProbe]::GetClassName($handle,$class,$class.Capacity)
    $rect = New-Object TrainerWindowProbe+RECT
    $bounds = if ([TrainerWindowProbe]::GetWindowRect($handle,[ref]$rect)) {
        '{0},{1},{2},{3}' -f $rect.Left,$rect.Top,$rect.Right,$rect.Bottom
    } else { 'unknown' }
    [int]$cloaked = 0
    $cloakState = if ([TrainerWindowProbe]::DwmGetWindowAttribute($handle,14,[ref]$cloaked,4) -eq 0) { [string]$cloaked } else { 'unknown' }
    'hwnd=0x{0:X} owner={1} pid={2} class={3} visible={4} minimized={5} maximized={6} cloaked={7} bounds={8}' -f $handle.ToInt64(),$name,$ownerId,$class.ToString(),[TrainerWindowProbe]::IsWindowVisible($handle),[TrainerWindowProbe]::IsIconic($handle),[TrainerWindowProbe]::IsZoomed($handle),$cloakState,$bounds
}
function Get-WindowState($processes) {
    if (-not $windowProbeReady) { return 'Window probe unavailable' }
    $foreground = [TrainerWindowProbe]::GetForegroundWindow()
    $shell = [TrainerWindowProbe]::GetShellWindow()
    $knownIds = @($processes | ForEach-Object { [uint32]$_.Id })
    $windows = foreach ($handle in [TrainerWindowProbe]::TopLevelWindows()) {
        [uint32]$ownerId = 0
        [void][TrainerWindowProbe]::GetWindowThreadProcessId($handle,[ref]$ownerId)
        $class = [Text.StringBuilder]::new(256)
        [void][TrainerWindowProbe]::GetClassName($handle,$class,$class.Capacity)
        if ($knownIds -contains $ownerId -or ($class.ToString() -eq 'ApplicationFrameWindow' -and [TrainerWindowProbe]::IsWindowVisible($handle))) {
            Get-WindowSnapshot $handle
        }
    }
    'FOREGROUND: ' + (Get-WindowSnapshot $foreground) + ' | SHELL: ' + (Get-WindowSnapshot $shell) + ' | OBSERVED WINDOWS: ' + (($windows | Sort-Object) -join '; ')
}
function Get-ObservedProcesses {
    @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match 'EyeGazeApiViability|EyeGazeTrainer|OverlayCompanion|Tobii|Dynavox|TDControl|TD.Control|GazeInteraction' })
}
function Get-State($processes) {
    $items = foreach ($p in $processes) {
        try {
            $window = $p.MainWindowHandle -ne [IntPtr]::Zero
            $response = if ($window) { [string]$p.Responding } else { 'NA(no main window)' }
            '{0} pid={1} window={2} responding={3}' -f $p.ProcessName,$p.Id,$window,$response
        } catch { '{0} pid={1} state unavailable: {2}' -f $p.ProcessName,$p.Id,$_.Exception.GetType().Name }
    }
    if (-not $items) { return 'No matching processes observed' }
    return ($items -join '; ')
}
Write-Host "External shutdown log: $log"
Write-Trace 'External monitor started. Process/window responsiveness is not proof of functional gaze control.'
$sessionCount = 0
$activeTrainerIds = ''
$candidateKey = ''
# Invalidate stale candidates from previous monitor runs.
foreach ($folder in $packageFolders) {
    try { Remove-Item (Join-Path $folder 'foreground-before-launch.txt') -ErrorAction SilentlyContinue } catch {}
}
$exitObserved = $null
$lastState = ''
$lastWindowState = ''
$lastHeartbeat = [DateTime]::MinValue
try {
    while ($true) {
        Copy-NewTraceLines
        $now = [DateTime]::UtcNow
        $processes = Get-ObservedProcesses
        $trainer = @($processes | Where-Object { $_.ProcessName -match '^(EyeGazeApiViability|EyeGazeTrainer)$' })
        $state = Get-State $processes
        if ($state -ne $lastState -or ($now - $lastHeartbeat).TotalSeconds -ge 5) {
            Write-Trace $state
            $lastState = $state
            $lastHeartbeat = $now
        }
        if ($windowProbeReady) {
            try {
                $windowState = Get-WindowState $processes
                if ($windowState -ne $lastWindowState) {
                    Write-Trace $windowState
                    $lastWindowState = $windowState
                }
            } catch { Write-Trace ('Window snapshot error: ' + $_.Exception.Message) }
        }
        # Keep a fresh candidate only before trainer launch. Ignore shell,
        # debug helper windows, and assistive software. Never activate anything.
        if ($trainer.Count -eq 0 -and $windowProbeReady) {
            try {
                $candidate = [TrainerWindowProbe]::GetForegroundWindow()
                [uint32]$candidateId = 0
                [void][TrainerWindowProbe]::GetWindowThreadProcessId($candidate,[ref]$candidateId)
                $candidateProcess = Get-Process -Id $candidateId -ErrorAction SilentlyContinue
                if ($candidateProcess) {
                    try {
                        if ([TrainerWindowProbe]::IsWindowVisible($candidate) -and
                            -not [TrainerWindowProbe]::IsIconic($candidate) -and
                            $candidateProcess.ProcessName -notmatch 'Tobii|Dynavox|TDControl|EyeAssist|EyeGaze|^explorer$|^ApplicationFrameHost$|^StartMenuExperienceHost$|^ShellExperienceHost$|^SearchHost$|^WindowsTerminal$|^powershell$|^cmd$') {
                            $key = '{0}|{1}' -f $candidate.ToInt64(),$candidateId
                            foreach ($folder in $packageFolders) {
                                if (Test-Path $folder) {
                                    [IO.File]::WriteAllText((Join-Path $folder 'foreground-before-launch.txt'), $key)
                                }
                            }
                            if ($key -ne $candidateKey) {
                                Write-Trace ('Prior foreground candidate for next session: ' + $key + ' owner=' + $candidateProcess.ProcessName)
                                $candidateKey = $key
                            }
                        }
                    } finally { $candidateProcess.Dispose() }
                }
            } catch { Write-Trace ('Prior foreground capture error: ' + $_.Exception.Message) }
        }
        $currentTrainerIds = (($trainer | ForEach-Object { $_.Id } | Sort-Object) -join ',')
        if ($trainer.Count -gt 0 -and $currentTrainerIds -ne $activeTrainerIds) {
            $sessionCount++
            $activeTrainerIds = $currentTrainerIds
            $exitObserved = $null
            $candidateKey = ''
            Write-Trace ('SESSION ' + $sessionCount + ' START; trainer pid=' + $currentTrainerIds)
        }
        if ($trainer.Count -eq 0 -and $activeTrainerIds -ne '') {
            Write-Trace ('SESSION ' + $sessionCount + ' EXIT; trainer pid=' + $activeTrainerIds + '; begin post-exit observation')
            $activeTrainerIds = ''
            $exitObserved = $now
        }
        if ($trainer.Count -eq 0 -and $null -ne $exitObserved -and ($now - $exitObserved).TotalSeconds -ge $PostExitSeconds) {
            Write-Trace ('Final session observation completed; sessions=' + $sessionCount)
            break
        }
        if ($sessionCount -eq 0 -and ($now - $started).TotalSeconds -ge $WaitForLaunchSeconds) {
            Write-Trace 'Trainer not observed before launch timeout.'; break
        }
        foreach ($p in $processes) { $p.Dispose() }
        Start-Sleep -Milliseconds 500
    }
} catch { Write-Trace ('Monitor exception: ' + $_.Exception.ToString()) }
finally {
    Copy-NewTraceLines
    Write-Trace 'Monitoring ended.'
    Write-Host "Saved: $log"
}
