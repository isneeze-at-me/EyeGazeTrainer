# Eye Gaze API Viability — 60-second proof-of-concept

**Purpose:** Determine if Windows UWP `GazeInputSourcePreview` receives actual gaze coordinates from a connected Tobii PCEye 5. This is NOT mouse tracking.

## Build and run (Windows 11 + Visual Studio 2022)
1. In Visual Studio Installer, add **Universal Windows Platform development** (where available), Windows 10 SDK 19041, and UWP C# tools. UWP components are legacy/optional and may require individual components.
2. Open `EyeGazeApiViability.csproj` and restore NuGet packages.
3. Choose **Debug / x64 / Local Machine**, then press F5. If the project fails to load, create a new **Blank App (Universal Windows)** C# UWP project and replace its App.xaml, App.xaml.cs, MainPage.xaml, MainPage.xaml.cs and package manifest with these files, adjusting the project namespace to `EyeGazeApiViability`.
4. Allow Windows gaze access if Windows prompts. Windows may use a system permission dialog; we cannot safely bypass it.
5. Watch for `Device found`, live X/Y coordinates, and the dot following your gaze. The app exits automatically after 60 seconds while running.

**Important:** The UWP gaze API does not work in a plain WinForms/WinUI 3 app. Do not close TD Control for the first test. Keep your usual recovery controls available. This test does not close, suspend, restart, or modify TD Control, Ability Drive, camera drivers, or calibration.

## Interpreting the screen
- `Device found | CanTrackEyes=True | State=Ready`: Windows recognizes a compatible eye tracker.
- `LIVE EYE GAZE`: Direct gaze coordinates arrived; gaze cursor sample changes are counted.
- `PASS`: At least 5 gaze coordinate changes were detected.
- `Waiting for device`: Windows may not expose this tracker to the UWP API.
- `Device ... not ready`: Tracker detected but cannot stream valid eye gaze (for instance calibration/setup).

## Automatic exit safety limitations
The test counts 60 seconds from the app page loading and automatically calls `CoreApplication.Exit()` via a dispatcher timer. Because Windows can suspend UWP apps or show a first-run consent dialog, this is **not a guaranteed external kill switch** and may run longer if the app is suspended or its UI dispatcher is blocked. Never rely on this alone as the only recovery mechanism for someone who cannot use a physical mouse or keyboard. Test with a trusted person present on the first run.

## Data/privacy
No coordinates are saved or transmitted; samples are shown only in memory. No network APIs are used.

## Development limitations
These sources were prepared in a Linux environment without the Windows UWP compiler/SDK. They have **not been compiled or hardware-tested**. A Visual Studio build is required to validate the project and the actual tracker compatibility.
