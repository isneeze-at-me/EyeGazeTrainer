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

## First real baseline capture and correction (2026-11-17 11:54)

User uploaded `TDControl-Snapshot-20261117-115407.txt`. It confirms the following were present/running at capture:

- `TobiiDynavox.EyeAssist.Engine`, PID 17132
- `Tobii.EyeX.Engine.exe`, PID 7796
- `Tobii.EyeX.Interaction.exe`, PID 7788
- `TobiiVirtualDevice.exe`, PID 5272
- Services `Tobii Service`, `TobiiGeneric`, `TobiiIS5LARGEPCEYE5`, `Tdx.ComputerControl.Updater`, and `Tdx.Switcher.Updater`: all reported RUNNING.

**Script defect:** the PROCESS TREE section failed with `Get-CimInstance : A positional parameter cannot be found that accepts argument '^'`, because the CMD script passed caret-escaped pipe characters into a quoted PowerShell command. The rest of the snapshot completed normally. Fixed script on `Test` at commit `ea24335`; the first report is a **partial baseline**, not complete success.

The captured process/service statuses do not prove gaze responsiveness, but this user's separate normal-operation reports establish that TD Control was responding during preceding read-only tests. Do not claim that the earlier fault is fixed or that recovery is validated.

Next: optionally recapture the read-only baseline using the corrected script, without activating the UWP live gaze diagnostic.

## Complete normal-operation baseline (2026-11-17 11:58:53)

User uploaded `TDControl-Snapshot-20261117-115853.txt` after pulling the quoting fix. All three sections completed and the process-tree command **no longer produced an error**. Observed processes (snapshot PIDs, not stable identifiers):

| Process | PID | Parent PID |
|---|---:|---:|
| `Tdx.ComputerControl.Updater.exe` | 5156 | 1268 |
| `Tdx.Switcher.Updater.exe` | 5220 | 1268 |
| `Tobii.Service.exe` | 5256 | 1268 |
| `TobiiVirtualDevice.exe` | 5272 | 1268 |
| `Tobii.EyeX.Interaction.exe` | 7788 | 5256 |
| `Tobii.EyeX.Engine.exe` | 7796 | 5256 |
| **`Tdx.ComputerControl.exe`** | **5640** | **3228** |
| `TobiiDynavox.EyeAssist.Engine.exe` | 17132 | 17400 |
| `TobiiDynavox.EyeAssist.RegionInteraction.Startup.exe` | 17904 | 17132 |
| `TobiiDynavox.EyeAssist.Smorgasbord.exe` | 18064 | 17132 |
| `TobiiDynavox.EyeTrackingSettings.exe` | 18144 | 17132 |
| `TobiiDynavox.EyeAssist.TrayIcon.exe` | 18168 | 17132 |

Monitored services `Tobii Service`, `TobiiGeneric`, `TobiiIS5LARGEPCEYE5`, `Tdx.ComputerControl.Updater`, `Tdx.Switcher.Updater`: all RUNNING with service and Windows exit codes 0.

**New finding:** `Tdx.ComputerControl.exe` is present as a separate process from `TobiiDynavox.EyeAssist.Engine.exe`, which makes distinguishing UI/app vs gaze engine valuable for future recovery research. Parent PID 3228 has not been identified; **do not chase changing PIDs without a clear diagnostic reason**.

**Not established:** Process presence, service RUNNING status, and exit codes **do not establish gaze-stream health** or prove which component froze. No user report of an actual frozen state has been captured by this snapshot. No automated or manual restoration action has been validated.

**Status:** Read-only snapshot baseline COMPLETE; original TD Control shutdown incident #9 OPEN. The next engineering work should investigate documented and non-invasive application-level state/recovery rather than repeatedly collecting identical normal-operation snapshots. No live gaze test, service restart, forced process exit, or automatic relaunch.

## Phase 2: Windows UI health (non-invasive, 2026-11-17)

Added `tools/TDControl-UIHealth.cmd` to `Test`. This independent command-line companion queries:

- `tasklist /v` for `Tdx.ComputerControl.exe`, `TobiiDynavox.EyeAssist.Engine.exe`, and TD Control UI companion processes, including Windows' **Status** field.
- PowerShell `Get-Process` `Responding`, `MainWindowHandle`, and `MainWindowTitle` for `Tdx.ComputerControl` (when accessible).

This is **read-only**: it does not open Windows Gaze APIs, alter services, kill/restart applications, change input focus deliberately, or run the live UWP diagnostic. Microsoft documents Windows `tasklist` status values as RUNNING, NOT RESPONDING or UNKNOWN (see https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/tasklist); PowerShell exposes Process.Responding (see https://learn.microsoft.com/en-us/powershell/scripting/learn/ps101/03-discovering-objects).

**Critical limitation:** A responding window/message loop is not proof that TD Control consumes live gaze data. The user's prior failure (cursor remains visible but stops moving) might show RUNNING and Responding=True even while unusable. Background/helper processes may have no main window; their GUI status isn't a health test. This tool cannot fix or fully diagnose gaze-stream freezes. Avoid interpreting it as a watchdog or automatic recovery mechanism.

The user has completed a baseline snapshot with all expected named components and five services present/running. No more repetitive process inventory is required; this UI status check is a *different* signal to evaluate separately, while everything is working normally.

### Safe validation

1. Pull the `Test` branch; review `tools/TDControl-UIHealth.cmd`.
2. Run `.\tools\TDControl-UIHealth.cmd` from Visual Studio Developer PowerShell **with TD Control operating normally**.
3. Record the displayed Status and Responding values, if any; TD Control must continue to accept gaze during and after.
4. **Do not** deliberately freeze the application, rerun the live gaze diagnostic, use the manual launch tool, or stop/restart any Tobii components.

No independent recovery has been validated. Issue #9 remains blocking.
