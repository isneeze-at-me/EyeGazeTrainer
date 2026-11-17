# Architecture Decision Records (ADRs)

Decisions here describe current choices and known tradeoffs. Record major new choices with date, alternatives, rationale, consequences and verification.

## ADR-001 — Test Windows gaze API before vendor SDK purchase
- **Date:** 2026-11-17
- **Status:** Accepted for diagnostic
- **Decision:** Use Windows UWP `GazeInputSourcePreview` to test direct gaze position from PCEye 5.
- **Why:** Avoid unnecessary SDK purchase or assumptions about Tobii Pro SDK compatibility.
- **Tradeoff:** UWP-specific API; exact device and coexistence behavior must be tested.
- **Evidence:** User-reported live-coordinate PASS in diagnostic.

## ADR-002 — Require skill-gated, helper-independent lessons
- **Date:** 2026-11-17
- **Status:** Product requirement, not yet implemented
- **Decision:** No level or surrounding UI may require a skill the user has not demonstrated. Level 1 is gaze-only.
- **Why:** Dwell, clicking, navigation and settings cannot be prerequisites to learning them.
- **Consequences:** Onboarding, pause, recovery, permissions and progression must be designed as part of the curriculum.

## ADR-003 — Preserve essential accessibility controls
- **Date:** 2026-11-17
- **Status:** Accepted safety constraint
- **Decision:** Do not automatically terminate or disable TD Control, Communicator 5 or Ability Drive during early tests.
- **Why:** Failure may strand a user without essential communication/control.
- **Consequences:** Independent coexistence/handoff tests require specific recovery procedures.

## ADR-004 — Maintain stable and integration branches
- **Date:** 2026-11-17
- **Status:** Accepted process
- **Decision:** Use `main` for validated baseline and `Test` for active development; prefer PRs to promote code.
- **Why:** Transparent handoff, review and regression control.
- **Consequences:** Documentation and test evidence should accompany changes.
