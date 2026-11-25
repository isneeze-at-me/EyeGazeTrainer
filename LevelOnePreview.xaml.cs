// ============================================================================
// EyeGazeTrainer
// File: LevelOnePreview.xaml.cs
// Purpose: Coordinates training screens, direct gaze input, adaptive assistance, diagnostics, and native shutdown.
// ============================================================================

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Input.Preview;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace EyeGazeApiViability
{
    // Level 1: mouse-driven developer preview OR deliberately armed real gaze.
    // Warning: real gaze acquisition/release has caused TD Control loss of input.
    // This is NOT approved for independent or unattended use (issue #9).
    public sealed partial class LevelOnePreview : Page
    {
        private const double DotDiameter = 44;
        private DisplayScaleManager displayScale;
        private double targetDiameter;
        private double bullseyeDiameter;
        private double pendingReferenceWidth;
        private bool displaySettingsVisible;
        private DisplayInformation displayInformation;
        private CoreCursor previousPointerCursor;
        private bool pointerCursorHidden;
        private Point lastDrawnGazeCursor = new Point(double.NaN, double.NaN);
        private bool lastDrawnGazeCursorWasLevelZero;
        private double TargetDiameter
        {
            get
            {
                // Keep the nominal 55 mm size unless a small view makes an
                // outer grid cell physically unreachable with that diameter.
                double w = TrainingCanvas.ActualWidth, h = TrainingCanvas.ActualHeight;
                if (w <= 0 || h <= 0) return targetDiameter;
                return Math.Min(targetDiameter, Math.Max(1, 2 *
                    (Math.Min(w / SpatialAccuracyMap.Columns, h / SpatialAccuracyMap.Rows) - 8)));
            }
        }
        private readonly MasterCursorSmoothingFilter cursorFilter = new MasterCursorSmoothingFilter();
        private readonly AdaptiveGazeProfile gazeProfile = new AdaptiveGazeProfile();
        // A completed round visits every cell of the existing full-screen map.
        private const int TargetsPerLevel = SpatialAccuracyMap.Rows * SpatialAccuracyMap.Columns;
        private const int InterTargetDelayMilliseconds = 500;
        private readonly LevelOneTargetDiagnostics levelOneDiagnostics = new LevelOneTargetDiagnostics();
        private readonly List<int> remainingTargetCells = new List<int>();
        private readonly int[] plannedDirections = new int[8];
        private DispatcherTimer targetDelayTimer;
        private int targetCell = -1;
        private int targetDirection = -1;
        private DateTimeOffset targetShownAt, targetLastSampleAt;
        private double? targetArrivalSeconds;
        private bool targetTrialInterrupted;
        private int targetMisses;
        private bool backOnRight;
        private readonly Random placementRandom = new Random();
        private readonly TrainingProgress trainingProgress = new TrainingProgress();
        private readonly TrainingReport trainingReport = new TrainingReport();
        private readonly DwellRecommendation dwellRecommendation = new DwellRecommendation();
        private DispatcherTimer practiceTimer;
        private DateTimeOffset lastPracticeTick;
        private readonly SpatialAccuracyMap spatialAccuracy = new SpatialAccuracyMap();
        private readonly SpatialJitterMap spatialJitter = new SpatialJitterMap();
        private readonly TextBlock[,] accuracyCells =
            new TextBlock[SpatialAccuracyMap.Rows, SpatialAccuracyMap.Columns];
        private readonly TextBlock[,] jitterCells =
            new TextBlock[SpatialJitterMap.Rows, SpatialJitterMap.Columns];
        private readonly Border[,] accuracyCellBorders =
            new Border[SpatialAccuracyMap.Rows, SpatialAccuracyMap.Columns];
        private readonly Border[,] jitterCellBorders =
            new Border[SpatialJitterMap.Rows, SpatialJitterMap.Columns];
        // Cache laid-out button rectangles, invalidated on screen/layout changes.
        // Do not cache disabled/hidden state, which can change independently.
        private readonly Dictionary<Button, Rect> gazeButtonBounds =
            new Dictionary<Button, Rect>();
        private Button[] diagnosticsButtons;
        private Button[] diagnosticsSkillsButtons;
        private Button[] diagnosticsSessionsButtons;
        private bool therapistExportInProgress;
        private DispatcherTimer therapistExportConfirmationTimer;
        private static readonly string[] TherapistSkillCategoryNames =
            { "Targets", "Dwell", "Tracking", "Endurance", "Function", "Navigation" };
        private Button[] skillCategoryButtons;
        private int selectedSkillCategory;
        private readonly TextBlock[] skillMetricNames = new TextBlock[6];
        private readonly TextBlock[] skillMetricValues = new TextBlock[6];
        private readonly TextBlock[] skillMetricStatuses = new TextBlock[6];
        private readonly TextBlock[] skillMetricDetails = new TextBlock[6];
        // These are explicitly NOT measured yet. Values must remain NA
        // until validated event-based statistics are implemented.
        private static readonly string[][] SkillMetrics =
        {
            new[]
            {
                "Target acquisition time|Median time to first assisted hit; excludes the required dwell",
                "Target precision|Mean raw settled center-accuracy score",
                "Target stability|Median raw settled RMS gaze spread (estimated mm)",
                "First-attempt success|Selected on first qualifying hold; tracking gaps excluded",
                "Directional control|Mean first-hold success across measured directions",
                "Spatial reach|Unique full-screen map cells selected out of 35"
            },
            new[]
            {
                "Dwell success rate|Completed deliberate dwell attempts divided by attempts",
                "Interrupted dwell attempts|Intentional holds ended before activation",
                "Accidental activations|Explicit no-select trials and documented unintentional choices",
                "Dwell completion time|Elapsed time from deliberate dwell onset to selection",
                "Look-away rearm|Successful release and reactivation after selecting a control",
                "Neighboring selection errors|Incorrect neighboring targets and required corrections"
            },
            new[]
            {
                "Tracking availability|Percent of requested practice time with valid gaze data",
                "Tracking interruptions|Distinct valid-to-invalid tracking transitions",
                "Longest tracking gap|Longest interval without valid gaze data",
                "Tracking recovery time|Time from detected loss until reliable tracking resumes",
                "Lost tracking by region|Locations associated with repeated tracking interruptions",
                "Tracker response latency|Measured delay to first reliable gaze signal"
            },
            new[]
            {
                "Early versus late accuracy|Comparable task accuracy at start and end of session",
                "Early versus late speed|Comparable target acquisition times during the session",
                "Early versus late jitter|Change in raw-gaze spread during equivalent tasks",
                "Rest breaks|Rest frequency, timing and time spent resting",
                "User-reported fatigue|Optional self-rating, never inferred from tracker jitter",
                "Best session conditions|Comparisons by posture, lighting and screen placement"
            },
            new[]
            {
                "Minimum reliable target size|Smallest consistently selectable target with trial counts",
                "Minimum reliable spacing|Closest reliably selectable adjacent targets",
                "Effective selections per minute|Correct useful selections accounting for corrections",
                "Functional task completion|Independent completion of practical communication tasks",
                "Independent navigation|Successful Back, menu, recovery and exit use",
                "Configuration and effort|Dwell duration, smoothing, hitbox size and effort rating"
            },
            new[]
            {
                "Menu navigation accuracy|Correct selections across independently navigated menus",
                "Back navigation success|Successful returns without assistance",
                "Task sequence completion|Independent completion of guided multi-step tasks",
                "Recovery from mistakes|Correcting mistaken selections and continuing",
                "Navigation efficiency|Time and selections to reach intended destination",
                "Independent exit reliability|Successful completion of the safe exit sequence"
            }
        };
        private Point targetPosition;
        private Point previousTargetPosition;
        private bool hasPreviousTargetPosition;
        private enum AppScreen { Menu, Levels, Lesson, Bonus, Settings, Diagnostics, LevelZero }
        private enum DiagnosticsTab { Overview, Skills, Maps, Sessions }
        private DiagnosticsTab selectedDiagnosticsTab = DiagnosticsTab.Overview;
        private AppScreen currentScreen = AppScreen.Menu;

        private GazeDeviceWatcherPreview watcher;
        private GazeInputSourcePreview gazeSource;
        private bool live;
        private bool stopping;
        private bool watcherStarted;
        private bool gazeSubscribed;
        private int cleanupStage;
        private DispatcherTimer cleanupTimer;
        private DispatcherTimer bonusTransitionTimer;
        private DispatcherTimer bonusQuestionTimer;
        private DateTimeOffset lastValidGazeSampleAt;
        private DateTimeOffset bonusTimerLastTick;
        private double bonusTimerSecondsRemaining;
        private readonly TriviaQuestionBank triviaBank = new TriviaQuestionBank();
        private readonly TrainingTargetMeasurement bonusButtonMeasurement = new TrainingTargetMeasurement();
        private TriviaQuestion currentTriviaQuestion;
        private int triviaAnswered;
        private int triviaCorrect;
        private bool triviaAwaitingAnswer;
        private bool triviaFinishing;
        private bool triviaLoading;
        private bool lessonIntroActive;
        private bool bonusIntroActive;
        private string tracePath;
        private int targetIndex = -1;
        private readonly TrainingTargetMeasurement targetMeasurement = new TrainingTargetMeasurement();
        private bool completed;
        private bool exitRequested;
        private DispatcherTimer isolationTimer;
        private bool isolationSampleSeen;
        private bool directGazeAttempted;
        private bool directTrackingInterrupted;
        private bool autoStartAttempted;
        private volatile bool trackerSeenDuringEnumeration;
        private bool trackerEnumerationFinished;
        private bool mouseOnlyFallback;
        private DispatcherTimer noTrackerNoticeTimer;
        private DispatcherTimer startupSplashTimer;
        private DispatcherTimer startupDiscoveryTimer;
        private bool startupSplashCompleted;
        private bool startupRouted;
        private const int StartupSplashDurationSeconds = 3;
        private const int StartupDiscoveryTimeoutSeconds = 5;
        private Button gazeControlTarget;
        private SharedButtonDwellWheel buttonDwellWheel;
        private SharedButtonDwellWheel targetDwellWheel;
        private Button[] menuButtons;
        private Button[] levelsButtons;
        private Button[] lessonButtons;
        private Button[] bonusButtons;
        private Button[] bonusBackOnlyButtons;
        private readonly Button[] noGazeButtons = new Button[0];
        private Button[] levelZeroContinueButtons;
        private Button[] displayButtons;
        private Button[] allLevelButtons;
        private DateTimeOffset gazeControlEnteredAt;
        private DateTimeOffset lastGazeAt;
        private bool gazeControlTriggered;
        private bool requireLookAwayAfterNavigation;
        private Point lastActivatedGazePosition;
        private Point mostRecentRawGazePosition;
        // A deliberate movement avoids requiring a tiny gap between
        // buttons that consume almost the entire screen.
        private const double NavigationRearmDistance = 65.0;
        private static readonly TimeSpan ControlDwell = TimeSpan.FromMilliseconds(1500);

        // Initialize persistent training models, visual controls and lifecycle callbacks.
        public LevelOnePreview()
        {
            InitializeComponent();
            buttonDwellWheel = new SharedButtonDwellWheel(CursorOverlay);
            // Training targets and gaze buttons share the exact same radial
            // wheel implementation, while retaining independent progress.
            targetDwellWheel = new SharedButtonDwellWheel(CursorOverlay);
            menuButtons = new[] { LevelsMenuButton, TdControlMenuButton,
                                  DiagnosticsMenuButton, SettingsMenuButton, ExitButton };
            diagnosticsButtons = new[] { DiagnosticsBackButton,
                DiagnosticsOverviewTabButton, DiagnosticsSkillsTabButton,
                DiagnosticsMapsTabButton, DiagnosticsSessionsTabButton };
            diagnosticsSessionsButtons = new[] { DiagnosticsBackButton,
                DiagnosticsOverviewTabButton, DiagnosticsSkillsTabButton,
                DiagnosticsMapsTabButton, DiagnosticsSessionsTabButton,
                TherapistExportButton };
            skillCategoryButtons = new[] { SkillsIntroButton, SkillsTargetsButton, SkillsDwellButton,
                SkillsTrackingButton, SkillsEnduranceButton, SkillsFunctionButton };
            diagnosticsSkillsButtons = new[] { DiagnosticsBackButton,
                DiagnosticsOverviewTabButton, DiagnosticsSkillsTabButton,
                DiagnosticsMapsTabButton, DiagnosticsSessionsTabButton,
                SkillsIntroButton, SkillsTargetsButton, SkillsDwellButton, SkillsTrackingButton,
                SkillsEnduranceButton, SkillsFunctionButton };
            BuildDiagnosticGrids();
            BuildSkillsGrid();
            SelectSkillsCategory(0);
            allLevelButtons = new[] { LevelOneButton, LevelTwoButton,
                LevelThreeButton, LevelFourButton, LevelFiveButton, LevelSixButton };
            levelsButtons = new[] { LevelZeroReplayButton, LevelOneButton,
                LevelTwoButton, LevelThreeButton, LevelFourButton, LevelFiveButton,
                LevelSixButton, LevelsBackButton };
            lessonButtons = new[] { LessonBackButton };
            levelZeroContinueButtons = new[] { LevelZeroContinueButton };
            bonusButtons = new[] { BonusBackButton, BonusTrueButton, BonusFalseButton };
            bonusBackOnlyButtons = new[] { BonusBackButton };
            displayButtons = new[] { ShorterButton, LongerButton, SaveDisplayButton,
                                     CancelDisplayButton, SettingsExitButton };
            displayScale = new DisplayScaleManager();
            ApplyDisplaySizes();
            displayInformation = DisplayInformation.GetForCurrentView();
            displayInformation.DpiChanged += OnDisplayDpiChanged;
            cursorFilter.SmoothingLevel = gazeProfile.SmoothingLevel;
            UpdateAccuracyDisplay();
            UpdateLevelBadges();
            // Do not show Level 0 or open the tracker before the full-screen
            // branding has been displayed. The splash overlays this page.
            ShowScreen(AppScreen.Menu);
            StartupSplashPanel.Visibility = Visibility.Visible;
            practiceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            practiceTimer.Tick += OnPracticeTimerTick;
            lastPracticeTick = DateTimeOffset.UtcNow;
            practiceTimer.Start();
            RootLayout.SizeChanged += OnGazeLayoutSizeChanged;
            Window.Current.Activated += OnTrainerWindowActivated;
            Loaded += OnPageLoaded;
        }

        // Startup and input acquisition ===
        // Select normal watcher-free startup or the explicitly requested isolation mode.
        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // Normal startup uses direct gaze events, never a device watcher.
            // The legacy watcher is retained only in explicitly selected isolation tests.
            var mode = (Application.Current as App)?.IsolationMode;
            if (mode != null && mode != "A" && isolationTimer == null && !autoStartAttempted)
            {
                isolationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                isolationTimer.Tick += OnIsolationElapsed;
                isolationTimer.Start();
                Trace("ISOLATION " + mode + ": automatic exit scheduled");
            }
            if (!autoStartAttempted)
            {
                autoStartAttempted = true;
                if (mode == "D")
                {
                    Trace("ISOLATION D: no gaze API calls");
                    startupSplashCompleted = true;
                    startupRouted = true;
                    StartupSplashPanel.Visibility = Visibility.Collapsed;
                    ShowScreen(AppScreen.Menu);
                    return;
                }
                if (mode == "H" || mode == "I")
                {
                    startupSplashCompleted = true;
                    startupRouted = true;
                    StartupSplashPanel.Visibility = Visibility.Collapsed;
                    ShowScreen(AppScreen.Menu);
                    StartDirectGazeIsolation(mode);
                    return;
                }
                if (mode == null) TryStartDirectGaze();
                else OnEnableLive(null, null);
            }
            if (mode == "D" || mode == "H" || mode == "I") return;
            if (startupSplashCompleted || startupSplashTimer != null) return;
            startupSplashTimer = new DispatcherTimer
                { Interval = TimeSpan.FromSeconds(StartupSplashDurationSeconds) };
            startupSplashTimer.Tick += OnStartupSplashElapsed;
            startupSplashTimer.Start();
        }

        // Reapply the page-specific pointer policy whenever the UWP window
        // becomes active. A mouse click can activate the app for the first
        // time; that should not be required to establish its cursor state.
        // Reapply the trainer-local pointer policy when the window regains activation.
        private void OnTrainerWindowActivated(object sender,
            WindowActivatedEventArgs args)
        {
            if (exitRequested || stopping || mouseOnlyFallback || !trackerSeenDuringEnumeration) return;
            if (args.WindowActivationState == CoreWindowActivationState.Deactivated)
                return;
            var mode = (Application.Current as App)?.IsolationMode;
            if (mode == "D" || mode == "E" || mode == "F" || mode == "G" || mode == "H" || mode == "I") return;
            HideWindowsPointerCursor();
            try
            {
                Trace("Window activated; gaze subscribed=" + gazeSubscribed +
                    "; pointer null=" + (Window.Current.CoreWindow.PointerCursor == null));
            }
            catch (Exception) { /* Activation diagnostics cannot block gaze. */ }
        }

        // Hide only the Windows arrow within this UWP window. The trainer
        // continues drawing its own eye-gaze cursor; the tracker, TD Control
        // companion, and pointer input event handling are unchanged.
        // Local cursor policy ===
        // Hide only this view's Windows arrow while retaining the original cursor for restoration.
        private void HideWindowsPointerCursor()
        {
            try
            {
                var coreWindow = Window.Current.CoreWindow;
                // Windows may reset a CoreWindow cursor as its activation
                // changes. Check actual state instead of trusting a cached
                // boolean that was set on the page's first load.
                if (coreWindow.PointerCursor == null)
                {
                    pointerCursorHidden = true;
                    return;
                }
                if (!pointerCursorHidden)
                    previousPointerCursor = coreWindow.PointerCursor;
                coreWindow.PointerCursor = null;
                pointerCursorHidden = true;
            }
            catch (Exception ex)
            {
                Trace("Unable to hide Windows arrow cursor: " + ex.GetType().Name);
            }
        }

        // Restore the previous view cursor without modifying TD Control or system-wide settings.
        private void RestoreWindowsPointerCursor()
        {
            if (!pointerCursorHidden) return;
            try
            {
                Window.Current.CoreWindow.PointerCursor =
                    previousPointerCursor ?? new CoreCursor(CoreCursorType.Arrow, 0);
                pointerCursorHidden = false;
                previousPointerCursor = null;
            }
            catch (Exception ex)
            {
                Trace("Unable to restore Windows arrow cursor: " + ex.GetType().Name);
            }
        }

        // Startup routing and fallback ===
        // Finish the splash interval and route startup when gaze evidence is available.
        private void OnStartupSplashElapsed(object sender, object e)
        {
            if (startupSplashTimer != null)
            {
                startupSplashTimer.Stop();
                startupSplashTimer.Tick -= OnStartupSplashElapsed;
                startupSplashTimer = null;
            }
            if (startupSplashCompleted || stopping || exitRequested) return;
            startupSplashCompleted = true;
            // Never block splash dismissal on the Windows gaze watcher:
            // EnumerationCompleted is not guaranteed to arrive promptly.
            CompleteStartupAfterTrackerScan();
            if (!startupRouted && !exitRequested && !stopping)
            {
                // Allow briefly for a slower device enumeration before deciding
                // that this is a mouse-only session.
                startupDiscoveryTimer = new DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(StartupDiscoveryTimeoutSeconds) };
                startupDiscoveryTimer.Tick += OnStartupDiscoveryTimeout;
                startupDiscoveryTimer.Start();
            }
        }

        // Stop waiting for startup evidence and select the appropriate input mode.
        private void OnStartupDiscoveryTimeout(object sender, object e)
        {
            StopStartupDiscoveryTimer();
            if (startupRouted || exitRequested || stopping) return;
            Trace((Application.Current as App)?.IsolationMode == null
                ? "Direct gaze startup timed out without valid gaze; selecting mouse mode"
                : "Initial gaze enumeration timed out; selecting startup mode");
            trackerEnumerationFinished = true;
            CompleteStartupAfterTrackerScan();
        }

        // Detach the startup timeout handler and release its timer.
        private void StopStartupDiscoveryTimer()
        {
            if (startupDiscoveryTimer == null) return;
            startupDiscoveryTimer.Stop();
            startupDiscoveryTimer.Tick -= OnStartupDiscoveryTimeout;
            startupDiscoveryTimer = null;
        }

        // Route once after the splash and readiness checks; normal readiness comes from valid gaze samples, not a watcher.
        private void CompleteStartupAfterTrackerScan()
        {
            if (!startupSplashCompleted || startupRouted ||
                exitRequested || stopping) return;
            // A discovered tracker can proceed immediately even when the
            // enumeration-completed notification is delayed or missing.
            if (!trackerSeenDuringEnumeration && !trackerEnumerationFinished) return;
            startupRouted = true;
            StopStartupDiscoveryTimer();
            StartupSplashPanel.Visibility = Visibility.Collapsed;
            if (!trackerSeenDuringEnumeration)
            {
                EnterMouseOnlyFallback();
                return;
            }

            // Keep pointer policy identical in D–G; do not hide the arrow or launch a companion.
            var isolationMode = (Application.Current as App)?.IsolationMode;
            if (isolationMode != "D" && isolationMode != "E" && isolationMode != "F" && isolationMode != "G")
            {
                HideWindowsPointerCursor();
                (Application.Current as App)?.TryStartOverlayCompanion();
            }
            else Trace("ISOLATION " + isolationMode + ": pointer unchanged; companion skipped");
            // Keep the existing single-owner gaze startup and tutorial route.
            if ((Application.Current as App)?.IsolationMode != null)
                ShowScreen(AppScreen.Menu);
            else if (ShouldStartLevelZero())
                StartLevelZero();
            else
                ShowScreen(AppScreen.Menu);
        }

        // Watcher-free normal gaze input ===
        // Acquire the view's gaze source directly and subscribe once; never create a device watcher during ordinary use.
        private void TryStartDirectGaze()
        {
            if (exitRequested || stopping || mouseOnlyFallback || gazeSubscribed ||
                (Application.Current as App)?.IsolationMode != null) return;
            directGazeAttempted = true;
            try
            {
                Trace("DIRECT GAZE: acquiring source; watcher disabled");
                if (gazeSource == null) gazeSource = GazeInputSourcePreview.GetForCurrentView();
                if (gazeSource == null) { Trace("DIRECT GAZE: source unavailable"); return; }
                live = true;
                gazeSource.GazeMoved += OnGazeMoved;
                gazeSubscribed = true;
                Trace("DIRECT GAZE: subscribed; awaiting valid gaze sample");
            }
            catch (Exception ex) { Trace("DIRECT GAZE: setup failed: " + ex); }
        }

        // Diagnostic-only gaze isolation ===
        // Exercise direct source acquisition with optional event delivery but no training actions or companion.
        private void StartDirectGazeIsolation(string mode)
        {
            // No watcher, no tracker enumeration, no cursor or overlay modifications.
            Trace("ISOLATION " + mode + ": BEGIN direct GetForCurrentView; watcher not created");
            try
            {
                gazeSource = GazeInputSourcePreview.GetForCurrentView();
                Trace("ISOLATION " + mode + ": END direct GetForCurrentView; source=" + (gazeSource != null));
                if (gazeSource == null)
                {
                    Trace("ISOLATION INVALID: direct gaze source unavailable");
                    return;
                }
                live = true;
                if (mode == "I")
                {
                    Trace("ISOLATION I: BEGIN GazeMoved subscription");
                    gazeSource.GazeMoved += OnGazeMoved;
                    gazeSubscribed = true;
                    Trace("ISOLATION I: END GazeMoved subscription");
                }
            }
            catch (Exception ex)
            {
                Trace("ISOLATION INVALID: direct gaze setup failed: " + ex);
            }
        }

        // Legacy watcher isolation path ===
        // Route normal requests to direct acquisition; create the legacy watcher only for explicit isolation modes.
        private void OnEnableLive(object sender, RoutedEventArgs e)
        {
            var mode = (Application.Current as App)?.IsolationMode;
            if (mode == null) { TryStartDirectGaze(); return; }
            if (live || stopping || mode == "D" || mode == "H" || mode == "I") return;
            try
            {
                tracePath = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                         "level1-live-gaze-trace.txt");
                File.WriteAllText(tracePath,
                    "SUPERVISED LIVE GAZE TEST — prior shutdown left TD Control unresponsive.\r\n" +
                    "No gaze coordinate values logged. No automatic exit.\r\n");
            }
            catch (Exception ex)
            {
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Trace unavailable (" +
                                      ex.GetType().Name + ")";
                // File logging is optional; never block review or mouse access.
            }

            live = true;
            LiveStatusText.Text = "Connecting to eye tracker…";
            Trace("BEGIN watcher start");
            try
            {
                watcher = GazeInputSourcePreview.CreateWatcher();
                watcher.Added += OnDeviceAdded;
                watcher.Updated += OnDeviceUpdated;
                watcher.Removed += OnDeviceRemoved;
                watcher.EnumerationCompleted += OnTrackerEnumerationCompleted;
                watcher.Start();
                watcherStarted = true;
                Trace("END watcher start");
            }
            catch (Exception ex)
            {
                Trace("ERROR watcher start: " + ex);
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Tracker setup failed: " + ex.GetType().Name;
                trackerEnumerationFinished = true;
                CompleteStartupAfterTrackerScan();
            }
        }

        // Finish legacy diagnostic discovery on the UI dispatcher.
        private void OnTrackerEnumerationCompleted(GazeDeviceWatcherPreview sender,
                                                    object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (exitRequested || stopping || mouseOnlyFallback) return;
                trackerEnumerationFinished = true;
                CompleteStartupAfterTrackerScan();
            });
        }

        // Mouse fallback ===
        // Release gaze resources where possible, preserve mouse access and display the temporary notice.
        private void EnterMouseOnlyFallback()
        {
            if (mouseOnlyFallback || exitRequested || stopping) return;
            mouseOnlyFallback = true;
            // Direct acquisition can succeed even without a physical tracker.
            // Unsubscribe before releasing the source, including mouse fallback.
            if (directGazeAttempted && gazeSource != null && gazeSubscribed)
            {
                try
                {
                    gazeSource.GazeMoved -= OnGazeMoved;
                    gazeSubscribed = false;
                    Trace("DIRECT GAZE: unsubscribed for mouse fallback");
                }
                catch (Exception ex)
                {
                    Trace("DIRECT GAZE: fallback unsubscribe failed; retain source for exit retry: " + ex);
                }
            }
            live = directGazeAttempted && gazeSubscribed;
            if (!live)
            {
                gazeSubscribed = false;
                gazeSource = null;
            }
            // Stop only the app's watcher. Never alter TD Control.
            if (watcher != null)
            {
                try
                {
                    watcher.Added -= OnDeviceAdded;
                    watcher.Updated -= OnDeviceUpdated;
                    watcher.Removed -= OnDeviceRemoved;
                    watcher.EnumerationCompleted -= OnTrackerEnumerationCompleted;
                    if (watcherStarted) watcher.Stop();
                }
                catch (Exception) { /* Keep mouse control available. */ }
                watcherStarted = false;
                watcher = null;
            }
            RestoreWindowsPointerCursor();
            PreviewDot.Visibility = Visibility.Collapsed;
            LevelZeroEyeCursor.Visibility = Visibility.Collapsed;
            LevelZeroReplayButton.IsEnabled = false;
            LevelZeroReplayButton.Visibility = Visibility.Collapsed;
            ShowScreen(AppScreen.Menu);
            LiveStatusText.Visibility = Visibility.Collapsed;
            NoTrackerNoticePanel.Visibility = Visibility.Visible;
            StopNoTrackerNoticeTimer();
            noTrackerNoticeTimer = new DispatcherTimer
                { Interval = TimeSpan.FromSeconds(10) };
            noTrackerNoticeTimer.Tick += OnNoTrackerNoticeTimeout;
            noTrackerNoticeTimer.Start();
        }

        // Dismiss the fallback notice automatically.
        private void OnNoTrackerNoticeTimeout(object sender, object e)
        {
            DismissNoTrackerNotice();
        }

        // Allow the fallback notice to be dismissed by its button.
        private void OnDismissNoTrackerNotice(object sender, RoutedEventArgs e)
        {
            DismissNoTrackerNotice();
        }

        // Hide the notice and cancel its pending timeout.
        private void DismissNoTrackerNotice()
        {
            StopNoTrackerNoticeTimer();
            NoTrackerNoticePanel.Visibility = Visibility.Collapsed;
        }

        // Detach and release the fallback notice timer.
        private void StopNoTrackerNoticeTimer()
        {
            if (noTrackerNoticeTimer == null) return;
            noTrackerNoticeTimer.Stop();
            noTrackerNoticeTimer.Tick -= OnNoTrackerNoticeTimeout;
            noTrackerNoticeTimer = null;
        }

        // Legacy watcher callbacks — diagnostic modes only ===
        // Handle legacy isolation device discovery on the UI dispatcher.
        private void OnDeviceAdded(GazeDeviceWatcherPreview sender,
                                   GazeDeviceWatcherAddedPreviewEventArgs args)
        {
            if (args.Device != null && args.Device.CanTrackEyes) trackerSeenDuringEnumeration = true;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (mouseOnlyFallback || stopping || exitRequested) return;
                if (args.Device != null && args.Device.CanTrackEyes)
                    UseReadyDevice(args.Device);
                CompleteStartupAfterTrackerScan();
            });
        }

        // Handle readiness changes for the legacy watcher tests.
        private void OnDeviceUpdated(GazeDeviceWatcherPreview sender,
                                     GazeDeviceWatcherUpdatedPreviewEventArgs args)
        {
            if (args.Device != null && args.Device.CanTrackEyes) trackerSeenDuringEnumeration = true;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (mouseOnlyFallback || stopping || exitRequested) return;
                if (args.Device != null && args.Device.CanTrackEyes)
                    UseReadyDevice(args.Device);
                CompleteStartupAfterTrackerScan();
            });
        }

        // Release a stale legacy gaze subscription unless recent valid data indicates it is still working.
        private void OnDeviceRemoved(GazeDeviceWatcherPreview sender,
                                     GazeDeviceWatcherRemovedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!live || stopping || exitRequested) return;
                // A Removed event can follow another device's Added/Updated
                // event. If valid cursor samples are still arriving, do not
                // invalidate a functioning gaze subscription.
                if (gazeSubscribed &&
                    (DateTimeOffset.UtcNow - lastValidGazeSampleAt).TotalSeconds < 2)
                {
                    Trace("Gaze device removed, but recent gaze is still valid");
                    return;
                }
                cursorFilter.Reset();
                targetMeasurement.CancelHold();
                HideTargetDwellVisual();
                // Release the stale event subscription without touching
                // the device watcher or shutting down the app. The watcher
                // stays running so a later Ready event can reconnect.
                if (gazeSubscribed && gazeSource != null)
                {
                    try
                    {
                        gazeSource.GazeMoved -= OnGazeMoved;
                        Trace("Gaze event unsubscribed after device removal");
                    }
                    catch (Exception ex)
                    {
                        Trace("Gaze event unsubscribe on removal failed: " + ex);
                    }
                }
                gazeSubscribed = false;
                gazeSource = null;
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Eye tracker disconnected. Waiting for reconnection.";
                Trace("Gaze device removed; awaiting next ready device");
            });
        }

        // Acquire or subscribe to the source for eligible legacy diagnostic modes only.
        private void UseReadyDevice(GazeDevicePreview device)
        {
            if (!live || stopping || exitRequested || mouseOnlyFallback ||
                gazeSubscribed || device == null) return;
            if (!device.CanTrackEyes ||
                device.ConfigurationState != GazeDeviceConfigurationStatePreview.Ready)
            {
                LiveStatusText.Text = "Tracker detected, but gaze not ready";
                return;
            }

            var mode = (Application.Current as App)?.IsolationMode;
            if (mode == "E") return;
            if (mode == "F" && gazeSource != null) return;
            try
            {
                Trace("BEGIN gaze source subscription");
                if (mode == "F" || mode == "G") Trace("ISOLATION " + mode + ": acquiring gaze source");
                var newSource = GazeInputSourcePreview.GetForCurrentView();
                if (newSource == null)
                {
                    LiveStatusText.Visibility = Visibility.Visible;
                    LiveStatusText.Text = "Eye tracker detected. Waiting for gaze input.";
                    Trace("Gaze source unavailable; awaiting device update");
                    return;
                }
                if (mode == "F")
                {
                    gazeSource = newSource;
                    Trace("ISOLATION F: source acquired; no event subscription");
                    return;
                }
                newSource.GazeMoved += OnGazeMoved;
                gazeSource = newSource;
                gazeSubscribed = true;
                if (mode != "G") HideWindowsPointerCursor();
                else Trace("ISOLATION G: subscribed; pointer unchanged");
                Trace("END gaze source subscription");
                LiveStatusText.Visibility = Visibility.Collapsed;
                LiveStatusText.Text = "Eye tracking ready. Look at LEVELS to start.";
            }
            catch (Exception ex)
            {
                Trace("ERROR gaze source subscription: " + ex);
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Unable to start gaze: " + ex.GetType().Name;
            }
        }

        // Gaze sample processing ===
        // Validate raw gaze data, confirm startup readiness, update visual smoothing and drive screen-specific interaction.
        private void OnGazeMoved(GazeInputSourcePreview sender, GazeMovedPreviewEventArgs e)
        {
            if (!live || stopping || exitRequested || mouseOnlyFallback) return;
            // G isolates delivery of gaze events from training/dwell actions.
            var mode = (Application.Current as App)?.IsolationMode;
            if (mode == "G" || mode == "I")
            {
                if (!isolationSampleSeen)
                {
                    isolationSampleSeen = true;
                    Trace("ISOLATION " + mode + ": first gaze event received");
                }
                return;
            }
            Point? screenPoint = e.CurrentPoint?.EyeGazePosition;
            if (!screenPoint.HasValue) return;
            var p = screenPoint.Value;
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) ||
                double.IsInfinity(p.X) || double.IsInfinity(p.Y)) return;
            mostRecentRawGazePosition = p;
            lastValidGazeSampleAt = DateTimeOffset.UtcNow;
            if (mode == null)
            {
                if (!trackerSeenDuringEnumeration)
                {
                    trackerSeenDuringEnumeration = true;
                    Trace("DIRECT GAZE: first valid sample; watcher not created");
                    CompleteStartupAfterTrackerScan();
                }
                if (!startupRouted) return;
                if (directTrackingInterrupted)
                {
                    directTrackingInterrupted = false;
                    Trace("DIRECT GAZE: valid tracking resumed");
                }
            }
            if (LiveStatusText.Visibility == Visibility.Visible &&
                gazeSubscribed)
            {
                LiveStatusText.Visibility = Visibility.Collapsed;
                Trace("Gaze cursor input resumed");
            }

            // UWP gaze points are in application-view coordinates, not
            // coordinates local to the canvas.
            // Show the indicator across the entire page, including Exit.
            // Gaze position remains in UWP view coordinates for button hit tests.
            // All gaze targets, including buttons, share the master stabilized
            // stream and adaptive hitboxes. Exit additionally accepts raw gaze
            // as a safety fallback, so stabilization cannot make it unreachable.
            if (p.X < 0 || p.Y < 0 ||
                p.X > RootLayout.ActualWidth || p.Y > RootLayout.ActualHeight)
            {
                // Eye trackers can report small excursions past the display
                // border. Preserve filter continuity and keep the *visual*
                // cursor at the nearest edge; do not treat those positions as
                // valid training samples or accumulate target dwell.
                double width = RootLayout.ActualWidth;
                double height = RootLayout.ActualHeight;
                if (width > 0 && height > 0)
                {
                    Point edge = new Point(
                        Clamp(p.X, 0, width),
                        Clamp(p.Y, 0, height));
                    UpdateCursor(cursorFilter.Update(edge, DateTimeOffset.UtcNow));
                }
                RecordInterruptedTargetDwell(DateTimeOffset.UtcNow);
                HideTargetDwellVisual();
                buttonDwellWheel.Hide();
                ProcessGazeControl(p, p);
                e.Handled = true;
                return;
            }

            Point stabilized = cursorFilter.Update(p, DateTimeOffset.UtcNow);
            UpdateCursor(stabilized);
             if (currentScreen == AppScreen.LevelZero)
                ProcessLevelZeroGaze(p, stabilized);
            if (currentScreen == AppScreen.Lesson)
            {
                Point canvasOrigin = TrainingCanvas.TransformToVisual(this)
                                      .TransformPoint(new Point(0, 0));
                ProcessPosition(new Point(stabilized.X - canvasOrigin.X,
                                          stabilized.Y - canvasOrigin.Y),
                                new Point(p.X - canvasOrigin.X,
                                          p.Y - canvasOrigin.Y));
            }
            ProcessGazeControl(stabilized, p);
            e.Handled = true;
        }


        // All controls use the shared stabilized stream and adaptive hitboxes.
        // Exit additionally accepts a raw-gaze hit to preserve accessibility.
        // Shared dwell and navigation ===
        // Resolve an eligible button, maintain uninterrupted dwell and require deliberate rearming after navigation.
        private void ProcessGazeControl(Point stabilizedPoint, Point rawPoint)
        {
            if (!live || stopping || exitRequested) return;
            var now = DateTimeOffset.UtcNow;
            // Do not allow an event gap to count as uninterrupted dwell.
            if (lastGazeAt != default(DateTimeOffset) &&
                now - lastGazeAt > TimeSpan.FromMilliseconds(500))
            {
                ObserveLevelZeroDwellTarget(false);
                bonusButtonMeasurement.CancelHold();
                gazeControlTarget = null;
                gazeControlTriggered = false;
                buttonDwellWheel.Hide();
            }
            lastGazeAt = now;
            Button target = FindGazeButton(stabilizedPoint, rawPoint);
            if (target == null)
            {
                ObserveLevelZeroDwellTarget(false);
                bonusButtonMeasurement.CancelHold();
                gazeControlTarget = null;
                gazeControlTriggered = false;
                requireLookAwayAfterNavigation = false;
                buttonDwellWheel.Hide();
                return;
            }
            if (requireLookAwayAfterNavigation)
            {
                double dx = rawPoint.X - lastActivatedGazePosition.X;
                double dy = rawPoint.Y - lastActivatedGazePosition.Y;
                if (dx * dx + dy * dy <
                    NavigationRearmDistance * NavigationRearmDistance)
                {
                    buttonDwellWheel.Hide();
                    return;
                }

                // Start a full, fresh dwell on the new button once the
                // gaze moves deliberately, even if no empty gap was crossed.
                requireLookAwayAfterNavigation = false;
                gazeControlTarget = null;
                gazeControlTriggered = false;
            }
            if (gazeControlTarget != target)
            {
                ObserveLevelZeroDwellTarget(false);
                if (target == LevelZeroContinueButton &&
                    currentScreen == AppScreen.LevelZero)
                    levelZeroDwellAttempts++;
                gazeControlTarget = target;
                gazeControlEnteredAt = now;
                bonusButtonMeasurement.CancelHold();
                if (IsBonusAnswer(target)) bonusButtonMeasurement.BeginTarget(now);
                gazeControlTriggered = false;
            }
            if (target == LevelZeroContinueButton && !gazeControlTriggered)
                ObserveLevelZeroDwellTarget(true);
            if (gazeControlTriggered) return;
            if (IsBonusAnswer(target)) bonusButtonMeasurement.Observe(rawPoint, now, true);
            double progress = (now - gazeControlEnteredAt).TotalMilliseconds /
                              ControlDwell.TotalMilliseconds;
            buttonDwellWheel.Present(target, progress);
            if (progress < 1.0) return;
            gazeControlTriggered = true;
            if (target == LevelZeroContinueButton)
                FinishLevelZeroDiagnosticDwell();
            lastActivatedGazePosition = rawPoint;
            buttonDwellWheel.Hide();
            // New screen buttons re-arm after intentional eye movement.
            if (target == ExitButton)
                OnExitRequested(null, null);
            else if (target == LevelsMenuButton)
                OnOpenLevels(null, null);
            else if (target == TdControlMenuButton)
                OnTdControlTrainer(null, null);
            else if (target == DiagnosticsMenuButton)
                OnOpenDiagnostics(null, null);
            else if (target == DiagnosticsBackButton)
                OnBackToMenu(null, null);
            else if (target == DiagnosticsOverviewTabButton ||
                     target == DiagnosticsSkillsTabButton ||
                     target == DiagnosticsMapsTabButton ||
                     target == DiagnosticsSessionsTabButton)
                SelectDiagnosticsTab(target);
            else if (target == TherapistExportButton)
                OnExportTherapistReport(null, null);
            else if (target == LevelZeroContinueButton)
                OnLevelZeroContinue(null, null);
            else if (target == SkillsIntroButton || target == SkillsTargetsButton ||
                     target == SkillsDwellButton ||
                     target == SkillsTrackingButton || target == SkillsEnduranceButton ||
                     target == SkillsFunctionButton)
                SelectSkillsCategory(Array.IndexOf(skillCategoryButtons, target));
            else if (target == SettingsMenuButton)
                OnDisplaySettingsRequested(null, null);
            else if (target == LevelsBackButton)
                OnBackToMenu(null, null);
            else if (target == LessonBackButton || target == BonusBackButton)
                OnBackToLevels(null, null);
            else if (target == BonusTrueButton || target == BonusFalseButton)
                SubmitBonusAnswer(target == BonusTrueButton, true);
            else if (target == LevelZeroReplayButton)
                OnLevelZeroReplay(null, null);
            else if (target == LevelOneButton)
                OnLevelOne(null, null);
            else if (target == LevelTwoButton || target == LevelThreeButton ||
                     target == LevelFourButton || target == LevelFiveButton ||
                     target == LevelSixButton)
                OnPlaceholderLevel(target, null);
            else if (target == ShorterButton)
                OnReferenceShorter(null, null);
            else if (target == LongerButton)
                OnReferenceLonger(null, null);
            else if (target == SaveDisplayButton)
                OnSaveDisplay(null, null);
            else if (target == CancelDisplayButton || target == SettingsExitButton)
                OnCancelDisplay(null, null);
        }

        // Central registration point for every gaze button on this page.
        // Future buttons are added to this collection, not given bespoke
        // filters, geometry, or dwell timers.
        // Return the controls eligible for gaze selection on the current screen.
        private Button[] GetGazeButtons()
        {
            // Reuse registered arrays; no allocations on gaze-move events.
            switch (currentScreen)
            {
                case AppScreen.Levels: return levelsButtons;
                case AppScreen.Lesson: return lessonIntroActive ? noGazeButtons : lessonButtons;
                case AppScreen.Bonus:
                    return bonusIntroActive ? noGazeButtons :
                           triviaAwaitingAnswer ? bonusButtons : bonusBackOnlyButtons;
                case AppScreen.Settings: return displayButtons;
                case AppScreen.LevelZero:
                    return levelZeroStage == LevelZeroStage.Continue
                        ? levelZeroContinueButtons : noGazeButtons;
                case AppScreen.Diagnostics:
                    return selectedDiagnosticsTab == DiagnosticsTab.Skills
                        ? diagnosticsSkillsButtons :
                        selectedDiagnosticsTab == DiagnosticsTab.Sessions
                            ? diagnosticsSessionsButtons : diagnosticsButtons;
                default: return menuButtons;
            }
        }

        // Invalidate cached interaction bounds when layout geometry changes.
        private void OnGazeLayoutSizeChanged(object sender, SizeChangedEventArgs e)
        {
            gazeButtonBounds.Clear();
            lastDrawnGazeCursor = new Point(double.NaN, double.NaN);
        }

        // Find the eligible control using the shared adaptive hit testing policy.
        private Button FindGazeButton(Point stabilizedPoint, Point rawPoint)
        {
            foreach (Button button in GetGazeButtons())
            {
                if (InsideButton(button, stabilizedPoint))
                    return button;

                // For the safety-critical Exit button, accept the same
                // adaptive hit region under raw gaze as well. This prevents
                // smoothing from delaying access when attention shifts to Exit.
                if (button == ExitButton &&
                    InsideButton(button, rawPoint))
                    return button;
            }
            return null;
        }

        // Test the gaze coordinate against a button's current interaction geometry.
        private bool InsideButton(Button button, Point gaze)
        {
            if (button == null || !button.IsEnabled ||
                button.Visibility != Visibility.Visible ||
                button.ActualWidth <= 0 || button.ActualHeight <= 0) return false;

            Rect bounds;
            if (!gazeButtonBounds.TryGetValue(button, out bounds) ||
                bounds.Width != button.ActualWidth ||
                bounds.Height != button.ActualHeight)
            {
                // Only calculate transforms when the visual layout changes,
                // never on every incoming eye-gaze sample.
                Point origin = button.TransformToVisual(this)
                                     .TransformPoint(new Point(0, 0));
                bounds = new Rect(origin.X, origin.Y,
                                  button.ActualWidth, button.ActualHeight);
                gazeButtonBounds[button] = bounds;
            }
            return AdaptiveHitTesting.ContainsRectangle(
                gaze, bounds, gazeProfile.HitBoxScale);
        }

        // Switch the visible training screen and reset navigation and dwell state.
        private void ShowScreen(AppScreen screen)
        {
            gazeButtonBounds.Clear();
            lastDrawnGazeCursor = new Point(double.NaN, double.NaN);
            if (screen != AppScreen.Diagnostics)
                HideTherapistExportConfirmation();
            if (currentScreen == AppScreen.LevelZero && screen != AppScreen.LevelZero)
                StopLevelZero();
            // Navigating away from the celebration must not fire a stale transition.
            if (screen != AppScreen.Lesson)
            {
                CancelTargetDelay();
                CancelBonusTransition();
            }
            if (screen != AppScreen.Bonus) CancelBonusQuestionTimer();
            if (screen != AppScreen.Lesson) TargetCountdown.Visibility = Visibility.Collapsed;
            lessonIntroActive = screen == AppScreen.Lesson && lessonIntroActive;
            bonusIntroActive = screen == AppScreen.Bonus && bonusIntroActive;
            if (screen != AppScreen.Lesson) LessonIntroOverlay.Visibility = Visibility.Collapsed;
            if (screen != AppScreen.Bonus) BonusIntroOverlay.Visibility = Visibility.Collapsed;
            AppScreen previousScreen = currentScreen;
            currentScreen = screen;
            // Any live-gaze screen transition (including automatic lesson
            // completion) needs a deliberate movement before another
            // button can activate. Startup and mouse-only use do not.
            requireLookAwayAfterNavigation = live && previousScreen != screen;
            if (requireLookAwayAfterNavigation)
                lastActivatedGazePosition = mostRecentRawGazePosition;
            gazeControlTarget = null;
            gazeControlTriggered = false;
            bonusButtonMeasurement.CancelHold();
            lastGazeAt = default(DateTimeOffset);
            // Old-screen smoothing must not slow acquisition on this screen.
            // Gaze device lifetime and controlled Exit are untouched.
            cursorFilter.Reset();
            displaySettingsVisible = screen == AppScreen.Settings;
            LevelZeroPanel.Visibility = screen == AppScreen.LevelZero
                ? Visibility.Visible : Visibility.Collapsed;
            LevelZeroEyeCursor.Visibility = screen == AppScreen.LevelZero
                ? Visibility.Visible : Visibility.Collapsed;
            PreviewDot.Visibility = screen == AppScreen.LevelZero
                ? Visibility.Collapsed : PreviewDot.Visibility;
            HomePanel.Visibility = screen == AppScreen.Menu ? Visibility.Visible : Visibility.Collapsed;
            LevelsPanel.Visibility = screen == AppScreen.Levels ? Visibility.Visible : Visibility.Collapsed;
            LessonPanel.Visibility = screen == AppScreen.Lesson ? Visibility.Visible : Visibility.Collapsed;
            BonusPanel.Visibility = screen == AppScreen.Bonus ? Visibility.Visible : Visibility.Collapsed;
            DisplaySettingsPanel.Visibility = screen == AppScreen.Settings ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = screen == AppScreen.Diagnostics ? Visibility.Visible : Visibility.Collapsed;
            if (screen == AppScreen.Diagnostics)
            {
                RefreshDiagnosticGrids();
                RefreshTrainingReport();
                SelectDiagnosticsTab(DiagnosticsOverviewTabButton);
            }
            targetMeasurement.CancelHold();
            buttonDwellWheel.Hide();
            HideTargetDwellVisual();
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            // Keep triggered gaze locked until the next look-away event.
            // This prevents entering another screen while still gazing at
            // the same coordinates from the previous screen's button.
            if (screen != AppScreen.Lesson) UpdateAccuracyDisplay();
        }

        // Count only active gaze-guided practice, excluding menu time,
        // unavailable gaze tracking, long suspension gaps, and shutdown.
        // Practice time and tracking recovery ===
        // Check gaze readiness and gaps, reset interrupted holds, and accumulate eligible practice time.
        private void OnPracticeTimerTick(object sender, object e)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            double elapsed = (now - lastPracticeTick).TotalSeconds;
            lastPracticeTick = now;
            if (!exitRequested && !stopping && !mouseOnlyFallback &&
                (Application.Current as App)?.IsolationMode == null)
            {
                if (!startupRouted && !gazeSubscribed) TryStartDirectGaze();
                if (startupRouted && gazeSubscribed && trackerSeenDuringEnumeration &&
                    !directTrackingInterrupted && (now - lastValidGazeSampleAt).TotalSeconds > 2)
                {
                    directTrackingInterrupted = true;
                    if (currentScreen == AppScreen.Lesson && targetCell >= 0) targetTrialInterrupted = true;
                    cursorFilter.Reset();
                    targetMeasurement.CancelHold();
                    bonusButtonMeasurement.CancelHold();
                    ObserveLevelZeroDwellTarget(false);
                    gazeControlTarget = null;
                    gazeControlTriggered = false;
                    HideTargetDwellVisual();
                    buttonDwellWheel.Hide();
                    LiveStatusText.Text = "Waiting for gaze input…";
                    LiveStatusText.Visibility = Visibility.Visible;
                    Trace("DIRECT GAZE: tracking gap; existing subscription retained for recovery");
                }
            }
            if (exitRequested || stopping || mouseOnlyFallback || !live ||
                (currentScreen != AppScreen.Lesson && currentScreen != AppScreen.Bonus) ||
                lastValidGazeSampleAt == default(DateTimeOffset) ||
                (now - lastValidGazeSampleAt).TotalSeconds > 2) return;
            trainingReport.AddPracticeSeconds(elapsed);
        }

        // Display a directional performance trend with the corresponding visual treatment.
        private static void SetTrend(TextBlock label, int trend)
        {
            // Up means improved; for jitter an improvement is a reduction.
            label.Text = trend > 0 ? "▲" : trend < 0 ? "▼" : "—";
            label.Foreground = trend > 0 ? GoodBrush :
                               trend < 0 ? PoorBrush : WhiteTextBrush;
        }

        // Arrows indicate actual filter percentage rising or falling.
        // A decrease in filtering is not by itself proof of better precision.
        // Display the direction of assistance changes separately from performance scores.
        private static void SetSmoothingTrend(TextBlock label, int direction)
        {
            label.Text = direction > 0 ? "▲" : direction < 0 ? "▼" : "—";
            label.Foreground = WhiteTextBrush;
        }

        // Apply the learned smoothing percentage and record it in the report.
        private void ApplyAdaptiveSmoothingAndRecord()
        {
            cursorFilter.SmoothingLevel = gazeProfile.SmoothingLevel;
            trainingReport.RecordSmoothing(cursorFilter.SmoothingLevel);
        }

        // Diagnostics reporting ===
        // Update the diagnostics overview from persisted training aggregates.
        private void RefreshTrainingReport()
        {
            int finished = 0;
            for (int level = 1; level <= 6; level++)
                if (trainingProgress.IsCompleted(level)) finished++;
            ReportLevelsText.Text = finished + " / 6";
            ReportRunsText.Text = trainingReport.CompletedRuns.ToString();
            ReportHoursText.Text = trainingReport.PracticeHours.ToString("0.00") + " h";
            RecommendedDwellText.Text = dwellRecommendation.Milliseconds + " ms";
            RecommendedDwellEvidenceText.Text = dwellRecommendation.HasEvidence
                ? dwellRecommendation.SuccessfulAttempts + " completed / " +
                  dwellRecommendation.InterruptedAttempts + " interrupted holds"
                : "Starting estimate; " +
                  (dwellRecommendation.SuccessfulAttempts + dwellRecommendation.InterruptedAttempts) +
                  " of 12 initial holds recorded";
            // Overall is the existing persistent adaptive accuracy score.
            ReportOverallText.Text = gazeProfile.Acquisitions > 0
                ? gazeProfile.AccuracyScore.ToString("0") + "%" : "NA";
            ReportAccuracyText.Text = trainingReport.LatestAccuracy.HasValue
                ? trainingReport.LatestAccuracy.Value.ToString("0") + "%" : "NA";
            ReportJitterText.Text = trainingReport.LatestJitter.HasValue
                ? trainingReport.LatestJitter.Value.ToString("0.0") + " mm" : "NA";
            SetTrend(ReportOverallTrend, trainingReport.OverallTrend);
            SetTrend(ReportAccuracyTrend, trainingReport.AccuracyTrend);
            SetTrend(ReportJitterTrend, trainingReport.JitterTrend);
            ReportSmoothingText.Text = cursorFilter.SmoothingLevel.ToString() + "%";
            SetSmoothingTrend(ReportSmoothingTrend, trainingReport.SmoothingTrend);
        }

        // Both maps use the same screen coordinates and 7x5 region geometry.
        // The colors are provisional performance bands, not medical cutoffs.
        private static readonly SolidColorBrush NoDataBrush =
            new SolidColorBrush(Color.FromArgb(255, 38, 53, 75));
        private static readonly SolidColorBrush GoodBrush =
            new SolidColorBrush(Color.FromArgb(255, 22, 101, 52));
        private static readonly SolidColorBrush MediumBrush =
            new SolidColorBrush(Color.FromArgb(255, 133, 77, 14));
        private static readonly SolidColorBrush PoorBrush =
            new SolidColorBrush(Color.FromArgb(255, 153, 27, 27));
        private static readonly SolidColorBrush WhiteTextBrush =
            new SolidColorBrush(Colors.White);

        // Create the reusable regional accuracy and jitter cell controls.
        private void BuildDiagnosticGrids()
        {
            CreateDiagnosticGrid(AccuracyHeatmapGrid, accuracyCells, accuracyCellBorders);
            CreateDiagnosticGrid(JitterHeatmapGrid, jitterCells, jitterCellBorders);
            RefreshDiagnosticGrids();
        }

        // Populate the supplied diagnostic grid and retain references to its labels and borders.
        private static void CreateDiagnosticGrid(Grid container, TextBlock[,] labels,
                                                 Border[,] backgrounds)
        {
            for (int row = 0; row < SpatialAccuracyMap.Rows; row++)
                container.RowDefinitions.Add(new RowDefinition
                { Height = new GridLength(1, GridUnitType.Star) });
            for (int col = 0; col < SpatialAccuracyMap.Columns; col++)
                container.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });

            for (int row = 0; row < SpatialAccuracyMap.Rows; row++)
                for (int col = 0; col < SpatialAccuracyMap.Columns; col++)
                {
                    var cell = new Border
                    {
                        Margin = new Thickness(2),
                        BorderBrush = new SolidColorBrush(
                            Color.FromArgb(255, 52, 82, 117)),
                        BorderThickness = new Thickness(1),
                        Background = NoDataBrush,
                        CornerRadius = new CornerRadius(2)
                    };
                    var text = new TextBlock
                    {
                        Text = "NA",
                        FontSize = 18,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = WhiteTextBrush,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    cell.Child = text;
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    container.Children.Add(cell);
                    labels[row, col] = text;
                    backgrounds[row, col] = cell;
                }
        }

        // Refresh regional cell values and visual treatment without rebuilding the grids.
        private void RefreshDiagnosticGrids()
        {
            for (int row = 0; row < SpatialAccuracyMap.Rows; row++)
                for (int col = 0; col < SpatialAccuracyMap.Columns; col++)
                {
                    double? accuracy = spatialAccuracy.Score(row, col);
                    accuracyCells[row, col].Text = accuracy.HasValue
                        ? Math.Round(accuracy.Value).ToString() + "%" : "NA";
                    accuracyCellBorders[row, col].Background = !accuracy.HasValue
                        ? NoDataBrush : accuracy.Value >= 80 ? GoodBrush :
                          accuracy.Value >= 60 ? MediumBrush : PoorBrush;

                    double? jitter = spatialJitter.AverageMillimeters(row, col);
                    jitterCells[row, col].Text = jitter.HasValue
                        ? jitter.Value.ToString("0.0") : "NA";
                    jitterCellBorders[row, col].Background = !jitter.HasValue
                        ? NoDataBrush : jitter.Value <= 2 ? GoodBrush :
                          jitter.Value <= 4 ? MediumBrush : PoorBrush;
                }
        }

        // Store only the physical RMS dispersion of a completed settled hold.
        // Conversion uses the existing DPI/reference-bar screen size setting.
        // Convert measured spread to millimeters and record the corresponding regional measurement.
        private void RecordMeasuredJitter(Point screenCenter, TargetPerformance result)
        {
            if (!live || result == null || !result.JitterViewUnits.HasValue ||
                displayScale == null || displayScale.ViewUnitsPerMillimeter <= 0) return;
            double jitterMillimeters = displayScale.ToMillimeters(result.JitterViewUnits.Value);
            spatialJitter.Record(screenCenter, RootLayout.ActualWidth,
                                 RootLayout.ActualHeight, jitterMillimeters);
            trainingReport.RecordJitter(jitterMillimeters);
        }

        // Navigation and lesson controls ===
        // Navigate to the lesson selection screen.
        private void OnOpenLevels(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested) return;
            UpdateLevelBadges();
            LevelsNoticeText.Text = "";
            ShowScreen(AppScreen.Levels);
        }

        // Handle the TD Control trainer menu action.
        private void OnTdControlTrainer(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Menu) return;
            HomeNoticeText.Text = "TD Control Trainer is coming soon.";
        }

        // Skills diagnostics ===
        // Initialize the skills diagnostics display, retaining unavailable measurements as NA.
        private void BuildSkillsGrid()
        {
            for (int i = 0; i < 3; i++)
                SkillsMetricsGrid.RowDefinitions.Add(new RowDefinition
                { Height = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 2; i++)
                SkillsMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 6; i++)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 33, 78, 131)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 118, 185, 243)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(15),
                    IsHitTestVisible = false
                };
                var content = new StackPanel
                {
                    Spacing = 7,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var heading = new TextBlock
                {
                    Foreground = WhiteTextBrush,
                    FontSize = 21,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                var value = new TextBlock
                {
                    Text = "NA",
                    Foreground = WhiteTextBrush,
                    FontSize = 30,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                var detail = new TextBlock
                {
                    Foreground = new SolidColorBrush(
                        Color.FromArgb(255, 222, 233, 248)),
                    FontSize = 16,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                var status = new TextBlock
                {
                    Text = "NOT YET RECORDED",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(
                        Color.FromArgb(255, 174, 200, 230)),
                    FontSize = 13,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center
                };
                content.Children.Add(heading);
                content.Children.Add(value);
                content.Children.Add(detail);
                content.Children.Add(status);
                card.Child = content;
                Grid.SetRow(card, i / 2);
                Grid.SetColumn(card, i % 2);
                SkillsMetricsGrid.Children.Add(card);
                skillMetricNames[i] = heading;
                skillMetricValues[i] = value;
                skillMetricStatuses[i] = status;
                skillMetricDetails[i] = detail;
            }
        }

        // Select the requested skills diagnostic category.
        private void OnSkillsCategoryClicked(object sender, RoutedEventArgs e)
        {
            if (currentScreen != AppScreen.Diagnostics ||
                selectedDiagnosticsTab != DiagnosticsTab.Skills ||
                stopping || exitRequested) return;
            SelectSkillsCategory(Array.IndexOf(skillCategoryButtons, sender as Button));
        }

        // Populate the six metric slots for the selected category.
        private void SelectSkillsCategory(int category)
        {
            if (category < 0 || category >= SkillMetrics.Length) return;
            selectedSkillCategory = category;
            for (int i = 0; i < 6; i++)
            {
                string[] parts = SkillMetrics[category][i].Split('|');
                skillMetricNames[i].Text = parts[0];
                skillMetricDetails[i].Text = parts[1];
                // Future-level measures remain NA until collected.
                skillMetricValues[i].Text = "NA";
                skillMetricValues[i].FontSize = 30;
                skillMetricStatuses[i].Text = "NOT YET RECORDED";
                if (category == 0)
                {
                    skillMetricValues[i].Text = levelOneDiagnostics.Latest(i);
                    skillMetricStatuses[i].Text = levelOneDiagnostics.Summary(i);
                    if (i == 4) skillMetricDetails[i].Text += "\n" + levelOneDiagnostics.Directions();
                }
            }
            for (int i = 0; i < skillCategoryButtons.Length; i++)
                skillCategoryButtons[i].Style = (Style)Resources[
                    i == category ? "PrimaryGazeButton" : "SecondaryGazeButton"];
        }

        // Route a diagnostics tab selection.
        private void OnDiagnosticsTabClicked(object sender, RoutedEventArgs e)
        {
            if (currentScreen != AppScreen.Diagnostics || stopping || exitRequested)
                return;
            SelectDiagnosticsTab(sender as Button);
        }

        // Update the active diagnostic section and its selection state.
        private void SelectDiagnosticsTab(Button selected)
        {
            gazeButtonBounds.Clear();
            if (selected == DiagnosticsOverviewTabButton)
                selectedDiagnosticsTab = DiagnosticsTab.Overview;
            else if (selected == DiagnosticsSkillsTabButton)
                selectedDiagnosticsTab = DiagnosticsTab.Skills;
            else if (selected == DiagnosticsMapsTabButton)
                selectedDiagnosticsTab = DiagnosticsTab.Maps;
            else if (selected == DiagnosticsSessionsTabButton)
                selectedDiagnosticsTab = DiagnosticsTab.Sessions;
            else return;

            DiagnosticsOverviewPanel.Visibility =
                selectedDiagnosticsTab == DiagnosticsTab.Overview
                    ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsSkillsPanel.Visibility =
                selectedDiagnosticsTab == DiagnosticsTab.Skills
                    ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsMapsPanel.Visibility =
                selectedDiagnosticsTab == DiagnosticsTab.Maps
                    ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsSessionsPanel.Visibility =
                selectedDiagnosticsTab == DiagnosticsTab.Sessions
                    ? Visibility.Visible : Visibility.Collapsed;
            if (selectedDiagnosticsTab == DiagnosticsTab.Sessions)
                RefreshLevelZeroSessionHistory();

            DiagnosticsOverviewTabButton.Style = (Style)Resources[
                selectedDiagnosticsTab == DiagnosticsTab.Overview
                    ? "PrimaryGazeButton" : "SecondaryGazeButton"];
            DiagnosticsSkillsTabButton.Style = (Style)Resources[
                selectedDiagnosticsTab == DiagnosticsTab.Skills
                    ? "PrimaryGazeButton" : "SecondaryGazeButton"];
            DiagnosticsMapsTabButton.Style = (Style)Resources[
                selectedDiagnosticsTab == DiagnosticsTab.Maps
                    ? "PrimaryGazeButton" : "SecondaryGazeButton"];
            DiagnosticsSessionsTabButton.Style = (Style)Resources[
                selectedDiagnosticsTab == DiagnosticsTab.Sessions
                    ? "PrimaryGazeButton" : "SecondaryGazeButton"];
        }

        // Display the saved introduction session summaries.
        private void RefreshLevelZeroSessionHistory()
        {
            // Read the bounded session file once per refresh rather than
            // opening and parsing it again to obtain the total count.
            string[] history = LevelZeroHistoryLines();
            string[] recent = history.Skip(Math.Max(0, history.Length - 3))
                .Reverse().Select(x => x.Replace("\t", " | ")).ToArray();
            LevelZeroSessionCountText.Text = "LEVEL 0: " +
                history.Length + " recorded sessions";
            LevelZeroSessionHistoryText.Text = recent.Length == 0
                ? "Complete Level 0 to record dated summaries." :
                string.Join("\n", recent.Select(x => x.Split('|')[0].Trim()));
            LevelZeroSessionComparisonText.Text = recent.Length < 2
                ? "Complete Level 0 again to compare sessions." :
                "Latest and previous session:\n" +
                recent[0] + "\n" + recent[1];
        }

        // Display the export filename and schedule automatic dismissal.
        private void ShowTherapistExportConfirmation(string fileName)
        {
            HideTherapistExportConfirmation();
            TherapistExportConfirmationFilename.Text = fileName;
            TherapistExportConfirmation.Visibility = Visibility.Visible;
            therapistExportConfirmationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            therapistExportConfirmationTimer.Tick += OnTherapistExportConfirmationTick;
            therapistExportConfirmationTimer.Start();
        }

        // Dismiss the temporary export confirmation after its interval.
        private void OnTherapistExportConfirmationTick(object sender, object e)
        {
            HideTherapistExportConfirmation();
        }

        // Hide the confirmation and release its timer.
        private void HideTherapistExportConfirmation()
        {
            if (therapistExportConfirmationTimer != null)
            {
                therapistExportConfirmationTimer.Stop();
                therapistExportConfirmationTimer.Tick -= OnTherapistExportConfirmationTick;
                therapistExportConfirmationTimer = null;
            }
            TherapistExportConfirmation.Visibility = Visibility.Collapsed;
        }

        // Uses the existing gaze-button dwell and saves with no modal picker.
        // Export is strictly opt-in and does not send data anywhere.
        // Report export ===
        // Build and save the text report using the current diagnostic aggregates.
        private async void OnExportTherapistReport(object sender, RoutedEventArgs e)
        {
            if (therapistExportInProgress || stopping || exitRequested ||
                currentScreen != AppScreen.Diagnostics ||
                selectedDiagnosticsTab != DiagnosticsTab.Sessions) return;

            therapistExportInProgress = true;
            TherapistExportButton.IsEnabled = false;
            TherapistExportStatusText.Text = "Preparing your text report...";
            Windows.Storage.StorageFile createdFile = null;
            try
            {
                DateTimeOffset now = DateTimeOffset.Now;
                string contents = TherapistTextReport.Build(
                    now, trainingProgress, trainingReport, gazeProfile,
                    spatialAccuracy, spatialJitter, cursorFilter.SmoothingLevel,
                    gazeProfile.HitBoxScale, TrainingTargetMeasurement.TargetHoldMilliseconds,
                    (int)ControlDwell.TotalMilliseconds, dwellRecommendation,
                    SkillMetrics, TherapistSkillCategoryNames,
                    new[] { LevelZeroMetricValue(0), LevelZeroMetricValue(1),
                            LevelZeroMetricValue(2), LevelZeroMetricValue(3),
                            LevelZeroMetricValue(4), LevelZeroMetricValue(5) },
                    Enumerable.Range(0, 6).Select(LevelZeroExtraValue).ToArray(),
                    LastLevelZeroSessions(100), levelOneDiagnostics);

                string name = "EyeGazeTrainer_TherapistReport_" +
                    now.ToString("yyyyMMdd_HHmmss") + ".txt";
                createdFile = await Windows.Storage.DownloadsFolder.CreateFileAsync(
                    name, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                await Windows.Storage.FileIO.WriteTextAsync(createdFile, contents,
                    Windows.Storage.Streams.UnicodeEncoding.Utf8);

                TherapistExportStatusText.Text =
                    "Saved to Downloads: " + createdFile.Name;
                if (!exitRequested && !stopping &&
                    currentScreen == AppScreen.Diagnostics)
                    ShowTherapistExportConfirmation(createdFile.Name);
            }
            catch (Exception)
            {
                // Never claim success if creating or writing the file failed.
                TherapistExportStatusText.Text =
                    "Export could not be saved. Please try again.";
                if (createdFile != null)
                {
                    try { await createdFile.DeleteAsync(); }
                    catch (Exception) { /* Ignore cleanup failure. */ }
                }
            }
            finally
            {
                therapistExportInProgress = false;
                TherapistExportButton.IsEnabled = true;
            }
        }

        // Open diagnostics and refresh the displayed measurements.
        private void OnOpenDiagnostics(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Menu) return;
            HomeNoticeText.Text = "";
            ShowScreen(AppScreen.Diagnostics);
        }

        // Return to the main menu.
        private void OnBackToMenu(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested) return;
            HomeNoticeText.Text = "";
            ShowScreen(AppScreen.Menu);
        }

        // Return to the lesson selection screen.
        private void OnBackToLevels(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested) return;
            CancelBonusQuestionTimer();
            // An unfinished lesson does not mark the level completed.
            completed = false;
            UpdateLevelBadges();
            LevelsNoticeText.Text = "";
            ShowScreen(AppScreen.Levels);
        }

        // Target lesson startup ===
        // Prepare the target lesson and start its introduction.
        private void OnLevelOne(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Levels) return;
            // A completed level stays selectable. Replay resets only the
            // current 35-target session, never the persisted completion flag.
            ShowScreen(AppScreen.Lesson);
            lessonIntroActive = true;
            ResetLesson();
            BeginLessonIntro();
        }

        // Present lesson instructions before interactive target practice.
        private void BeginLessonIntro()
        {
            LessonIntroOverlay.Visibility = Visibility.Visible;
            TargetCountdown.Visibility = Visibility.Collapsed;
            LessonBackButton.Visibility = Visibility.Collapsed;
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            HideTargetDwellVisual();
            // No dwell or click is required to dismiss instructions.
            StartBonusTimer(TimeSpan.FromSeconds(5), () =>
            {
                if (currentScreen != AppScreen.Lesson || stopping || exitRequested) return;
                lessonIntroActive = false;
                LessonIntroOverlay.Visibility = Visibility.Collapsed;
                LessonBackButton.Visibility = Visibility.Visible;
                PresentNextTarget();
            });
        }

        // Present bonus-round instructions before the first question.
        private void BeginTriviaIntro()
        {
            bonusIntroActive = true;
            BonusChoicesPanel.Visibility = Visibility.Collapsed;
            BonusFinalPanel.Visibility = Visibility.Collapsed;
            BonusIntroOverlay.Visibility = Visibility.Visible;
            // Apply the same guided, automatic transition to every future
            // mini-game; no unlearned selection action is needed.
            StartBonusTimer(TimeSpan.FromSeconds(5), () =>
            {
                if (currentScreen != AppScreen.Bonus || stopping || exitRequested) return;
                bonusIntroActive = false;
                BonusIntroOverlay.Visibility = Visibility.Collapsed;
                _ = StartBonusRoundAsync();
            });
        }

        // Handle a lesson placeholder without pretending the lesson is implemented.
        private void OnPlaceholderLevel(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Levels) return;
            var button = sender as Button;
            LevelsNoticeText.Text = (button != null ? button.Content.ToString() : "This level") +
                " is coming soon.";
        }

        // Refresh completion indicators from saved training progress.
        private void UpdateLevelBadges()
        {
            // Level 0 uses its own persisted completion flag because the
            // introductory tutorial is not part of TrainingProgress levels 1–6.
            LevelZeroReplayButton.Content = "LEVEL 0" +
                (ShouldStartLevelZero() ? "" : "  COMPLETED");
            for (int i = 0; i < allLevelButtons.Length; i++)
            {
                bool done = trainingProgress.IsCompleted(i + 1);
                allLevelButtons[i].Content = "LEVEL " + (i + 1) +
                    (done ? "  COMPLETED" : i == 0 ? "" : "  · SOON");
            }
        }

        // Respond to training canvas layout changes.
        private void OnCanvasSized(object sender, SizeChangedEventArgs e)
        {
            ApplyDisplaySizes();
            RepositionTarget();
        }

        // Physical display scaling ===
        // Refresh physical-size conversion after display DPI changes.
        private async void OnDisplayDpiChanged(DisplayInformation sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (exitRequested || stopping) return;
                displayScale = new DisplayScaleManager();
                ApplyDisplaySizes();
                RepositionTarget();
                if (displaySettingsVisible) ResetReferencePreview();
            });
        }

        // Apply the current physical-reference conversion to training visuals.
        private void ApplyDisplaySizes()
        {
            gazeButtonBounds.Clear();
            lastDrawnGazeCursor = new Point(double.NaN, double.NaN);
            if (displayScale == null) return;
            double size = displayScale.ToViewUnits(
                DisplayScaleManager.LevelOneDiameterMillimeters);
            targetDiameter = Math.Max(1, size);
            bullseyeDiameter = Math.Max(1, displayScale.ToViewUnits(
                DisplayScaleManager.BullseyeDiameterMillimeters));
            TargetRing.Width = targetDiameter;
            TargetRing.Height = targetDiameter;
            TargetBullseye.Width = bullseyeDiameter;
            TargetBullseye.Height = bullseyeDiameter;

            // Preserve generous minimum gaze-button sizes on small screens.
            double buttonHeight = Math.Max(76, displayScale.ToViewUnits(20));
            ExitButton.MinHeight = Math.Max(88, buttonHeight);
            LevelsMenuButton.MinHeight = Math.Max(100, buttonHeight);
            SettingsMenuButton.MinHeight = Math.Max(76, buttonHeight);
            LevelsMenuButton.MinHeight = Math.Max(76, buttonHeight);
            TdControlMenuButton.MinHeight = Math.Max(76, buttonHeight);
            DiagnosticsMenuButton.MinHeight = Math.Max(76, buttonHeight);
        }

        // Initialize the editable display-reference width.
        private void ResetReferencePreview()
        {
            pendingReferenceWidth = displayScale.ReferenceBarViewWidth;
            UpdateReferencePreview();
        }

        // Refresh the displayed calibration reference and its unit conversions.
        private void UpdateReferencePreview()
        {
            double available = RootLayout.ActualWidth - 48;
            double limit = Math.Max(80, available);
            pendingReferenceWidth = Math.Max(30,
                Math.Min(limit, pendingReferenceWidth));
            CalibrationReferenceBar.Width = pendingReferenceWidth;
            double rawPixelsPerView = displayInformation != null
                ? Math.Max(1.0, displayInformation.RawPixelsPerViewPixel) : 1.0;
            CalibrationStatusText.Text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Bar width: {0:0.00} DIP  |  {1:0.00} screen pixels  |  {2:0.000} pixels/mm",
                pendingReferenceWidth, pendingReferenceWidth * rawPixelsPerView,
                DisplayScaleManager.GetViewUnitsPerMillimeter(pendingReferenceWidth) * rawPixelsPerView);
        }

        // Open the display-reference settings panel.
        private void OnDisplaySettingsRequested(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Menu) return;
            ShowScreen(AppScreen.Settings);
            ResetReferencePreview();
        }

        // Decrease the editable reference width.
        private void OnReferenceShorter(object sender, RoutedEventArgs e)
        {
            if (!displaySettingsVisible) return;
            pendingReferenceWidth -= 6;
            UpdateReferencePreview();
        }

        // Increase the editable reference width.
        private void OnReferenceLonger(object sender, RoutedEventArgs e)
        {
            if (!displaySettingsVisible) return;
            pendingReferenceWidth += 6;
            UpdateReferencePreview();
        }

        // Persist the reference and apply the updated display sizes.
        private void OnSaveDisplay(object sender, RoutedEventArgs e)
        {
            if (!displaySettingsVisible) return;
            try
            {
                displayScale.SaveReferenceWidth(pendingReferenceWidth);
                ApplyDisplaySizes();
                RepositionTarget();
                CloseDisplaySettings();
                LiveStatusText.Text = "Display size saved.";
            }
            catch (Exception ex)
            {
                CalibrationStatusText.Text = "Unable to save display size: " +
                                              ex.GetType().Name;
            }
        }

        // Discard the unsaved reference adjustment.
        private void OnCancelDisplay(object sender, RoutedEventArgs e)
        {
            if (displaySettingsVisible) CloseDisplaySettings();
        }

        // Return from the display settings panel.
        private void CloseDisplaySettings()
        {
            ShowScreen(AppScreen.Menu);
        }

        // Mouse and cursor presentation ===
        // Handle the mouse-driven training interaction where allowed.
        private void OnTrainingCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!mouseOnlyFallback || stopping || lessonIntroActive ||
                currentScreen != AppScreen.Lesson) return;
            var point = e.GetCurrentPoint(TrainingCanvas);
            if (!point.Properties.IsLeftButtonPressed) return;
            ProcessPosition(point.Position, point.Position, true);
            e.Handled = true;
        }

        // Handle mouse movement without treating it as measured raw gaze.
        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Mouse preview remains available only when hardware is not engaged.
            if (live || stopping || currentScreen != AppScreen.Lesson) return;
            Point canvasPoint = e.GetCurrentPoint(TrainingCanvas).Position;
            Point canvasOrigin = TrainingCanvas.TransformToVisual(this)
                                  .TransformPoint(new Point(0, 0));
            if (!mouseOnlyFallback)
            {
                UpdateCursor(new Point(canvasPoint.X + canvasOrigin.X,
                                       canvasPoint.Y + canvasOrigin.Y));
                ProcessPosition(canvasPoint, canvasPoint);
            }
        }

        // This overlay belongs to the page, not the training canvas. It cannot
        // intercept Exit Button clicks and is independent of TD Control's trace.
        // Update the rendered training cursor from a view-relative coordinate.
        private void UpdateCursor(Point viewPoint)
        {
            if (CursorOverlay.ActualWidth <= DotDiameter ||
                CursorOverlay.ActualHeight <= DotDiameter) return;
            Point origin = CursorOverlay.TransformToVisual(this)
                                        .TransformPoint(new Point(0, 0));
            double x = viewPoint.X - origin.X;
            double y = viewPoint.Y - origin.Y;
            // Clamp only the rendered cursor. This prevents boundary jitter
            // from repeatedly hiding and showing the cursor when the tracker
            // reports a position just outside the view.
            x = Clamp(x, 0, CursorOverlay.ActualWidth);
            y = Clamp(y, 0, CursorOverlay.ActualHeight);

            bool levelZero = currentScreen == AppScreen.LevelZero;
            if (levelZero)
            {
                PreviewDot.Visibility = Visibility.Collapsed;
                LevelZeroEyeCursor.Visibility = Visibility.Visible;
            }
            else
            {
                LevelZeroEyeCursor.Visibility = Visibility.Collapsed;
                PreviewDot.Visibility = Visibility.Visible;
            }

            // Visual cursor placement may skip tiny changes, but every raw
            // gaze sample still drives hit testing and diagnostic measurements.
            if (lastDrawnGazeCursorWasLevelZero == levelZero &&
                Math.Abs(lastDrawnGazeCursor.X - x) < 0.35 &&
                Math.Abs(lastDrawnGazeCursor.Y - y) < 0.35) return;
            lastDrawnGazeCursor = new Point(x, y);
            lastDrawnGazeCursorWasLevelZero = levelZero;
            if (levelZero)
            {
                Canvas.SetLeft(LevelZeroEyeCursor, Clamp(x - 56, 0,
                    CursorOverlay.ActualWidth - 112));
                Canvas.SetTop(LevelZeroEyeCursor, Clamp(y - 32.5, 0,
                    CursorOverlay.ActualHeight - 65));
            }
            else
            {
                Canvas.SetLeft(PreviewDot, Clamp(x - DotDiameter / 2, 0,
                    CursorOverlay.ActualWidth - DotDiameter));
                Canvas.SetTop(PreviewDot, Clamp(y - DotDiameter / 2, 0,
                    CursorOverlay.ActualHeight - DotDiameter));
            }
        }

        private void ProcessPosition(Point p, Point rawPoint, bool mouseClick = false)
        {
            if (completed || exitRequested || stopping ||
                currentScreen != AppScreen.Lesson || lessonIntroActive || targetDelayTimer != null) return;

            if (TrainingCanvas.ActualWidth < TargetDiameter + 24 ||
                TrainingCanvas.ActualHeight < TargetDiameter + 24 ||
                p.X < 0 || p.Y < 0 ||
                p.X > TrainingCanvas.ActualWidth ||
                p.Y > TrainingCanvas.ActualHeight)
            {
                RecordInterruptedTargetDwell(DateTimeOffset.UtcNow);
                TargetRing.StrokeThickness = 8;
                UpdateTargetDwellVisual(false);
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (targetIndex < 0 || targetCell < 0)
            {
                PresentNextTarget();
                return;
            }

            Point center = TargetCenter();
            bool inRing = AdaptiveHitTesting.ContainsCircle(
                p, center, TargetDiameter, gazeProfile.HitBoxScale);
            if (live && !mouseOnlyFallback)
            {
                // Do not treat a tracker/sample gap as a failed deliberate hold.
                if ((now - targetLastSampleAt).TotalMilliseconds > 300)
                {
                    targetTrialInterrupted = true;
                    targetMeasurement.CancelHold();
                }
                targetLastSampleAt = now;
                if (inRing && !targetArrivalSeconds.HasValue)
                    targetArrivalSeconds = Math.Max(0, (now - targetShownAt).TotalSeconds);
            }
            if (!inRing)
            {
                RecordInterruptedTargetDwell(now);
                UpdateTargetDwellVisual(false);
                return;
            }

            // A full 1000ms uninterrupted hold is required for acquisition.
            bool acquired = mouseOnlyFallback
                ? mouseClick : targetMeasurement.Observe(rawPoint, now, true);
            if (!mouseOnlyFallback) UpdateTargetDwellVisual(inRing);
            if (!acquired) return;
            TargetRing.StrokeThickness = 8;
            UpdateTargetDwellVisual(false);
            if (live)
            {
                TargetPerformance result = targetMeasurement.Measure(
                    center, TargetDiameter * 0.5, now);
                if (!mouseOnlyFallback && (Application.Current as App)?.IsolationMode == null)
                {
                    double? jitterMm = result.JitterViewUnits.HasValue && displayScale != null
                        ? (double?)displayScale.ToMillimeters(result.JitterViewUnits.Value) : null;
                    levelOneDiagnostics.RecordTarget(targetCell, targetDirection,
                        targetTrialInterrupted ? null : targetArrivalSeconds,
                        !targetTrialInterrupted && targetArrivalSeconds.HasValue,
                        targetMisses == 0, result, jitterMm);
                }
                gazeProfile.RecordTargetPerformance(result, 1);
                dwellRecommendation.RecordCompletedHold();
                trainingReport.RecordOverall(gazeProfile.AccuracyScore);
                trainingReport.RecordAccuracy(result.Precision);
                Point canvasOriginOnPage = TrainingCanvas.TransformToVisual(RootLayout)
                    .TransformPoint(new Point(0, 0));
                spatialAccuracy.Record(
                    new Point(center.X + canvasOriginOnPage.X,
                              center.Y + canvasOriginOnPage.Y),
                    RootLayout.ActualWidth, RootLayout.ActualHeight, result.Overall);
                RecordMeasuredJitter(
                    new Point(center.X + canvasOriginOnPage.X,
                              center.Y + canvasOriginOnPage.Y), result);
                ApplyAdaptiveSmoothingAndRecord();
                UpdateAccuracyDisplay();
            }

            targetIndex++;
            UpdateTargetCountdown();
            if (targetIndex >= TargetsPerLevel)
            {
                completed = true;
                // Reviewers may finish the mouse demo, but mouse clicks must
                // not certify eye-gaze completion or inflate training history.
                if (live)
                {
                    trainingProgress.MarkCompleted(1);
                    trainingReport.RecordCompletedRun();
                    if (!mouseOnlyFallback && (Application.Current as App)?.IsolationMode == null)
                        levelOneDiagnostics.CompleteRound();
                }
                Trace("Level 1 completed: 35 targets; source=" +
                      (live ? "live gaze" : "mouse"));
                UpdateLevelBadges();
                BeginBonusTransition();
                return;
            }
            ScheduleNextTarget();
        }

        // Celebrate on the lesson's existing black background, then enter the
        // dedicated bonus screen without any gaze activation or menu selection.
        // Bonus round and trivia ===
        // Present the transition from completed targets to the trivia round.
        private void BeginBonusTransition()
        {
            CancelBonusTransition();
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            HideTargetDwellVisual();
            buttonDwellWheel.Hide();
            CompletionOverlay.Visibility = Visibility.Visible;
            TargetCountdown.Visibility = Visibility.Collapsed;
            LessonBackButton.Visibility = Visibility.Collapsed;
            bonusTransitionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            bonusTransitionTimer.Tick += OnBonusTransitionTick;
            bonusTransitionTimer.Start();
        }

        // Advance after the transition timer expires.
        private void OnBonusTransitionTick(object sender, object e)
        {
            CancelBonusTransition();
            if (!completed || stopping || exitRequested ||
                currentScreen != AppScreen.Lesson) return;
            ShowScreen(AppScreen.Bonus);
            BeginTriviaIntro();
        }

        // Stop and release the pending bonus transition.
        private void CancelBonusTransition()
        {
            if (bonusTransitionTimer != null)
            {
                bonusTransitionTimer.Stop();
                bonusTransitionTimer.Tick -= OnBonusTransitionTick;
                bonusTransitionTimer = null;
            }
            CompletionOverlay.Visibility = Visibility.Collapsed;
            LessonBackButton.Visibility = lessonIntroActive
                ? Visibility.Collapsed : Visibility.Visible;
        }

        // Identify whether a control represents a trivia answer.
        private bool IsBonusAnswer(Button button)
        {
            return currentScreen == AppScreen.Bonus && triviaAwaitingAnswer &&
                   (button == BonusTrueButton || button == BonusFalseButton);
        }

        // Load the trivia bank and initialize the current round.
        private async Task StartBonusRoundAsync()
        {
            if (triviaLoading) return;
            triviaLoading = true;
            triviaAnswered = 0;
            triviaCorrect = 0;
            triviaAwaitingAnswer = false;
            triviaFinishing = false;
            currentTriviaQuestion = null;
            BonusChoicesPanel.Visibility = Visibility.Collapsed;
            BonusFinalPanel.Visibility = Visibility.Collapsed;
            BonusProgressText.Text = "BONUS ROUND";
            BonusFactText.Text = "Getting your trivia ready…";
            try
            {
                await triviaBank.LoadAsync();
                if (currentScreen != AppScreen.Bonus || stopping || exitRequested) return;
                triviaBank.BeginRound();
                NextBonusQuestion();
            }
            catch (Exception ex)
            {
                BonusProgressText.Text = "TRIVIA UNAVAILABLE";
                BonusFactText.Text = "Couldn't load the questions. Return to Levels and try again.";
                BonusChoicesPanel.Visibility = Visibility.Collapsed;
                BonusFinalPanel.Visibility = Visibility.Collapsed;
                Trace("Trivia loading failed: " + ex.GetType().Name);
                // The normal button-based navigation isn't available before the
                // player has learned it; return without requiring a click.
                StartBonusTimer(TimeSpan.FromSeconds(5), () => ShowScreen(AppScreen.Levels));
            }
            finally { triviaLoading = false; }
        }

        // Deal the next question and reset its interaction state.
        private void NextBonusQuestion()
        {
            if (currentScreen != AppScreen.Bonus || stopping || exitRequested) return;
            CancelBonusQuestionTimer();
            if (triviaAnswered >= 10)
            {
                FinishBonusRound();
                return;
            }

            try { currentTriviaQuestion = triviaBank.Draw(); }
            catch (Exception ex)
            {
                Trace("Trivia draw failed: " + ex.GetType().Name);
                FinishBonusRound();
                return;
            }
            triviaAwaitingAnswer = true;
            BonusProgressText.Text = "QUESTION " + (triviaAnswered + 1) + " OF 10";
            BonusFactText.Text = currentTriviaQuestion.Statement;
            BonusChoicesPanel.Visibility = Visibility.Visible;
            BonusFinalPanel.Visibility = Visibility.Collapsed;
            BonusTrueButton.IsEnabled = true;
            BonusFalseButton.IsEnabled = true;
            // Enforce a new, complete gaze dwell for each question. Do not
            // automatically select the same button on the next fact.
            gazeControlTarget = null;
            gazeControlTriggered = false;
            bonusButtonMeasurement.CancelHold();
            buttonDwellWheel.Hide();
            cursorFilter.Reset();
            requireLookAwayAfterNavigation = live;
            lastActivatedGazePosition = mostRecentRawGazePosition;
        }

        // Submit the True button selection.
        private void OnBonusTrue(object sender, RoutedEventArgs e)
        {
            SubmitBonusAnswer(true, false);
        }

        // Submit the False button selection.
        private void OnBonusFalse(object sender, RoutedEventArgs e)
        {
            SubmitBonusAnswer(false, false);
        }

        // Record the selected answer and schedule feedback; knowledge correctness is separate from gaze performance.
        private void SubmitBonusAnswer(bool selectedTrue, bool gazeActivated)
        {
            if (currentScreen != AppScreen.Bonus || !triviaAwaitingAnswer ||
                stopping || exitRequested || currentTriviaQuestion == null) return;
            triviaAwaitingAnswer = false;
            bool right = selectedTrue == currentTriviaQuestion.IsTrue;
            if (right) triviaCorrect++;
            triviaAnswered++;

            // Accuracy measures successful gaze-button selection, not factual
            // knowledge. Incorrect trivia answers do NOT lower or raise it.
            if (gazeActivated && live && !mouseOnlyFallback)
            {
                try
                {
                    Button button = selectedTrue ? BonusTrueButton : BonusFalseButton;
                    Point origin = button.TransformToVisual(this)
                                         .TransformPoint(new Point(0, 0));
                    Point center = new Point(origin.X + button.ActualWidth / 2,
                                             origin.Y + button.ActualHeight / 2);
                    double radius = Math.Min(button.ActualWidth, button.ActualHeight) / 2;
                    TargetPerformance performance =
                        bonusButtonMeasurement.Measure(center, radius,
                                                       DateTimeOffset.UtcNow);
                    // Transfer observations include both factual outcomes, but
                    // remain separate from the comparable circular-target metrics.
                    if ((Application.Current as App)?.IsolationMode == null)
                        levelOneDiagnostics.RecordBonus(performance,
                            performance.JitterViewUnits.HasValue && displayScale != null
                            ? (double?)displayScale.ToMillimeters(performance.JitterViewUnits.Value) : null);
                    // Preserve the original adaptive-scoring rule: wrong quiz
                    // answers are neutral. The new transfer measurement is not.
                    if (right)
                    {
                        gazeProfile.RecordTargetPerformance(performance, 1);
                        trainingReport.RecordOverall(gazeProfile.AccuracyScore);
                        trainingReport.RecordAccuracy(performance.Precision);
                        spatialAccuracy.Record(center, RootLayout.ActualWidth,
                                               RootLayout.ActualHeight, performance.Overall);
                        RecordMeasuredJitter(center, performance);
                        ApplyAdaptiveSmoothingAndRecord();
                        UpdateAccuracyDisplay();
                    }
                }
                catch (InvalidOperationException)
                {
                    // No valid settled gaze samples: do not fabricate a score.
                }
            }
            bonusButtonMeasurement.CancelHold();
            buttonDwellWheel.Hide();
            gazeControlTarget = null;
            gazeControlTriggered = false;
            BonusTrueButton.IsEnabled = false;
            BonusFalseButton.IsEnabled = false;
            BonusChoicesPanel.Visibility = Visibility.Collapsed;
            BonusProgressText.Text = right ? "THAT'S RIGHT!" : "GOOD TRY!";
            BonusFactText.Text = "Answer: " +
                (currentTriviaQuestion.IsTrue ? "TRUE" : "FALSE") +
                "\n\n" + currentTriviaQuestion.Explanation;
            StartBonusTimer(TimeSpan.FromSeconds(5), NextBonusQuestion);
        }

        // Present the completed round result and arrange the return to lesson selection.
        private void FinishBonusRound()
        {
            if (currentScreen != AppScreen.Bonus || triviaFinishing) return;
            triviaFinishing = true;
            triviaAwaitingAnswer = false;
            currentTriviaQuestion = null;
            BonusChoicesPanel.Visibility = Visibility.Collapsed;
            BonusFinalPanel.Visibility = Visibility.Visible;
            BonusProgressText.Text = "BONUS ROUND COMPLETE!";
            BonusFactText.Text = "";
            BonusFinalText.Text = triviaCorrect + " OUT OF " + triviaAnswered + " CORRECT!";
            StartBonusTimer(TimeSpan.FromSeconds(3), () =>
            {
                if (currentScreen != AppScreen.Bonus || exitRequested || stopping) return;
                UpdateLevelBadges();
                LevelsNoticeText.Text = "";
                ShowScreen(AppScreen.Levels);
            });
        }

        // Schedule a bonus-round action using the supplied delay.
        private void StartBonusTimer(TimeSpan delay, Action callback)
        {
            CancelBonusQuestionTimer();
            bonusTimerSecondsRemaining = delay.TotalSeconds;
            bonusTimerLastTick = DateTimeOffset.UtcNow;
            bonusQuestionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            EventHandler<object> tick = null;
            tick = (sender, args) =>
            {
                if (bonusQuestionTimer == null) return;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                double elapsed = Math.Max(0, (now - bonusTimerLastTick).TotalSeconds);
                bonusTimerLastTick = now;
                // Do not silently advance when the live gaze stream stops.
                // Mouse-only preview has no gaze device to monitor.
                bool gazeReady = !live || (gazeSubscribed &&
                    lastValidGazeSampleAt != default(DateTimeOffset) &&
                    now - lastValidGazeSampleAt <= TimeSpan.FromSeconds(1));
                if (!gazeReady) return;
                bonusTimerSecondsRemaining -= elapsed;
                if (bonusTimerSecondsRemaining > 0) return;
                bonusQuestionTimer.Stop();
                bonusQuestionTimer.Tick -= tick;
                bonusQuestionTimer = null;
                callback();
            };
            bonusQuestionTimer.Tick += tick;
            bonusQuestionTimer.Start();
        }

        // Cancel the pending question or feedback timer.
        private void CancelBonusQuestionTimer()
        {
            if (bonusQuestionTimer != null)
            {
                bonusQuestionTimer.Stop();
                bonusQuestionTimer = null;
            }
            bonusTimerSecondsRemaining = 0;
            triviaAwaitingAnswer = false;
            bonusButtonMeasurement.CancelHold();
        }

        // Score only an actual started dwell (at least 150 ms), not an
        // ordinary search across the black canvas. CancelIncompleteHold
        // clears the attempt so the same miss cannot be counted twice.
        // Hardware disconnects and intentional lesson navigation use the
        // regular CancelHold() path, which never adds a penalty.
        // Target practice and scoring ===
        // Record an eligible interrupted deliberate hold without repeated penalties.
        private void RecordInterruptedTargetDwell(DateTimeOffset now)
        {
            if (!live || completed || currentScreen != AppScreen.Lesson ||
                targetIndex < 0 || targetCell < 0 || targetDelayTimer != null || lessonIntroActive)
            {
                targetMeasurement.CancelHold();
                return;
            }
            // Out-of-view samples also end a hold. Gaps are not scored as
            // misses and invalidate the comparable acquisition observation.
            if ((now - targetLastSampleAt).TotalMilliseconds > 300)
            {
                targetTrialInterrupted = true;
                targetMeasurement.CancelHold();
                return;
            }
            if (targetMeasurement.CancelIncompleteHold(now))
            {
                targetMisses++;
                gazeProfile.RecordIncompleteTargetDwell(1);
                dwellRecommendation.RecordInterruptedHold();
                if (gazeProfile.Acquisitions > 0)
                    trainingReport.RecordOverall(gazeProfile.AccuracyScore);
                trainingReport.RecordAccuracy(0);
                Point missedTarget = TargetCenter();
                Point offset = TrainingCanvas.TransformToVisual(RootLayout)
                    .TransformPoint(new Point(0, 0));
                spatialAccuracy.Record(
                    new Point(missedTarget.X + offset.X, missedTarget.Y + offset.Y),
                    RootLayout.ActualWidth, RootLayout.ActualHeight, 0);
                ApplyAdaptiveSmoothingAndRecord();
                UpdateAccuracyDisplay();
            }
        }

        // Reset target practice state for a new run.
        private void ResetLesson()
        {
            CancelTargetDelay();
            CancelBonusTransition();
            targetIndex = -1;
            targetCell = -1;
            completed = false;
            hasPreviousTargetPosition = false;
            remainingTargetCells.Clear();
            remainingTargetCells.AddRange(Enumerable.Range(0, TargetsPerLevel));
            Array.Clear(plannedDirections, 0, plannedDirections.Length);
            levelOneDiagnostics.BeginRound();
            SetLessonBackPosition(false);
            TargetCountdown.HorizontalAlignment = HorizontalAlignment.Left;
            TargetCountdown.Margin = new Thickness(20, 0, 0, 16);
            TargetCountdown.Visibility = Visibility.Collapsed;
            UpdateTargetCountdown();
            targetMeasurement.CancelHold();
            TargetRing.StrokeThickness = 8;
            HideTargetDwellVisual();
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            cursorFilter.Reset();
            ApplyAdaptiveSmoothingAndRecord();
            // The first trial is started only after the intro is dismissed.
        }

        // Refresh the displayed training performance values.
        private void UpdateAccuracyDisplay()
        {
            // Only the persisted composite score is shown to the learner.
            // Component metrics still drive the adaptive smoothing/hitbox.
            AccuracyText.Text = gazeProfile.Acquisitions == 0
                ? "ACCURACY —"
                : "ACCURACY " + Math.Round(gazeProfile.AccuracyScore) + "%";
        }

        // Grid placement and pacing ===
        // Map each target center to the actual full-screen 7 x 5 grid.
        // Edge cells retain a fully visible circle and radial wheel, even when
        // the original wide edge inset must be reduced to reach their centers.
        private Point TargetCenter()
        {
            double w = TrainingCanvas.ActualWidth, h = TrainingCanvas.ActualHeight;
            int row = Math.Max(0, targetCell) / SpatialAccuracyMap.Columns;
            int col = Math.Max(0, targetCell) % SpatialAccuracyMap.Columns;
            double cw = w / SpatialAccuracyMap.Columns, ch = h / SpatialAccuracyMap.Rows;
            double inset = Math.Max(TargetDiameter * 0.5 + 4, Math.Min(46, Math.Min(cw, ch) - 4));
            double left = Math.Max(inset, col * cw + 1);
            double right = Math.Min(w - inset, (col + 1) * cw - 1);
            double top = Math.Max(inset, row * ch + 1);
            double bottom = Math.Min(h - inset, (row + 1) * ch - 1);
            return new Point(Clamp(targetPosition.X * w, left, Math.Max(left, right)),
                Clamp(targetPosition.Y * h, top, Math.Max(top, bottom)));
        }

        // Classify the intended center-to-center displacement in view units.
        // Index order matches the persisted diagnostic: R, DR, D, DL, L, UL, U, UR.
        private int DirectionTo(Point from, Point to)
        {
            double dx = (to.X - from.X) * TrainingCanvas.ActualWidth;
            double dy = (to.Y - from.Y) * TrainingCanvas.ActualHeight;
            if (Math.Abs(dx) + Math.Abs(dy) < .001) return -1;
            return ((int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) + 8) % 8;
        }

        // Sample only unvisited cells; favor underrepresented directions and
        // meaningful travel distances, but randomize ties and within-cell points.
        private void PickRandomTarget()
        {
            if (remainingTargetCells.Count == 0) return;
            int chosen = remainingTargetCells[0], bestDirection = -1;
            Point chosenPoint = new Point();
            double best = double.NegativeInfinity;
            foreach (int cell in remainingTargetCells)
            {
                int row = cell / SpatialAccuracyMap.Columns, col = cell % SpatialAccuracyMap.Columns;
                Point candidate = new Point((col + .2 + .6 * placementRandom.NextDouble()) / SpatialAccuracyMap.Columns,
                    (row + .2 + .6 * placementRandom.NextDouble()) / SpatialAccuracyMap.Rows);
                int direction = hasPreviousTargetPosition ? DirectionTo(previousTargetPosition, candidate) : -1;
                double dx = candidate.X - previousTargetPosition.X, dy = candidate.Y - previousTargetPosition.Y;
                double score = placementRandom.NextDouble();
                if (direction >= 0) score += -4 * plannedDirections[direction] + Math.Sqrt(dx * dx + dy * dy);
                if (score > best)
                {
                    best = score; chosen = cell; chosenPoint = candidate; bestDirection = direction;
                }
            }
            targetCell = chosen;
            remainingTargetCells.Remove(chosen);
            targetPosition = chosenPoint;
            // Use actual clamped target centers for directional diagnostics.
            Point actual = TargetCenter();
            Point normalized = new Point(actual.X / TrainingCanvas.ActualWidth, actual.Y / TrainingCanvas.ActualHeight);
            targetDirection = hasPreviousTargetPosition ? DirectionTo(previousTargetPosition, normalized) : -1;
            if (targetDirection >= 0) plannedDirections[targetDirection]++;
            previousTargetPosition = normalized;
            hasPreviousTargetPosition = true;
        }

        // Keep Back accessible without covering an upper-left grid target.
        // Move it to the opposite side only when the target overlaps its zone.
        // Re-arm gaze after relocation to prevent an accidental navigation.
        private void SetLessonBackPosition(bool right)
        {
            if (backOnRight == right) return;
            backOnRight = right;
            LessonBackButton.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            LessonBackButton.Margin = right ? new Thickness(0, 16, 20, 0) : new Thickness(20, 16, 0, 0);
            gazeButtonBounds.Clear();
            gazeControlTarget = null;
            gazeControlTriggered = false;
            buttonDwellWheel.Hide();
            requireLookAwayAfterNavigation = live;
            lastActivatedGazePosition = mostRecentRawGazePosition;
        }

        // Start timing only when the actual target is revealed, never during
        // the introduction, blank interval, or previous target's dwell.
        private void PresentNextTarget()
        {
            if (stopping || exitRequested || completed || lessonIntroActive ||
                currentScreen != AppScreen.Lesson || remainingTargetCells.Count == 0 ||
                TrainingCanvas.ActualWidth < TargetDiameter + 24 ||
                TrainingCanvas.ActualHeight < TargetDiameter + 24) return;
            if (targetIndex < 0) targetIndex = 0;
            PickRandomTarget();
            Point center = TargetCenter();
            double radius = Math.Max(TargetDiameter * .5, 46);
            double backWidth = Math.Max(LessonBackButton.ActualWidth, LessonBackButton.MinWidth);
            double backHeight = Math.Max(LessonBackButton.ActualHeight, LessonBackButton.MinHeight);
            bool topOverlap = center.Y - radius < backHeight + 20;
            bool leftOverlap = center.X - radius < backWidth + 24;
            SetLessonBackPosition(topOverlap && leftOverlap);
            targetShownAt = targetLastSampleAt = DateTimeOffset.UtcNow;
            targetArrivalSeconds = null;
            targetTrialInterrupted = false;
            targetMisses = 0;
            targetMeasurement.BeginTarget(targetShownAt);
            RepositionTarget();
        }

        // Hide both visuals and clear the hold for exactly one independent
        // 500 ms UI timer interval; gaze samples cannot select a hidden target.
        private void ScheduleNextTarget()
        {
            CancelTargetDelay();
            targetCell = -1;
            targetMeasurement.CancelHold();
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            HideTargetDwellVisual();
            targetDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(InterTargetDelayMilliseconds) };
            targetDelayTimer.Tick += OnTargetDelayElapsed;
            targetDelayTimer.Start();
        }

        private void OnTargetDelayElapsed(object sender, object args)
        {
            CancelTargetDelay();
            PresentNextTarget();
        }

        // Cancel delayed presentation on navigation, replay or shutdown.
        private void CancelTargetDelay()
        {
            if (targetDelayTimer == null) return;
            targetDelayTimer.Stop();
            targetDelayTimer.Tick -= OnTargetDelayElapsed;
            targetDelayTimer = null;
        }

        // Remaining-target count decreases only after a successful selection.
        // The visual stays visible through the 500 ms inter-target pause.
        private void UpdateTargetCountdown()
        {
            int remaining = Math.Max(0, TargetsPerLevel - Math.Max(0, targetIndex));
            TargetCountdownText.Text = remaining + " remaining";
        }

        // Keep the noninteractive countdown away from the target and radial
        // wheel, using the opposite bottom corner whenever necessary.
        private void PositionTargetCountdown(Point center)
        {
            const double marginX = 20, marginY = 16;
            double clearance = Math.Max(TargetDiameter * .5, 46) + 8;
            double width = TargetCountdown.Width, height = TargetCountdown.Height;
            bool right = center.X - clearance < marginX + width &&
                center.X + clearance > marginX &&
                center.Y + clearance > TrainingCanvas.ActualHeight - marginY - height;
            TargetCountdown.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            TargetCountdown.Margin = right ? new Thickness(0, 0, marginX, marginY) : new Thickness(marginX, 0, 0, marginY);
            UpdateTargetCountdown();
            TargetCountdown.Visibility = Visibility.Visible;
        }

        // Apply the current target position to its visual controls.
        private void RepositionTarget()
        {
            if (currentScreen != AppScreen.Lesson || targetIndex < 0 || targetCell < 0 ||
                targetDelayTimer != null || lessonIntroActive || completed || TrainingCanvas.ActualWidth < TargetDiameter + 24 ||
                TrainingCanvas.ActualHeight < TargetDiameter + 24)
            {
                TargetRing.Visibility = Visibility.Collapsed;
                TargetBullseye.Visibility = Visibility.Collapsed;
                HideTargetDwellVisual();
                return;
            }

            Point center = TargetCenter();
            PositionTargetCountdown(center);
            TargetRing.Width = TargetRing.Height = TargetDiameter;
            Canvas.SetLeft(TargetRing, center.X - TargetDiameter / 2);
            Canvas.SetTop(TargetRing, center.Y - TargetDiameter / 2);
            Canvas.SetLeft(TargetBullseye, center.X - bullseyeDiameter / 2);
            Canvas.SetTop(TargetBullseye, center.Y - bullseyeDiameter / 2);
            TargetRing.Visibility = Visibility.Visible;
            TargetBullseye.Visibility = Visibility.Visible;
            UpdateTargetDwellVisual(false);
        }

        // Reuses SharedButtonDwellWheel rather than a separate custom arc
        // or numeric countdown. Target completion remains 1000ms; button
        // completion continues to use its own 1500ms dwell.
        // Refresh the target's shared radial dwell indicator.
        private void UpdateTargetDwellVisual(bool inside)
        {
            if (!inside || currentScreen != AppScreen.Lesson ||
                TargetRing.Visibility != Visibility.Visible)
            {
                HideTargetDwellVisual();
                return;
            }

            Point center = TargetCenter();
            Point origin = TrainingCanvas.TransformToVisual(CursorOverlay)
                                         .TransformPoint(new Point(0, 0));
            targetDwellWheel.PresentAt(
                new Point(center.X + origin.X, center.Y + origin.Y),
                targetMeasurement.HoldProgress);
        }

        // Clear the visible target dwell progress.
        private void HideTargetDwellVisual()
        {
            targetDwellWheel.Hide();
        }

        // Restrict a numeric value to the supplied bounds.
        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        // Shutdown and isolation lifecycle ===
        // Cancel the diagnostic-only automatic-exit timer.
        private void StopIsolationTimer()
        {
            if (isolationTimer == null) return;
            isolationTimer.Stop();
            isolationTimer.Tick -= OnIsolationElapsed;
            isolationTimer = null;
        }

        // Record isolation state and invoke the shared exit sequence.
        private void OnIsolationElapsed(object sender, object e)
        {
            StopIsolationTimer();
            Trace("ISOLATION automatic exit; mode=" + (Application.Current as App)?.IsolationMode + "; live=" + live + "; mouse=" + mouseOnlyFallback + "; watcher=" + watcherStarted + "; source=" + (gazeSource != null) + "; subscribed=" + gazeSubscribed + "; eventReceived=" + isolationSampleSeen);
            OnExitRequested(null, null);
        }

        // Stop training activity and begin orderly resource cleanup before navigating away.
        private void OnExitRequested(object sender, RoutedEventArgs e)
        {
            if (exitRequested || stopping) return;
            exitRequested = true;
            StopIsolationTimer();
            Trace("EXIT SELECTED; live=" + live + "; mouse=" + mouseOnlyFallback + "; subscribed=" + gazeSubscribed + "; watcherStarted=" + watcherStarted);
            Trace("BEGIN stop startup timer");
            StopStartupDiscoveryTimer();
            Trace("END stop startup timer; BEGIN flush practice time");
            trainingReport.FlushPracticeTime();
            Trace("END flush practice time; BEGIN dismiss notice and stop Level 0");
            DismissNoTrackerNotice();
            StopLevelZero();
            Trace("END stop Level 0; BEGIN stop practice and bonus timers");
            CancelTargetDelay();
            HideTherapistExportConfirmation();
            if (practiceTimer != null)
            {
                practiceTimer.Stop();
                practiceTimer.Tick -= OnPracticeTimerTick;
                practiceTimer = null;
            }
            CancelBonusTransition();
            CancelBonusQuestionTimer();
            LessonIntroOverlay.Visibility = Visibility.Collapsed;
            BonusIntroOverlay.Visibility = Visibility.Collapsed;
            lessonIntroActive = false;
            bonusIntroActive = false;
            displaySettingsVisible = false;
            targetMeasurement.CancelHold();
            HideTargetDwellVisual();
            cursorFilter.Reset();
            buttonDwellWheel.Hide();
            ExitButton.IsEnabled = false;
            SettingsExitButton.IsEnabled = false;
            Trace("END stop timers and reset UI; EXIT requested; source active=" + live);
            if (live && (!mouseOnlyFallback || gazeSubscribed)) BeginCleanup();
            else NavigateToCleanExit();
        }

        // Start staged gaze resource release on the UI thread.
        private void BeginCleanup()
        {
            if (!live || stopping) return;
            stopping = true;
            Trace("BEGIN staged cleanup; subscribed=" + gazeSubscribed + "; watcherStarted=" + watcherStarted);
            ExitButton.IsEnabled = false;
            SettingsExitButton.IsEnabled = false;
            LiveStatusText.Text = "Closing EyeGazeTrainer…";
            cleanupStage = 0;
            cleanupTimer = new DispatcherTimer();
            // Preserve the release order without the former 3-second
            // diagnostic pause between stages. Allow the UI dispatcher one
            // brief turn between steps rather than blocking the UI thread.
            cleanupTimer.Interval = TimeSpan.FromMilliseconds(100);
            cleanupTimer.Tick += OnCleanupTick;
            CleanupNextStage(); // first stage immediately
            cleanupTimer.Start();
        }

        // Advance one stage of the asynchronous shutdown sequence.
        private void OnCleanupTick(object sender, object e)
        {
            CleanupNextStage();
        }

        // Unsubscribe gaze events, release any legacy watcher, drop references and finish cleanup in order.
        private void CleanupNextStage()
        {
            var phase = cleanupStage;
            string phaseName;
            switch (phase)
            {
                case 0: phaseName = "unsubscribe GazeMoved"; break;
                case 1: phaseName = "stop watcher"; break;
                case 2: phaseName = "remove watcher handlers"; break;
                case 3: phaseName = "drop local references"; break;
                default:
                    cleanupTimer.Stop();
                    cleanupTimer.Tick -= OnCleanupTick;
                    cleanupTimer = null;
                    stopping = false;
                    live = false;
                    watcherStarted = false;
                    gazeSubscribed = false;
                    LiveStatusText.Text =
                        "Returning to your computer…";
                    Trace("FINISHED staged cleanup; TD Control status not verified");
                    if (exitRequested)
                    {
                        Trace("Cleanup complete; navigating off gaze page before exit");
                        NavigateToCleanExit();
                    }
                    return;
            }
            Trace("BEGIN " + phaseName);
            LiveStatusText.Text = "Closing EyeGazeTrainer…";
            try
            {
                switch (phase)
                {
                    case 0:
                        if (gazeSubscribed && gazeSource != null)
                            gazeSource.GazeMoved -= OnGazeMoved;
                        gazeSubscribed = false;
                        break;
                    case 1:
                        if (watcher != null && watcherStarted)
                            watcher.Stop();
                        watcherStarted = false;
                        break;
                    case 2:
                        if (watcher != null)
                        {
                            watcher.Added -= OnDeviceAdded;
                            watcher.Updated -= OnDeviceUpdated;
                            watcher.Removed -= OnDeviceRemoved;
                            // Added by mouse-fallback detection; must release
                            // during normal gaze shutdown too. Otherwise a
                            // watcher callback can outlive the closing page.
                            watcher.EnumerationCompleted -= OnTrackerEnumerationCompleted;
                        }
                        break;
                    case 3:
                        gazeSource = null;
                        watcher = null;
                        break;
                }
                Trace("END " + phaseName);
                cleanupStage++;
            }
            catch (Exception ex)
            {
                Trace("ERROR " + phaseName + ": " + ex);
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "HALTED at " + phaseName +
                    ". Helper assistance needed; do not close.";
                cleanupTimer.Stop();
                // Do not navigate or exit after a failed cleanup.
                exitRequested = false;
                ExitButton.IsEnabled = true;
                SettingsExitButton.IsEnabled = true;
            }
        }

        private bool cleanExitNavigationPending;

        // Await the companion's restoration acknowledgement before entering the gaze-free closing view.
        private async void NavigateToCleanExit()
        {
            if (cleanExitNavigationPending) return;
            cleanExitNavigationPending = true;
            // Mouse mode has no companion and must not run gaze handoff tasks.
            if (!mouseOnlyFallback)
            {
                var app = Application.Current as App;
                if (app != null) await app.RequestOverlayRestorationAsync();
            }
            // Based on the successful observed Back -> X run, not a safety guarantee.
            Trace("BEGIN navigation away from gaze page");
            try
            {
                bool navigated = Frame.Navigate(typeof(MainPage), mouseOnlyFallback && !directGazeAttempted && (Application.Current as App)?.IsolationMode == null
                    ? "EXIT_NO_GAZE_DIAGNOSTIC" : "EXIT_AFTER_GAZE_CLEANUP");
                Trace("END navigation; result=" + navigated);
                if (!navigated)
                {
                    cleanExitNavigationPending = false;
                    Trace("ERROR navigation returned false; exit cancelled");
                    LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Unable to navigate; app remains open.";
                    exitRequested = false;
                    ExitButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                cleanExitNavigationPending = false;
                Trace("ERROR navigation: " + ex);
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Navigation failed; app remains open.";
                exitRequested = false;
                ExitButton.IsEnabled = true;
            }
        }

        // Write trainer diagnostic messages through the application lifecycle trace.
        private void Trace(string message)
        {
            App.TraceLifecycle("TRAINER | " + message);
            if (string.IsNullOrEmpty(tracePath)) return;
            try
            {
                File.AppendAllText(tracePath, DateTimeOffset.Now.ToString("O") +
                    " | " + message + "\r\n");
            }
            catch (Exception ex)
            {
                LiveStatusText.Text = "Trace failed: " + ex.GetType().Name;
            }
        }

        // Detach view callbacks, restore the local pointer and release remaining page resources.
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StopIsolationTimer();
            CancelTargetDelay();
            Trace("BEGIN OnNavigatedFrom");
            // Navigation follows staged gaze release; make the Windows arrow
            // available again on the gaze-free closing page and desktop.
            Window.Current.Activated -= OnTrainerWindowActivated;
            if (!mouseOnlyFallback) RestoreWindowsPointerCursor();
            Trace("END cursor restoration");
            StopStartupDiscoveryTimer();
            if (startupSplashTimer != null)
            {
                startupSplashTimer.Stop();
                startupSplashTimer.Tick -= OnStartupSplashElapsed;
                startupSplashTimer = null;
            }
            RootLayout.SizeChanged -= OnGazeLayoutSizeChanged;
            gazeButtonBounds.Clear();
            trainingReport.FlushPracticeTime();
            StopNoTrackerNoticeTimer();
            StopLevelZero();
            HideTherapistExportConfirmation();
            if (practiceTimer != null)
            {
                practiceTimer.Stop();
                practiceTimer.Tick -= OnPracticeTimerTick;
                practiceTimer = null;
            }
            CancelBonusTransition();
            CancelBonusQuestionTimer();
            if (displayInformation != null)
            {
                displayInformation.DpiChanged -= OnDisplayDpiChanged;
                displayInformation = null;
            }
            Trace("Page OnNavigatedFrom; gaze active=" + live +
                  " cleanup stage=" + cleanupStage + " exit requested=" + exitRequested);
            base.OnNavigatedFrom(e);
            Trace("END OnNavigatedFrom");
        }
    }
}
