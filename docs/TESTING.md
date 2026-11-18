> **CRITICAL FIELD INCIDENT, 2026-11-17 — DO NOT RERUN:** On repeat diagnostic, user observed gaze coordinate PASS, followed by ~1-second freeze, ~1-second black screen and TD Control becoming unresponsive at auto-exit; recovery required reboot. See [issue #9](https://github.com/isneeze-at-me/EyeGazeTrainer/issues/9). Cause unknown. Previous exit reliability remains unverified and current diagnostic is unsafe to repeat pending investigation.

# Testing and evidence ledger

## Supervised Level 1 live PCEye 5 results — 2026-11-17

**User report, two runs, `Test` branch:** Live gaze tracking worked, and four gaze-only targets could be activated without dwell/click. The four manually initiated staged gaze shutdown checkpoints completed while TD Control remained responsive.

| Run | After staged STOP | App close path | Observed TD Control behavior |
| --- | --- | --- | --- |
| **A** | TD Control continued to respond during the staged cleanup | Closed Windows window with **X directly from Level 1 page** | Brief black screen (~2s); TD Control cursor remained visible but its normal circle/overlay disappeared, temporarily unresponsive; **recovered spontaneously after a few seconds**, no reboot |
| **B** | TD Control continued to respond during staged cleanup | Clicked **Back to Diagnostics**, then closed the application with **X** | No observed black screen or TD Control interruption |

**Important interpretation:** The difference in navigation sequence is a **plausible lead**, not established causation; one success and one transient failure do not show that Back→X is consistently safe. A possible distinction is closing while the page that previously held a `GazeInputSourcePreview` is still active versus navigating off that page first. The staged stop removes event handlers and drops managed references; it is not proof all system resources have been released at a native/framework level. Windows app view deactivation and TD Control overlay focus may also contribute.

**Safety classification:** Improved over the original indefinite loss of gaze, but **NOT a complete pass**. Temporary loss of essential eye-gaze control remains a blocking incident (#9). Keep supervised use only; do not authorize independent/unattended operation or automatic exit. Do not run more experiments without a helper and independent computer input.

**Recommended immediate supervised workaround:** After STOP reports completion, verify TD Control responsiveness, navigate **Back to Diagnostics**, verify responsiveness again, then close with X. This is a *provisional observation-based sequence*, not a guaranteed safe recovery path.

**Next code investigation:** instrument `LevelOnePreview.OnNavigatedFrom` and UWP app window `Suspending`/`EnteredBackground` as separate non-invasive trace points; review whether actual gaze source cleanup is complete before navigation/window close and whether page-scoped handlers survive navigation. Do not assume `CoreApplication.Exit` causes this version's issue; it is not called during these two tests.


## Reported evidence — 2026-11-17

| Test | Environment | Result | Evidence / boundary |
| --- | --- | --- | --- |
| Build after manifest and UWP project corrections | Visual Studio Community 2022 17.14.45, Debug, x64, Windows 11 | **PASS — user screenshot** | Rebuild log showed 1 succeeded, 0 failed |
| Deployment recognition | Same setup | **PASS — user report** | Deploy checkbox became selectable and checked |
| Windows gaze API acquisition | Tobii Dynavox PCEye 5, Windows 11, diagnostic UWP app | **PASS — user report** | User stated that coordinates were detected and diagnostic displayed PASS |
| Independent streaming with TD Control stopped | Not executed | **NOT TESTED** | Must be tested separately with reliable recovery |
| Automatic termination at 60s | Not independently confirmed | **NOT VERIFIED** | Timer exists in source; UI suspension/hang can defeat it |
| Fully independent ALS user operation | Not executed | **NOT TESTED** | Current prototype is not ready for unattended use |

User test results above are not an independent automated test run. Reproduce before asserting broad hardware compatibility.

## Reproduce diagnostic

1. Confirm Visual Studio UWP tools and Windows SDK 10.0.19041.0.
2. Select Debug/x64/Local Machine; deployment enabled.
3. Rebuild and note errors/warnings.
4. With a recovery person present on first runs and TD Control left available, run the diagnostic.
5. Record device detection, tracking readiness, whether gaze X/Y changes as eyes move, PASS/FAIL, permission dialog behavior and whether auto-exit occurred at approximately 60 seconds.
6. Do **not** test closing TD Control or Ability Drive as part of this first diagnostic.

## Template for new evidence

- Date and tester (use a non-identifying alias in public repository):
- Git commit SHA / branch:
- OS build, IDE version, architecture, display scaling:
- Tracker model/firmware and accompanying software version:
- Setup / permissions / whether TD Control active:
- Expected behavior:
- Observed behavior and PASS/FAIL:
- Exact errors and repeatability:
- Recovery required / any safety incident:
- Links to issue/PR and redacted logs:

Do not upload personal gaze-coordinate traces, screenshots revealing private information or identifying health details.

## Controlled startup and dedicated Exit milestone (2026-11-17)

**Scope freeze:** no new training levels. Focus exclusively on startup, gaze acquisition, resource release, and closing the UWP process without losing TD Control.

New `Test` behavior (implementation, **not yet hardware verified**):
1. App starts on MainPage; this page does not open gaze APIs. A lifecycle timestamp is appended to `gaze-lifecycle-trace.txt`.
2. A clearly named button opens the live gaze diagnostic page; gaze acquisition remains opt-in.
3. Diagnostic has a **dedicated EXIT APP — CONTROLLED SHUTDOWN** button. During live mode, pressing it automatically runs the existing four stages (unsubscribe GazeMoved, stop watcher, detach watcher events, drop references), about three seconds between stages. If cleanup throws, automatic exit is cancelled.
4. On completed cleanup, the app navigates to MainPage with a special exit parameter. MainPage clears navigation history, waits ~2 seconds, records a final log checkpoint, then calls `Application.Current.Exit()` from the non-gaze page.
5. MainPage has its own dedicated EXIT APP button. It exits from the non-gaze page without creating a gaze session. The existing STOP (stay open) remains available for diagnostics.
6. The Windows title-bar **X is outside the UWP page's controlled flow**. The app currently does **not** intercept X or guarantee that OS window-close/suspend skips the old problem. Prefer the dedicated Exit while supervised.

**Non-claims:** No verified TD Control restoration mechanism, no guarantee that page navigation releases native system gaze resources, no guarantee of failure-free exit. The prior direct-X run briefly blanked display and paused TD Control; the Back-then-X run worked once. Both results remain evidence to compare against, not proof that a new exit method is safe.

**Supervised validation needed only after successful build** (person with an independent keyboard/mouse present; do not involve active wheelchair control):
- Start app and verify TD Control remains responsive before gaze is enabled.
- Explicitly enable live gaze; record whether TD Control remains responsive and gaze indicator works.
- Use dedicated EXIT (not Windows X). Record visible cleanup checkpoint/last stage and whether app closes by itself from main dashboard.
- Observe TD Control after app closes and note any momentary blanking, overlay disappearance, delay to responsiveness, or required reboot. Do not continue if gaze access fails.
- Review `gaze-lifecycle-trace.txt` and `level1-live-gaze-trace.txt` (no raw gaze coordinates).
- **Do not** call this an unattended-safe exit until repeated tests show no interference and an independent recovery route exists.



## Native gaze input / TD Control overlay distinction (2026-11-17)

User reports that after cleanly building the dedicated-exit implementation, the app still **used TD Control to click its UI buttons outside the eye test**, and **TD Control's circle stayed visible**. This is expected with prior code: `GazeMoved` was only used for moving the test indicator / targeting four circles; ordinary XAML button clicks still depended on a pointer-emulating accessibility layer. The separate TD Control overlay is drawn by TD Control and not by this app.

**Experimental Test branch changes:**
- `App.OnLaunched` navigates directly into `LevelOnePreview`, bypassing the main dashboard button that required a TD Control click.
- The gaze page automatically requests UWP gaze tracking in its `Loaded` handler, rather than requiring a TD Control click on ENABLE LIVE GAZE.
- Gaze coordinates directly hit-test the app's Exit and Stop controls; looking at a control for 1500ms invokes its action, without Windows pointer clicks. A gap over 500ms in the gaze event stream resets the hold.
- The dedicated Exit still runs staged gaze cleanup, navigates to the hardware-free dashboard and then exits, so no dashboard interaction is needed.
- This is **not** a complete learner-ready control system; the STOP-GAZE-without-exit action leaves a screen without app-native gaze input and can still require helper intervention. Button dwell/stop behavior and simultaneous TD Control click paths are not yet field-validated.
- **No attempt was made to programmatically hide or terminate TD Control.** Its trace/circle is a distinct external overlay. Tolt's gaze-aware window specification documents a TD Control interoperability convention using a desktop extended window style, but it is not known to be safely applicable to this UWP app; introducing a Win32 helper or setting another app's window properties without validation would add risk.

**Safety:** Auto acquisition means launching the app immediately starts a gaze client; it can reproduce the prior TD Control disruption. Test only while a helper with independent keyboard/mouse control is present. App X or forced closure still bypasses controlled cleanup. This change is not proof of safe TD Control coexistence or a hidden cursor. No further training-level work until startup/shutdown and input ownership are validated.

References:
- https://learn.microsoft.com/en-us/windows/uwp/ui-input/gaze-interactions
- https://github.com/Tolt-Technologies/eye-gaze-aware

## Single Exit control (2026-11-17)

At user request the active gaze screen now has **one visible action button**: a large, full-width, minimum 96px-high **EXIT APP — LOOK FOR 1.5 SECONDS** control. Removed the Stop Gaze, Restart Gaze and Back to Diagnostics buttons from this screen and corresponding unused handlers. Direct UWP gaze-coordinate hit testing activates Exit after a 1500ms continuous look; mouse/touch click remains a helper fallback. Opening the app still auto-starts the gaze source. Exit continues the existing staged unsubscribe/watcher cleanup, navigation to a non-gaze page, and delayed application exit.

**Unverified:** this UI change alone does not fix or hide the TD Control circle/trace and does not prove safe gaze handoff. Running with PCEye 5 remains supervised only; prior user runs showed a brief loss of TD Control with direct X and a successful Back→X sequence. The dedicated Exit must be explicitly validated separately. No training levels added.


## Brief black window and title-bar flash during dedicated Exit (2026-11-17)

User reports that after initiating the latest dedicated Exit diagnostic the display went black for a couple of seconds. During that interval a **small white Windows title bar in the upper-left corner** showed only Minimize and X buttons; when the black screen cleared, the desktop and TD Control returned **immediately**. They did not report a reboot or persistent TD Control outage in this run.

**Status: abnormal transition, unresolved.** No evidence identifies whether the transient black area is the UWP window, TD Control's overlay, Windows compositor, or another window. The small title bar is a visual clue but does not by itself identify a process or prove focus handoff.

To gather evidence without changing gaze ownership or shutdown timings, added timestamp-only app lifecycle instrumentation on `Test` in `App.xaml.cs`: constructor, launch, root frame navigation, window activation, visibility changes, UWP window close, suspension and resume. All write to the existing `gaze-lifecycle-trace.txt` in the UWP app LocalFolder. Existing `level1-live-gaze-trace.txt` records watcher cleanup checkpoints. No gaze coordinates are logged. This should help correlate a future observed black screen with application events; it cannot directly log OS compositor or TD Control overlay state.

**No fix claimed.** Avoid repeated live exit experiments unless supervised with independent computer input and recovery available; do not manipulate TD Control processes or Windows services.


## Dedicated controlled Exit — successful observed run (2026-11-17)

User reports that on the most recent test, after using Exit the app **briefly displayed the Diagnostics window**, then **exited cleanly**. No black screen or prolonged TD Control interruption was reported in that run.

The brief Diagnostics window is consistent with the current intentional route: staged gaze cleanup -> `Frame.Navigate(typeof(MainPage), "EXIT_AFTER_GAZE_CLEANUP")` -> MainPage's 2-second scheduled `Application.Current.Exit()`. The visible navigation is an expected consequence of this design, not by itself a failure.

**Evidence boundary:** This is a successful observed shutdown, not proof that every shutdown will remain interruption-free. The earlier transient black-screen and TD Control effects remain documented. Preserve the working route while gathering reliability evidence; don't replace navigation and exit timing just to suppress the brief Diagnostics view without isolated validation.


## Full-window in-app gaze cursor and TD Control overlay (2026-11-17)

User requested that EyeGazeTrainer's **green circle follow gaze throughout the entire application window**, including the large Exit button, and that the **external TD Control circle be removed**.

**Implemented on Test:** The green `PreviewDot` has moved from inside `TrainingCanvas` to a topmost, `IsHitTestVisible=False` `CursorOverlay` spanning all three rows of `RootLayout`. On every valid `GazeMoved`, `UpdateCursor` uses application-view gaze coordinates to position it throughout the page. The existing canvas-relative training area calculations and full-page gaze Exit hit test remain unchanged. The transparent/noninteractive overlay does **not** block the Exit button. This covers the UWP **page content/client area**, not the OS-drawn title bar outside the page.

**TD Control external overlay is NOT automatically hidden by this change.** A separate vendor option exists: TD Control's **Hide Trace** action (particularly documented in Repeat Click mode) hides its circular gaze trace without stopping tracking, and is reversible. A third-party draft Eye Gaze Aware convention suggests special Win32 foreground-window properties / TD-specific extended style to signal overlay suppression, but neither is a verified, supported UWP interaction here. Modifying a shared TD Control service/window or blindly applying the draft style is not safe or justified by these results. If hiding trace manually, user/helper should verify that TD Control remains accessible and that restoring visible trace is straightforward.

Sources:
- https://se.tobiidynavox.com/blogs/support-articles/how-do-i-use-repeat-click-in-td-control
- https://github.com/Tolt-Technologies/eye-gaze-aware

**Verification pending:** GitHub Windows CI and, only with helper/alternate input, a supervised test that the green dot moves over Exit and the unchanged staged Exit preserves TD Control functionality.


## Black-screen / brief freeze recurs despite clean cursor behavior (2026-11-17)

User confirms the updated green gaze indicator works correctly across the app, including outside the training canvas. **However**, after Exit the black screen and a short freeze **recurred**, followed by recovery. A prior supervised trial showed clean Exit with momentary Diagnostics-page display, but the behavior is **intermittent**. Keep issue #9 open. The cursor-overlay change cannot be assumed responsible; no logs yet tie it to the window transition.

### Immediate investigation — inspect existing logs, no new live test

Added read-only `tools/Export-GazeExitLogs.cmd` on `Test`. When run from the repository on the same Windows account, it copies existing UWP LocalState trace files (if present) to `%USERPROFILE%\Documents\EyeGazeTrainerDiagnostics\LastExit`:

- `gaze-lifecycle-trace.txt`: launch, visibility, navigation/main-page exit timing, app window close and suspension entries when emitted.
- `level1-live-gaze-trace.txt`: gaze watcher creation/subscription/cleanup stages and page navigation checkpoints. The live-gaze trace is overwritten each time the app starts, so it should reflect the latest session when available.

The export does **not** start EyeGazeTrainer, access gaze APIs, stop/restart services or close processes. Share both logs for event-order investigation (review exception details for personal paths first). Their timestamps cannot by themselves identify the TD Control overlay process or the Windows compositor, but can distinguish whether the black screen appears while the app is still active, between page navigation and process exit, or after the last in-app trace.

Do **not** change the working staged cleanup sequence or force further eye-gaze testing until these current logs are reviewed. No claim of clean exit reliability is warranted.


## Uploaded shutdown traces — correlated timeline (2026-11-17, latest session 13:08–13:09 PST)

User supplied exported `gaze-lifecycle-trace.txt` (historical appended launches/exits) and `level1-live-gaze-trace.txt` (latest session; overwritten on startup).

Last recorded launch 13:08:57.037 App constructor, 13:08:57.177 OnLaunched, 13:08:57.263 Activate completed, 13:08:57.360 Window visible. Watcher started 13:08:57.296–13:08:57.301; subscription began/ended 13:08:57.338–13:08:57.341. Lesson complete 13:09:04.399; native-gaze Exit activated 13:09:06.875. Four resource-cleanup checkpoints all have BEGIN/END with no recorded exception: unsubscribe GazeMoved 13:09:06.879, watcher.Stop 13:09:09.890–.891, detach watcher events 13:09:12.923–.925, drop managed references 13:09:15.973–.974. Completed at 13:09:19.002, page OnNavigatedFrom 13:09:19.010 with gaze active=False and exit requested=True; MainPage navigated 13:09:19.011, scheduled exit 13:09:19.013, and invoked `Application.Current.Exit()` at **13:09:21.023**. All timestamps local -08:00.

Two other recent sessions (13:06 and 12:57) similarly recorded MainPage scheduling and invoking `Application.Current.Exit()` without logged failures; older sessions lack expanded lifecycle instrumentation.

**Inference boundary:** The last trace entry is the call to `Application.Current.Exit()`, not proof that the app has fully terminated or every native gaze resource is free. `Window.Closed` and `Suspending` do not appear in these logs: there is no basis to interpret their absence as either a crash or completion, because UWP lifecycle event semantics differ for programmatic exit. User has not supplied a precise timestamp for the black-screen/freeze interval, so it cannot be placed conclusively before/after the exit call. The trace **narrows the investigation** toward window/process teardown or cross-process focus/compositor handoff; it **does not prove root cause**, or rule out asynchronous effects initiated by `watcher.Stop()` or object release.

**Next engineering priority:** preserve the current working cleanup stages and visible UI; research non-disruptive UWP exit/lifecycle behavior and native gaze API handoff, and design logging capable of distinguishing managed exit request from actual process termination without adding a background watcher or disrupting TD Control. Do not claim a clean shutdown based only on the last logged line. Do not repeat live testing merely to reconfirm the same successful stage logs.


## Hardware-free UWP exit isolation (2026-11-17)

Microsoft confirms `Application.Exit()` shuts down a UWP app and `Window.Close()` closes its window, but these descriptions do not say anything about coordinated handoff to TD Control. Existing field logs show the gaze page leaves successfully and the non-gaze dashboard calls `Application.Current.Exit()`, while intermittent visual/TD Control interruptions remain unlocated.

To isolate Windows/window exit from eye-tracker cleanup, added an **independent URI protocol activation** to `Test`:
- Manifest protocol: `eyegazetrainer-exittest:`.
- `App.OnActivated` accepts only that protocol, directly navigates to `MainPage` with `EXIT_NO_GAZE_DIAGNOSTIC`, activates its window, and **does not instantiate `LevelOnePreview`, create a watcher, or open gaze APIs**. If an EyeGazeTrainer gaze page is already active, it refuses the diagnostic rather than bypassing cleanup.
- `MainPage` identifies the hardware-free route in `gaze-lifecycle-trace.txt`, then runs the **same existing 2-second delayed `Application.Current.Exit()`** as a normal managed exit. The normal live gaze startup and supervised shutdown paths are untouched.

**Safest next test (after successful build/deploy; do not run while the live app is open):**
1. Confirm EyeGazeTrainer is closed and TD Control is working normally.
2. With independent keyboard/mouse available, run `Start-Process 'eyegazetrainer-exittest:'` in Developer PowerShell or the Windows Run dialog. This is a protocol URI, **not** a gaze diagnostic. It should briefly open the non-gaze dashboard and exit by itself without touching the tracker.
3. Observe whether the same black screen or TD Control circle/freeze occurs. Do not run repeated trials if any disruption occurs.
4. The final `gaze-lifecycle-trace.txt` should include `HARDWARE-FREE exit-isolation protocol invoked`, `hardwareFreeExitTest=True`, and `CALL Application.Current.Exit()`. No `level1-live-gaze-trace.txt` should be rewritten by this session.

**Interpretation:** If black-screen interruption occurs with the hardware-free path, generic UWP window teardown or external app/compositor behavior merits priority; if not, the field issue might involve residual gaze integration or a timing interaction, but a single negative trial does not rule out an intermittent generic UWP exit issue.

No additional controls are added to the gaze page. This remains an experimental diagnostic, not a claimed fix. Window-close and native gaze release behavior remain unverified.


## Two successful shutdown observations — 2026-11-17 ~14:34 PST

User confirmed protocol activation now works after deployment/registration: **hardware-free exit test completed with no black screen**. They subsequently ran the application **normally with gaze tracking** and report it **exited cleanly**. This is an observed pass for both routes, not a demonstrated permanent fix.

**Interpretation:** Generic UWP exit is capable of completing without the visual interruption, and the native gaze startup/cleanup/exit route can also complete cleanly. Because prior user tests showed intermittent black screen and TD Control brief freezes, the contrast cannot prove root cause or establish how reliably the full path works. Do not infer that protocol registration fixed gaze shutdown.

Preserve the working full-window cursor, gaze-only 1.5-second Exit, four-stage watcher cleanup, navigation to the non-gaze MainPage, and two-second delayed app exit until there is evidence that changing any of them improves reliability. Do not close issue #9; avoid unattended use until verified recovery/robust repeated operation. No additional live test is required immediately.


## Optional TD Control trace suppression interoperability probe (2026-11-17)

User reports that the four-step gaze shutdown is now **consistently working** and requests that TD Control's gaze circle/trace be hidden **only while EyeGazeTrainer is the active window**.

Research:
- Tolt Technologies' [draft Gaze Aware Specification](https://github.com/Tolt-Technologies/eye-gaze-aware) describes Win32 `SetProp(hwnd, "eyegaze:aware", ...)` and says TD Control/Communicator 5 additionally recognize the foreground window's extended-style bit **0x2**, `WS_TOBIIGAZEAWARE`. It cautions against assuming the bit is reserved for gaze awareness.
- Windows documentation defines that same **0x2** bit as `WS_EX_NOPARENTNOTIFY`, so repurposing it may affect window behavior. The published spec is a **draft** and gives no guaranteed TD Control version support or UWP implementation path.
- Tobii Dynavox also provides a user-operated **Hide Trace** action; it is reversible but not documented as an external app API or per-window setting.

**Experimental implementation on Test:** `tools/TDControlOverlayProbe.ps1` is a separate optional PowerShell diagnostic, **not invoked by EyeGazeTrainer**.
- By default it only observes the active window (no changes).
- `-Apply` is an **explicit opt-in** experiment: for up to 90 seconds it looks for the foreground `ApplicationFrameWindow` with title **exactly** `Eye Gaze API Viability`, sets **only** the 0x2 bit in that window's `GWL_EXSTYLE` if not already set, and removes the bit if that window loses focus or the probe ends.
- If the bit was already set, it is left unchanged. The tool does not manipulate other application windows, TD Control processes or services, gaze camera APIs, user registry settings or TD Control preferences.
- No app lifecycle code, startup/exit timing, gaze cursor, or Exit button is changed. The companion is **not part of the UWP build**.
- **Limitations:** The host window title/class assumption may differ with Windows versions. It may not hide TD Control's circle at all; draft claims are not verified. While the bit is applied it alters a genuine Win32 extended style and may produce unintended window behavior. PowerShell crash or forced termination can interrupt its normal restore logic; closing the UWP window destroys its style state. Do not infer its presence guarantees TD Control restoration.
- This tests an opt-in interoperability signal rather than sending simulated clicks, stopping TD Control, changing its settings, or layering an opaque rectangle over the desktop. We do **not** recommend automatic activation until a supervised test shows predictable behavior and restoration.

**Supervised test only, helper with independent input present:**
1. Pull `Test`, confirm the gaze app is closed. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\TDControlOverlayProbe.ps1` to observe. No changes.
2. If you deliberately opt into the style experiment, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\TDControlOverlayProbe.ps1 -Apply -DurationSeconds 90` before opening EyeGazeTrainer. When the foreground window matches, the probe prints an applied confirmation.
3. Observe only whether TD Control's circle hides **while** the app has focus. Continue using EyeGazeTrainer's large gaze Exit button to leave normally; verify TD Control's circle and its normal interaction return. If any interruption occurs, stop and use helper's alternative input to recover.
4. Report whether the console printed 'Target active' / 'Applied experimental gaze-aware bit', whether the external circle disappeared, and whether it returned on exit. If the title never matches, no style was changed.

**Result pending.** No claim of actual overlay suppression until independently observed on the target TD Control installation.


## Confirmed TD Control handoff and one-command launcher (2026-11-17)

User **confirmed** that after running `TDControlOverlayProbe.ps1 -Apply`, the TD Control circle disappeared while EyeGazeTrainer was foreground; when EyeGazeTrainer exited, **the circle returned and TD Control controls worked normally**. This verifies the experimental Win32 extended-style 0x2 interoperability behavior **on this installation** in one supervised test. Its dual purpose as `WS_EX_NOPARENTNOTIFY`, and draft specification status, still matter.

**Implemented, as a separate opt-in launcher (not yet automatic for Windows Start-menu/Visual Studio launches):**
- `tools/Launch-EyeGazeTrainer-WithOverlayHandoff.cmd` is the new single-click entry point.
- Calls the existing proven `tools/TDControlOverlayProbe.ps1` with `-Apply -Launch -DurationSeconds 28800` (up to 8 hours). The updated probe finds the installed UWP `EyeGazeApiViability` package and activates `shell:AppsFolder\<PackageFamilyName>!App` using Explorer, which follows normal app startup; it does not use a second gaze API.
- Foreground window detection remains strict: `ApplicationFrameWindow` with title `Eye Gaze API Viability`. It applies bit 0x2 to that window only, and removes **only the bit it set** on focus loss or on helper exit; it re-applies if focus returns. If already set, it leaves existing style unchanged.
- Once it has seen the EyeGazeTrainer window, it exits after the window handle is gone for two seconds. If it never sees the window, it exits after 40 seconds; the absolute maximum run is 8 hours. The helper remains visible during use, and can be stopped by Ctrl+C with normal `finally` restoration.
- **No UWP app code, startup gaze watcher, green cursor, Exit button, four-stage gaze release, navigation or app exit timing was modified.**
- This **does not install an always-on background service, global hook, or new restricted `runFullTrust` capability**. It works only when launched through this new command/shortcut. Opening the app directly from Start or Visual Studio bypasses the helper and may show TD Control's trace again.

**How to test:** With a helper's independent input available, pull `Test` and run `tools\Launch-EyeGazeTrainer-WithOverlayHandoff.cmd`. Confirm the target recognized, circle disappeared while app foreground, our green cursor remains, and TD Control returns on normal large Exit. If it fails to launch, do not use a direct X while gaze is active; use the established large Exit path if the app is open. The PowerShell companion remains an external prototype; do not claim built-in automatic suppression or fully production-safe interoperability yet.

**Longer-term true in-app automation:** UWP app containers do not freely mutate desktop window styles. Microsoft supports a packaged **full-trust companion** via a `windows.fullTrustProcess` extension and restricted `runFullTrust` capability, but that entails Windows-only packaging, separate executable, and additional permissions/build validation. Keep this as a later explicit design choice; do not add restricted capabilities to the now-working UWP package without testing.

References:
- https://github.com/Tolt-Technologies/eye-gaze-aware
- https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.fulltrustprocesslauncher


## In-app TD Control overlay integration (2026-11-17) — Test branch

At user's explicit request the previously confirmed `WS_TOBIIGAZEAWARE` (same numeric bit 0x2 as `WS_EX_NOPARENTNOTIFY`) signal has now been integrated **into the packaged UWP app** rather than requiring the PowerShell launcher.

**Implementation:**
- Added `OverlayCompanion/OverlayCompanion.csproj` (x64 .NET Framework 4.8 WinExe) and `OverlayCompanion/Program.cs`: a small packaged Win32 companion. It polls the foreground window at 250ms intervals, requires the exact observed UWP title/class (`Eye Gaze API Viability`, `ApplicationFrameWindow`), and sets bit 0x2 only while that window is foreground. It clears the bit if focus leaves (and in its `finally` on ordinary termination) but does not modify pre-existing style bits. It exits when the observed window disappears, or after 40 seconds if the app window is never detected, or at its 8-hour maximum. No TD Control process, settings, registry, shared device services or gaze APIs are accessed.
- Updated `Package.appxmanifest` to register `desktop:Extension Category="windows.fullTrustProcess" Executable="OverlayCompanion.exe"` and the restricted `runFullTrust` capability.
- Updated the UWP project to build the companion from source and stage it at the root of the app package.
- `App.OnLaunched` calls `FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync` **once per UWP process** after the window is activated, catching failures so the gaze app remains available even when desktop companion launch fails. The hardware-free URI diagnostic path does *not* launch this helper.
- **No changes** to `LevelOnePreview` gaze acquisition, full-page green cursor, 1.5-second Exit, four-stage cleanup, MainPage navigation, or the 2-second exit delay.

**Security and compatibility:** `runFullTrust` is a restricted Windows package capability with greater permissions than a pure UWP app. The companion only uses user32 APIs targeting the app window, but its package-level permissions should be reviewed before distribution. Windows desktop platform and compatible .NET Framework 4.8 installation required. This flag is a draft Tobii/Tolt interoperability convention, independently verified **only on this user's installed TD Control** by the prior PowerShell probe. Do not claim universal support.

**Testing:** Pull `Test`; **Deploy** (not merely Build) to register updated manifest and package the companion; launch EyeGazeTrainer normally from Start or Visual Studio. Confirm TD Control's external trace disappears while the app is foreground, our green cursor remains, and TD Control circle/controls return following the established in-app large Exit. This is a new packaging path with a restricted capability and needs **helper-supervised first deployment and run**, although the underlying window-style toggle has been field-tested. If it does not hide, inspect `gaze-lifecycle-trace.txt` for `Overlay companion launch requested`, `launch accepted`, or an exception; check installed `OverlayCompanion.exe` in the package. Do not close the live-gaze window with X to recover; use the established Exit. Use the existing external `-Apply` probe only for diagnostics, not simultaneously with packaged helper.

**Build and runtime verification pending** at time of commit. Existing one-click external launcher is retained as a fallback, not a prerequisite.


### Build validation update (2026-11-17)

Automated Windows CI successfully compiled both UWP and `OverlayCompanion` with the Windows Desktop Extension SDK reference, validated the full-trust package manifest and created an unsigned x64 Debug `.msix`. The follow-on CI step opened that `.msix` and verified it contains **`OverlayCompanion.exe`** and **`AppxManifest.xml`**. An `APPX0006` warning notes Microsoft recommends a Windows Application Packaging Project when shipping restricted `runFullTrust` capability packages; this is not a deployment or runtime success guarantee. On-device installation, activation of `FullTrustProcessLauncher`, foreground style changes, and post-exit TD Control recovery of this newly integrated build remain **unverified** until helper-supervised live testing.


## User-facing UI transition: remove exit diagnostics dashboard (2026-11-17)

At user request the visible developer UI is retired:
- `MainPage.xaml` is now a minimal gaze-free **Closing EyeGazeTrainer / Returning to your computer** page. The old multi-button diagnostic dashboard, dry-run control, device status, staged trace display and hardware-disabled messaging have been removed from the UI.
- `MainPage.xaml.cs` drops the dry-run simulator and dashboard handlers, but **preserves** the working `EXIT_AFTER_GAZE_CLEANUP` and hardware-free `EXIT_NO_GAZE_DIAGNOSTIC` routes, clears navigation history and retains the **same 2-second delay before `Application.Current.Exit()`** on the gaze-free page. Technical lifecycle logging remains internal.
- The training page's header, introductory text, Exit label and status messages now use concise user-facing language. The large gaze-triggered Exit button (1.5s hold), full-window green cursor, four targets, direct gaze processing, and existing four-stage resource cleanup remain in place.
- `LevelOnePreview` and `MainPage` class names are kept for compatibility. Code trace files and external troubleshooting tools remain available; the request was to remove developer **UI**, not to remove important evidence or alter a proven shutdown.

**Safety:** The gaze-free closing page is still intentionally shown briefly. Removing the intermediate navigation would change a tested startup/exit behavior. Full-trust overlay companion integration is unchanged and must still be verified after deployment. The UI update is not proof of hardware reliability; test with helper/backup input until the combined build is validated.


## Faster exit and maximized launch (2026-11-17)

At user's request removed the **3-second diagnostic pause between each of the four eye-tracker cleanup operations**. `LevelOnePreview.BeginCleanup` now uses a `DispatcherTimer` interval of **100 ms**, leaving the first step immediate and the four operations in the same order (unsubscribe GazeMoved, watcher.Stop, detach watcher events, release local references). The final completion tick now occurs roughly **0.4 seconds** after Exit rather than ~12 seconds, excluding actual device or dispatcher delays. The existing gaze-free closing page and its separate **2-second exit delay remain unchanged**, to avoid removing the previously successful window handoff mechanism. Logging remains in place, not on-screen.

The packaged `OverlayCompanion` now also calls `ShowWindow(hwnd, SW_MAXIMIZE)` **once per newly recognized exact EyeGazeTrainer foreground window**, then applies the same approved window-style overlay suppression logic. This produces an ordinary maximized Windows app window with its title bar, not an exclusive or borderless full-screen view. It does not maximize other apps, modify TD Control, or alter gaze APIs. If the companion fails to launch, app maximization is not guaranteed; check companion package and launch traces if needed. No added UI buttons or training levels.

**Verification:** New faster cleanup timing and maximized-window behavior have not yet been validated on PCEye 5 hardware together. Because native gaze resource handoff had previously caused black screens and temporary loss of TD Control, perform first run only with helper/independent input available, and ensure TD Control responds after exit. The 100ms spacing is not evidence of native-device readiness.


## Shared circular gaze-button dwell wheel (2026-11-17)

User reports the shorter cleanup and maximized launch have **no issues** and requests the same **shared circular button dwell timer** used in Homestead Influencer.

Source implementation reviewed directly in private `isneeze-at-me/HomesteadInfluencer`, `src/UI/ApprovedButtonInteraction.cs` (`ApprovedDwellWheelControl` and `ApprovedButtonInteractionService`) and `src/UI/SharedDwellWheelOverlay.cs`. Its approved design uses one dark circular track, a light clockwise progress arc starting at 12 o'clock, about 38px radius, 6px track and 7px progress thickness. It is visual-only; the interaction state owns activation. Homestead attaches its wheel to a registered button's local visual so transforms don't drift. The port uses the existing UWP page-wide hit-test-transparent overlay and calculates the registered button's center from `TransformToVisual`.

**Implementation on Test:**
- Added reusable `SharedButtonDwellWheel.cs`, a visual-only class that draws a circular track and an arc in `CursorOverlay`, centered on the active UWP `Button`. Its `Present(button, progress)`, `Hide()` and `Reset()` methods can be reused for later gaze-target buttons.
- The wheel never captures pointer input and does not open any gaze APIs; it reuses `LevelOnePreview.ProcessGazeControl` and the existing **1.5-second Exit dwell**.
- Wheel is visible when gazing at Exit and fills continuously, resetting immediately when gaze leaves the button or when the incoming gaze-event stream has a gap over 500ms. It hides after activation and on Exit.
- Replaced the old `LiveStatusText` percentage update while looking at Exit with the visible circular wheel. No progress bars or extra buttons.
- Four-step shutdown, two-second gaze-free exit handoff, maximized launch, and TD Control overlay companion unchanged.

**Verification:** GitHub CI build and supervised PCEye 5 runtime test pending at the time of writing. Confirm a visible wheel centered on Exit, clockwise fill from top, look-away reset, single activation at 1.5s, and normal TD Control recovery. A separate gaze-gap reset is evaluated when the next gaze event arrives; no new gaze APIs or timers were added.


## Adaptive master cursor smoothing and target assistance (2026-11-17)

At user request, EyeGazeTrainer now has a single, reusable `MasterCursorSmoothingFilter` with `SmoothingLevel` **0–99** (`0` = untouched raw position), starting at **85**, and a persistent learner `AdaptiveGazeProfile` in **UWP LocalSettings**.

**Adaptation:**
- The filter is a timestamp-based exponential low-pass filter with a maximum half-life of 120ms at level 99. Large intentional movements (raw-to-stabilized distance >=160 app-view coordinate units) snap immediately to raw. A >500ms gaze gap, off-window sample, or Exit resets smoothing history; no Windows cursor or TD Control trace is moved.
- The training circle's **visible diameter stays unchanged**. Its circular logical hit area starts at **110%** of the visible diameter and gradually narrows toward **100%**.
- Each successfully acquired training circle records **one** raw-gaze sample's normalized distance from the visible circle's center, scored from 100 for very central hits to 0 at the outer assisted boundary. No raw gaze coordinates are persisted. The `AccuracyScoreV1` is an exponential average (90% previous score / 10% new precision) and `AcquisitionsV1` is cumulative. Both are kept in the app's LocalSettings.
- **No adaptive reduction in assistance for the first 8 acquisitions**, even if precision is high. From acquisition 9 through 56 a gradual confidence multiplier ramps toward full adaptation. As raw precision increases, the shared filter's smoothing target drops from 85 toward 0 and the logical training hitbox approaches 100%; if precision worsens, assistance grows again. Individual targets never implement their own independent smoothing parameters.
- The main page displays a persistent `Accuracy: N%` indicator after the first successfully acquired target. This is an **approximate raw-center precision proxy, NOT a measured clinical tracking-accuracy percentage**. The current lesson has four targets, so initial sessions intentionally remain fully assisted. Future lessons can reuse the same profile.
- Cursor visualization and training target acquisition consume the **same stabilized point**. Precision scoring uses the **raw** point. The safety-critical EXIT button continues to hit-test and dwell against **raw gaze**, NOT the smoothed point, with its unchanged 1.5s control dwell and circular progress wheel; this avoids trapping the user's Exit behind cursor smoothing.
- Shutdown resource ordering, gaze-free 2-second exit handoff, maximized window, packaged TD Control overlay suppression and logging remain unchanged.

**Configuration points:** `MasterCursorSmoothingFilter.SmoothingLevel` is the only filter-strength setting (integer 0–99). `AdaptiveGazeProfile.BeginnerSmoothingLevel` sets the initial starting strength; `BeginnerHitBoxScale=1.10` and `ExperiencedHitBoxScale=1.00` specify the logical hitbox endpoints. This adaptation policy is an initial accessibility hypothesis, not clinically validated; it may need tuning after user-supervised field tests.

**Supervised verification:** Pull `Test`, Deploy Debug x64. Confirm early cursor is stabilized without a noticeable lag between target centers; ring activates from slightly beyond its visible edge, Exit dwell activates from **raw gaze** with look-away reset and TD Control stays responsive after exit. Reopen several times and verify accuracy persists; confirm smoothing/hitbox difficulty adapt gradually only after enough target acquisitions. Do not merge to main before approval.
