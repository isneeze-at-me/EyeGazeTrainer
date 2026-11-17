# PCEye 5 / TD Control coexistence validation

**Status:** protocol proposed on 2026-11-17; **not executed**. Related: #2, #4.

## Question

Does the Windows UWP gaze API continue producing usable gaze-position events when TD Control is not running? A confirmed gaze-coordinate PASS while TD Control is active does **not** answer this.

## Scope and safety

- The current diagnostic does not control, terminate, pause or relaunch TD Control, Communicator 5 or Ability Drive.
- The in-process 60-second timeout is **not** an independently reliable watchdog; it may fail if a system dialog blocks the UI or the app hangs.
- Do not change or interrupt wheelchair-control software for this test.
- Do **not** independently stop TD Control if it is necessary for communication or to restore computer control. A recovery-capable helper must be present for any off-state test; arrange a separate alternate communication/control method.
- Do not run a potentially disruptive transition during unsupervised operation.

## Stage A — safe baseline (TD Control stays active)

1. Confirm normal mouse/eye-gaze control and the ability to exit Visual Studio.
2. In GitHub Desktop, check out `Test` and pull latest. In VS 2022 open `EyeGazeApiViability.csproj`; select Debug/x64/Local Machine with Deploy enabled.
3. Rebuild and confirm clean compilation. If the app is still running or debugger is paused, stop debugging first.
4. Start the diagnostic **without closing or pausing TD Control**.
5. Move gaze widely across the app canvas. Record whether a device is detected, whether coordinates change, whether a PASS displays, and whether app exits at about 60 seconds.
6. Confirm TD Control remains usable afterwards. Report any permission prompts, tracking stalls or loss of control.

Expected: replicate previously reported gaze-coordinate PASS. It remains possible to receive no samples even if device enumeration succeeds.

## Stage B — independent-input validation (only with safe supervised recovery)

Prerequisites: Stage A verified in current version; reliable non-TD-Control method for helper to restore control; explicit plan for restarting TD Control; no reliance on Ability Drive wheelchair controls; permission prompts previously resolved.

1. Save work and identify how helper will re-launch TD Control if gaze control fails.
2. Have helper stop **TD Control only**, not tracker drivers/services or Ability Drive; user does not need to operate the computer alone in this risky phase.
3. Start the same 60-second diagnostic. Observe device identification, sample count, changing coordinates and PASS.
4. If anything abnormal happens, helper restores TD Control immediately; do not wait on the app timer.
5. After test, helper restores TD Control, verifies actual gaze-driven computer control, and records results. Restarting the app without TD Control is not evidence of safe autonomous handoff.

**Pass criterion:** gaze-position samples change in response to looking around while TD Control is confirmed stopped, and normal control is restored afterwards. Record uncertainty if exact TD Control process state is not verified.

**Failure criterion:** tracker absent/not ready, gaze source fails, samples never arrive, or control cannot be recovered without intervention.

## Evidence to record (no identifying raw gaze traces)

- Date, branch/commit, OS build, Visual Studio version, hardware model
- TD Control version and running/stopped status
- Device detected? CanTrackEyes? configuration state?
- Gaze sample count/change count, PASS/FAIL, permission messages
- Did automatic exit occur? Was helper intervention required?
- Did TD Control resume and computer control return?
- Full redacted error details if failure; avoid uploading gaze coordinates, personal health data or screen captures exposing private content

## Next engineering gates

Before integrating automatic TD Control handoff: determine documented interoperability mechanism; add a robust independent watchdog/recovery design; confirm no disruption to essential communication or wheelchair operation; repeat acceptance testing. Do not call current diagnostic safe for unassisted shutdown/restart testing.
