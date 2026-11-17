# Testing and evidence ledger

## Reported evidence — 2026-11-17

| Test | Environment | Result | Evidence / boundary |
| --- | --- | --- | --- |
| Build after manifest and UWP project corrections | Visual Studio Community 2022 17.14.45, Debug, x64, Windows 11 | **PASS — user screenshot** | Rebuild log showed 1 succeeded, 0 failed |
| Deployment recognition | Same setup | **PASS — user report** | Deploy checkbox became selectable and checked |
| Windows gaze API acquisition | Tobii Dynavox PCEye 5, Windows 11, diagnostic UWP app | **PASS — user report** | User stated that coordinates were detected and diagnostic displayed PASS |
| Independent streaming with TD Control stopped | Not executed | **NOT TESTED** | Must be tested separately with reliable recovery |
| Automatic termination at 60s | Not independently confirmed | **NOT VERIFIED** | Timer exists in source; UI suspension/hang can defeat it |
| Fully independent ALS user operation | Not executed | **NOT TESTED** | Current prototype is not ready for unattended use |

User test results above are not an independent automated test run. Reproduce before asserting broad hardware compatibility.

## Reproduce diagnostic

1. Confirm Visual Studio UWP tools and Windows SDK 10.0.19041.0.
2. Select Debug/x64/Local Machine; deployment enabled.
3. Rebuild and note errors/warnings.
4. With a recovery person present on first runs and TD Control left available, run the diagnostic.
5. Record device detection, tracking readiness, whether gaze X/Y changes as eyes move, PASS/FAIL, permission dialog behavior and whether auto-exit occurred at approximately 60 seconds.
6. Do **not** test closing TD Control or Ability Drive as part of this first diagnostic.

## Template for new evidence

- Date and tester (use a non-identifying alias in public repository):
- Git commit SHA / branch:
- OS build, IDE version, architecture, display scaling:
- Tracker model/firmware and accompanying software version:
- Setup / permissions / whether TD Control active:
- Expected behavior:
- Observed behavior and PASS/FAIL:
- Exact errors and repeatability:
- Recovery required / any safety incident:
- Links to issue/PR and redacted logs:

Do not upload personal gaze-coordinate traces, screenshots revealing private information or identifying health details.
