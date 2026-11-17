# Architecture and constraints

## Current implementation — diagnostic only

- Language: C# / XAML; UWP application, Visual Studio 2022 project `EyeGazeApiViability.csproj`.
- Target: Windows SDK 10.0.19041.0, Debug/x64 or Release/x64 configurations.
- NuGet: `Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14.
- Manifest: `Package.appxmanifest` includes `gazeInput` device capability.
- Startup: `App.xaml.cs` creates/navigates a Frame to MainPage.
- `MainPage.xaml.cs` creates `GazeInputSourcePreview.CreateWatcher()`, listens for tracker added/updated/removed, checks `CanTrackEyes` and `ConfigurationState`, gets `GazeInputSourcePreview.GetForCurrentView()` and handles `GazeMoved`.
- UI displays gaze X/Y values, a position dot, valid samples, and coordinate-change count. PASS threshold: at least five changes exceeding 2 coordinate units from the previous sample.
- An in-process DispatcherTimer checks a Stopwatch and calls `CoreApplication.Exit()` around 60 seconds after the MainPage Loaded event, if UI thread is running.
- No local gaze-data logging, network data collection, lessons, settings or persistent learner profiles currently implemented.

## Important limitations

- `Windows.Devices.Input.Preview` here is a UWP gaze API, not an ordinary WinForms mouse API.
- Coordinates are for the current app view, not necessarily raw unfiltered camera sensor data.
- Positive samples do not prove exclusive tracker access, TD Control shutdown compatibility, clinical accuracy or permission-free startup.
- `CoreApplication.Exit()` is not an independent watchdog or guaranteed fallback.
- `EyeGazeApiViability` is the current internal assembly/application name, despite the repository being named EyeGazeTrainer. Rename as a separate validated change.

## Proposed modular evolution (not implemented)

1. `IGazeInputProvider`: gaze position, validity, timestamps, connection state; Windows provider first.
2. `TrainingEngine`: lessons, state transitions, skill prerequisites, progression and recovery.
3. `InteractionPolicy`: per-skill allowed gestures and control affordances.
4. `Feedback`: gaze indicators and optional accessible text/audio cues.
5. `SessionStore`: local settings and progress, consent-aware and privacy-preserving.
6. `SafetyCoordinator`: tracking-loss handling, fallback/recovery and interaction with competing accessibility software.
7. `Diagnostics`: optional privacy-safe health/status measurements without automatically storing gaze traces.

Keep training logic decoupled from UWP API so a future supported Windows app framework or another eye tracker can be evaluated without rewriting the curriculum.

## Key questions

- Does PCEye 5 gaze streaming continue when TD Control is stopped or paused?
- How will the application avoid interfering with Ability Drive or other essential interfaces?
- How are OS permissions and initial installation handled for gaze-only users?
- How do we make early-lesson pause/exit possible without introducing an unlearned gesture?
- Can a separate watchdog reliably recover after UI hangs or process termination?
- What distribution and Windows framework support requirements apply?

Do not claim these questions are resolved.
