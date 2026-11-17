> **STOP TESTING — CRITICAL (2026-11-17):** During the timed diagnostic exit, the screen froze, briefly went black, and TD Control stopped responding, requiring a reboot (user report). **Do not run this diagnostic or test with TD Control stopped until issue [#9](https://github.com/isneeze-at-me/EyeGazeTrainer/issues/9) is investigated and a safe recovery is validated.** Gaze-input PASS remains confirmed, but safe shutdown is NOT confirmed.

# EyeGazeTrainer

**Status:** Windows Gaze API feasibility prototype; not a released training application.

EyeGazeTrainer is intended to become a guided, independently operated eye-gaze training program for people living with ALS and other users who cannot use conventional mouse or keyboard input. The overriding principle is **never require an interaction skill before the user has been taught and demonstrated it**.

## Current proof of concept

The repository currently contains a UWP/C# diagnostic named `EyeGazeApiViability` (internal name retained until a dedicated rename change). It uses the Windows `Windows.Devices.Input.Preview` gaze API, detects eye trackers and displays gaze coordinates, a dot, sample counts and a basic PASS indication. It exits after approximately 60 seconds of active UI execution.

**Observed on November 17, 2026 (user-reported):**
- Visual Studio Community 2022 17.14.45, Debug/x64: rebuild succeeded.
- Tobii Dynavox PCEye 5: Windows gaze API detected coordinates and displayed PASS.
- The initial UWP manifest PhoneIdentity and project recognition/build-deployment issues were repaired.

**Not yet demonstrated:** independence from TD Control when it is stopped; reliable tracker handoff; operation across other eye trackers; production reliability; independent fail-safe exit; a finished lesson; clinical training effectiveness.

A reported PASS means samples changed, **not** that TD Control can safely be closed, that calibration is adequate, or that a production app is ready.

## Build the diagnostic

1. Use Windows 11 and Visual Studio 2022 with **Universal Windows Platform tools** and **Windows 10 SDK 10.0.19041.0**.
2. Open `EyeGazeApiViability.csproj`.
3. Configure **Debug**, **x64**, **Local Machine** with **Deploy** enabled in Configuration Manager.
4. Restore packages and rebuild. Current NuGet dependency: `Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14.
5. For an initial test, keep TD Control running and have someone able to restore communication if control is lost; then start the application.
6. If prompted, grant Windows gaze permission. Look around and observe device status, gaze coordinates and PASS/FAIL.

**Safety:** the 60-second timeout uses an in-process UI dispatcher timer, **not** an independent watchdog. A hang, suspended UWP app or blocking permission dialog may prevent timely exit. Do not use it as the sole recovery method. Do not terminate TD Control or Ability Drive for this test.

## Project goals and design invariants

- A user should be able to start and finish training without a helper, physical keyboard/mouse or speech input during ordinary operation, after supported gaze tracking and necessary system permissions are already configured.
- First launch and introductory lessons must not use dwell, clicking, menus or precise target selection.
- Skills unlock progressively: gaze awareness -> target switching -> gaze holding -> dwell -> look-away rearming -> navigation -> precision -> computer controls.
- Pause/rest/recovery/navigation/settings must never demand skills not yet learned.
- Never silently advance when gaze tracking disappears. Support fatigue and variable accuracy without punishing the user.
- Keep exercises adult-oriented, clear, legible, and low effort. Do not interfere with communication or wheelchair-control software.
- Prefer on-device processing; no collection, transmission or storage of gaze coordinates in the current diagnostic.

## Repository map

| Path | Role |
| --- | --- |
| `EyeGazeApiViability.csproj` | Legacy UWP C# application project |
| `App.xaml(.cs)` | UWP startup |
| `MainPage.xaml(.cs)` | Diagnostic interface, gaze watcher and timer |
| `Package.appxmanifest` | App identity and gaze capability |
| `docs/PRODUCT_REQUIREMENTS.md` | Intended behavior and acceptance criteria |
| `docs/ARCHITECTURE.md` | Implementation facts, constraints and unknowns |
| `docs/TESTING.md` | Repeatable checks and confirmed evidence |
| `docs/DECISIONS.md` | Architecture decision log |
| `docs/HANDOFF.md` | Handoff and roadmap |
| `AGENTS.md` | Guidance for AI-assisted coding and documentation |
| `CONTRIBUTING.md` | Branch, commit and PR conventions |

## Branches and contribution

- `main`: stable baseline. Prefer reviewed PRs and successful builds for merges.
- `Test`: ongoing development/integration. Start features here or in short-lived feature branches off `Test`.
- Do not commit generated `bin/`, `obj/`, `.vs/`, logs or personal gaze-test data.
- Use **Conventional Commits** (e.g. `fix(uwp): register app identity for deployment`). State reason and tests in the commit body when useful.
- Update documentation and evidence in the same PR as code changes. See [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation generation

The `docs/` files and `AGENTS.md` intentionally separate **implemented**, **verified**, **planned** and **unknown** information. An AI may generate future developer/user documentation from them, but must not turn goals into claims of implemented behavior. Cite relevant code paths and dated testing evidence; confirm claims against source before publishing.

## Further information

Read [handoff](docs/HANDOFF.md), [architecture](docs/ARCHITECTURE.md), [requirements](docs/PRODUCT_REQUIREMENTS.md), and [testing](docs/TESTING.md). No safety, efficacy or compatibility guarantees are made at this stage.
