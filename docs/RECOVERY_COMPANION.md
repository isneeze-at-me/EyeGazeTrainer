# TD Control recovery companion (preliminary, unverified)

**Issue #9 remains open. Do not run the original live eye-gaze diagnostic.**

## Goals
Prepare helper-operated Windows recovery tooling that runs outside the EyeGazeTrainer UWP process and never depends on eye gaze. Keep observation separate from any action that may change TD Control.

## Tools on `Test`

- `tools/TDControl-RecoveryStatus.cmd`: **read-only**, uses built-in Windows `tasklist` and `sc query` to display known TD Control and tracker process/service presence. Does not load Windows gaze APIs, does not launch/stop/restart anything. Requires no PowerShell script execution-policy change.
- `tools/TDControl-OpenRegisteredApp.cmd`: **separate, explicit manual launch utility** using the observed Windows Start AppID `TobiiDynavox.ComputerControl` through `explorer.exe shell:AppsFolder\...`. Displays warning and requires typing `OPEN`. Not an automatic restart; may do nothing if a frozen TD Control engine is still running. May open/change focus. Only for a helper when TD Control is *not running*, never as a frozen-cursor experiment on an eye-gaze-dependent workstation.

No file terminates processes, restarts services, toggles device state or alters system configuration.

## Known vs unknown
The app ID came from a read-only local `Get-StartApps` inventory. Microsoft documents AppIDs for application launching, and the `shell:AppsFolder` namespace lists registered apps; this does **not** guarantee a successful launch/recovery on this installed TD Control version.

- https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid
- https://learn.microsoft.com/en-us/powershell/module/startlayout/get-startapps
- https://support.microsoft.com/en-us/windows/keyboard-shortcuts-in-windows

**A running process != responsive eye-gaze cursor.** A process status tool cannot tell whether TD Control is consuming gaze updates, and cannot repair a stuck stream. Actual recovery still lacks a verified method.

## Safe next validation
1. Review the scripts in GitHub before execution. Check Windows build on `Test` separately.
2. While TD Control operates normally, a helper can run only `TDControl-RecoveryStatus.cmd` to verify read-only reporting and that TD Control stays responsive. No actual fault induction.
3. Do **not** exercise the launch utility while TD Control is already running and working; do not close it merely to test launching.
4. Do not run a live gaze test until an independent recovery path for the original failure is established. An external keyboard/mouse operator must be able to regain access and the recovery method itself must be validated before hazardous tests.

## Additional engineering work
Find a supported, documented method to reinitialize TD Control's gaze session **without affecting shared tracker services or other communication/accessibility software**. No vendor contact, per user request. If that method does not exist, consider an isolated spare PC for further investigation instead of experimenting on the user's essential system.

## Timestamped baseline snapshots (2026-11-17)

Added `tools/TDControl-CaptureSnapshot.cmd` to **Test**. It runs independently of the UWP app and is an observation-only companion. It does not stop processes, change service states, restart the tracker, or access the Windows gaze APIs.

It writes a unique timestamped report under:
`%USERPROFILE%\Documents\EyeGazeTrainerDiagnostics\TDControl-Snapshot-YYYYMMDD-HHMMSS.txt`

The report includes time, matching Tobii/EyeAssist/Tdx process names/PIDs/parent PIDs, known process presence, and known service states. This is intentionally **not** a log of gaze coordinates, a probe of tracker responsiveness, or an automatic watchdog. A missing process or service may be normal depending on software version.

### Baseline validation (while TD Control works normally)

1. Pull `Test`.
2. In Visual Studio Developer PowerShell, run `.\tools\TDControl-CaptureSnapshot.cmd`. Windows CMD scripts do not require changing PowerShell's script-execution policy.
3. Confirm TD Control stays responsive to eye movements. Upload the created report for review if comfortable; review its contents first.
4. Do not run or intentionally recreate the live-gaze shutdown failure. This snapshot would only be useful for a future comparison if an independent recovery and control path has been established.

### Research note

Current TD Control manuals describe *Pause Gaze / Resume Gaze* as a user feature, but that does **not** constitute a documented way to revive an already-visible, unresponsive gaze cursor. Microsoft's Windows Eye Control troubleshooting applies to a different feature and is not evidence of a safe TD Control restart method. Sources:
- https://download.mytobiidynavox.com/Computer%20Control/Documentation/Users%20Manual/TobiiDynavox_UsersManual_TDControl_en-US.pdf
- https://support.microsoft.com/en-us/accessibility/windows/eye-control/eye-control-troubleshooting-guide

The live shutdown incident is still open and no independently validated automated recovery exists. Do not interpret normal snapshot output as recovery success.
