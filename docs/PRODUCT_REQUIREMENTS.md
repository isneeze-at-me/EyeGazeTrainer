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
