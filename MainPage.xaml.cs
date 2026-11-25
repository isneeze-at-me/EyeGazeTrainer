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
        private bool foregroundHandoff;
        private DispatcherTimer exitTimer;

        public MainPage()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string route = e.Parameter as string;
            bool afterGazeCleanup = route == "EXIT_AFTER_GAZE_CLEANUP";
            bool hardwareFreeExitTest = route == "EXIT_NO_GAZE_DIAGNOSTIC";
            LogLifecycle("Gaze-free closing page entered; requestedExit=" +
                         afterGazeCleanup + "; hardwareFreeExitTest=" +
                         hardwareFreeExitTest);

            if (afterGazeCleanup || hardwareFreeExitTest)
            {
                // Do not keep the previous gaze-enabled page in the back stack.
                Frame.BackStack.Clear();
                Frame.ForwardStack.Clear();
                // The gaze-enabled page has already completed cleanup and
                // companion restoration. Leave immersive mode separately from
                // window destruction, retaining the existing two-second timer.
                foregroundHandoff = afterGazeCleanup;
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

        private void ScheduleExit(string reason)
        {
            if (exitScheduled) return;
            exitScheduled = true;
            LogLifecycle("EXIT scheduled: " + reason);
            exitTimer = new DispatcherTimer();
            exitTimer.Interval = TimeSpan.FromSeconds(2);
            exitTimer.Tick += OnExitTimer;
            exitTimer.Start();
        }

        private async void OnExitTimer(object sender, object e)
        {
            exitTimer.Stop();
            exitTimer.Tick -= OnExitTimer;
            exitTimer = null;
            try
            {
                LogLifecycle("Before termination; fullScreen=" +
                    ApplicationView.GetForCurrentView().IsFullScreenMode);
            }
            catch (Exception ex) { LogLifecycle("Full-screen status unavailable: " + ex.Message); }
            if (foregroundHandoff)
            {
                var app = Application.Current as App;
                if (app != null) await app.RequestForegroundHandoffAsync();
            }
            LogLifecycle("CALL Application.Current.Exit() from gaze-free closing page");
            Application.Current.Exit();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (exitTimer != null)
            {
                exitTimer.Stop();
                exitTimer.Tick -= OnExitTimer;
                exitTimer = null;
            }
        }

        private void LogLifecycle(string message)
        {
            App.TraceLifecycle("CLOSING | " + message);
        }
    }
}
