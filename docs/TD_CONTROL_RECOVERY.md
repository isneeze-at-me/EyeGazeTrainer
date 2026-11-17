# TD Control independent recovery — phase 1 inventory

**Status: preparation only. No gaze hardware testing, no automated TD Control recovery.**

## What we know
- A prior live UWP gaze diagnostic passed, but TD Control's visible cursor stopped responding after the diagnostic exited; Windows remained usable and a reboot restored TD Control.
- The no-hardware six-stage simulator built and passed. User confirmed TD Control remained responsive during and after.
- We **do not know** TD Control's installed process name, package identity, services, supported restart command or recovery contract.
- **Do not contact vendor support** (user preference). Proceed with public documentation and local, read-only inspection.

## Windows-supported avenues (not an established remedy)
- Windows Task Manager opens with **Ctrl+Shift+Esc**. This is a possible caregiver-operated access route if gaze input is unavailable, but it does not itself recover TD Control.
  - Microsoft: https://support.microsoft.com/en-us/windows/keyboard-shortcuts-in-windows
- Windows exposes Start-menu application identifiers with **Get-StartApps**; these identify registered launch entries. Identifying a TD Control AppID does not prove launching it will reinitialize a running frozen instance.
  - Microsoft: https://learn.microsoft.com/en-us/powershell/module/startlayout/get-startapps
  - Microsoft: https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid
- Microsoft Windows Eye Control troubleshooting sometimes recommends restarting a *Tobii Service* on supported hardware, but that is a **different product/system context**. We must not assume any particular named service belongs to or safely resets TD Control. Do not perform service restarts as an experiment.
  - Microsoft: https://support.microsoft.com/en-us/accessibility/windows/eye-control/get-started-with-eye-control-in-windows

## Safe phase 1: inventory only
Read `tools/TDControl-RecoveryInventory.ps1` before use. The standalone PowerShell script:
1. Lists processes whose names resemble TD Control/Tobii/eye tracking.
2. Lists Start-menu application names and AppIDs matched by those terms.
3. Lists matching Windows services with status, **without changing them**.
4. Writes `EyeGazeTrainer-Recovery-Inventory.txt` to the user's Documents folder.
5. Never opens gaze APIs, stops a process, changes a service, or launches any application.

It is intended to be run with TD Control **working normally**. Report process names, AppIDs and services (review for anything you prefer not to share). A match may be incomplete or misleading; inventory is not proof of a safe restart method.

### Suggested command (PowerShell terminal from repository root)
```powershell
.\tools\TDControl-RecoveryInventory.ps1
```
If local execution policy blocks scripts, **stop and report the error** rather than bypassing policy or changing security settings.

## Recovery design requirements
- Operate **outside EyeGazeTrainer's UWP process** and be usable by an independent keyboard/mouse operator, because the user relies on TD Control for computer access.
- No automatic killing of TD Control, driver reset, service restart, reboot, forced app activation or self-triggering recovery.
- Start with a read-only inventory. After identifying the actual installed app identity and a **supported recovery method**, write an explicit caregiver-accessible procedure and validate **under normal conditions first**.
- Never assume opening a second TD Control instance will recover the original one.
- No live UWP gaze test until a separately operable recovery method has been validated in the presence of an alternate control path.
- Do not affect Communicator 5 or Ability Drive.

## Open engineering questions
1. Does TD Control register as a Win32 app, an MSIX package, or both?
2. Which process is the UI, and which process (if any) owns eye-tracker data?
3. Can an authorized alternate operator recover it by a normal in-app reopen, rather than service or process manipulation?
4. Can a separately controlled desktop recovery program reliably detect recovery without itself consuming gaze data? This may require observing TD Control's normal UI or a supported diagnostic signal, not gaze API subscriptions.

## Limits
This inventory is not a watchdog, not a fix for issue #9 and not confirmation that the last diagnostic is safe. Collecting process names only reveals basic installation details and cannot establish causality or recoverability.

## Confirmed local inventory (user screenshot, 2026-11-17)

The user ran three **read-only** PowerShell commands successfully (the `.ps1` file itself had been blocked by execution policy; policy remained unchanged).

Registered Start apps:
- `TD Control` — `TobiiDynavox.ComputerControl`
- `Eye Tracking Settings` — executable `TobiiDynavox.EyeTrackingSettings.exe` (long registered AppID with GUID/path prefix)

Running matching processes (PIDs are a snapshot, not stable identifiers):
- `Tobii.EyeX.Engine`, `Tobii.EyeX.Interaction`, `Tobii.Service`
- `TobiiDynavox.EyeAssist.Engine`
- `TobiiDynavox.EyeAssist.RegionInteraction.Startup`
- `TobiiDynavox.EyeAssist.Smorgasbord`
- `TobiiDynavox.EyeAssist.TrayIcon`
- `TobiiDynavox.EyeTrackingSettings`
- `TobiiVirtualDevice`

Matching services reported RUNNING:
- `Tdx.ComputerControl.Updater` — Tobii Dynavox Control Updater Service
- `Tdx.Switcher.Updater` — Tobii Dynavox Switcher Updater Service
- `Tobii Service` — Tobii Service
- `TobiiGeneric` — Tobii Eye Tracker Generic Service
- `TobiiIS5LARGEPCEYE5` — Tobii Runtime Service

**Interpretation:** The UI and gaze-related processes are distinct. The previous incident (TD Control pointer visible but not moving) might be loss of incoming gaze data, a stuck TD Control input processor, or a handoff problem; this inventory **cannot determine which**. The Start AppID is a registered launch identity, *not* evidence that relaunching an already-running TD Control instance will restore gaze. Do not kill/restart any of these processes or services based on their names.

## Read-only next step: correlate processes and service owners

Use PowerShell *individual inline commands*, not an execution-policy override:

```powershell
Get-CimInstance Win32_Service | Where-Object { $_.Name -match 'Tobii|Tdx' } | Select-Object Name, State, ProcessId, StartMode
```

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'Tobii|EyeAssist' } | Select-Object Name, ProcessId, ParentProcessId, ExecutablePath
```

These only query Windows management information. Paths can contain user-specific data; review before sharing. This identifies which service PID belongs to which process and whether TD Control's UI is hosted separately. It still does not establish a safe recovery mechanism. No hardware tests until independent control and recovery are validated.
