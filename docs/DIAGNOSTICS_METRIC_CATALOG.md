# Diagnostics — therapist-facing screen and metric catalog

**Status 2026-11-18:** UI shell and **user-triggered plain-text (.txt) therapist export** implemented on `Test`. The new Skills measurements remain **intentional placeholders (NA)**, not active data collection. Existing lifetime report and 7×5 maps remain connected to persisted data. No new eye-tracker watcher, event stream, database or cloud service was added. Windows compilation and hardware tests remain pending.

## Navigation and appearance

- **OVERVIEW:** Existing lifetime Training Report with seven real metrics (unique levels, completed runs, practice hours, adaptive overall accuracy, latest target precision, last jitter and actual smoothing percentage); trends use the existing history. A distinct explanatory card explains the meaning of trends. Currently the denominator is **six implemented level slots**, not the future Level 0–8 curriculum.
- **SKILLS:** Six large, gaze-controlled category tabs (LEVEL 0, TARGETS, DWELL, TRACKING, ENDURANCE, FUNCTION). Each category shows **six metric cards** in a legible 2-column × 3-row layout. Each card displays name, **NA**, definition and **NOT YET RECORDED**. Selecting a category changes only the displayed cards; all work is local and view-only.
- **MAPS:** Two existing persisted 7×5 location maps, Accuracy (percent, higher better) and Eye Jitter (estimated mm, lower steadier), displayed side by side with no change to collection or color rules.
- **SESSIONS:** Four areas: individual session entries (NA), matched-session comparisons (NA), optional context/condition notes (not recorded), and an **active EXPORT .TXT REPORT** gaze-dwell button. No fabricated historical time series, data values or inferred results.
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
- **TXT export now:** Users explicitly activate EXPORT .TXT REPORT on Sessions with the shared 1.5-second gaze dwell. The app creates a uniquely named UTF-8 text file in Windows Downloads using `DownloadsFolder.CreateFileAsync(..., GenerateUniqueName)` and `FileIO.WriteTextAsync`, without an inaccessible Save File picker. The screen displays a success filename only after write completes, or an error otherwise. No automatic sending, cloud upload, or disclosure. On write failure it attempts to remove the incomplete file.
- **Safety:** maintain established gaze watcher teardown and the unresolved PCEye 5/TD Control coexistence precaution. Avoid claiming independent readiness without supervised hardware tests.

## Verification status

Static checks performed for the current screen: XAML tag balance, unique x:Names, all Click methods found, six skill category names present. **Windows/UWP compilation, Surface scaling checks and PCEye 5 gaze-only interaction have not yet been performed.**

## Next work after Diagnostics sign-off

1. User tests the redesigned Overview, Skills, Maps and Sessions screens for readable cards, button targeting and gaze rearm.
2. Instrument skill measurements gradually, starting with tracking availability, acquisition latency, dwell attempts and persisted session summaries. Keep placeholders until real data is available.
3. Proceed to **Level 0: audio-only eye-cursor tutorial**, as specified in [LEVEL_ZERO_AUDIO_TUTORIAL.md](LEVEL_ZERO_AUDIO_TUTORIAL.md), without altering Level 1's current exercise.

## Therapist TXT export specification (implemented in source)

Filename pattern: `EyeGazeTrainer_TherapistReport_YYYYMMDD_HHMMSS.txt`, with a unique suffix automatically applied for duplicates. The report uses an ASCII-art heading, consistently spaced labels, numbered sections, generous section separators, and plain-English explanations. Contents:

1. **Training overview:** unique finished levels, completed runs, lifetime practice hours, number of gaze acquisitions, and historical counting limitations.
2. **Performance and trends:** adaptive overall score, last recorded target precision, last measured raw gaze jitter, current smoothing percent, optional precision/speed/stability components, current trend direction and interpretation.
3. **Current assistance settings:** smoothing, hitbox scale, target hold and menu/button dwell.
4. **Two 7×5 heatmaps:** aligned rows/columns (top to bottom, left to right), actual available cell scores and observation counts; NA for empty regions, metric definitions included.
5. **All 36 therapist measurements:** six categories, six explicit metrics each with a short definition and `NA (not recorded)` until a future pipeline implements them.
6. **Session and conditions:** explicit NA for session histories, comparisons, early/late trends, positioning, and voluntary effort ratings until recorded in future.
7. **Interpretation limits and privacy:** data is descriptive and influenced by calibration, head movement, task difficulty and tracking conditions; no clinical diagnosis, raw paths, personal ID, automatic share, or background upload.

**Testing pending on actual Windows UWP hardware:** ensure the file is readable in Windows Downloads as UTF-8, confirm user can activate button only on Sessions, verify gaze rearm and Back immediately after export, no duplicate files from one sustained dwell, failure/cleanup after disabled Downloads access, unique names across exports, correct counts and NA placeholders in a sample report, and no TD Control interference. Confirm Microsoft Store/sandbox behavior under actual package identity before release.

## Eight-second export confirmation (implemented in source)

After the text file is **successfully written** using Windows `DownloadsFolder`, a large, high-contrast, non-interactive confirmation card appears centered on Diagnostics. It states **REPORT SAVED**, explicitly identifies the **Downloads folder**, and displays the actual file name assigned by Windows. A one-shot `DispatcherTimer` hides the card automatically **eight seconds** after it appears. No gaze selection is required to dismiss it; the card is `IsHitTestVisible=False` so Back and the navigation tabs remain available. Leaving Diagnostics or exiting the app dismisses the card and stops/unsubscribes its timer. A new successful export resets the eight-second countdown. Failed exports do not display the success confirmation; the existing inline error text remains visible.

**Verify on the device:** successful export shows exact saved name and Downloads; popup auto-dismisses in eight seconds; Back/other gaze navigation remains accessible; failed write never claims success; no orphan timers after navigation/exit; saved text file is readable in Downloads. Level 0 remains untouched.
