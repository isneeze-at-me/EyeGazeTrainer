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
- The bottom-left quadrant contains a scalable Surface Pro-style tablet *vector illustration* with a black display and a 7-column × 5-row matrix. The illustration is drawn using XAML; it is not a Microsoft product photograph.
- Every cell displays **NA** until a recorded attempt is available, and otherwise shows the average spatial performance percentage for that cell. The matrix indexes the full application view, not just the lesson canvas.
- A *measured live-gaze target acquisition* contributes its target performance (precision, speed and stability composite). A started but interrupted live-gaze target dwell contributes a zero-valued attempt to the location of that target, revealing regions where holds are difficult. Valid live-gaze trivia selections that also count toward the global accuracy score contribute their measured button-performance outcome. Wrong trivia answers never change this spatial map.
- Only 35 aggregated scores and attempt counts are stored in per-app local settings, not raw gaze coordinates, paths, or timestamped traces. The data persists across restarts; prior 7-row aggregate history is migrated into 5 rows using the midpoint of each former row (approximate because individual gaze locations were not retained).
- The existing 1.5-second shared gaze dwell, smoothing and adaptive hitboxes are used for entering Diagnostics and for Back. No new gaze device or watcher is opened to view diagnostic scores.
- The spatial percentages are internal performance indicators, **not a clinical calibration score**, and sparse cells should not be treated as reliable diagnosis.

**Acceptance tests pending:** Build with Debug/x64, verify 35-cell rendering and font sizes at Surface display scaling, navigate in/out with gaze, ensure NA cells stay NA until sampled, record successful and incomplete holds in different regions, restart and check persistence, confirm quiz wrong answers leave the map unchanged, and ensure eye-tracker cleanup remains unchanged. Follow the existing supervised hardware safety guidance.

## Accuracy and eye-jitter heatmaps (Test branch, 2026-11-18)

**Implemented in source; UWP build and hardware behavior are not yet verified.**

- Bottom-left **Accuracy** and bottom-right **Eye Jitter** each use a matching vector tablet with **7 columns × 5 rows** on the same app-view coordinate grid; remaining upper quadrants stay reserved.
- Accuracy cell backgrounds: green for average score **≥80%**, yellow for **60% through <80%**, red for **<60%**, neutral blue-gray for **NA**. Existing stored accuracy aggregates are reused unchanged.
- Jitter is the root-mean-square radial dispersion of **raw, unsmoothed gaze coordinates around their mean**, calculated from the **last 300 ms** of a completed target hold. At least **3 valid gaze samples spanning 100 ms** are required; otherwise no jitter value is recorded. Deliberate travel before the settled window and interrupted holds do not contribute jitter measurements. A correctly answered, gaze-activated trivia choice also contributes its measured settled hold when enough samples exist.
- The metric is converted from UWP view units to approximate **millimeters** using the existing DPI/optional display-size reference setting. These are **estimated on-screen gaze-position deviations**, not eye-movement measurements or clinical diagnoses; tracker noise, calibration, and head motion can affect them.
- Jitter cell backgrounds use **provisional, non-clinical thresholds**: green **≤2 mm**, yellow **>2–4 mm**, red **>4 mm**, and neutral for **NA**. Each populated cell displays the mean jitter with one decimal place; the quadrant legend states its units and that lower is steadier.
- Persistent jitter storage contains only each cell's **sum of observed RMS jitter and sample count** (35 pairs), not gaze coordinates, event timestamps, per-attempt traces, or reconstructed paths. Cells remain **NA** until new valid jitter measurements are collected, even if older accuracy data exists.
- Both maps refresh when the user opens Diagnostics and continue to use existing gaze-only menu/back dwell controls. No changes were made to gaze acquisition, cursor smoothing, timer behavior, or eye-tracker shutdown.
- Before relying on jitter bands, validate them using actual PCEye 5 observations, screen scaling, and expected normal sample rates. Colors express relative training indicators only.

**Regression checklist:** verify the app builds in Visual Studio 2022 with UWP Debug/x64; both 7×5 grids render correctly and show NA initially; accuracy colors match stored values; jitter values and colors update after valid target holds; no mouse preview or interrupted hold invents jitter; jitter and accuracy persist after restart; both maps index the same screen cells; wrong trivia responses leave the map unchanged; Back/Exit still work by gaze without extra steps. Hardware regression must follow the documented supervised safety precautions.

## Top-left training report (Test branch, 2026-11-18)

**Implemented in source; a Visual Studio/UWP build and supervised on-device verification remain pending.**

The upper-left Diagnostics quadrant now displays the **TRAINING REPORT**:
- **Levels completed:** distinct levels marked complete by the existing permanent `TrainingProgress` flags (currently 1–6, shown as `X / 6`).
- **Completed runs:** cumulative number of times the user has finished a ten-target lesson, including replays. Runs completed before this counter existed cannot be reconstructed; past completion flags are not treated as run counts. Like the existing progress flags, completing a mouse-preview lesson also increments completed runs.
- **Practice time:** persistent hours with two decimals. A 1-second dispatcher tick counts elapsed time only while live gaze is running, the app is on the lesson or quiz screen, and valid gaze input was received in the last two seconds. Menu/Diagnostics/Settings, gaze loss, shutdown and long suspended tick gaps do not accrue time. Earlier usage cannot be backfilled.
- **Overall accuracy:** the existing adaptive `AdaptiveGazeProfile.AccuracyScore` (%). Its upward/downward trend tracks changes to this score; it is intentionally level-weighted rather than a raw success percentage.
- **Target accuracy:** the most recent measured raw-gaze **precision component** (%) from successful target or correct trivia-button acquisitions; an interrupted deliberate target dwell is recorded as 0 for this report. This is separate from overall adaptive accuracy.
- **Eye jitter:** the latest valid settled RMS gaze spread in approximate screen millimeters. Lower is steadier; a drop is marked as improving.

For each metric, a compact rolling history of up to **20** observations is stored locally. A direction appears only once there are **10** observations of that metric: the most recent five are compared with the preceding five. A green **▲** means improvement, red **▼** means deterioration, and neutral **—** means insufficient history or no material change. Provisional thresholds are 0.5 percentage points for overall accuracy, 1 percentage point for target accuracy, and 0.15 mm for jitter. The jitter arrow reverses the numeric direction (decreasing jitter is improving).

Only counts, time totals and small aggregate metric histories are persisted; no gaze positions, raw gaze traces, or personal identifiers are stored. Historical level completion data is preserved. Report trends start accumulating with this feature and should not be interpreted as clinically meaningful changes.

**Acceptance tests:** verify independent completion/run totals, no counting unfinished replays, active-gaze-only time accumulation and persistence after restart, ten-observation arrow onset, higher-accuracy/lower-jitter improving arrows, no jitter entry from invalid short holds, no timer leaks after navigating off the gaze page, and responsive gaze Back/Exit. Prior eye-tracker safety precautions remain in force.

## Smoothing filter metric and diagnostics header spacing (Test branch, 2026-11-18)

- The upper-left Training Report adds **Smoothing filter** as a seventh row, showing the **actual current MasterCursorSmoothingFilter.SmoothingLevel percentage (0–99%)** rather than estimating a value from accuracy.
- Existing adaptive update points now append smoothing settings to a rolling, locally persistent 20-observation history. The report uses the same **five most recent vs five preceding** comparison as other trends, with at least 10 observations and a 0.5 percentage point difference to show direction.
- For smoothing, **▲ means the filter percentage increased**, **▼ means it decreased**, and **— means steady/insufficient history**. The arrow is neutral-colored because more or less filtering alone does not prove a change in gaze-control quality.
- No smoothing formula or filter execution has changed; this is reporting only. Older sessions have no smoothing history to backfill, but the current value displays immediately.
- The Diagnostics header reserves additional height and adds an 18-unit gap above the quadrant grid so the matching upper-left BACK button is visually separated from the top quadrant borders.

**Verification:** Build Debug/x64, check the seven report lines fit at Surface scaling, ensure Back is accessible with 1.5-second gaze dwell, confirm smoothing value matches the live filter, trend begins after 10 recorded observations, and history persists across restart. Test gaze handling only with existing supervision/recovery precautions.
