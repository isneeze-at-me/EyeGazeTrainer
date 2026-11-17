@echo off
setlocal EnableExtensions
title EyeGazeTrainer - Check hardware-free test registration
echo =============================================
echo EyeGazeTrainer protocol registration check
echo READ ONLY - DOES NOT LAUNCH GAZE OR EXIT TEST
echo =============================================
echo.
echo [1] Installed UWP package:
powershell.exe -NoProfile -Command "Get-AppxPackage -Name 'EyeGazeApiViability' | Select-Object Name,PackageFullName,InstallLocation | Format-List"
echo.
echo [2] Registered URI handler:
reg query "HKCU\Software\Classes\eyegazetrainer-exittest" /s 2>nul
reg query "HKCR\eyegazetrainer-exittest" /s 2>nul
echo.
echo NOTE: UWP protocol registrations may be stored within
echo Windows' packaged-app contract database rather than a simple
echo Classes registry key. No registry key is NOT proof of failure.
echo.
echo [3] Manifest protocol in installed package:
powershell.exe -NoProfile -Command "$p = Get-AppxPackage -Name 'EyeGazeApiViability' | Select-Object -First 1; if ($p) { $m = Join-Path $p.InstallLocation 'AppxManifest.xml'; if (Test-Path $m) { Select-String -Path $m -SimpleMatch 'eyegazetrainer-exittest' } else { Write-Host 'Installed manifest not readable' } } else { Write-Host 'PACKAGE NOT INSTALLED' }"
echo.
echo No eye tracking APIs accessed. No app launched.
pause
endlocal
