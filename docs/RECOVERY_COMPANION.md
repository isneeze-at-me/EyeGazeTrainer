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
