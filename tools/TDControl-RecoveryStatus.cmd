@echo off
setlocal EnableExtensions
title EyeGazeTrainer - Read Only TD Control Recovery Status
echo ============================================================
echo EyeGazeTrainer - TD Control recovery status (READ-ONLY)
echo ============================================================
echo.
echo This tool DOES NOT restart, stop, launch, or modify anything.
echo It DOES NOT read or open gaze input.
echo.
echo [TD Control engine and UI processes]
tasklist /FI "IMAGENAME eq TobiiDynavox.EyeAssist.Engine.exe"
tasklist /FI "IMAGENAME eq TobiiDynavox.EyeAssist.Smorgasbord.exe"
tasklist /FI "IMAGENAME eq TobiiDynavox.EyeAssist.TrayIcon.exe"
echo.
echo [Shared tracker processes]
tasklist /FI "IMAGENAME eq Tobii.EyeX.Engine.exe"
tasklist /FI "IMAGENAME eq Tobii.EyeX.Interaction.exe"
tasklist /FI "IMAGENAME eq TobiiVirtualDevice.exe"
echo.
echo [Registered services (read only)]
sc query "Tobii Service"
sc query "TobiiGeneric"
sc query "TobiiIS5LARGEPCEYE5"
echo.
echo NOTE: RUNNING is not proof the cursor is responding.
echo NOTE: Missing results may mean app version/naming changed.
echo.
echo Status collection complete. No recovery action taken.
pause
endlocal
