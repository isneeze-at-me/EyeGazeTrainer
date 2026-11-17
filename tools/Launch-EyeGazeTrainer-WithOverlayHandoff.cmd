@echo off
setlocal EnableExtensions
title EyeGazeTrainer - Automatic TD Control Overlay Handoff
echo EyeGazeTrainer launcher - gaze-aware overlay handoff
echo Starts EyeGazeTrainer normally and suppresses the TD Control trace
echo while the app window is foreground, using the confirmed experimental
echo window style. Normal gaze cleanup and Exit are unchanged.
echo.
echo Keep this helper open while using EyeGazeTrainer.
echo It will end after the EyeGazeTrainer window closes.
echo A helper with independent keyboard/mouse access should supervise testing.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0TDControlOverlayProbe.ps1" -Apply -Launch -DurationSeconds 28800
if errorlevel 1 (
  echo.
  echo The overlay helper reported an error; the normal EyeGazeTrainer
  echo gaze shutdown path is not modified by this launcher.
  pause
)
endlocal
