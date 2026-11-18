# Read-only TD Control recovery inventory. NO processes/services are stopped or restarted.
# Run from PowerShell on a Windows desktop when TD Control is functioning normally.
# Collects only matching process names/IDs, matching registered Start apps and
# matching service name/display name/status. No eye tracking or raw coordinates.
$ErrorActionPreference = 'Continue'
$pattern = 'Tobii|TD Control|TDControl|Eye.?Tracker|Eye.?Tracking'
$destination = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'EyeGazeTrainer-Recovery-Inventory.txt'

function As-TableText($items, $columns) {
    if (-not $items -or @($items).Count -eq 0) { return '(no matches)' }
    return (($items | Format-Table -Property $columns -AutoSize | Out-String -Width 200).TrimEnd())
}

try {
    $processes = @(Get-Process -ErrorAction Stop |
        Where-Object { $_.ProcessName -match $pattern } |
        Select-Object ProcessName, Id)
} catch {
    $processes = @()
    Write-Warning ('Process enumeration failed: ' + $_.Exception.Message)
}
try {
    $startApps = @(Get-StartApps -ErrorAction Stop |
        Where-Object { $_.Name -match $pattern } |
        Select-Object Name, AppID)
} catch {
    $startApps = @()
    Write-Warning ('Start-app enumeration failed: ' + $_.Exception.Message)
}
try {
    $services = @(Get-Service -ErrorAction Stop |
        Where-Object { $_.Name -match $pattern -or $_.DisplayName -match $pattern } |
        Select-Object Name, DisplayName, Status)
} catch {
    $services = @()
    Write-Warning ('Service enumeration failed: ' + $_.Exception.Message)
}

$report = @(
    'EyeGazeTrainer — Read-only Recovery Inventory'
    'Captured at: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')
    'SAFETY: discovery only. Does NOT verify restart/recovery.'
    ''
    '== Matching running processes =='
    (As-TableText $processes @('ProcessName', 'Id'))
    ''
    '== Matching registered Start applications =='
    (As-TableText $startApps @('Name', 'AppID'))
    ''
    '== Matching Windows services =='
    (As-TableText $services @('Name', 'DisplayName', 'Status'))
    ''
    'Do NOT use taskkill, Stop-Process, Restart-Service, or stop TD Control based on this file.'
)
try {
    $report | Set-Content -LiteralPath $destination -Encoding UTF8 -ErrorAction Stop
    Write-Host ('Saved read-only inventory: ' + $destination)
    Write-Host 'No applications or services were changed.'
} catch {
    Write-Error ('Could not write inventory: ' + $_.Exception.Message)
}
