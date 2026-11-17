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
