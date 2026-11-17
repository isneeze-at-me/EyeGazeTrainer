# Developer handoff

## Project at a glance

EyeGazeTrainer is an ALS-focused, guided eye-gaze learning application **in planning**. The checked-in code is a **diagnostic prototype**, not the trainer. User confirmed a PCEye 5 gaze-coordinate PASS on Windows 11 using the UWP gaze API on 2026-11-17.

**Repository:** `isneeze-at-me/EyeGazeTrainer`.
**Typical local checkout:** `C:\Users\ToltTech\Documents\CodingProjects\EyeGazeTrainer` (machine-specific; not required for other contributors).
**Current UWP project name:** `EyeGazeApiViability`.
**Development:** VS Community 2022, UWP tools, Windows SDK 10.0.19041.0, C#/XAML, Debug/x64.

## Read in order

`README.md` -> `docs/PRODUCT_REQUIREMENTS.md` -> `docs/ARCHITECTURE.md` -> `docs/TESTING.md` -> `docs/DECISIONS.md` -> `CONTRIBUTING.md` -> `AGENTS.md`.

## Demonstrated vs uncertain

**Demonstrated (user report):** build works, deploy recognized, gaze coordinates detected and changing.
**Not demonstrated:** tracker independence with TD Control absent, reliable 60-second termination, accessible permission flow, training lesson, safety recovery under failure conditions.
**Never assume:** successful PCEye 5 acquisition makes it safe to switch off other AAC or wheelchair-related software.

## Near-term backlog

1. Confirm consistent diagnostic startup/exit and capture redacted test evidence.
2. Establish separate **recovery-first** TD Control coexistence test; retain accessibility software and have a fallback before attempting any exclusive handoff.
3. Design and implement a self-guided **gaze-only Level 1** with no dwell/click and no unlearned navigation.
4. Define tests for tracking loss, permission dialogs, inability to advance, rest and exit.
5. Refactor direct gaze input behind a provider interface.
6. Only after review, add more lessons and user progress persistence.
7. Rename internal project/namespace/app identity to EyeGazeTrainer in its own change, verify fresh deployment and migration implications.

## Documentation preservation

Each PR should explain change scope, reason, code paths, before/after behavior, prerequisite skill, testing (or reason untested), environment and risks. Keep ADRs and testing ledger current. AI-generated future documentation must distinguish implemented code from requirements and reports.

## Release readiness gates

No public training release until first-run permissions, fail-safe recovery, genuine independent operation, hardware/software coexistence, accessibility testing and privacy review have been validated. This is a development plan, not a claim of medical device certification.
