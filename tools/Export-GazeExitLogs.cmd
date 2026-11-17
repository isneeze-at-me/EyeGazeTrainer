@echo off
setlocal EnableExtensions
title EyeGazeTrainer - Export existing gaze lifecycle logs
echo ========================================================
echo Read-only log export - DOES NOT RUN EyeGazeTrainer
echo Does not change tracker, TD Control or Windows services.
echo ========================================================
set "DEST=%USERPROFILE%\Documents\EyeGazeTrainerDiagnostics\LastExit"
if not exist "%DEST%" mkdir "%DEST%"
set "FOUND=0"
for /d %%D in ("%LOCALAPPDATA%\Packages\EyeGazeApiViability_*") do (
    if exist "%%~fD\LocalState\gaze-lifecycle-trace.txt" (
        copy /y "%%~fD\LocalState\gaze-lifecycle-trace.txt" "%DEST%\gaze-lifecycle-trace.txt" >nul
        echo Copied gaze-lifecycle-trace.txt
        set "FOUND=1"
    )
    if exist "%%~fD\LocalState\level1-live-gaze-trace.txt" (
        copy /y "%%~fD\LocalState\level1-live-gaze-trace.txt" "%DEST%\level1-live-gaze-trace.txt" >nul
        echo Copied level1-live-gaze-trace.txt
        set "FOUND=1"
    )
)
echo.
echo Log export location:
echo %DEST%
echo.
echo If no files were copied, locate the installed UWP package's
echo LocalState folder and check the package identity.
echo These logs may include exception details. Review before sharing.
echo No new live gaze test is required.
echo.
pause
endlocal
