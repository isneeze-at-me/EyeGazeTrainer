# Product requirements — working draft

**Status: planned unless explicitly stated otherwise.** This is a requirements document, not a feature-completion claim.

## Mission

A guided, adult-oriented eye-gaze training program allowing a person with ALS to learn eye-gaze computer control without routine caregiver assistance.

## Non-negotiable interaction rule

The UI may require **only previously taught and demonstrated skills**, including for starting, pausing, resting, restarting, settings, navigation, recovery and exit. There must not be a hidden button, system menu or timeout that requires a future skill.

## Proposed curriculum (subject to usability validation)

| Level | Newly taught skill | Allowed prerequisite input |
| --- | --- | --- |
| 1 | Discover gaze-response cause and effect | Gaze presence/location only; no click/dwell |
| 2 | Move gaze between targets | Level 1 only |
| 3 | Hold gaze steadily | Levels 1–2; no activation required |
| 4 | Learn dwell activation | Levels 1–3; guided instruction before expecting dwell |
| 5 | Look away to rearm | Levels 1–4 |
| 6 | Navigate and access controls | Levels 1–5 |
| 7 | Increase precision | Levels 1–6 |
| 8 | Simulated menus, scrolling and keyboard | Levels 1–7 |

The first two levels must not depend on an undisclosed dwell mechanism. Skill-gated UI must adapt per-user, not just per-screen.

## Session behavior

- Launch into the earliest appropriate lesson without requiring an unlearned Start selection.
- Give multimodal instructions with readable text, optional speech and clear visual feedback.
- Never treat lost gaze data, application suspension or nonresponse as a success or permission to advance.
- Support voluntary rest, repetition, reduced difficulty and persistent lesson progress without requiring more advanced skills.
- Track session outcomes in future versions; do not assert clinical diagnostic significance.
- Use safe default settings before user has learned how to change them.

## Safety acceptance criteria before unattended use

- User can reach a safe rest/exit/recovery state with already mastered actions.
- If tracking disappears, stop progression and preserve the ability to resume.
- Permission dialogs, first-use setup and unexpected app exits have a validated recovery plan.
- Tracker access must not interrupt wheelchair control or essential AAC.
- Test handoff with TD Control separately; do not assume a direct gaze stream proves coexistence or handoff.
- Implement and verify a genuinely independent recovery path before calling unattended operation safe.

## MVP after diagnostic

Build a gaze-only Level 1 that explains itself, responds to eye position without dwell/click, safely handles absent tracking, and records enough non-sensitive evidence for reproducible testing. Full curriculum and analytics are later phases.

## November 17 engineering milestone — Level 1 mouse preview

**Implemented on `Test` as a developer preview, not a gaze lesson validated with the tracker.**

- `LevelOnePreview.xaml` and `LevelOnePreview.xaml.cs` provide an adult-oriented cause-and-effect lesson screen.
- Moving the **mouse pointer** in the lesson canvas moves a green indicator; entering each of four large targets advances immediately. No click or dwell is required for the targets. A developer-only back button returns to the diagnostics page.
- The preview is manually entered through a clearly labeled diagnostics button, not the eventual learner-facing gaze-only startup flow. It does not implement prior-skill-only navigation for the real user and is **not** ready for independent ALS use.
- Deliberately never opens `Windows.Devices.Input.Preview`, starts a watcher, stops TD Control, changes services, collects gaze coordinates, or invokes `CoreApplication.Exit()`.
- Progress is session-only; a trainer should not claim eye-gaze performance, persistent learning or clinical effectiveness based on this simulation.
- Original TD Control issue #9 remains blocking for live hardware testing. A no-hardware preview can validate UI interactions but cannot validate gaze handoff or safe shutdown.

## Experimental opt-in live-gaze Level 1 (2026-11-17)

At user's explicit request, `LevelOnePreview` now has an **ENABLE LIVE GAZE — SUPERVISED ONLY** control, while retaining mouse preview mode. Unlike the dry-run, live mode creates a `GazeDeviceWatcherPreview`, detects a ready device, subscribes to `GazeInputSourcePreview.GetForCurrentView().GazeMoved`, converts application-view gaze coordinates into the lesson canvas, and advances four large targets through gaze position alone. No dwell is required. A helper-operated STOP button invokes 4 visibly separated shutdown operations and saves a local `level1-live-gaze-trace.txt` with checkpoint timestamps but no gaze coordinates; **no automatic app exit**.

**This is an experimental hardware path, not approved unattended operation or a fix to #9.** It can still cause the previous TD Control handoff failure. Compiling cannot verify gaze stream coexistence, watcher teardown or access recovery. Do not test on a computer whose only accessible controls depend on TD Control. Any attempt requires a competent helper with independently working mouse/keyboard and a proven means of restoring access. A reboot may still be needed; no verified non-reboot recovery method exists.

The manually operated STOP control and BACK button are **developer controls** and violate the final learner-facing no-unlearned-click design until replaced with a proven gaze-only first-lesson interaction and independent recovery. Do not treat the prototype as appropriate for independent ALS users. The user requested development of the live mode, not proof that it is safe.

## Diagnostics screen (Test branch, 2026-11-18)

**Implemented in source; Visual Studio/UWP build and hardware verification pending.**

- The home screen now has five gaze-accessible actions: **Eye Gaze Trainer** (existing Levels screen), **TD Control Trainer** (text-only placeholder), **Diagnostics**, **Settings**, and **Exit**.
- Diagnostics is a black-background page within the existing gaze-owning view, with a centered DIAGNOSTICS title, a matching top-left Back button, and four blue padded quadrants. Three quadrants are reserved for future metrics.
- The bottom-left quadrant contains a scalable Surface Pro-style tablet *vector illustration* with a black display and a 7-column × 7-row matrix. The illustration is drawn using XAML; it is not a Microsoft product photograph.
- Every cell displays **NA** until a recorded attempt is available, and otherwise shows the average spatial performance percentage for that cell. The matrix indexes the full application view, not just the lesson canvas.
- A *measured live-gaze target acquisition* contributes its target performance (precision, speed and stability composite). A started but interrupted live-gaze target dwell contributes a zero-valued attempt to the location of that target, revealing regions where holds are difficult. Valid live-gaze trivia selections that also count toward the global accuracy score contribute their measured button-performance outcome. Wrong trivia answers never change this spatial map.
- Only 49 aggregated scores and attempt counts are stored in per-app local settings, not raw gaze coordinates, paths, or timestamped traces. The data persists across restarts; no per-location history exists from before the feature was installed, so those cells initially display NA.
- The existing 1.5-second shared gaze dwell, smoothing and adaptive hitboxes are used for entering Diagnostics and for Back. No new gaze device or watcher is opened to view diagnostic scores.
- The spatial percentages are internal performance indicators, **not a clinical calibration score**, and sparse cells should not be treated as reliable diagnosis.

**Acceptance tests pending:** Build with Debug/x64, verify 49-cell rendering and font sizes at Surface display scaling, navigate in/out with gaze, ensure NA cells stay NA until sampled, record successful and incomplete holds in different regions, restart and check persistence, confirm quiz wrong answers leave the map unchanged, and ensure eye-tracker cleanup remains unchanged. Follow the existing supervised hardware safety guidance.
