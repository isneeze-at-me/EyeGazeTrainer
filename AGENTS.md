# AI developer and documentation guidance

This file is guidance for coding assistants and documentation generators working on EyeGazeTrainer.

## Sources of truth
1. Current checked-in source and package manifest determine what is implemented.
2. Dated results in `docs/TESTING.md` determine what has actually been tested.
3. `docs/DECISIONS.md` records design decisions and tradeoffs.
4. `docs/PRODUCT_REQUIREMENTS.md` documents intended behavior, **not** completed features.
5. `docs/HANDOFF.md` records open questions and next actions.

Mark statements **Implemented**, **User-verified**, **Planned**, **Unknown**, or **Unverified**. Never equate successful compilation with a passing hardware test. Do not invent API compatibility, tracking rates, test outcomes, safety or accessibility certifications.

## Critical product invariant
A person with ALS must be able to learn and operate without outside assistance during ordinary app use. At every stage, all necessary controls including settings, rest, retry, navigation and exit must be operable with **previously learned** skills. Level 1 is gaze only: no dwell, clicks or menus. Do not automatically progress on tracking loss. Provide alternatives for system permission prompts and initial deployment; these OS-level prerequisites are not solved by the current prototype.

## Safety and privacy
- Current one-minute UWP timer is **not** a guaranteed independent kill switch.
- Never automatically disable TD Control, Ability Drive, Communicator 5 or a person's communication channel without verified recovery and explicit user direction.
- Do not introduce network transfer or persistent storage of gaze samples without an explicit specification and privacy review.
- Keep actual gaze coordinates separate from OS mouse-pointer data. Distinguish direct gaze-stream proof from control-software independence.

## Coding and docs workflow
- Develop on `Test` or feature branches; treat `main` as validated baseline.
- Use precise commits and PR descriptions with rationale, tests and known limitations.
- Update architecture, requirements, decision log and test evidence whenever impacted.
- For generated manuals, identify audience and release/version; cross-check each behavior with current code, mark unavailable features, include provenance of test evidence and human review before publication.
- Prefer reproducible build instructions, data flow/sequence diagrams, interfaces, error paths, lifecycle behavior and acceptance criteria over unsupported prose.
