@echo off
setlocal EnableExtensions
title EyeGazeTrainer - Manual TD Control Launch (NOT RECOVERY)
echo ============================================================
echo EyeGazeTrainer - MANUAL TD Control launch
echo ============================================================
echo WARNING: This is NOT a verified recovery action.
echo It may do nothing if TD Control is running but frozen.
echo It may also open a second instance or change focus.
echo.
echo Never use while the main computer relies solely on gaze.
echo Only a helper with independent keyboard/mouse control
echo should consider this when TD Control is NOT running.
echo Do NOT use this to test recovery of a frozen TD Control.
echo.
set "ANSWER="
set /p "ANSWER=Type OPEN to launch the registered TD Control app, or press Enter to cancel: "
if /I not "%ANSWER%"=="OPEN" goto cancelled
echo.
echo Launching registered Windows AppID:
echo TobiiDynavox.ComputerControl
echo.
start "" explorer.exe "shell:AppsFolder\TobiiDynavox.ComputerControl"
echo Launch request sent. This does NOT confirm TD Control started or recovered.
goto done
:cancelled
echo Cancelled. Nothing changed.
:done
pause
endlocal
