using System;
using System.IO;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.ViewManagement;

namespace EyeGazeApiViability
{
    sealed partial class App : Application
    {
        private bool windowEventsRegistered;
        private bool overlayHelperLaunchAttempted;
        private string overlaySessionId;
        private const string OverlaySessionFile = "eyegazetrainer-overlay-session.id";

        public App()
        {
            InitializeComponent();
            TraceLifecycle("App constructor");
            Suspending += (s, e) => TraceLifecycle("Application.Suspending");
            Resuming += (s, e) => TraceLifecycle("Application.Resuming");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            TraceLifecycle("OnLaunched START, previousState=" + e.PreviousExecutionState);
            // Replace any previous session token BEFORE making our window
            // visible, so older helpers disengage before the new launch.
            BeginOverlaySession();
            try
            {
                // Prefer a full-screen window at the moment UWP creates it,
                // avoiding a brief small-window launch where supported.
                ApplicationView.PreferredLaunchWindowingMode =
                    ApplicationViewWindowingMode.FullScreen;
            }
            catch (Exception ex)
            {
                TraceLifecycle("Fullscreen launch preference unavailable: " +
                               ex.GetType().Name);
            }
            var frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                frame = new Frame();
                Window.Current.Content = frame;
                TraceLifecycle("Created root Frame");
            }

            if (!windowEventsRegistered)
            {
                Window.Current.VisibilityChanged += OnVisibilityChanged;
                Window.Current.Closed += OnWindowClosed;
                windowEventsRegistered = true;
            }

            // Windows reads this preference during activation. Calling
            // TryEnterFullScreenMode after Activate was too late and caused
            // the app to appear briefly as a small window first.
            try
            {
                ApplicationView.PreferredLaunchWindowingMode =
                    ApplicationViewWindowingMode.FullScreen;
                TraceLifecycle("Preferred startup windowing mode: FullScreen");
            }
            catch (Exception ex)
            {
                TraceLifecycle("Fullscreen preference unavailable: " +
                               ex.GetType().Name);
            }

            if (frame.Content == null)
            {
                TraceLifecycle("Navigate to gaze screen");
                frame.Navigate(typeof(LevelOnePreview));
            }

            // Request immersive mode before making the window visible.
            // The launch preference above also covers subsequent cold starts.
            try
            {
                bool fullScreen = ApplicationView.GetForCurrentView()
                    .TryEnterFullScreenMode();
                TraceLifecycle("Fullscreen pre-activation accepted=" + fullScreen);
            }
            catch (Exception ex)
            {
                TraceLifecycle("Fullscreen request unavailable: " +
                               ex.GetType().Name);
            }
            Window.Current.Activate();
            TraceLifecycle("Window.Activate completed");
            // Do NOT launch the gaze-aware companion here. The splash now
            // delays tracker startup, so marking this window at activation
            // can make TD Control release its overlay/device too early.
            // LevelOnePreview starts it after a real gaze sample arrives.
        }

        // The helper operates on this app's own foreground window, not the
        // gaze APIs or TD Control. Never delay or fail gaze startup if absent.
        private void BeginOverlaySession()
        {
            if (!string.IsNullOrEmpty(overlaySessionId)) return;
            try
            {
                overlaySessionId = Guid.NewGuid().ToString("N");
                File.WriteAllText(
                    Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                 OverlaySessionFile), overlaySessionId);
                TraceLifecycle("Overlay session token created before activation");
            }
            catch (Exception ex)
            {
                overlaySessionId = null;
                TraceLifecycle("Overlay session setup failed: " +
                               ex.GetType().Name);
            }
        }

        internal static void StartOverlayCompanionAfterValidGaze()
        {
            var app = Application.Current as App;
            if (app != null) app.TryStartOverlayCompanion();
        }

        private async void TryStartOverlayCompanion()
        {
            if (overlayHelperLaunchAttempted) return;
            overlayHelperLaunchAttempted = true;
            if (string.IsNullOrEmpty(overlaySessionId))
            {
                TraceLifecycle("Overlay helper skipped: session setup failed");
                return;
            }
            try
            {
                TraceLifecycle("Overlay companion launch requested");
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                TraceLifecycle("Overlay companion launch accepted");
            }
            catch (Exception ex)
            {
                EndOverlaySession();
                // Unavailable full-trust support means TD Control's circle stays
                // visible. All UWP gaze and controlled-exit behavior is intact.
                TraceLifecycle("Overlay companion unavailable: " + ex.GetType().Name +
                               " HResult=" + ex.HResult.ToString("X8"));
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind != ActivationKind.Protocol)
            {
                base.OnActivated(args);
                return;
            }

            var protocol = args as ProtocolActivatedEventArgs;
            if (protocol == null ||
                protocol.Uri == null ||
                !string.Equals(protocol.Uri.Scheme, "eyegazetrainer-exittest",
                               StringComparison.OrdinalIgnoreCase))
            {
                TraceLifecycle("Unknown protocol activation ignored");
                return;
            }

            TraceLifecycle("HARDWARE-FREE exit-isolation protocol invoked");
            var frame = Window.Current.Content as Frame;

            // Do not let this diagnostic interrupt or bypass cleanup for
            // an existing active gaze session.
            if (frame != null && frame.Content is LevelOnePreview)
            {
                TraceLifecycle("Exit-isolation refused: gaze page is active");
                return;
            }

            if (frame == null)
            {
                frame = new Frame();
                Window.Current.Content = frame;
                TraceLifecycle("Hardware-free test root Frame created");
            }
            if (!windowEventsRegistered)
            {
                Window.Current.VisibilityChanged += OnVisibilityChanged;
                Window.Current.Closed += OnWindowClosed;
                windowEventsRegistered = true;
            }

            // Never construct LevelOnePreview or call a gaze API in this path.
            frame.Navigate(typeof(MainPage), "EXIT_NO_GAZE_DIAGNOSTIC");
            Window.Current.Activate();
            TraceLifecycle("Hardware-free exit test Window.Activate completed");
        }

        private void OnVisibilityChanged(object sender,
            Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            TraceLifecycle("Window.VisibilityChanged visible=" + e.Visible);
        }

        private void OnWindowClosed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            EndOverlaySession();
            TraceLifecycle("Window.Closed");
        }

        internal static void EndOverlaySession()
        {
            var app = Application.Current as App;
            if (app == null || string.IsNullOrEmpty(app.overlaySessionId))
                return;
            try
            {
                string path = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                           OverlaySessionFile);
                // Never delete a token written by a newer launch.
                if (File.Exists(path) &&
                    File.ReadAllText(path) == app.overlaySessionId)
                    File.Delete(path);
                TraceLifecycle("Overlay session ended");
            }
            catch (Exception ex)
            {
                TraceLifecycle("Overlay session cleanup failed: " +
                               ex.GetType().Name);
            }
            app.overlaySessionId = null;
        }

        internal static void TraceLifecycle(string message)
        {
            try
            {
                // Event names + timestamps only, no gaze positions or other personal data.
                string path = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                           "gaze-lifecycle-trace.txt");
                File.AppendAllText(path, DateTimeOffset.Now.ToString("O") +
                                   " | " + message + "\r\n");
            }
            catch (Exception)
            {
                // Diagnostics must never block startup, cleanup, or restore.
            }
        }
    }
}
