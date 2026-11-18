# EyeGazeTrainer — six-level curriculum, therapist diagnostics and delivery plan

**Status:** Implementation roadmap, prepared 2026-11-18. Diagnostics tab shell is implemented on `Test`; levels 2–6 and the additional measurement pipelines below are **not** implemented. No claim of verified live gaze hardware safety.

## Product constraints

1. **Independent eye-only operation:** Starting, instruction, navigation, rest, retry, help, adjustment, resume and exit must not require a skill the learner has not been taught. Onboarding must begin with gaze presence and/or gaze location only—no undisclosed dwell or click. Existing prototype menu navigation uses dwell and must be redesigned before being called beginner-safe.
2. **AAC / wheelchair availability:** EyeGazeTrainer must never leave TD Control, Communicator, or wheelchair-control accessibility unavailable. The experimental Windows gaze input and overlay lifecycle have previously caused loss of TD Control access (issue #9). Do not change acquisition/cleanup behavior casually. Confirm device handoff with supervised independent input and a validated recovery path before unattended testing.
3. **Respect the user's autonomy:** no forced caregiver task, no automatic progression solely because a timer expires, gaze-operated voluntary rest, and appropriate options to replay or skip entertainment without corrupting learning metrics.
4. **Keep reporting honest:** observed gaze uncertainty may reflect tracking noise, mounting, calibration, head pose, lighting and fatigue. Scores are training indicators, not diagnoses. Mark unavailable metrics **NA**; distinguish observed from inferred. Do not infer intentionality from raw gaze samples alone.
5. **Privacy:** aggregate counters and session summaries locally; raw gaze samples are short-lived in RAM, off by default for exporting, and never persist as timestamped gaze traces without informed user consent. PDF/CSV export is initiated and confirmed by the user.

## Curriculum and measurement design

| Level | New skill and prerequisite | Guided core activity | Optional games (two or three per level) | Evidence and report fields | Advancement gate |
| --- | --- | --- | --- | --- | --- |
| **1 Discover & Look** | Gaze causes a visible effect; **zero previous skills**, no click/dwell necessary | Large central visual stimulus, then large targets away from edges. Looking triggers light or animation immediately; no hold required initially. Teach first independent rest/exit path using a gaze-only non-dwell mechanism validated with users. | **Star Catcher**, **Fireworks** | Valid gaze uptime, detection latency, large-target acquisition, region visits, sensor disconnects. Label existing 1-second-hold prototype as legacy until updated. | Demonstrate repeated intentional cause/effect and safe escape without an unlearned activation |
| **2 Move & Explore** | Move gaze deliberately; requires Level 1 gaze discovery | Travel between large stationary targets left/right/up/down and diagonals. Immediate arrival feedback; no dwell requirement. | **Balloon Pop**, **Follow the Firefly**, **Treasure Hunt** | Acquisition/search time by direction, movement distance, screen coverage, edge reach, tracking gaps, repeated overshoot proxy | Repeated accurate arrival across directions, with optional individualized thresholds |
| **3 Hold & Steady** | Sustained gaze, requires Level 2 targeting | Explicitly teach holding still with visible non-activating fill meter and user-controlled rest | **Butterfly Garden**, **Fill the Aquarium**, **Grow a Flower** | Uninterrupted fixation durations, raw gaze RMS jitter in mm, drift, hold-breaks, performance across 7×5 screen map, within-session change | Consistent holds for a user-adjustable goal, without requiring dwell *activation* for navigation |
| **4 Activate with Dwell** | Voluntary selection, requires Level 3 steadiness | Demonstrate dwell activation; show radial countdown before selection; graduated dwell durations | **Launch the Rocket**, **Magic Buttons**, **Treasure Chest** | Dwell attempts/completions/interruptions, dwell latency, false activations using explicitly labeled catch trials, completion rate by duration | Reliable intentional dwell with an independently usable Back/rest route |
| **5 Choose & Control** | Select between neighboring options and look away to rearm; requires Level 4 | Demonstrate look-away/rearm, two-choice menu navigation, corrections and increasing density | **Matching Pairs**, **Pattern Memory**, **Color Mixer** | Target size/spacing thresholds, rearm successes/failures, neighboring mis-selections, correction effort, menu return success | Reliable multi-choice selections, accessible corrections and safe Back navigation |
| **6 Everyday Eye-Gaze** | Functional communication and desktop-style tasks; requires Level 5 | Build messages and complete everyday multi-step actions (not tied to live Windows shell) | **Message Builder**, **Gaze Shopping**, **Daily Missions** | Independent task completion, time-to-goal, corrected selections/minute, accuracy under sustained use, rest frequency, setting suitability | Demonstrated user-selected functional goals and independent recovery, not a single composite score |

**Curriculum rule:** Separate *discovery of a skill* from *activation of UI controls that require that skill*. Before Level 4, any navigation must be triggered by previously taught gaze-location-only gestures or another demonstrably accessible mechanism. Avoid one-size-fits-all timers or compulsory mini-games. Completing the core level and completing a game are separate records. Repeatable game sessions should not artificially unlock unrelated skills.

**Game principles:** Short 1–3 minute rounds; optional sound and high-contrast effects; adjustable stimulus motion and complexity; no flashing triggers; clear large opt-out controls; selectable low-visual-load mode; provide immediate feedback. An optional playful theme should not prevent adults from using a clean, non-childlike presentation.

## Standardized instrumentation schema

Define a single `TrainingEvent` abstraction on top of the *existing* UWP gaze-event stream—do not create a second watcher. Use immutable event objects in memory, then aggregate into user-controlled local reports.

| Metric | Operational definition | Collection | Reason |
| --- | --- | --- | --- |
| Acquisition time | Elapsed from target visible to intentional first arrival; also report time to completed selection separately | On each target spawn/acquisition | Speed; compare locations and directions |
| Acquisition success | Arrived at the indicated target within task-specific time window; timeouts distinguished from explicit abort and tracking loss | Per trial | Reliability |
| Target precision | Settled gaze offset relative to intended target center, normalized by target size; also physical displacement when display size is known | Completed acquisitions | Access and target size decisions |
| Jitter | RMS spread of raw points about mean during an uninterrupted settled window; ≥3 samples and ≥100ms span, as currently implemented | Successful holds | Stability (not a measurement of physiological eye movements) |
| Dwell reliability | Completed deliberate dwells / deliberate attempts, with explicit attempt onset and reason for failure | Levels 4–6 | Dwell time tuning |
| Accidental activation | Selection in explicit no-activate / distractor task or a clearly signaled undo; **not** inferred from a wrong trivia answer | Catch trials | Avoid accidental activations |
| Directional access | Per-direction arrival latency and success with equivalent target dimensions | Level 2+ | Screen placement and configuration |
| Tracking availability | Valid gaze time / active requested tracking time; separately report no-device, stale data, out-of-view and unknown if distinguishable | During active trials | Detect environmental tracking issues |
| Tracking loss/recovery | Number and durations of valid→invalid→valid transitions, longest gap | During active trials | Interruptions |
| Target-size threshold | Smallest size with adequate observed success over a minimum number of comparable trials; show trial count/confidence | Levels 4–5 | AAC button dimensions |
| Effective selections/min | Successful intended selections minus recognized corrections per active task minute; not guessed from gaze velocity | Level 6 | Practical communication efficiency |
| Fatigue proxy | Compare matched trials early vs late in a session (speed, accuracy, jitter, tracking availability); account for difficulty changes | Session aggregates | Discussion point, not fatigue diagnosis |
| Session context | User-entered optional categories: posture/positioning, display mounting, lighting, reported tiredness/discomfort, corrective lenses, time of day | User-accessible prompts, not mandatory | Understanding variability |
| App assistance | Filter % (0–99), hitbox scale, dwell duration, target size, game difficulty, raw/smoothed source, selected level | Snapshot at start/end | Interpret apples-to-apples comparisons |
| Progress | Unique levels completed, completed runs, attempts, minutes practiced, best recent trial, independent navigation success | Per level/session | Longitudinal outcomes |

**Score definitions to preserve:** Existing `AdaptiveGazeProfile.AccuracyScore` is a level-capped, asymmetric adaptive score; `TargetPerformance.Precision` is a separate precision metric; `SpatialAccuracyMap` currently averages composite target performance plus zero-valued misses; `SpatialJitterMap` averages physical RMS spread. Do **not** silently relabel these as interchangeable; reports should show a metric definition, sample size and relevant date window.

**Future score analysis:** Consider Wilson intervals for success rates and median/percentile acquisition times once trials are sufficient; avoid unstable arrows from sparse samples. Use consistent comparisons only (same level, target size, dwell time and difficulty). Define arrows for jitter as lower-is-better and for smoothing as purely numeric up/down, not necessarily improvement.

## New Diagnostics information architecture

Implemented on `Test` as four large non-scrolling, 1.5-second shared gaze-dwell tabs (with matching gaze Back): **OVERVIEW / SKILLS / MAPS / SESSIONS**.

1. **OVERVIEW** — existing cumulative Training Report: completed levels, runs, practice hours, overall adaptive score and trend, target precision and trend, jitter and trend, smoothing % and direction. Companion explanatory card replaces overcrowded four-quadrant grid. Future: recent best result, independent rest/return success, an explicitly *nonclinical* observation callout.
2. **SKILLS** — four spacious tiles: Target acquisition, Dwell reliability, Tracker availability, Fatigue & access. Presently **NA / planned**, pending real measurement wiring. Later: choose tile with a single taught dwell to see full details, metrics by level and active configuration, sample sizes. Do not offer skills tabs as a prerequisite to first-time novice navigation.
3. **MAPS** — existing persistent 7-column × 5-row Accuracy and Eye Jitter tablet maps side by side at larger readable sizes; consistent green/yellow/red/NA and units. Future: explicit legend, sample-count overlay, user-controlled selection to show one region (without ever requiring tiny 35-cell gaze activation for novice use).
4. **SESSIONS** — currently a clearly labeled placeholder. Future: most recent sessions as large selectable rows, early/middle/late matched-task comparisons, date/game/difficulty, optional context notes, user-approved export to PDF/CSV. Do not treat today's aggregate report as if it were historically reconstructed per-session data.

Global layout: fixed black header with readable title + existing **BACK**, large separated tab buttons (minimum gaze targets), no automatic scroll, sufficient spacing to avoid adjacent-button misactivation, selected-tab contrast, single gaze-owning page. Preserve look-away rearm and common dwell wheel. Maintain session and tab state if eye-tracking availability changes without requiring a new skill.

## Phased delivery (each phase goes to Test, then verified before main)

### Phase 0 — Build, baseline & safety (blocking)

- [ ] Confirm `Test` compiles with VS2022 Debug/x64; verify UI layout at actual 3:2 Surface resolution and 125–200% scaling.
- [ ] Automated XAML, persistence and scoring tests; verify report timer doesn't count suspended/background time.
- [ ] Reproduce/understand TD Control takeover issue #9; test current overlay helper only with independently working backup controls and supervised recovery.
- [ ] Add non-invasive manual diagnostic/error reporting; never alter gaze API lifecycle just to test UI.
- [ ] Document accessibility validation protocol for Level 1 no-dwell entry, rest and exit. Do not claim independent safety before passing it.

**Exit gate:** reproducible development build and a safe, documented supervised test protocol.

### Phase 1 — Instrumentation and honest therapist metrics

- [ ] Introduce centralized metrics recorder with event IDs, level/game/trial, explicit intent and cancellation reason, monotonic elapsed times, configuration snapshot, sample count.
- [ ] Define tracking validity and active-attempt time before computing availability; include unknown when the device API doesn't disclose a reason.
- [ ] Persist session summaries and trial aggregates with versioned schema (not raw gaze paths); separate previews, mouse testing and live-gaze measurements.
- [ ] Implement acquisition latency, dwell numerator/denominator, interruptions, tracking uptime/gaps, and directional/time-window comparisons; annotate metrics with denominator.
- [ ] Introduce consistent baseline session timestamps, practice counts and timer handling across rest/stop/navigation.
- [ ] Add regression tests for duplicate events, tracking dropout, shutdown, crashes, malformed storage and legacy data migration.

**Exit gate:** every displayed metric is backed by valid measured input and is labeled NA otherwise.

### Phase 2 — Guided Levels 1 & 2

- [ ] Rework beginner launch, instructions, controls, rest and exit to use only gaze presence/location; remove compulsory dwell before taught.
- [ ] Implement adaptive large-target Level 1 and its two optional games, with measurable intentional cause/effect.
- [ ] Level 2 direction/coverage exercises and games; persist tracking/dropout and latency metrics.
- [ ] Validate novice usability without caregiver assistance under a safe observed setup.

**Exit gate:** repeatable independent session at both levels, complete gaze-only recovery routes.

### Phase 3 — Levels 3 & 4

- [ ] Implement stationary hold measurement and dedicated raw-jitter trials, with pauses/dropout filtering.
- [ ] Add adjustable dwell teaching sequence, 3 games, and explicit canceled/false-activation classification.
- [ ] Validate metrics against controlled gaze sample fixtures and user-selected dwell durations.

**Exit gate:** users can learn to dwell rather than being required to already know it.

### Phase 4 — Levels 5 & 6

- [ ] Implement look-away rearm, correction and two-choice/multichoice guided menus.
- [ ] Introduce safe target-size and spacing trials; avoid tiny buttons in navigation before success.
- [ ] Implement Message Builder, Gaze Shopping and Daily Missions, plus effective selections/min and independence metrics.

**Exit gate:** all task routes, including rest and exit, remain accessible when performance declines.

### Phase 5 — Therapist reporting, personalized insights and export

- [ ] Populate SKILLS with real metrics, data-quality indicators and sample counts; filter comparisons by level, conditions and settings.
- [ ] Populate SESSIONS with chronological summaries and matched early-vs-late performance plot; show provisional trends with caveats.
- [ ] Add simple user-controlled optional post-session self-rating of effort/comfort, reachable with existing taught input.
- [ ] Add consent-controlled **Export PDF** and **Export CSV** (summary only), including metric definitions, sample counts, date interval, device/display settings, 7×5 maps, completed levels/runs and user-approved notes. Do not auto-email, sync or upload.
- [ ] Define optional therapist note workflow without forcing it on users or requiring typed input to end a session.

**Exit gate:** clinician can inspect a truthful report; user independently controls export/share.

### Phase 6 — Validation, performance and release

- [ ] Study realistic trial sizes, usable thresholds and color palette with AAC users, SLPs and OTs; make thresholds configurable/explicit.
- [ ] Run measured accessibility tests on Surface Pro and PCEye 5; confirm reasonable latency, 1.5s dwell, look-away rearm, contrast, font scaling and no unwanted tracker handoff.
- [ ] Compare running app CPU/RAM/battery overhead before and after instrumentation; keep gaze event handler lightweight and aggregate off hot path.
- [ ] Support manual export, delete/report reset, privacy notice and any needed local-encryption decisions.
- [ ] Once CI, supervised integration, and independent-access criteria pass, merge `Test` into `main` with a versioned release checklist.

**Exit gate:** explicit acceptance evidence, not a claim of certification or clinical validity.

## Priority backlog after tab-shell commit

1. **Blocker:** TD Control coexistence plus beginner-safe interaction model (without this, do not claim the app is usable without assistance).
2. Complete a reproducible **Windows build and UI verification** of the newly tabbed Diagnostics shell.
3. Implement **reliable session and event recording** and real SKILLS measurements, with unit tests.
4. Build **Levels 2 and 3** (arrival directions and hold stability) and instrument them. Level 1 must be corrected for unlearned dwell prerequisites first.
5. Implement the dwell and choice levels, then practical AAC tasks.
6. Complete Sessions and user-controlled PDF/CSV export after sufficient valid data exists.

## Testing notes

- Do not assume eye gaze location equals the user's intention. Accidental-activation measurements require explicit task-design labels.
- Comparisons of different difficulty levels or gaze cursor smoothing settings must be stratified, not averaged into a deceptively precise trend.
- Practice time should exclude time in menu/report/pause and long gaps from app suspension.
- Verify blanked external windows / overlay interaction separately without changing gaze teardown logic.
- Users should be able to navigate back, rest or stop without waiting for scoring, animations or an unavailable tracking signal.
- Never treat this app as a medical device or interpret observed data as clinical impairment.

## External references

- ASHA AAC Practice Portal: https://www.asha.org/practice-portal/professional-issues/augmentative-and-alternative-communication/
- Tobii Dynavox TD Eye Gaze Pathway Instructions & Resources: https://download.mytobiidynavox.com/Resources/TDEyeGaze_Pathway_TDISeries_Instructions-And-Resources_en-US.pdf
- Tobii Dynavox Sensory Eye FX 2: https://uk.tobiidynavox.com/products/sensory-eye-fx-2
- SET-BC Eye Gaze Technology resources: https://www.setbc.org/resources/eye-gaze-technology/
