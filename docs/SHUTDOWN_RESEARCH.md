# Gaze shutdown research and staged diagnostic — 2026-11-17

**Status:** engineering investigation; **NO permission for unattended testing**. Blocker: [issue #9](https://github.com/isneeze-at-me/EyeGazeTrainer/issues/9).

## Observed incident

Windows remained operational. On the earlier 60-second diagnostic exit, the screen briefly froze (~1s) and went black (~1s), while TD Control's cursor remained visible but no longer responded to gaze. Reboot was needed for recovery. The user later provided a reliability report showing power-button-associated bugcheck 0x1C8, which may document the forced recovery, not a root cause.

## External research (documentation, not hardware proof)

1. Microsoft's `GazeInputSourcePreview` is associated with the application's view and UI thread; `GazeMoved` is an event subscription. There is no documented guarantee that another application will immediately regain gaze control upon exit. Source: https://learn.microsoft.com/en-us/uwp/api/windows.devices.input.preview.gazeinputsourcepreview
2. Microsoft's `GazeDeviceWatcherPreview.Stop()` stops **device state monitoring**, not explicitly all underlying input sessions; this distinction matters. Source: https://learn.microsoft.com/en-us/uwp/api/windows.devices.input.preview.gazedevicewatcherpreview
3. Microsoft demonstrates watcher stop in navigation cleanup; it doesn't constitute a vendor-specific TD Control handoff protocol. Source: https://learn.microsoft.com/en-us/windows/uwp/ui-input/gaze-interactions
4. `CoreApplication.Exit()` is the supported UWP shutdown API, but provides no tracker handoff guarantee. Source: https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.core.coreapplication.exit
5. Tolt's eye-gaze-aware draft describes a generic gaze-aware window property and a **Tobii Dynavox variation** for TD Control/Communicator 5 interoperability. This draft is not verified for this UWP app; UWP restrictions may complicate setting HWND properties. Source: https://github.com/Tolt-Technologies/eye-gaze-aware

## Hypotheses to distinguish

- TD Control fails to reacquire a shared gaze resource when the other UWP gaze client exits.
- Watcher teardown or gaze-event subscription cleanup interacts unexpectedly with shared components.
- Application-view shutdown changes tracking focus/ownership state without a restoration notification.
- Driver/service or display transient interrupts TD Control's gaze stream independently of the explicit teardown calls.

These remain hypotheses. `watcher.Stop()` and `CoreApplication.Exit()` must not be assigned blame without evidence.

## Staged diagnostic on Test (unverified)

The updated prototype **does not start tracking at launch**. It displays an explicit ARM control and a danger notice. When explicitly armed (supervised use only), it runs a 60-second gaze-input observation and then uses six UI-visible checkpoints with approximately 3 seconds between them:

1. Prepare (no-op control)
2. Unsubscribe GazeMoved
3. Stop device watcher
4. Unsubscribe device watcher events
5. Release managed local references
6. Invoke CoreApplication.Exit

The app writes timestamped `BEGIN` and `END` entries (and exception details) to UWP `ApplicationData.Current.LocalFolder/shutdown-trace.txt`. It stores **counts only**, not gaze coordinates. Last completed END vs next BEGIN can narrow which phase was underway when TD Control lost functionality. Do not infer cause solely from temporal correlation.

**Limitations:** UWP view termination, window focus or OS permissions may cause TD Control failures outside our checkpoints. If UI thread hangs, progress markers and the in-app timer may stop too. A visible checkpoint cannot prove TD Control is receiving gaze unless observed by a person with a separate input/recovery path. The logger may itself be interrupted. This is not an independently controlled watchdog.

## Required before hardware retest

- Rebuild cleanly in UWP CI and on a test system.
- Prepare a non-gaze alternative for computer control and a person capable of restoring TD Control without using the frozen cursor.
- Prefer conducting hazardous coexistence tests on a non-essential development device; do not involve active wheelchair control.
- Identify a TD Control recovery/restart mechanism with vendor support and document it **before** testing.
- Avoid running any revised test without the operator's explicit decision and supervised recovery in place.

## Next diagnostic interpretation

If logs end at `BEGIN 03/06` without `END`, the watcher stop is a strong temporal candidate, not confirmed causal. If all END markers appear and the cursor fails only on final `CoreApplication.Exit`, investigate app teardown/focus and resource reacquisition. If UI logs complete despite TD Control failing earlier, investigate cross-process/driver ownership. Always correlate user-observed failure time with log timestamps and Event Viewer.

## Open request to Tobii Dynavox

Ask whether Windows UWP gaze source subscriptions are expected to coexist with TD Control for PCEye 5; whether TD Control supports a documented suspend/resume or gaze-aware handoff; and how to safely restore its gaze input without reboot after third-party app exit.

## Follow-up: interoperability and recovery investigation (2026-11-17)

**Important new finding:** Tolt's draft explicitly distinguishes the general `eyegaze:aware` Win32 window property from a **Tobii Dynavox-specific** extended-window-style check using bit `0x2` (`WS_TOBIIGAZEAWARE`). This is a legacy interoperability convention described for TD Control/Communicator 5, not an officially verified public TD Control API. Setting Win32 HWND window properties from a sandboxed UWP app may not be feasible or safe; do not blindly apply `SetWindowLong` or change styles on another app. Source: https://github.com/Tolt-Technologies/eye-gaze-aware

The draft also suggests registry discovery under `Software\\Classes\\eyegaze\\applications` and URI notifications such as `appuri:start` and `appuri:end`, but these are **draft conventions**, not proof that installed TD Control implements these callbacks or provides a restart/reacquire endpoint. Do not send guessed protocol URIs to the system.

Tobii Dynavox's TD Control user manual documents a built-in **Pause Gaze / Resume Gaze** feature, where a paused state is visibly indicated by an orange border and resume is triggered by looking below the tracker. This is normal pause/resume functionality, **not a validated remedy for a frozen TD Control cursor** and cannot be assumed to work when gaze updates are missing. Manual: https://download.mytobiidynavox.com/Computer%20Control/Documentation/Users%20Manual/TobiiDynavox_UsersManual_TDControl_en-US.pdf

**No supported external API or documented, verified command was found that safely restarts TD Control gaze acquisition after third-party UWP gaze app exit.** This remains an open vendor-support question.

### Safer engineering direction

1. Keep the diagnostic and shutdown instrumentation **not approved for use** until alternate non-eye-gaze control/recovery is established.
2. Consult Tobii Dynavox regarding supported app handoff, exclusive/shared gaze stream ownership, TD Control version-specific interop, and a documented restart/reinitialization mechanism.
3. Implement **non-invasive, read-only** application-state/log inspection first. Avoid experimenting with TD Control process termination, tracker driver resets, Win32 window flags or undocumented URI commands on an essential AAC computer.
4. Any eventual stage-by-stage on-device test requires independent caregiver-accessible controls and explicit stop/recovery procedures. A 60-second in-process timer is not a reliable watchdog.
5. If cooperation requires desktop HWND interop, evaluate a narrowly scoped desktop helper for future versions, but do not deploy it before documented compatibility and security review.

### Evidence needed from Tobii Dynavox

- Is there an official TD Control-compatible third-party gaze-aware app protocol for PCEye 5 and UWP apps?
- Which window property/style and protocol URIs, if any, are supported by current TD Control builds?
- Can TD Control release and reacquire eye-tracker data after another app closes without restarting Windows?
- Is there a supported, safe restart/reinitialize action or documented automation surface?
- What diagnostic logs and versions should be collected after a visible-but-unresponsive gaze cursor?
