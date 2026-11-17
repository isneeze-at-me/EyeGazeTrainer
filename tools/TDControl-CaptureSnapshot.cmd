@echo off
setlocal EnableExtensions
title EyeGazeTrainer - Read-Only TD Control Snapshot
rem No process stop, app launch, service state changes, gaze access, or policy override.
set "OUTDIR=%USERPROFILE%\Documents\EyeGazeTrainerDiagnostics"
if not exist "%OUTDIR%" mkdir "%OUTDIR%"
if not exist "%OUTDIR%" (
  echo ERROR: Could not create diagnostics directory.
  pause
  exit /b 1
)
set "STAMP="
for /f "delims=" %%T in ('powershell.exe -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%T"
if not defined STAMP set "STAMP=undated"
set "OUTFILE=%OUTDIR%\TDControl-Snapshot-%STAMP%.txt"
echo Taking a read-only snapshot. TD Control will NOT be changed.
(
  echo EyeGazeTrainer TD Control status snapshot
  echo Captured on %DATE% at %TIME%
  echo Read-only: status information is NOT a gaze responsiveness test.
  echo.
  echo ==== PROCESS TREE: NAMES, IDS, PARENTS ====
  powershell.exe -NoProfile -Command "Get-CimInstance Win32_Process ^| Where-Object { $_.Name -match 'Tobii^|EyeAssist^|Tdx' } ^| Select-Object Name,ProcessId,ParentProcessId ^| Format-Table -AutoSize ^| Out-String -Width 220"
  echo.
  echo ==== PROCESS STATUS ====
  tasklist /FI "IMAGENAME eq TobiiDynavox.EyeAssist.Engine.exe"
  tasklist /FI "IMAGENAME eq Tobii.EyeX.Engine.exe"
  tasklist /FI "IMAGENAME eq Tobii.EyeX.Interaction.exe"
  tasklist /FI "IMAGENAME eq TobiiVirtualDevice.exe"
  echo.
  echo ==== WINDOWS SERVICE STATUS ====
  sc query "Tobii Service"
  sc query "TobiiGeneric"
  sc query "TobiiIS5LARGEPCEYE5"
  sc query "Tdx.ComputerControl.Updater"
  sc query "Tdx.Switcher.Updater"
  echo.
  echo ==== OBSERVATION LIMITS ====
  echo A running process or service does not prove eye gaze is functioning.
  echo No processes, services, drivers or eye-gaze APIs were changed.
) > "%OUTFILE%" 2>&1
if exist "%OUTFILE%" (
  echo.
  echo Saved read-only snapshot:
  echo %OUTFILE%
  echo.
  echo No changes were made to TD Control.
) else (
  echo ERROR: Snapshot was not created.
)
pause
endlocal
