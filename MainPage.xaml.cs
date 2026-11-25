// ============================================================================
// EyeGazeTrainer
// File: MainPage.xaml.cs
// Purpose: Provides the gaze-free closing view and Windows-managed native shutdown path.
// ============================================================================

using System;
using System.IO;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace EyeGazeApiViability
{
    // Gaze-free intermediary. Retains the proven navigation-before-exit
    // ordering; this is not a user-facing developer diagnostics page.
    public sealed partial class MainPage : Page
    {
        private bool exitScheduled;
        private bool nativeCloseExperiment;
        private DispatcherTimer exitTimer;

        // Initialize the closing view and its lifecycle events.
        public MainPage()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        // Interpret the requested closing route, release navigation history and schedule the appropriate exit.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string route = e.Parameter as string;
            bool afterGazeCleanup = route == "EXIT_AFTER_GAZE_CLEANUP";
            bool hardwareFreeExitTest = route == "EXIT_NO_GAZE_DIAGNOSTIC";
            LogLifecycle("Gaze-free closing page entered; requestedExit=" +
                         afterGazeCleanup + "; hardwareFreeExitTest=" +
                         hardwareFreeExitTest);

            if (route == "ISOLATION_A")
            {
                Content = new TextBlock { Text = "Shutdown isolation A — no gaze or companion\nAutomatic close in 30 seconds", FontSize = 32, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                nativeCloseExperiment = true;
                ScheduleExit("ISOLATION A", 30);
                return;
            }
            if (afterGazeCleanup || hardwareFreeExitTest)
            {
                // Do not keep the previous gaze-enabled page in the back stack.
                Frame.BackStack.Clear();
                Frame.ForwardStack.Clear();
                // The gaze-enabled page has already completed cleanup and
                // companion restoration. Leave immersive mode separately from
                // window destruction, retaining the existing two-second timer.
                nativeCloseExperiment = afterGazeCleanup;
                if (afterGazeCleanup) LeaveFullScreenForExit();
                ScheduleExit(hardwareFreeExitTest
                    ? "HARDWARE FREE: no gaze APIs opened"
                    : "after gaze-page navigation");
            }
            else
            {
                // MainPage is no longer a destination. If reached directly,
                // leave the app on its gaze-free closing page instead of
                // accidentally starting a new gaze session.
                LogLifecycle("Gaze-free closing page reached without exit route");
            }
        }

        // Leave full-screen mode before requesting native view consolidation.
        private void LeaveFullScreenForExit()
        {
            try
            {
                var view = ApplicationView.GetForCurrentView();
                LogLifecycle("BEGIN full-screen exit; fullScreen=" + view.IsFullScreenMode);
                if (view.IsFullScreenMode) view.ExitFullScreenMode();
                LogLifecycle("END full-screen exit request; fullScreen=" + view.IsFullScreenMode);
            }
            catch (Exception ex)
            {
                LogLifecycle("ERROR full-screen exit; continuing normal shutdown: " + ex);
            }
        }

        private void ScheduleExit(string reason, int seconds = 2)
        {
            if (exitScheduled) return;
            exitScheduled = true;
            LogLifecycle("EXIT scheduled: " + reason);
            exitTimer = new DispatcherTimer();
            exitTimer.Interval = TimeSpan.FromSeconds(seconds);
            exitTimer.Tick += OnExitTimer;
            exitTimer.Start();
        }

        // Attempt Windows-managed native closure and use explicit application exit only on the applicable fallback path.
        private async void OnExitTimer(object sender, object e)
        {
            exitTimer.Stop();
            exitTimer.Tick -= OnExitTimer;
            exitTimer = null;
            if ((Application.Current as App)?.IsolationMode == "A") LeaveFullScreenForExit();
            try
            {
                LogLifecycle("Before termination; fullScreen=" +
                    ApplicationView.GetForCurrentView().IsFullScreenMode);
            }
            catch (Exception ex) { LogLifecycle("Full-screen status unavailable: " + ex.Message); }
            if (nativeCloseExperiment)
            {
                // Supported UWP equivalent of the user closing the view.
                // Only fall back to process termination if native close fails.
                try
                {
                    LogLifecycle("BEGIN native view close (TryConsolidateAsync)");
                    var operation = ApplicationView.GetForCurrentView().TryConsolidateAsync();
                    var closeTask = System.WindowsRuntimeSystemExtensions.AsTask(operation);
                    var finished = await System.Threading.Tasks.Task.WhenAny(closeTask,
                        System.Threading.Tasks.Task.Delay(1500));
                    if (finished == closeTask)
                    {
                        bool accepted = await closeTask;
                        LogLifecycle("END native view close; accepted=" + accepted);
                        if (accepted)
                        {
                            LogLifecycle("NATIVE CLOSE ACCEPTED; leaving suspension and termination to Windows");
                            return;
                        }
                        LogLifecycle("Native view close rejected; continuing termination");
                    }
                    else
                        LogLifecycle("Native view close timed out; continuing termination");
                }
                catch (Exception ex)
                {
                    LogLifecycle("Native view close failed; continuing termination: " + ex);
                }
            }
            LogLifecycle("CALL Application.Current.Exit() from gaze-free closing page");
            Application.Current.Exit();
        }

        // Stop the pending closing timer if the page is unloaded.
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (exitTimer != null)
            {
                exitTimer.Stop();
                exitTimer.Tick -= OnExitTimer;
                exitTimer = null;
            }
        }

        // Forward closing-page trace messages to the shared lifecycle log.
        private void LogLifecycle(string message)
        {
            App.TraceLifecycle("CLOSING | " + message);
        }
    }
}
