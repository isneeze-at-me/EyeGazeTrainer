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
$seenTrainer = $false
$exitObserved = $null
$lastState = ''
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
        if ($trainer.Count -gt 0 -and -not $seenTrainer) {
            $seenTrainer = $true
            Write-Trace 'Trainer process observed.'
        }
        if ($seenTrainer -and $trainer.Count -eq 0 -and $null -eq $exitObserved) {
            $exitObserved = $now
            Write-Trace 'Trainer process no longer observed; begin post-exit observation.'
        }
        if ($null -ne $exitObserved -and ($now - $exitObserved).TotalSeconds -ge $PostExitSeconds) { break }
        if (-not $seenTrainer -and ($now - $started).TotalSeconds -ge $WaitForLaunchSeconds) {
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
