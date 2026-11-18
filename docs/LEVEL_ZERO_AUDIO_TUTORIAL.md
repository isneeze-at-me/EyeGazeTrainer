# Level 0 — Audio-Guided Eye Cursor (Adult First-Run Experience)

**Status (2026-11-18):** Initial implementation is committed on the `Test` branch: the first-run Level 0 screen, eyeball-shaped gaze cursor, local Windows spoken instructions with activity-based feedback, slow geometric pursuit target, and a single large button using the shared 1.5-second dwell to open the existing home screen. Existing Level 1 and eye-tracker shutdown lifecycle were not changed. **Windows build and live PCEye 5/TD Control coexistence have not been validated.** A spoken non-dwell gaze gesture, replay option, and aggregate six-metric Level 0 reporting have since been added. They still require practical accessibility validation; do not represent this as a complete accessible release.

## Core experience

- **No written instructional text.** All directions, feedback and encouragement are spoken using locally available Windows speech (SAPI); no internet, microphone or ElevenLabs key is required. The only essential visual controls are a cursor, a target, and finally a large accessible main-screen icon/button.
- The **cursor itself looks like a pair of eyeballs**. Their position follows the user's gaze using the existing smoothing filter. Do not display separate eyes that follow the cursor or look toward the user; that creates a competing fixation point.
- Visually adult: neutral black/slate background, restrained blue accents, simple professional eye shapes rather than cartoon/googly eyes, one softly glowing moving geometric target. No progress text, stickers, childlike music or childish congratulations.
- No button, dwell, hold or reading required in Stages A and B. Each skill is taught before it becomes a required control. User must be able to independently recover or exit before selection is taught; design and hardware-test this non-dwell safety route first.

## Stage A: free exploration

1. Once valid gaze is detected, say: **“Your eyes move the cursor. Take a moment to look around the screen.”**
2. Let the learner move the eyeball cursor freely without selecting anything. Spend at least approximately 10–15 seconds but **never advance solely on a timer**.
3. Detect genuine horizontal/vertical exploration and visits to coarse screen regions. Corner visits require multiple valid samples across approximately 250ms, not a single noisy event, and must be possible without reaching the extreme edge.
4. Use brief, contingent spoken feedback. Examples:
   - After horizontal movement: **“Nice. You've moved the cursor from side to side.”**
   - After vertical movement: **“Good. Now try looking toward a corner.”**
   - On an actual stable corner-region visit: **“Good job looking toward the corner.”**
   - After broad exploration: **“Excellent. You've explored much of the screen.”**
   - After a long pause with valid tracking: **“Take your time. Try looking to the left or right.”**
5. Do not require all four corners; adapt to observed accessible range and avoid trapping a person with limited movement. Give no false praise for unseen motion.

## Stage B: pursuit

1. After meaningful exploration, say: **“Now follow the moving target with your eye cursor.”**
2. Introduce one large slowly moving circle/ring, inset away from the screen edges. Start left-to-right, then gentle vertical arcs. No jumps, sharp changes or timed penalties.
3. Keep the cursor as eyeballs throughout.
4. Use raw gaze positions to measure proximity to the moving target, fraction of valid tracked time near it, continuous pursuit duration and interruptions, with generous tolerances. Do not assume tracking loss means the learner failed.
5. If observable following is consistent: **“You're following it well.”** If there is sustained difficulty: **“Try keeping your eyes near the moving circle.”** Pause speech while the learner is actively pursuing.
6. Continue until there is meaningful participation and observed progress, with individualized relaxed progress conditions rather than a rigid pass/fail timer.

## Stage C: first intentional selection

1. Remove the moving target and present one **very large Main Screen icon/button**. The label may be conveyed entirely by an icon and spoken audio; no instructions are written on screen.
2. Say: **“Now look at the large button to open the main screen.”** Then explain the new interaction before demanding it: **“Keep looking until the circle fills.”**
3. Use the app's existing shared dwell wheel, generous hit area, and an introductory forgiving activation period. Do not make simply crossing the button trigger navigation.
4. On success, say: **“You did it. Opening the main screen.”** Persist first-run completion and navigate to the normal home screen.
5. On repeated incomplete intentional holds, after cooldown say: **“Almost there. Keep looking until the circle fills.”** Provide a gaze-only non-dwell recovery mechanism during any stage.

## Spoken feedback selection

- **Speech is triggered by observed activity**, not a narration schedule. Favor meaningful coaching over constant praise. Do not repeat a corner greeting every time a gaze cursor re-enters the same corner.
- Maintain a single speech queue and deduplicate events. Prioritize safety/recovery, new-stage instructions, difficulty coaching, then achievement feedback.
- Allow a **12–20 second cooldown** between ordinary feedback prompts. Short mandatory stage instructions may be spoken when previous speech has stopped. Never overlap spoken phrases.
- Avoid a stream of prompts following each eye movement. Silence is a feature when the user is engaging successfully.
- Tracking unavailable: avoid guessing or blaming the learner. After confirmed sustained loss, optionally say **“I can't see your eye movement right now. Try looking toward the center of the screen.”** Don't speak it repeatedly.
- Use a calm, respectful adult voice at a comfortable rate and volume; no childlike voice or childish praise. Future audio settings must be accessible without requiring reading or a skill not taught yet.
- No listening/microphone processing needed.

## Reportable Level 0 observations

- Time to first valid gaze and ratio of valid tracked time; explicit unknown for unavailable validity.
- Raw gaze extent along both axes and screen regions visited, including genuine stable corner visits.
- Moving-target pursuit: time close to target, sustained-follow duration, lost-tracking intervals, and movement-path complexity.
- Final selection: started dwells, canceled dwells, successful selection and time to activate.
- Aggregate measurements only, stored locally; do not retain raw time-stamped gaze positions or audio of the user. These are training observations, **not clinical diagnoses**.
- Mark measurements unavailable until adequate samples exist; use confidence/sample counts to prevent false conclusions.

## First-run and safety rules

- Run Level 0 on an actual first launch; skip for returning learners who completed it. Provide an accessible Replay Introduction option on the home/levels menu after implementation.
- Never force a novice to use the 1.5-second regular menu dwell before Level 0 has taught selection. After Stage C, transfer to the existing Level 1 without changing its ten-target exercise.
- Do not time out of the tutorial merely because the gaze tracker is slow; ensure a previously learned gaze-only way to rest, recover and leave.
- The existing PCEye 5 / TD Control coexistence issue must be tested under supervision before claiming independent use. Do not modify gaze watcher activation/cleanup based solely on this design.
- Keep speech nonblocking so gaze controls and safe Exit are responsive even during announcements.

## Acceptance checklist

- [ ] On first launch, audio instruction plays once when gaze is ready; no written instructional text appears.
- [ ] Only the **eye-shaped cursor** follows gaze (not two stationary tracked eyes).
- [ ] Stage A and B need no dwell or keyboard/mouse input.
- [ ] Stable, correctly observed corner visits trigger occasional accurate spoken feedback; no repetitive speech.
- [ ] Slow moving target is followable and does not enter unsafe edge zones.
- [ ] Stage C verbally teaches first dwell, uses the existing visual dwell wheel and goes to home on success.
- [ ] Progression never relies solely on elapsed time or requires all four corners.
- [ ] Level 0 completion is persisted; subsequent launches open home and accessible replay is available.
- [ ] Local reports store aggregate data, with NA for insufficient observations.
- [ ] Gaze-only safety/back/recovery and TD Control handoff are supervised and verified.

**Implementation progress:** [x] first-run routing for a new learner (existing learners with training records skip); [x] local speech service; [x] eye-shaped cursor; [x] free-movement and corner feedback; [x] animated slow target and sampled proximity; [x] large final dwell button; [x] completion flag and entry to home; [x] first implementation of a gaze-only non-dwell escape (top-left, bottom-left, repeat; spoken coaching); [x] gaze-dwell REPLAY INTRODUCTION button in Levels; [x] persist six aggregate Level 0 summary measurements and populate Skills / TXT export; [ ] user test and refine no-dwell escape reliability, instruction pacing and metric thresholds; [ ] supervised Windows compile/layout/device tests. Level 0 is **development-preview only** until those remaining items are addressed.

### Current implementation notes

- First-run Level 0 starts automatically only when no previous Level 1 acquisition/completion/run records are present and the completion flag is absent. It does not override an existing user's stored training progress.
- Stage A automatically speaks when the **first valid gaze sample** is observed; stable coarse corner visits require repeated valid points. The target-following stage uses a slow sinusoidal arc, sampled at intervals to prevent frame-rate inflation of follow counts.
- Optional **movement-only escape:** after about 22 seconds the user is told how to sweep between the upper-left and lower-left corners twice to return to home without activating a dwell button. This is provisional: the two-sweep gesture can be difficult for a new learner or accidentally match exploration, and must be refined through supervised evaluation.
- Stage C displays one large home icon and uses the same shared dwell wheel as the rest of the app. After selection the tutorial marks completion and opens the existing home screen.
- Aggregate first-run measurements for initial response, 9-region coverage, four corners, horizontal/vertical range, pursuit sample-proximity, and final button selection are saved locally only on completion. These fields appear in Diagnostics > Skills > Level 0 and the existing TXT therapist report when recorded.
- Returning users can choose **REPLAY INTRODUCTION** on the Levels menu; the existing Level 1 training and its bonus round remain unchanged.
- Gaze watcher creation, ownership, TD Control coexistence, and staged shutdown were not modified.

**Safety and build disclaimer:** Static code/XAML integration checks do not establish an executable build, usability, or tracker compatibility. Do not use Level 0 as unattended independent access until on-device safety, audio availability and non-dwell recovery have passed.

## Bug-fix notes — 2026-11-18 (voice / early exit / destination)

- **Voice:** The invisible UWP speech `MediaElement` is kept **Visible at 1×1 and 0 opacity** rather than `Collapsed`. The synthesized `SpeechSynthesisStream` stays alive until a following phrase or exit, instead of being closed by the synthesis method after an estimated delay. Media playback errors are traced to the existing local diagnostic trace. These changes address likely sources of silence but must be verified on the Surface/Windows audio device.
- **Unintended early exit:** Removed the experimental upper-left/lower-left repeated-sweep shortcut. Normal exploratory gaze movements could match its pattern and send the learner away before pursuit or the final button. No automatic exit should occur on exploration. A new, explicitly requested and independently accessible pre-dwell escape control must be designed and tested separately; do not claim novice independent escape is solved yet.
- **Destination:** After a successful deliberate dwell on the single final large button, Level 0 saves measurements/completion and goes to the **Levels screen**, not the main home menu. The voice says “levels screen,” and the button's accessibility name matches.
- **No change** to the shared gaze watcher, existing Level 1 lesson, bonus round or staged device cleanup.
- **Build and hardware tests outstanding:** verify initial audible speech and each stage prompt; ensure audio does not overlap; observe target and final button in sequence; verify Level 0 can't leave before deliberate selection; verify successful dwell enters Levels; verify TD Control coexistence.

## Voice and motion fix — 2026-11-18 (second device test)

**Reported:** No audio after retaining the stream and making the media element visible; the pursuit target also moved slightly jerkily.

**Code changes on Test:**
- Replaced the XAML `MediaElement` speech player with a nonvisual `Windows.Media.Playback.MediaPlayer` and `Windows.Media.Core.MediaSource.CreateFromStream`. Speech still uses the local `Windows.Media.SpeechSynthesis.SpeechSynthesizer` with no network dependency. The synthesized stream remains alive while audio is in use and is disposed on replacement or Level 0 exit. Native media failures are logged via the existing trace mechanism.
- Replaced the 50 ms `DispatcherTimer` that moved the target in 20 Hz steps with `Windows.UI.Xaml.Media.CompositionTarget.Rendering`, synchronized to display redraw. The callback is attached only during pursuit, detached at the final-button stage or on exit, so it does not consume unnecessary frame resources elsewhere.
- The eye cursor, stage criteria, Level 1 and shared gaze-device cleanup remain otherwise unchanged. Completion still opens the Levels screen.

**Pending on-device checks:** Rebuild the Test branch and verify audible instruction on the first valid gaze, voice playback for each stage, a smooth target on the Surface screen, audio error trace if still silent, and successful handoff to Levels. Static structural checks passed; UWP compilation and Tobii hardware verification have **not** been run here.

## Guided introductory movement sequence — implementation update

The initial Level 0 free exploration has been revised to teach deliberate cursor movement in this order:

1. **Discover movement:** Spoken welcome asks the learner to move their eyes around the screen. A stationary cursor is not praised or passed. A minimum of **15 cumulative seconds of observed non-jitter cursor movement** and meaningful cumulative distance triggers the next step; if no valid gaze or movement is detected, offer a helpful spoken reminder.
2. **Up and down:** Speak the up/down task. Remain in it for at least **15 seconds**, requiring multiple genuinely vertical movements, changes of direction and useful travel before advancement. If gaze moves predominantly horizontally or diagonally, describe what was observed and gently remind the learner to look up/down. Do not praise unrelated movement.
3. **Left and right:** Celebrate completing vertical movement, then explain left/right. Remain for at least **15 seconds** with genuine horizontal direction reversals and travel. Coach specifically if the learner is practicing another direction.
4. **Circles:** Celebrate left/right control, then explain how to trace a large circle with the eyes. Require at least **15 seconds**, measurable angular movement around screen center and coverage of multiple directions. Do not pass because of stationary jitter or elapsed time alone.
5. **Follow the moving circle:** After the circular movement is demonstrated, give a spoken congratulation and introduce the existing slow moving visual target. This preserves the original pursuit and final home-icon selection phases.

Coaching is triggered periodically when the requested task is not being observed. The detector samples stabilized cursor positions at up to 10 Hz, ignores tiny and implausibly large jumps, and does not mistake a timer for practice completion. The training state resets on replay. The voice system marshals native MediaPlayer completion back to the UWP UI dispatcher before revealing controls.

**Status:** Implemented on `Test` but not compiled or tested with an actual PCEye 5. The detection thresholds are initial estimates and need device testing and accessibility tuning. No eye tracker acquisition, ownership, or staged cleanup was changed.

**Step 1 revision (2026-11-18):** Require **15 seconds of cumulative actual free movement**, not 15 seconds elapsed on screen. No specific direction is prescribed in this phase. Each sampled interval contributes only when the stabilized cursor moves beyond the jitter threshold (and without a large invalid jump). Pauses do not count; the learner may take as long as needed. A minimum total normalized travel of 0.50 also applies. The next step provides the up/down instruction only after these criteria are met.
