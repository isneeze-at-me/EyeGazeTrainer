using System;
using System.IO;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace EyeGazeApiViability
{
    sealed partial class App : Application
    {
        private bool windowEventsRegistered;
        private bool overlayHelperLaunchAttempted;

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

            if (frame.Content == null)
            {
                TraceLifecycle("Navigate to gaze screen");
                frame.Navigate(typeof(LevelOnePreview));
            }

            Window.Current.Activate();
            TraceLifecycle("Window.Activate completed");
            TryStartOverlayCompanion();
        }

        // The helper operates on this app's own foreground window, not the
        // gaze APIs or TD Control. Never delay or fail gaze startup if absent.
        private async void TryStartOverlayCompanion()
        {
            if (overlayHelperLaunchAttempted) return;
            overlayHelperLaunchAttempted = true;
            try
            {
                TraceLifecycle("Overlay companion launch requested");
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                TraceLifecycle("Overlay companion launch accepted");
            }
            catch (Exception ex)
            {
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
            TraceLifecycle("Window.Closed");
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
