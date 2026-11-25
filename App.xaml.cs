// ============================================================================
// EyeGazeTrainer
// File: App.xaml.cs
// Purpose: Owns UWP activation, diagnostic session identity, packaged overlay coordination and lifecycle tracing.
// ============================================================================

using System;
using System.IO;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace EyeGazeApiViability
{
    sealed partial class App : Application
    {
        private bool windowEventsRegistered;
        internal string IsolationMode { get; private set; }
        private bool isolationActive;
        private bool overlayHelperLaunchAttempted;
        private bool overlayHelperLaunchAccepted;
        private bool overlayHelperLaunchFinished;
        private const string OverlaySessionFile = "overlay-session.txt";
        private static string ShutdownSessionId = NewSessionId();

        // Generate a unique timestamped identifier for correlating trainer and companion shutdown traces.
        private static string NewSessionId()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        // Initialize application resources and register lifecycle observers.
        public App()
        {
            InitializeComponent();
            TraceLifecycle("App constructor; build=2026-11-24-watcher-free-v5");
            Suspending += (s, e) => TraceLifecycle("Application.Suspending");
            Resuming += (s, e) => TraceLifecycle("Application.Resuming");
        }

        // Create or reuse the normal application view and request full-screen training.
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            // Windows may retain the closed application in a suspended state.
            // A new explicit launch must not reopen the gaze-free closing page
            // or reuse the previous companion's restoration channel.
            if (frame != null && frame.Content is MainPage)
            {
                ShutdownSessionId = NewSessionId();
                overlayHelperLaunchAttempted = false;
                overlayHelperLaunchAccepted = false;
                overlayHelperLaunchFinished = false;
                frame = new Frame();
                Window.Current.Content = frame;
                TraceLifecycle("Fresh session after previous native close");
            }
            if (isolationActive && frame != null && frame.Content is LevelOnePreview)
            {
                TraceLifecycle("Normal launch ignored: isolation session active");
                Window.Current.Activate();
                return;
            }
            IsolationMode = null;
            isolationActive = false;
            TraceLifecycle("OnLaunched START, previousState=" + e.PreviousExecutionState);
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

            // Size the app independently of the gaze-aware companion. Request
            // full-screen launch before activation, so the splash occupies the
            // entire display even when tracker discovery has not finished.
            try
            {
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
                var view = ApplicationView.GetForCurrentView();
                bool fullScreen = view.IsFullScreenMode || view.TryEnterFullScreenMode();
                TraceLifecycle("Startup full-screen request before activation: " + fullScreen);
            }
            catch (Exception ex)
            {
                TraceLifecycle("Startup full-screen request failed: " + ex);
            }
            Window.Current.Activate();
            TraceLifecycle("Window.Activate completed");
        }

        // The helper operates on this app's own foreground window, not the
        // gaze APIs or TD Control. Never delay or fail gaze startup if absent.
        // Launch the packaged full-trust helper once per eligible gaze session; isolation modes can suppress it.
        internal async void TryStartOverlayCompanion()
        {
            if (IsolationMode != null && IsolationMode != "C") { TraceLifecycle("ISOLATION " + IsolationMode + ": companion intentionally skipped"); return; }
            if (overlayHelperLaunchAttempted) return;
            overlayHelperLaunchAttempted = true;
            try
            {
                File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path, OverlaySessionFile), ShutdownSessionId);
                TraceLifecycle("Overlay companion launch requested; session=" + ShutdownSessionId);
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                overlayHelperLaunchAccepted = true;
                TraceLifecycle("Overlay companion launch accepted");
            }
            catch (Exception ex)
            {
                // Unavailable full-trust support means TD Control's circle stays
                // visible. All UWP gaze and controlled-exit behavior is intact.
                TraceLifecycle("Overlay companion unavailable: " + ex.GetType().Name +
                               " HResult=" + ex.HResult.ToString("X8"));
            }
            finally { overlayHelperLaunchFinished = true; }
        }

        // Request verified restoration of the trainer window style before closing the view; bound the acknowledgement wait.
        internal async System.Threading.Tasks.Task RequestOverlayRestorationAsync()
        {
            if (!overlayHelperLaunchAttempted)
            {
                TraceLifecycle("Overlay restore not needed: companion not launched");
                return;
            }
            try
            {
                string folder = ApplicationData.Current.LocalFolder.Path;
                string request = Path.Combine(folder, "overlay-restore-" + ShutdownSessionId + ".request");
                string ack = Path.Combine(folder, "overlay-restore-" + ShutdownSessionId + ".ack");
                // Publish even if the launch is still pending: the helper checks
                // before touching any window. Never block the UI dispatcher.
                File.WriteAllText(request, ShutdownSessionId);
                TraceLifecycle("OVERLAY RESTORE REQUESTED; session=" + ShutdownSessionId);
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                while (elapsed.ElapsedMilliseconds < 3000)
                {
                    if (File.Exists(ack))
                    {
                        string result = File.ReadAllText(ack).Trim();
                        if (result == "RESTORED" || result == "NOT_CHANGED")
                        {
                            TraceLifecycle("OVERLAY RESTORE ACK: " + result + "; elapsedMs=" + elapsed.ElapsedMilliseconds);
                            return;
                        }
                        if (result == "FAILED")
                        {
                            TraceLifecycle("OVERLAY RESTORE FAILED; continuing normal exit");
                            return;
                        }
                    }
                    if (overlayHelperLaunchFinished && !overlayHelperLaunchAccepted)
                    {
                        TraceLifecycle("Overlay restore not needed: launch failed");
                        return;
                    }
                    await System.Threading.Tasks.Task.Delay(50);
                }
                TraceLifecycle("OVERLAY RESTORE TIMEOUT; continuing normal exit");
            }
            catch (Exception ex)
            {
                TraceLifecycle("OVERLAY RESTORE ERROR: " + ex);
            }
        }

        // Handle explicit shutdown-isolation protocol activation separately from normal training.
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

            string mode = protocol.Uri.Host.ToUpperInvariant();
            if (mode.Length != 1 || "ABCDEFGHI".IndexOf(mode, StringComparison.Ordinal) < 0)
            {
                TraceLifecycle("Isolation refused: specify A through I");
                return;
            }
            var frame = Window.Current.Content as Frame;
            if (frame != null && frame.Content is LevelOnePreview)
            {
                TraceLifecycle("Isolation refused: training page is active");
                return;
            }
            if (isolationActive && frame != null && frame.Content is MainPage &&
                Window.Current.Visible)
            {
                TraceLifecycle("Isolation refused: diagnostic already active");
                return;
            }
            IsolationMode = mode;
            isolationActive = true;
            ShutdownSessionId = NewSessionId();
            overlayHelperLaunchAttempted = false;
            overlayHelperLaunchAccepted = false;
            overlayHelperLaunchFinished = false;
            TraceLifecycle("OnLaunched START; ISOLATION " + mode + "; 30-second auto-close; build=2026-11-24-watcher-free-v5");
            frame = new Frame();
            Window.Current.Content = frame;
            if (!windowEventsRegistered)
            {
                Window.Current.VisibilityChanged += OnVisibilityChanged;
                Window.Current.Closed += OnWindowClosed;
                windowEventsRegistered = true;
            }
            frame.Navigate(mode == "A" ? typeof(MainPage) : typeof(LevelOnePreview),
                mode == "A" ? "ISOLATION_A" : null);
            try
            {
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
                var view = ApplicationView.GetForCurrentView();
                TraceLifecycle("ISOLATION full-screen=" + (view.IsFullScreenMode || view.TryEnterFullScreenMode()));
            }
            catch (Exception ex) { TraceLifecycle("ISOLATION full-screen failed: " + ex); }
            Window.Current.Activate();
            TraceLifecycle("ISOLATION " + mode + " activated");
        }

        // Record visibility transitions for external shutdown correlation.
        private void OnVisibilityChanged(object sender,
            Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            TraceLifecycle("Window.VisibilityChanged visible=" + e.Visible);
        }

        // Record the Windows view closure notification.
        private void OnWindowClosed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            TraceLifecycle("Window.Closed");
        }

        // Append timestamped diagnostic messages to the per-session lifecycle log.
        internal static void TraceLifecycle(string message)
        {
            try
            {
                // Event names + timestamps only, no gaze positions or other personal data.
                string path = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                           "gaze-lifecycle-trace.txt");
                string line = DateTimeOffset.Now.ToString("O") + " | " + message + "\r\n";
                string sessionPath = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                    "shutdown-" + ShutdownSessionId + ".txt");
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line);
                using (var stream = new FileStream(sessionPath, FileMode.Append,
                    FileAccess.Write, FileShare.ReadWrite))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.AppendAllText(path, line);
            }
            catch (Exception)
            {
                // Diagnostics must never block startup, cleanup, or restore.
            }
        }
    }
}
