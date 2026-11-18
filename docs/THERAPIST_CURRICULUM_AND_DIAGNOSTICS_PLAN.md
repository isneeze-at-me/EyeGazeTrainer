# EyeGazeTrainer — adult-oriented eight-level curriculum, therapist diagnostics and delivery plan

**Status:** Roadmap updated 2026-11-18. The four-tab Diagnostics layout, six gaze-navigable Skills categories with 36 clearly labeled NA metric cards, session/report layout, existing lifetime report, and 7×5 maps are committed on `Test`. New Skills recording and dated session history are **not** implemented; user-triggered TXT export of the existing aggregates and NA placeholders **is implemented**. Level 0 and Levels 2–8 remain planned. Windows build and gaze hardware tests are pending. See [Diagnostics Metric Catalog](DIAGNOSTICS_METRIC_CATALOG.md).

> **Level 0 design update (2026-11-18):** The app's first-run introduction is a separate, **audio-only Level 0** before the existing ten-target Level 1. The eyeball shape is the **moving gaze cursor itself**, not a pair of stationary eyes. Spoken prompts depend on real activity (for example stable visits to screen corners), then a slow moving target is introduced, followed by one large gaze-dwell Main Screen button. **No text instructions, no child-themed games, no compulsory dwell before it is taught.** Keep the current Level 1 exercise. Full design and acceptance tests: [Level 0 Audio Tutorial](LEVEL_ZERO_AUDIO_TUTORIAL.md). This is specification only, not implemented.

## Product constraints

1. **Independent eye-only operation:** Starting, instruction, navigation, rest, retry, help, adjustment, resume and exit must not require a skill the learner has not been taught. Onboarding must begin with gaze presence and/or gaze location only—no undisclosed dwell or click. Existing prototype menu navigation uses dwell and must be redesigned before being called beginner-safe.
2. **AAC / wheelchair availability:** EyeGazeTrainer must never leave TD Control, Communicator, or wheelchair-control accessibility unavailable. The experimental Windows gaze input and overlay lifecycle have previously caused loss of TD Control access (issue #9). Do not change acquisition/cleanup behavior casually. Confirm device handoff with supervised independent input and a validated recovery path before unattended testing.
3. **Respect the user's autonomy:** no forced caregiver task, no automatic progression solely because a timer expires, gaze-operated voluntary rest, and appropriate options to replay or skip entertainment without corrupting learning metrics.
4. **Keep reporting honest:** observed gaze uncertainty may reflect tracking noise, mounting, calibration, head pose, lighting and fatigue. Scores are training indicators, not diagnoses. Mark unavailable metrics **NA**; distinguish observed from inferred. Do not infer intentionality from raw gaze samples alone.
5. **Privacy:** aggregate counters and session summaries locally; raw gaze samples are short-lived in RAM, off by default for exporting, and never persist as timestamped gaze traces without informed user consent. PDF/CSV export is initiated and confirmed by the user.

## Adult learner curriculum and measurement design — revised

**Audience and design intent:** Adults encountering eye gaze for the first time, including people using AAC because of ALS or other motor limitations. The experience must resemble a polished professional training application or premium puzzle interface, **not a children's game**. Use uncluttered high-contrast panels, precise geometry, attractive restrained animations, adult-interest trivia, clear instructions, dignified progress celebrations, optional sound, and familiar functional activities. Do **not** introduce balloons, butterflies, fireworks, cartoon animals, childish narrative voices, playful mascots, or Comic Sans as product defaults.

**Level 1 decision:** Keep the existing ten-target, black-background exercise with large randomized targets, margins away from edges, shared target progress wheel and persistent scoring. Do not replace it with a new themed game. The existing live Level 1 requires a 1-second sustained gaze acquisition and the current menu/Back requires 1.5-second dwell; these dependencies must be introduced through an automatic, no-dwell first-use entry and an initial zero-hold cause-and-effect demonstration **inside Level 1**, before longer holds become necessary. Preserve the current measured hold variant as the later part of Level 1. Retain optional adult general-knowledge trivia as a reward; allow a previously learned gaze-only skip/return path when possible. Replace Comic Sans or juvenile celebration language later as part of a coordinated adult visual refresh, not a rushed change to working gaze logic.

| Level | Skill and prerequisite | Guided activity (adult visual design) | Optional adult-oriented exercises | Therapist measurements and gates |
| --- | --- | --- | --- | --- |
| **1 — Introduction to Eye Gaze** | Awareness of gaze feedback; no previous ability or dwell | **Retain existing ten large targets** on black, with short introductory instantaneous-gaze stage before 1-second hold stage; automatic instructions and completion; safe non-dwell way to rest/return for a complete novice | Existing adult-interest true/false trivia; optional different abstract target palettes only | Initial detection, successful acquisitions, short hold tolerance, region coverage, acquisition latency, tracking availability. Pass without needing an unlearned button or menu dwell |
| **2 — Gaze Movement** | Deliberate left/right/up/down/diagonal movement; Level 1 | Move between large directional targets with immediate arrival response, no required activation | **Directional Challenge**, **Target Sequence**, **Screen Exploration** | Directional arrival time, movement difficulty, reachability, spatial coverage, overshoot proxy, dropouts |
| **3 — Gaze Stability** | Sustained focus; Level 2 | Progressive steady hold with a precise progress ring and non-activating confirmation | **Focus Challenge**, **Steady Signal**, **Hold the Indicator** | RMS raw gaze jitter, hold durations/breaks, stability across regions, tracking validity |
| **4 — Dwell Activation** | Intentional selectable activation; Level 3 | Explicit coaching for dwell progress, activation, and canceling an incomplete selection | **Control Center**, **System Activation**, **Precision Switches** | Dwell success rate, activation time, cancellations, explicitly labeled unintended selections |
| **5 — Selection Accuracy** | Choose among competing targets; Level 4 | Large, contrasting multi-choice targets; gradually add distractors | **Visual Identification**, **Pattern Recognition**, **Selection Challenge** | Correct intended selections, neighboring errors, correction count, false activation on catch trials, spacing |
| **6 — Gaze Navigation** | Screen navigation, Back, rest and look-away rearm; Level 5 | Simulated professional menu hierarchy and trained recovery controls | **Dashboard Navigation**, **Route Planner**, **Control Panel Tasks** | Navigation time, look-away rearm success, wrong-screen entries, recovery and independent exits |
| **7 — Advanced Precision** | Small and close-spaced targets; Level 6 | Adaptively reduce target size and spacing only after previous success | **Precision Grid**, **Fine Targeting**, **Accuracy Challenge** | Minimum reliably selectable size, region-specific error rate, corrected selections/min, target size and dwell sensitivity |
| **8 — Everyday Applications** | Integrated functional use; Level 7 | Adult-oriented AAC message composition and simulated practical computing | **Message Builder**, **Information Finder**, **Daily Task Simulation** | Functional task completion, effective selections/minute, correction effort, fatigue proxies, independent recovery |

**Progression rules:** Advancement is based on successful repeated demonstrated skills, not entertainment completion. A learner may replay any unlocked activity. Optional games should not block progression. Reporting must distinguish target arrival, sustained hold, and deliberate activation, including whether smoothing/size assistance was active. Each level must offer navigation/stop controls accessible using only **previously taught** skills. Level 1 may need a non-dwell gaze gesture, safe automatically returning mode, or carefully verified non-interactive route; this is a blocker for unattended novice operation, not permission to assume caregiver support.

**Adult interaction and visuals:** Dark slate/black background, cobalt/teal highlights, restrained amber successes, readable sans-serif typography, tasteful subtle motion, geometric panels and visual puzzles; no artificial child voice or cartoons. Use professional, non-patronizing instructions (e.g., “Look at the highlighted target” and “Target acquired”). Celebrate successful progress without overstimulation. Keep optional sound and all animation nonessential to input success. Allow adult-interest quizzes as optional recreation, avoiding literacy or trivia ability as a measure of gaze competence.

**Session design:** Short guided segments with user-controlled rest, repeat, and exit as soon as those controls are teachable; any 1–3 minute round lengths are suggestions, not forced timeouts. Test accessibility and fatigue in the target population.

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
2. **SKILLS** — implemented as six large gaze-dwell categories: Level 0, Targets, Dwell, Tracking, Endurance and Function. Each category displays six defined metrics in a 2×3 card layout, totaling **36 NA placeholders** until validated measurement sources are wired. Do not require this tab during first-time novice onboarding.
3. **MAPS** — existing persistent 7-column × 5-row Accuracy and Eye Jitter tablet maps side by side at larger readable sizes; consistent green/yellow/red/NA and units. Future: explicit legend, sample-count overlay, user-controlled selection to show one region (without ever requiring tiny 35-cell gaze activation for novice use).
4. **SESSIONS** — four separate placeholder tiles for session history, early/middle/late matched-task comparisons, user-controlled conditions notes, and user-triggered .txt export (implemented for current aggregates). No historical sessions are fabricated.

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

### Phase 2 — Implement audio-only Level 0, preserve Level 1, then build Level 2

- [ ] Implement first-run **Level 0** with the eye-shaped gaze cursor, spoken-only instructions, context-sensitive verbal feedback, slow pursuit exercise, taught final large-button dwell, and independent non-dwell recovery; see [spec](LEVEL_ZERO_AUDIO_TUTORIAL.md).
- [ ] Preserve the existing ten-target Level 1; Level 0 handles first-time cursor discovery and introduces the selection concept. Retain optional adult-interest trivia.
- [ ] Build Level 2 directional movement and adult-styled challenges; persist tracking/dropout and latency metrics.
- [ ] Validate novice usability without caregiver assistance under a safe observed setup.

**Exit gate:** repeatable independent session at both levels, complete gaze-only recovery routes.

### Phase 3 — Levels 3 & 4 (steady gaze and dwell activation)

- [ ] Implement stationary hold measurement and dedicated raw-jitter trials, with pauses/dropout filtering.
- [ ] Add adjustable dwell teaching sequence, adult-styled control-panel exercises, and explicit canceled/false-activation classification.
- [ ] Validate metrics against controlled gaze sample fixtures and user-selected dwell durations.

**Exit gate:** users can learn to dwell rather than being required to already know it.

### Phase 4 — Levels 5–8 (selection, navigation, precision, functional use)

- [ ] Implement Level 5 selection accuracy and Level 6 look-away rearm, correction and two-choice/multichoice guided menus.
- [ ] Introduce Level 7 safe target-size and spacing trials; avoid tiny buttons in navigation before success.
- [ ] Implement Level 8 adult functional challenges (Message Builder, Information Finder, Daily Task Simulation), effective selections/min and independence metrics.

**Exit gate:** all task routes, including rest and exit, remain accessible when performance declines.

### Phase 5 — Therapist reporting, personalized insights and export

- [ ] Populate SKILLS with real metrics, data-quality indicators and sample counts; filter comparisons by level, conditions and settings.
- [ ] Populate SESSIONS with chronological summaries and matched early-vs-late performance plot; show provisional trends with caveats.
- [ ] Add simple user-controlled optional post-session self-rating of effort/comfort, reachable with existing taught input.
- [x] Add gaze-operated **Export .TXT** with current progress, definitions, sample counts, two 7×5 maps, settings and explicit NA for unavailable data; creates a unique local Downloads file only on user activation. [ ] After new session/context collection is implemented, expand the same text report with genuine dated sessions and user-approved notes. Do not auto-email, sync or upload.
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
4. Build **Levels 2 and 3** (directional arrival and hold stability) and instrument them; retain the current Level 1 exercise and add a first-use non-dwell introduction and independent recovery route.
5. Implement **Levels 4–8**: dwell, selection accuracy, menu navigation, advanced precision and practical AAC tasks.
6. Complete Sessions measurement and expand the existing user-controlled TXT export after sufficient valid data exists.

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
