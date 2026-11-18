using System;
using System.IO;
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
        private double TargetDiameter { get { return targetDiameter; } }
        private readonly MasterCursorSmoothingFilter cursorFilter = new MasterCursorSmoothingFilter();
        private readonly AdaptiveGazeProfile gazeProfile = new AdaptiveGazeProfile();
        private const int TargetsPerLevel = 10;
        private readonly Random placementRandom = new Random();
        private readonly TrainingProgress trainingProgress = new TrainingProgress();
        private readonly SpatialAccuracyMap spatialAccuracy = new SpatialAccuracyMap();
        private readonly TextBlock[,] accuracyCells =
            new TextBlock[SpatialAccuracyMap.Rows, SpatialAccuracyMap.Columns];
        private Button[] diagnosticsButtons;
        private Point targetPosition;
        private Point previousTargetPosition;
        private bool hasPreviousTargetPosition;
        private enum AppScreen { Menu, Levels, Lesson, Bonus, Settings, Diagnostics }
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
        private bool autoStartAttempted;
        private Button gazeControlTarget;
        private SharedButtonDwellWheel buttonDwellWheel;
        private SharedButtonDwellWheel targetDwellWheel;
        private Button[] menuButtons;
        private Button[] levelsButtons;
        private Button[] lessonButtons;
        private Button[] bonusButtons;
        private Button[] bonusBackOnlyButtons;
        private readonly Button[] noGazeButtons = new Button[0];
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

        public LevelOnePreview()
        {
            InitializeComponent();
            buttonDwellWheel = new SharedButtonDwellWheel(CursorOverlay);
            // Training targets and gaze buttons share the exact same radial
            // wheel implementation, while retaining independent progress.
            targetDwellWheel = new SharedButtonDwellWheel(CursorOverlay);
            menuButtons = new[] { LevelsMenuButton, TdControlMenuButton,
                                  DiagnosticsMenuButton, SettingsMenuButton, ExitButton };
            diagnosticsButtons = new[] { DiagnosticsBackButton };
            BuildAccuracyGrid();
            allLevelButtons = new[] { LevelOneButton, LevelTwoButton,
                LevelThreeButton, LevelFourButton, LevelFiveButton, LevelSixButton };
            levelsButtons = new[] { LevelOneButton, LevelTwoButton,
                LevelThreeButton, LevelFourButton, LevelFiveButton,
                LevelSixButton, LevelsBackButton };
            lessonButtons = new[] { LessonBackButton };
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
            ShowScreen(AppScreen.Menu);
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (autoStartAttempted) return;
            autoStartAttempted = true;
            OnEnableLive(null, null);
        }

        private void OnEnableLive(object sender, RoutedEventArgs e)
        {
            if (live || stopping) return;
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
                LiveStatusText.Text = "Cannot begin: trace unavailable (" +
                                      ex.GetType().Name + ")";
                return;
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
                watcher.Start();
                watcherStarted = true;
                Trace("END watcher start");
            }
            catch (Exception ex)
            {
                Trace("ERROR watcher start: " + ex);
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Tracker setup failed: " + ex.GetType().Name +
                                      ". Use EXIT for supervised cleanup.";
            }
        }

        private void OnDeviceAdded(GazeDeviceWatcherPreview sender,
                                   GazeDeviceWatcherAddedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                    () => UseReadyDevice(args.Device));
        }

        private void OnDeviceUpdated(GazeDeviceWatcherPreview sender,
                                     GazeDeviceWatcherUpdatedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                    () => UseReadyDevice(args.Device));
        }

        private void OnDeviceRemoved(GazeDeviceWatcherPreview sender,
                                     GazeDeviceWatcherRemovedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!live || stopping) return;
                cursorFilter.Reset();
                targetMeasurement.CancelHold();
                HideTargetDwellVisual();
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Eye tracker disconnected. Please ask for assistance.";
                Trace("Gaze device removed");
            });
        }

        private void UseReadyDevice(GazeDevicePreview device)
        {
            if (!live || stopping || gazeSubscribed) return;
            if (!device.CanTrackEyes ||
                device.ConfigurationState != GazeDeviceConfigurationStatePreview.Ready)
            {
                LiveStatusText.Text = "Tracker detected, but gaze not ready";
                return;
            }

            try
            {
                Trace("BEGIN gaze source subscription");
                gazeSource = GazeInputSourcePreview.GetForCurrentView();
                gazeSource.GazeMoved += OnGazeMoved;
                gazeSubscribed = true;
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

        private void OnGazeMoved(GazeInputSourcePreview sender, GazeMovedPreviewEventArgs e)
        {
            if (!live || stopping) return;
            Point? screenPoint = e.CurrentPoint?.EyeGazePosition;
            if (!screenPoint.HasValue) return;
            var p = screenPoint.Value;
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) ||
                double.IsInfinity(p.X) || double.IsInfinity(p.Y)) return;
            mostRecentRawGazePosition = p;
            lastValidGazeSampleAt = DateTimeOffset.UtcNow;

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
                cursorFilter.Reset();
                RecordInterruptedTargetDwell(DateTimeOffset.UtcNow);
                HideTargetDwellVisual();
                PreviewDot.Visibility = Visibility.Collapsed;
                buttonDwellWheel.Hide();
                ProcessGazeControl(p, p);
                e.Handled = true;
                return;
            }

            Point stabilized = cursorFilter.Update(p, DateTimeOffset.UtcNow);
            UpdateCursor(stabilized);
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
        private void ProcessGazeControl(Point stabilizedPoint, Point rawPoint)
        {
            if (!live || stopping || exitRequested) return;
            var now = DateTimeOffset.UtcNow;
            // Do not allow an event gap to count as uninterrupted dwell.
            if (lastGazeAt != default(DateTimeOffset) &&
                now - lastGazeAt > TimeSpan.FromMilliseconds(500))
            {
                bonusButtonMeasurement.CancelHold();
                gazeControlTarget = null;
                gazeControlTriggered = false;
                buttonDwellWheel.Hide();
            }
            lastGazeAt = now;
            Button target = FindGazeButton(stabilizedPoint, rawPoint);
            if (target == null)
            {
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
                gazeControlTarget = target;
                gazeControlEnteredAt = now;
                bonusButtonMeasurement.CancelHold();
                if (IsBonusAnswer(target)) bonusButtonMeasurement.BeginTarget(now);
                gazeControlTriggered = false;
            }
            if (gazeControlTriggered) return;
            if (IsBonusAnswer(target)) bonusButtonMeasurement.Observe(rawPoint, now, true);
            double progress = (now - gazeControlEnteredAt).TotalMilliseconds /
                              ControlDwell.TotalMilliseconds;
            buttonDwellWheel.Present(target, progress);
            if (progress < 1.0) return;
            gazeControlTriggered = true;
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
            else if (target == SettingsMenuButton)
                OnDisplaySettingsRequested(null, null);
            else if (target == LevelsBackButton)
                OnBackToMenu(null, null);
            else if (target == LessonBackButton || target == BonusBackButton)
                OnBackToLevels(null, null);
            else if (target == BonusTrueButton || target == BonusFalseButton)
                SubmitBonusAnswer(target == BonusTrueButton, true);
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
                case AppScreen.Diagnostics: return diagnosticsButtons;
                default: return menuButtons;
            }
        }

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

        private bool InsideButton(Button button, Point gaze)
        {
            if (button == null || !button.IsEnabled ||
                button.Visibility != Visibility.Visible ||
                button.ActualWidth <= 0 || button.ActualHeight <= 0) return false;

            Point origin = button.TransformToVisual(this).TransformPoint(new Point(0, 0));
            var bounds = new Rect(origin.X, origin.Y, button.ActualWidth,
                                  button.ActualHeight);
            return AdaptiveHitTesting.ContainsRectangle(
                gaze, bounds, gazeProfile.HitBoxScale);
        }

        private void ShowScreen(AppScreen screen)
        {
            // Navigating away from the celebration must not fire a stale transition.
            if (screen != AppScreen.Lesson) CancelBonusTransition();
            if (screen != AppScreen.Bonus) CancelBonusQuestionTimer();
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
            HomePanel.Visibility = screen == AppScreen.Menu ? Visibility.Visible : Visibility.Collapsed;
            LevelsPanel.Visibility = screen == AppScreen.Levels ? Visibility.Visible : Visibility.Collapsed;
            LessonPanel.Visibility = screen == AppScreen.Lesson ? Visibility.Visible : Visibility.Collapsed;
            BonusPanel.Visibility = screen == AppScreen.Bonus ? Visibility.Visible : Visibility.Collapsed;
            DisplaySettingsPanel.Visibility = screen == AppScreen.Settings ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = screen == AppScreen.Diagnostics ? Visibility.Visible : Visibility.Collapsed;
            if (screen == AppScreen.Diagnostics) RefreshAccuracyGrid();
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

        // A small, fixed 7x7 grid is rendered into a vector tablet silhouette.
        // Cells are display-only; no new gaze interactions are required.
        private void BuildAccuracyGrid()
        {
            for (int i = 0; i < SpatialAccuracyMap.Rows; i++)
                AccuracyHeatmapGrid.RowDefinitions.Add(new RowDefinition
                { Height = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < SpatialAccuracyMap.Columns; i++)
                AccuracyHeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });

            for (int row = 0; row < SpatialAccuracyMap.Rows; row++)
            {
                for (int col = 0; col < SpatialAccuracyMap.Columns; col++)
                {
                    var cell = new Border
                    {
                        Margin = new Thickness(2),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 52, 82, 117)),
                        BorderThickness = new Thickness(1),
                        Background = new SolidColorBrush(Color.FromArgb(255, 17, 35, 58)),
                        CornerRadius = new CornerRadius(2)
                    };
                    var label = new TextBlock
                    {
                        FontSize = 19,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Colors.White),
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    cell.Child = label;
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    AccuracyHeatmapGrid.Children.Add(cell);
                    accuracyCells[row, col] = label;
                }
            }
            RefreshAccuracyGrid();
        }

        private void RefreshAccuracyGrid()
        {
            for (int row = 0; row < SpatialAccuracyMap.Rows; row++)
                for (int col = 0; col < SpatialAccuracyMap.Columns; col++)
                {
                    double? score = spatialAccuracy.Score(row, col);
                    accuracyCells[row, col].Text = score.HasValue
                        ? Math.Round(score.Value).ToString() + "%"
                        : "NA";
                }
        }

        private void OnOpenLevels(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested) return;
            UpdateLevelBadges();
            LevelsNoticeText.Text = "";
            ShowScreen(AppScreen.Levels);
        }

        private void OnTdControlTrainer(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Menu) return;
            HomeNoticeText.Text = "TD Control Trainer is coming soon.";
        }

        private void OnOpenDiagnostics(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Menu) return;
            HomeNoticeText.Text = "";
            ShowScreen(AppScreen.Diagnostics);
        }

        private void OnBackToMenu(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested) return;
            HomeNoticeText.Text = "";
            ShowScreen(AppScreen.Menu);
        }

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

        private void OnLevelOne(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Levels) return;
            // A completed level stays selectable. Replay resets only the
            // current ten-target session, never the persisted completion flag.
            ShowScreen(AppScreen.Lesson);
            lessonIntroActive = true;
            ResetLesson();
            BeginLessonIntro();
        }

        private void BeginLessonIntro()
        {
            LessonIntroOverlay.Visibility = Visibility.Visible;
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
                if (targetIndex >= 0)
                    targetMeasurement.BeginTarget(DateTimeOffset.UtcNow);
                RepositionTarget();
            });
        }

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

        private void OnPlaceholderLevel(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Levels) return;
            var button = sender as Button;
            LevelsNoticeText.Text = (button != null ? button.Content.ToString() : "This level") +
                " is coming soon.";
        }

        private void UpdateLevelBadges()
        {
            for (int i = 0; i < allLevelButtons.Length; i++)
            {
                bool done = trainingProgress.IsCompleted(i + 1);
                allLevelButtons[i].Content = "LEVEL " + (i + 1) +
                    (done ? "  COMPLETED" : i == 0 ? "" : "  · SOON");
            }
        }

        private void OnCanvasSized(object sender, SizeChangedEventArgs e)
        {
            ApplyDisplaySizes();
            RepositionTarget();
        }

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

        private void ApplyDisplaySizes()
        {
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

        private void ResetReferencePreview()
        {
            pendingReferenceWidth = displayScale.ReferenceBarViewWidth;
            UpdateReferencePreview();
        }

        private void UpdateReferencePreview()
        {
            double available = RootLayout.ActualWidth - 48;
            double limit = Math.Max(80, available);
            pendingReferenceWidth = Math.Max(30,
                Math.Min(limit, pendingReferenceWidth));
            CalibrationReferenceBar.Width = pendingReferenceWidth;
            CalibrationStatusText.Text =
                "Match the line to the middle camera glass, then SAVE.";
        }

        private void OnDisplaySettingsRequested(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Menu) return;
            ShowScreen(AppScreen.Settings);
            ResetReferencePreview();
        }

        private void OnReferenceShorter(object sender, RoutedEventArgs e)
        {
            if (!displaySettingsVisible) return;
            pendingReferenceWidth -= 6;
            UpdateReferencePreview();
        }

        private void OnReferenceLonger(object sender, RoutedEventArgs e)
        {
            if (!displaySettingsVisible) return;
            pendingReferenceWidth += 6;
            UpdateReferencePreview();
        }

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

        private void OnCancelDisplay(object sender, RoutedEventArgs e)
        {
            if (displaySettingsVisible) CloseDisplaySettings();
        }

        private void CloseDisplaySettings()
        {
            ShowScreen(AppScreen.Menu);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Mouse preview remains available only when hardware is not engaged.
            if (live || stopping || currentScreen != AppScreen.Lesson) return;
            Point canvasPoint = e.GetCurrentPoint(TrainingCanvas).Position;
            Point canvasOrigin = TrainingCanvas.TransformToVisual(this)
                                  .TransformPoint(new Point(0, 0));
            UpdateCursor(new Point(canvasPoint.X + canvasOrigin.X,
                                   canvasPoint.Y + canvasOrigin.Y));
            ProcessPosition(canvasPoint, canvasPoint);
        }

        // This overlay belongs to the page, not the training canvas. It cannot
        // intercept Exit Button clicks and is independent of TD Control's trace.
        private void UpdateCursor(Point viewPoint)
        {
            if (CursorOverlay.ActualWidth <= DotDiameter ||
                CursorOverlay.ActualHeight <= DotDiameter) return;
            Point origin = CursorOverlay.TransformToVisual(this)
                                        .TransformPoint(new Point(0, 0));
            double x = viewPoint.X - origin.X;
            double y = viewPoint.Y - origin.Y;
            if (x < 0 || y < 0 ||
                x > CursorOverlay.ActualWidth || y > CursorOverlay.ActualHeight)
            {
                PreviewDot.Visibility = Visibility.Collapsed;
                return;
            }

            PreviewDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(PreviewDot, Clamp(x - DotDiameter / 2, 0,
                CursorOverlay.ActualWidth - DotDiameter));
            Canvas.SetTop(PreviewDot, Clamp(y - DotDiameter / 2, 0,
                CursorOverlay.ActualHeight - DotDiameter));
        }

        private void ProcessPosition(Point p, Point rawPoint)
        {
            if (completed || exitRequested || stopping ||
                currentScreen != AppScreen.Lesson || lessonIntroActive) return;

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
            if (targetIndex < 0)
            {
                targetIndex = 0;
                PickRandomTarget();
                targetMeasurement.BeginTarget(now);
                return;
            }

            Point center = TargetCenter();
            bool inRing = AdaptiveHitTesting.ContainsCircle(
                p, center, TargetDiameter, gazeProfile.HitBoxScale);
            if (!inRing)
            {
                RecordInterruptedTargetDwell(now);
                UpdateTargetDwellVisual(false);
                return;
            }

            // A full 1000ms uninterrupted hold is required for acquisition.
            bool acquired = targetMeasurement.Observe(rawPoint, now, true);
            UpdateTargetDwellVisual(inRing);
            if (!acquired) return;
            TargetRing.StrokeThickness = 8;
            UpdateTargetDwellVisual(false);
            if (live)
            {
                TargetPerformance result = targetMeasurement.Measure(
                    center, TargetDiameter * 0.5, now);
                gazeProfile.RecordTargetPerformance(result, 1);
                Point canvasOriginOnPage = TrainingCanvas.TransformToVisual(RootLayout)
                    .TransformPoint(new Point(0, 0));
                spatialAccuracy.Record(
                    new Point(center.X + canvasOriginOnPage.X,
                              center.Y + canvasOriginOnPage.Y),
                    RootLayout.ActualWidth, RootLayout.ActualHeight, result.Overall);
                cursorFilter.SmoothingLevel = gazeProfile.SmoothingLevel;
                UpdateAccuracyDisplay();
            }

            targetIndex++;
            if (targetIndex >= TargetsPerLevel)
            {
                completed = true;
                trainingProgress.MarkCompleted(1);
                Trace("Level 1 completed: ten targets; source=" +
                      (live ? "live gaze" : "mouse"));
                UpdateLevelBadges();
                BeginBonusTransition();
                return;
            }
            PickRandomTarget();
            targetMeasurement.BeginTarget(now);
        }

        // Celebrate on the lesson's existing black background, then enter the
        // dedicated bonus screen without any gaze activation or menu selection.
        private void BeginBonusTransition()
        {
            CancelBonusTransition();
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            HideTargetDwellVisual();
            buttonDwellWheel.Hide();
            CompletionOverlay.Visibility = Visibility.Visible;
            LessonBackButton.Visibility = Visibility.Collapsed;
            bonusTransitionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            bonusTransitionTimer.Tick += OnBonusTransitionTick;
            bonusTransitionTimer.Start();
        }

        private void OnBonusTransitionTick(object sender, object e)
        {
            CancelBonusTransition();
            if (!completed || stopping || exitRequested ||
                currentScreen != AppScreen.Lesson) return;
            ShowScreen(AppScreen.Bonus);
            BeginTriviaIntro();
        }

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

        private bool IsBonusAnswer(Button button)
        {
            return currentScreen == AppScreen.Bonus && triviaAwaitingAnswer &&
                   (button == BonusTrueButton || button == BonusFalseButton);
        }

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

        private void OnBonusTrue(object sender, RoutedEventArgs e)
        {
            SubmitBonusAnswer(true, false);
        }

        private void OnBonusFalse(object sender, RoutedEventArgs e)
        {
            SubmitBonusAnswer(false, false);
        }

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
            if (right && gazeActivated && live)
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
                    gazeProfile.RecordTargetPerformance(performance, 1);
                    spatialAccuracy.Record(center, RootLayout.ActualWidth,
                                           RootLayout.ActualHeight, performance.Overall);
                    cursorFilter.SmoothingLevel = gazeProfile.SmoothingLevel;
                    UpdateAccuracyDisplay();
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
        private void RecordInterruptedTargetDwell(DateTimeOffset now)
        {
            if (!live || completed || currentScreen != AppScreen.Lesson ||
                targetIndex < 0)
            {
                targetMeasurement.CancelHold();
                return;
            }
            if (targetMeasurement.CancelIncompleteHold(now))
            {
                gazeProfile.RecordIncompleteTargetDwell(1);
                Point missedTarget = TargetCenter();
                Point offset = TrainingCanvas.TransformToVisual(RootLayout)
                    .TransformPoint(new Point(0, 0));
                spatialAccuracy.Record(
                    new Point(missedTarget.X + offset.X, missedTarget.Y + offset.Y),
                    RootLayout.ActualWidth, RootLayout.ActualHeight, 0);
                cursorFilter.SmoothingLevel = gazeProfile.SmoothingLevel;
                UpdateAccuracyDisplay();
            }
        }

        private void ResetLesson()
        {
            CancelBonusTransition();
            targetIndex = -1;
            completed = false;
            hasPreviousTargetPosition = false;
            targetMeasurement.CancelHold();
            TargetRing.StrokeThickness = 8;
            HideTargetDwellVisual();
            TargetRing.Visibility = Visibility.Collapsed;
            TargetBullseye.Visibility = Visibility.Collapsed;
            cursorFilter.Reset();
            cursorFilter.SmoothingLevel = gazeProfile.SmoothingLevel;
            if (TrainingCanvas.ActualWidth >= TargetDiameter + 24 &&
                TrainingCanvas.ActualHeight >= TargetDiameter + 24)
            {
                targetIndex = 0;
                PickRandomTarget();
                targetMeasurement.BeginTarget(DateTimeOffset.UtcNow);
            }
        }

        private void UpdateAccuracyDisplay()
        {
            // Only the persisted composite score is shown to the learner.
            // Component metrics still drive the adaptive smoothing/hitbox.
            AccuracyText.Text = gazeProfile.Acquisitions == 0
                ? "ACCURACY —"
                : "ACCURACY " + Math.Round(gazeProfile.AccuracyScore) + "%";
        }

        // Keep the whole 55 mm target and radial wheel well inside the
        // screen edges. Scale the extra inset with screen size, allowing
        // smaller tablet screens without forcing an impossible layout.
        private double TargetCenterMargin()
        {
            double smaller = Math.Min(TrainingCanvas.ActualWidth,
                                      TrainingCanvas.ActualHeight);
            double clearSpace = Math.Max(36, Math.Min(72, smaller * 0.10));
            return Math.Min(smaller / 2 - 2,
                TargetDiameter * 0.5 + clearSpace);
        }

        private Point TargetCenter()
        {
            double margin = TargetCenterMargin();
            return new Point(
                margin + Math.Max(0, TrainingCanvas.ActualWidth - margin * 2) * targetPosition.X,
                margin + Math.Max(0, TrainingCanvas.ActualHeight - margin * 2) * targetPosition.Y);
        }

        private void PickRandomTarget()
        {
            if (TrainingCanvas.ActualWidth < TargetDiameter + 24 ||
                TrainingCanvas.ActualHeight < TargetDiameter + 24) return;

            double margin = TargetCenterMargin();
            Point chosen = new Point(0.5, 0.5);
            Point candidate = chosen;
            double bestDistance = double.MinValue;

            // Keep clear of the gaze-accessible BACK control, and favor
            // visibly different placements for consecutive acquisitions.
            Point backOrigin = LessonBackButton.TransformToVisual(TrainingCanvas)
                                                .TransformPoint(new Point(0, 0));
            double backRight = backOrigin.X + LessonBackButton.ActualWidth + 20;
            double backBottom = backOrigin.Y + LessonBackButton.ActualHeight + 20;

            for (int attempt = 0; attempt < 90; attempt++)
            {
                candidate = new Point(placementRandom.NextDouble(),
                                      placementRandom.NextDouble());
                Point center = new Point(
                    margin + Math.Max(0, TrainingCanvas.ActualWidth - margin * 2) * candidate.X,
                    margin + Math.Max(0, TrainingCanvas.ActualHeight - margin * 2) * candidate.Y);
                double visualRadius = TargetDiameter * 0.5 + 46;
                bool overlapsBack = center.X - visualRadius < backRight &&
                                    center.Y - visualRadius < backBottom;
                double distance = hasPreviousTargetPosition
                    ? Math.Sqrt(Math.Pow(candidate.X - previousTargetPosition.X, 2) +
                                Math.Pow(candidate.Y - previousTargetPosition.Y, 2))
                    : 1;
                if (!overlapsBack && distance > bestDistance)
                {
                    bestDistance = distance;
                    chosen = candidate;
                }
                if (!overlapsBack && distance >= 0.28) break;
            }

            targetPosition = chosen;
            previousTargetPosition = chosen;
            hasPreviousTargetPosition = true;
            RepositionTarget();
        }

        private void RepositionTarget()
        {
            if (currentScreen != AppScreen.Lesson || targetIndex < 0 ||
                lessonIntroActive || completed || TrainingCanvas.ActualWidth < TargetDiameter + 24 ||
                TrainingCanvas.ActualHeight < TargetDiameter + 24)
            {
                TargetRing.Visibility = Visibility.Collapsed;
                TargetBullseye.Visibility = Visibility.Collapsed;
                HideTargetDwellVisual();
                return;
            }

            Point center = TargetCenter();
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

        private void HideTargetDwellVisual()
        {
            targetDwellWheel.Hide();
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private void OnExitRequested(object sender, RoutedEventArgs e)
        {
            if (exitRequested || stopping) return;
            exitRequested = true;
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
            Trace("EXIT requested; source active=" + live);
            if (live) BeginCleanup();
            else NavigateToCleanExit();
        }

        private void BeginCleanup()
        {
            if (!live || stopping) return;
            stopping = true;
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

        private void OnCleanupTick(object sender, object e)
        {
            CleanupNextStage();
        }

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

        private void NavigateToCleanExit()
        {
            // Based on the successful observed Back -> X run, not a safety guarantee.
            Trace("BEGIN navigation away from gaze page");
            try
            {
                bool navigated = Frame.Navigate(typeof(MainPage), "EXIT_AFTER_GAZE_CLEANUP");
                if (!navigated)
                {
                    Trace("ERROR navigation returned false; exit cancelled");
                    LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Unable to navigate; app remains open.";
                    exitRequested = false;
                    ExitButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Trace("ERROR navigation: " + ex);
                LiveStatusText.Visibility = Visibility.Visible;
                LiveStatusText.Text = "Navigation failed; app remains open.";
                exitRequested = false;
                ExitButton.IsEnabled = true;
            }
        }

        private void Trace(string message)
        {
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
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
        }
    }
}
