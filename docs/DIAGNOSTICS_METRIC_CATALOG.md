# Diagnostics — therapist-facing screen and metric catalog

**Status 2026-11-18:** UI shell implemented on `Test`. The new Skills measurements are **intentional placeholders (NA)**, not active data collection. Existing lifetime report and 7×5 maps remain connected to their existing persisted data. No new eye-tracker watcher, event stream, database, cloud service or data export was added.

## Navigation and appearance

- **OVERVIEW:** Existing lifetime Training Report with seven real metrics (unique levels, completed runs, practice hours, adaptive overall accuracy, latest target precision, last jitter and actual smoothing percentage); trends use the existing history. A distinct explanatory card explains the meaning of trends. Currently the denominator is **six implemented level slots**, not the future Level 0–8 curriculum.
- **SKILLS:** Six large, gaze-controlled category tabs (LEVEL 0, TARGETS, DWELL, TRACKING, ENDURANCE, FUNCTION). Each category shows **six metric cards** in a legible 2-column × 3-row layout. Each card displays name, **NA**, definition and **NOT YET RECORDED**. Selecting a category changes only the displayed cards; all work is local and view-only.
- **MAPS:** Two existing persisted 7×5 location maps, Accuracy (percent, higher better) and Eye Jitter (estimated mm, lower steadier), displayed side by side with no change to collection or color rules.
- **SESSIONS:** Four clearly labeled regions, all planned: individual session entries, matched-session comparisons, optional context/condition notes, and consent-controlled therapist PDF/CSV export. No fake dates, historical time series, export button that cannot work, or inferred retrospective results.
- Four main tabs, six Skills categories, and Back use **existing native large UWP Buttons**, common gaze smoothing, a single 1500ms dwell wheel, intentional look-away rearm, and the existing gaze input ownership. The six Skills selectors are only registered for gaze hit testing while Skills is active; hidden buttons must not capture gaze.

## Catalog of new Skills metrics

Every card is present in the UI, but its value is **NA until a later implementation creates and validates matching measurements**.

### LEVEL 0 — initial eye-cursor tutorial (future)
1. Time to first gaze: valid gaze detected after tutorial start.
2. Cursor screen coverage: percent of coarse regions explored.
3. Stable corner visits: corners confirmed across multiple valid samples.
4. Horizontal and vertical range: extent of active gaze exploration.
5. Moving-target pursuit: valid tracked time within target tolerance.
6. First selection: deliberate dwell attempts and successful Main Screen navigation.

### TARGETS
1. Target acquisition time: target appears → first valid arrival.
2. Target selection time: target appears → completed intended selection.
3. Acquisition success rate: arrived / eligible attempts; specify exclusions.
4. Directional access: arrival by left/right/up/down/diagonals.
5. Spatial reach and edges: visit/reach problems by screen region.
6. Target precision: raw offset from intended center, with sample counts.

### DWELL
1. Dwell success rate: completed intentional dwell attempts / started attempts.
2. Interrupted dwell attempts: meaningful holds broken before completion.
3. Accidental activations: only explicit catch trials or verified corrections, not ordinary wrong quiz answers.
4. Dwell completion time: intentional onset → activation.
5. Look-away rearm: successful resets and activation after deliberate shift.
6. Neighboring selection errors: nearby target errors and correction burden.

### TRACKING
1. Tracking availability: valid gaze time / active requested tracking time.
2. Tracking interruptions: distinct tracking loss events.
3. Longest tracking gap: longest invalid/no-sample duration.
4. Tracking recovery time: gap start → reliable valid gaze recovery.
5. Lost tracking by region: valid region-associated tracking losses (do not infer from absent coordinates).
6. Tracker response latency: time until first reliable gaze signal.

### ENDURANCE
1. Early versus late accuracy: compare similar attempts within a session.
2. Early versus late speed: comparable acquisition time.
3. Early versus late jitter: comparable raw RMS spread.
4. Rest breaks: number and duration of actual rest actions.
5. User-reported fatigue: optionally prompted, never silently diagnosed.
6. Best session conditions: session-by-session comparison of entered posture, lighting, mounting.

### FUNCTION
1. Minimum reliable target size: reliable measured acquisition with adequate attempt count.
2. Minimum reliable spacing: neighbor spacing threshold.
3. Effective selections/minute: net useful selections with corrections.
4. Functional task completion: tasks finished independently.
5. Independent navigation: Back, menu, recovery and safe exit successes.
6. Configuration and effort: dwell time, smoothing, hitbox, effort self-rating.

## Reporting rules

- **No guessed numbers:** display NA until enough actual valid samples exist. Avoid interpreting true lack of tracking as inability to control eyes.
- **Denominators:** all percentages and rates must show attempts and applicable conditions after instrumentation.
- **Comparable trials:** stratify comparisons by level, target size, hold time, smoothing assistance and difficulty.
- **Trend meaning:** for jitter, a smaller RMS value is improvement. For smoothing, numeric up/down is neutral—it does not necessarily mean the user's skill changed.
- **Session summaries:** preserve per-session date/time, activity, duration, basic aggregates, configuration and optional user-entered condition notes, but do not retain raw gaze streams or gaze paths.
- **Export later:** PDF/CSV requires an actual implemented user-controlled flow and consent before sharing.
- **Safety:** maintain established gaze watcher teardown and the unresolved PCEye 5/TD Control coexistence precaution. Avoid claiming independent readiness without supervised hardware tests.

## Verification status

Static checks performed for the current screen: XAML tag balance, unique x:Names, all Click methods found, six skill category names present. **Windows/UWP compilation, Surface scaling checks and PCEye 5 gaze-only interaction have not yet been performed.**

## Next work after Diagnostics sign-off

1. User tests the redesigned Overview, Skills, Maps and Sessions screens for readable cards, button targeting and gaze rearm.
2. Instrument skill measurements gradually, starting with tracking availability, acquisition latency, dwell attempts and persisted session summaries. Keep placeholders until real data is available.
3. Proceed to **Level 0: audio-only eye-cursor tutorial**, as specified in [LEVEL_ZERO_AUDIO_TUTORIAL.md](LEVEL_ZERO_AUDIO_TUTORIAL.md), without altering Level 1's current exercise.
