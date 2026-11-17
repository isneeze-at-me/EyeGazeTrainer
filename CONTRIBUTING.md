# Contributing

## Branch and review policy

- `main`: stable and preferably protected. Do not experiment directly here.
- `Test`: integration/testing branch. Develop changes here or on `feature/<short-name>` / `fix/<short-name>` branches cut from `Test`.
- After validation, open a pull request from `Test` to `main`. Describe scope, impact, risk, tests and rollback.
- Do not merge unresolved regressions, especially loss-of-control or assistive-tech integration problems. Avoid force pushes to shared branches.

## Commits

Use descriptive Conventional Commits: `feat(scope): ...`, `fix(scope): ...`, `docs: ...`, `test(scope): ...`, `refactor(scope): ...`, `chore: ...`.

One conceptual change per commit. Prefer imperative subjects under about 72 characters; explain **why**, user impact and verification in the body when not obvious. Examples:

```
fix(uwp): declare application container identity

Restore UWP deployment recognition in VS 2022.
Validation: Debug/x64 rebuild succeeded on 2026-11-17.
```

```
feat(training): introduce gaze-only discovery lesson

No dwell or click is required; progression uses observed gaze.
Validation: pending PCEye 5 usability testing.
```

Never claim a test passed unless it was actually run.

## Pull request checklist

- [ ] Linked issue or stated purpose and acceptance criteria
- [ ] Scope, important implementation choices and risks described
- [ ] Build/test results, platform/device/versions and date recorded
- [ ] Effects on independent operation, accessibility and recovery reviewed
- [ ] `docs/` and `AGENTS.md` updated where behavior/design changes
- [ ] No personal data, tokens, binaries or build artifacts committed
- [ ] Recovery/rollback documented if input handling or app lifecycle changes

## Quality expectations

Keep input acquisition, user interaction/lesson state, navigation and persistence independent. Avoid blocking UI thread. Make failure states explicit. Do not close or suppress TD Control, Communicator 5 or Ability Drive without separate validated interoperability and restoration procedures. Introduce no app behavior that requires an unlearned interaction skill.

## Handoff

A new contributor should begin with `README.md`, `docs/HANDOFF.md`, `docs/ARCHITECTURE.md`, `docs/TESTING.md` and `docs/DECISIONS.md`. Update known gaps whenever work changes them.
