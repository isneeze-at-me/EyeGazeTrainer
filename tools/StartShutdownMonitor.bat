@echo off
REM ============================================================================
REM EyeGazeTrainer
REM File: tools/StartShutdownMonitor.bat
REM Purpose: Start the independent shutdown observer using its sibling script.
REM ============================================================================
REM Use the batch file location so the command works from any working directory.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ShutdownMonitor.ps1"
REM Keep the console open so the saved log path can be read.
pause
