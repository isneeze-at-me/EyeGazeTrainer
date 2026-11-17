using System;
using System.IO;
using Windows.Devices.Input.Preview;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace EyeGazeApiViability
{
    // Level 1: mouse-driven developer preview OR deliberately armed real gaze.
    // Warning: real gaze acquisition/release has caused TD Control loss of input.
    // This is NOT approved for independent or unattended use (issue #9).
    public sealed partial class LevelOnePreview : Page
    {
        private const double TargetDiameter = 116;
        private const double DotDiameter = 44;
        private static readonly Point[] TargetFractions =
        {
            new Point(0.25, 0.50), new Point(0.75, 0.50),
            new Point(0.50, 0.28), new Point(0.50, 0.72)
        };

        private GazeDeviceWatcherPreview watcher;
        private GazeInputSourcePreview gazeSource;
        private bool live;
        private bool stopping;
        private bool watcherStarted;
        private bool gazeSubscribed;
        private int cleanupStage;
        private DispatcherTimer cleanupTimer;
        private string tracePath;
        private int targetIndex = -1;
        private bool insideTarget;
        private bool completed;
        private bool exitRequested;
        private bool autoStartAttempted;
        private Button gazeControlTarget;
        private DateTimeOffset gazeControlEnteredAt;
        private DateTimeOffset lastGazeAt;
        private bool gazeControlTriggered;
        private static readonly TimeSpan ControlDwell = TimeSpan.FromMilliseconds(1500);

        public LevelOnePreview()
        {
            InitializeComponent();
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
                LiveStatusText.Text = "Eye tracking ready. Look at the yellow circles.";
                ResetLesson();
            }
            catch (Exception ex)
            {
                Trace("ERROR gaze source subscription: " + ex);
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

            // UWP gaze points are in application-view coordinates, not
            // coordinates local to the canvas.
            // Show the indicator across the entire page, including Exit.
            // Gaze position remains in UWP view coordinates for button hit tests.
            UpdateCursor(p);
            Point canvasOrigin = TrainingCanvas.TransformToVisual(this)
                                  .TransformPoint(new Point(0, 0));
            ProcessPosition(new Point(p.X - canvasOrigin.X, p.Y - canvasOrigin.Y));
            ProcessGazeControl(p);
            e.Handled = true;
        }


        // Direct gaze-position hit test: no Windows mouse or TD Control click.
        private void ProcessGazeControl(Point viewPoint)
        {
            if (!live || stopping || exitRequested) return;
            var now = DateTimeOffset.UtcNow;
            // Do not allow an event gap to count as uninterrupted dwell.
            if (lastGazeAt != default(DateTimeOffset) &&
                now - lastGazeAt > TimeSpan.FromMilliseconds(500))
            {
                gazeControlTarget = null;
                gazeControlTriggered = false;
            }
            lastGazeAt = now;
            Button target = InsideButton(ExitButton, viewPoint) ? ExitButton : null;
            if (target == null)
            {
                gazeControlTarget = null;
                gazeControlTriggered = false;
                return;
            }
            if (gazeControlTarget != target)
            {
                gazeControlTarget = target;
                gazeControlEnteredAt = now;
                gazeControlTriggered = false;
            }
            if (gazeControlTriggered) return;
            double progress = (now - gazeControlEnteredAt).TotalMilliseconds /
                              ControlDwell.TotalMilliseconds;
            LiveStatusText.Text = "Exiting — " +
                                  Math.Min(100, (int)(progress * 100)) + "%";
            if (progress < 1.0) return;
            gazeControlTriggered = true;
            gazeControlTarget = null;
            Trace("Native gaze control activated: EXIT");
            OnExitRequested(null, null);
        }

        private bool InsideButton(Button button, Point gaze)
        {
            if (button == null || !button.IsEnabled ||
                button.Visibility != Visibility.Visible ||
                button.ActualWidth <= 0 || button.ActualHeight <= 0) return false;
            Point origin = button.TransformToVisual(this).TransformPoint(new Point(0, 0));
            return gaze.X >= origin.X && gaze.X <= origin.X + button.ActualWidth &&
                   gaze.Y >= origin.Y && gaze.Y <= origin.Y + button.ActualHeight;
        }

        private void OnCanvasSized(object sender, SizeChangedEventArgs e)
        {
            RepositionTarget();
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Mouse preview remains available only when hardware is not engaged.
            if (live || stopping) return;
            Point canvasPoint = e.GetCurrentPoint(TrainingCanvas).Position;
            Point canvasOrigin = TrainingCanvas.TransformToVisual(this)
                                  .TransformPoint(new Point(0, 0));
            UpdateCursor(new Point(canvasPoint.X + canvasOrigin.X,
                                   canvasPoint.Y + canvasOrigin.Y));
            ProcessPosition(canvasPoint);
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

        private void ProcessPosition(Point p)
        {
            if (TrainingCanvas.ActualWidth <= DotDiameter ||
                TrainingCanvas.ActualHeight <= DotDiameter ||
                p.X < 0 || p.Y < 0 ||
                p.X > TrainingCanvas.ActualWidth ||
                p.Y > TrainingCanvas.ActualHeight) return;

            if (completed) return;
            if (targetIndex < 0)
            {
                targetIndex = 0;
                InstructionText.Text = live
                    ? "Look at the yellow ring. No click or dwell is needed."
                    : "Move the green circle into the yellow ring. No click is needed.";
                ProgressText.Text = "Target 1 of 4";
                RepositionTarget();
                return;
            }

            Point target = TargetCenter();
            double dx = p.X - target.X;
            double dy = p.Y - target.Y;
            bool inRing = dx * dx + dy * dy <= 58 * 58;
            if (inRing && !insideTarget)
            {
                insideTarget = true;
                targetIndex++;
                if (targetIndex >= TargetFractions.Length)
                {
                    completed = true;
                    TargetRing.Visibility = Visibility.Collapsed;
                    InstructionText.Text = "All four targets activated without clicking.";
                    ProgressText.Text = live
                        ? "Great job! You found all four circles."
                        : "Great job! You found all four circles.";
                    Trace("Lesson completed; source=" + (live ? "live gaze" : "mouse"));
                    return;
                }

                ProgressText.Text = "Target " + (targetIndex + 1) +
                                    " of " + TargetFractions.Length;
                RepositionTarget();
                insideTarget = false;
            }
            else if (!inRing) insideTarget = false;
        }

        private void ResetLesson()
        {
            targetIndex = -1;
            insideTarget = false;
            completed = false;
            TargetRing.Visibility = Visibility.Collapsed;
            PreviewDot.Visibility = Visibility.Collapsed;
            ProgressText.Text = "Look at the blue area to begin";
            InstructionText.Text = "Look at each yellow ring. No dwell or click is needed.";
        }

        private Point TargetCenter()
        {
            Point fraction = TargetFractions[Math.Max(0,
                Math.Min(targetIndex, TargetFractions.Length - 1))];
            double margin = TargetDiameter / 2;
            return new Point(
                margin + Math.Max(0, TrainingCanvas.ActualWidth - TargetDiameter) * fraction.X,
                margin + Math.Max(0, TrainingCanvas.ActualHeight - TargetDiameter) * fraction.Y);
        }

        private void RepositionTarget()
        {
            if (targetIndex < 0 || completed ||
                TrainingCanvas.ActualWidth < TargetDiameter ||
                TrainingCanvas.ActualHeight < TargetDiameter) return;
            Point p = TargetCenter();
            Canvas.SetLeft(TargetRing, p.X - TargetDiameter / 2);
            Canvas.SetTop(TargetRing, p.Y - TargetDiameter / 2);
            TargetRing.Visibility = Visibility.Visible;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private void OnExitRequested(object sender, RoutedEventArgs e)
        {
            if (exitRequested || stopping) return;
            exitRequested = true;
            ExitButton.IsEnabled = false;
            Trace("EXIT requested; source active=" + live);
            if (live) BeginCleanup();
            else NavigateToCleanExit();
        }

        private void BeginCleanup()
        {
            if (!live || stopping) return;
            stopping = true;
            ExitButton.IsEnabled = false;
            LiveStatusText.Text = "Closing EyeGazeTrainer…";
            cleanupStage = 0;
            cleanupTimer = new DispatcherTimer();
            cleanupTimer.Interval = TimeSpan.FromSeconds(3);
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
            LiveStatusText.Text = "Closing — step " + (phase + 1) + "/4: " + phaseName;
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
                LiveStatusText.Text = "HALTED at " + phaseName +
                    ". Helper assistance needed; do not close.";
                cleanupTimer.Stop();
                // Do not navigate or exit after a failed cleanup.
                exitRequested = false;
                ExitButton.IsEnabled = true;
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
                    LiveStatusText.Text = "Unable to navigate; app remains open.";
                    exitRequested = false;
                    ExitButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Trace("ERROR navigation: " + ex);
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
            Trace("Page OnNavigatedFrom; gaze active=" + live +
                  " cleanup stage=" + cleanupStage + " exit requested=" + exitRequested);
            base.OnNavigatedFrom(e);
        }
    }
}
