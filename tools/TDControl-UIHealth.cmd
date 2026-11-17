@echo off
setlocal EnableExtensions
title EyeGazeTrainer - TD Control UI Health (READ ONLY)
echo ============================================================
echo TD Control UI responsiveness observation - READ ONLY
echo ============================================================
echo This does not access gaze data, restart apps or services,
echo terminate processes, move the mouse, or change settings.
echo.
echo [TD Control application - Windows tasklist status]
tasklist /v /FI "IMAGENAME eq Tdx.ComputerControl.exe"
echo.
echo [EyeAssist engine - Windows tasklist status]
tasklist /v /FI "IMAGENAME eq TobiiDynavox.EyeAssist.Engine.exe"
echo.
echo [TD Control components - Windows tasklist status]
tasklist /v /FI "IMAGENAME eq TobiiDynavox.EyeAssist.Smorgasbord.exe"
tasklist /v /FI "IMAGENAME eq TobiiDynavox.EyeAssist.RegionInteraction.Startup.exe"
echo.
echo [Windows process GUI properties - main application]
powershell.exe -NoProfile -Command "Get-Process -Name 'Tdx.ComputerControl' -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Responding,MainWindowHandle,MainWindowTitle | Format-List"
echo.
echo IMPORTANT LIMITATION:
echo RUNNING/Responding=True only measures process or GUI message-pump
echo state; it DOES NOT show whether TD Control receives gaze input.
echo A frozen gaze cursor can coexist with a responding interface.
echo.
echo Inspection completed; no changes made.
pause
endlocal
