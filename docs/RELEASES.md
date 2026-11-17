# Versioning and release policy

## Version numbers

Use SemVer `MAJOR.MINOR.PATCH` for future tagged builds. While experimental, use pre-release identifiers such as `v0.1.0-alpha.1`. A working build does not mean hardware-validated software.

**Current state (2026-11-17):** proof-of-concept only. No release has been created or tagged. The first tentative milestone is `v0.1.0` after a reproducible, reviewed diagnostic is ready; this is **not** an announced release date.

## Release checklist

- Build from an identified commit using documented Windows tools
- Confirm CI compilation (when CI is operational)
- Document eye tracker, OS and TD Control conditions for any hardware testing
- Verify appropriate control-loss recovery and app-exit behavior
- Produce release notes outlining additions, fixes, known limitations and upgrade instructions
- Check privacy, accessibility and software-interference risk
- Create a GitHub release and tag only after checks pass

## Documentation generation

Generate user guides from implemented behavior and developer docs from inspected source. Include code revision, exact environment and test provenance. Do not claim the training curriculum is present while the app remains a gaze diagnostic.
