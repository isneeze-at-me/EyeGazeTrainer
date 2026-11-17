using System;
using System.IO;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace EyeGazeApiViability
{
    sealed partial class App : Application
    {
        private bool windowEventsRegistered;

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
