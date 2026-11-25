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
        private bool overlayHelperLaunchAttempted;
        private bool overlayHelperLaunchAccepted;
        private bool overlayHelperLaunchFinished;
        private const string OverlaySessionFile = "overlay-session.txt";
        private static readonly string ShutdownSessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

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
        internal async void TryStartOverlayCompanion()
        {
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

        internal async System.Threading.Tasks.Task RequestForegroundHandoffAsync()
        {
            if (!overlayHelperLaunchAccepted) return;
            try
            {
                string folder = ApplicationData.Current.LocalFolder.Path;
                string request = Path.Combine(folder, "overlay-focus-" + ShutdownSessionId + ".request");
                string ack = Path.Combine(folder, "overlay-focus-" + ShutdownSessionId + ".ack");
                File.WriteAllText(request, ShutdownSessionId);
                TraceLifecycle("FOREGROUND HANDOFF REQUESTED");
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                while (elapsed.ElapsedMilliseconds < 750)
                {
                    if (File.Exists(ack))
                    {
                        TraceLifecycle("FOREGROUND HANDOFF ACK: " + File.ReadAllText(ack).Trim());
                        return;
                    }
                    await System.Threading.Tasks.Task.Delay(50);
                }
                TraceLifecycle("FOREGROUND HANDOFF TIMEOUT; continuing exit");
            }
            catch (Exception ex) { TraceLifecycle("FOREGROUND HANDOFF ERROR: " + ex.Message); }
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
