using System;
using System.Diagnostics;
using Windows.Devices.Input.Preview;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Core;

namespace EyeGazeApiViability
{
    public sealed partial class MainPage : Page
    {
        private const int TestSeconds = 60;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly DispatcherTimer ticker = new DispatcherTimer();
        private GazeDeviceWatcherPreview watcher;
        private GazeInputSourcePreview source;
        private bool started;
        private bool finished;
        private int validSamples;
        private int coordinateChanges;
        private Point? previous;
        private string state = "Waiting for device";

        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Start timer before requesting gaze permission. A first-run Windows
            // permission dialog might still postpone execution: see README.
            clock.Start();
            ticker.Interval = TimeSpan.FromMilliseconds(100);
            ticker.Tick += Tick;
            ticker.Start();
            try
            {
                watcher = GazeInputSourcePreview.CreateWatcher();
                watcher.Added += DeviceAdded;
                watcher.Updated += DeviceUpdated;
                watcher.Removed += DeviceRemoved;
                watcher.Start();
            }
            catch (Exception ex)
            {
                state = "Watcher error: " + ex.GetType().Name;
                DeviceText.Text = state;
                ResultText.Text = "FAIL: Windows gaze watcher unavailable";
            }
        }

        private void DeviceAdded(GazeDeviceWatcherPreview sender, GazeDeviceWatcherAddedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UseDevice(args.Device));
        }
        private void DeviceUpdated(GazeDeviceWatcherPreview sender, GazeDeviceWatcherUpdatedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UseDevice(args.Device));
        }
        private void DeviceRemoved(GazeDeviceWatcherPreview sender, GazeDeviceWatcherRemovedPreviewEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (finished) return;
                state = "Eye tracker removed";
                DeviceText.Text = state;
                StopSource();
            });
        }

        private void UseDevice(GazeDevicePreview device)
        {
            if (finished) return;
            state = "Device found | CanTrackEyes=" + device.CanTrackEyes +
                    " | State=" + device.ConfigurationState;
            DeviceText.Text = state;
            if (!device.CanTrackEyes || device.ConfigurationState != GazeDeviceConfigurationStatePreview.Ready)
            {
                ResultText.Text = "Device found but not ready for gaze streaming";
                return;
            }
            if (started) return;
            try
            {
                // Must be invoked from UWP UI thread; triggers Windows consent on first use.
                source = GazeInputSourcePreview.GetForCurrentView();
                source.GazeMoved += GazeMoved;
                started = true;
                ResultText.Text = "Gaze source opened; waiting for live samples";
            }
            catch (Exception ex)
            {
                state = "Gaze source error: " + ex.GetType().Name + " (0x" + ex.HResult.ToString("X8") + ")";
                DeviceText.Text = state;
                ResultText.Text = "FAIL: Unable to open gaze source";
            }
        }

        private void GazeMoved(GazeInputSourcePreview sender, GazeMovedPreviewEventArgs args)
        {
            if (finished) return;
            var position = args.CurrentPoint?.EyeGazePosition;
            if (!position.HasValue) return;
            var p = position.Value;
            if (double.IsNaN(p.X) || double.IsNaN(p.Y)) return;
            validSamples++;
            if (previous.HasValue &&
                (Math.Abs(p.X - previous.Value.X) > 2 || Math.Abs(p.Y - previous.Value.Y) > 2))
                coordinateChanges++;
            previous = p;
            DataText.Text = $"LIVE EYE GAZE: X={p.X:F1}  Y={p.Y:F1}";
            CountText.Text = $"Valid samples: {validSamples} | Coordinate changes: {coordinateChanges}";
            GazeDot.Visibility = Visibility.Visible;
            // Coordinates are DIP relative to application view. Canvas starts in row 1.
            var canvasTop = GazeCanvas.TransformToVisual(this).TransformPoint(new Point(0, 0)).Y;
            var canvasLeft = GazeCanvas.TransformToVisual(this).TransformPoint(new Point(0, 0)).X;
            Canvas.SetLeft(GazeDot, Math.Max(0, Math.Min(GazeCanvas.ActualWidth - 35, p.X - canvasLeft - 17.5)));
            Canvas.SetTop(GazeDot, Math.Max(0, Math.Min(GazeCanvas.ActualHeight - 35, p.Y - canvasTop - 17.5)));
            ResultText.Text = coordinateChanges >= 5 ? "PASS: Independent gaze coordinates are changing" : "Receiving gaze samples; move your eyes around";
            args.Handled = true;
        }

        private void Tick(object sender, object e)
        {
            var remaining = Math.Max(0, TestSeconds - clock.Elapsed.TotalSeconds);
            CountdownText.Text = $"Automatic exit in {Math.Ceiling(remaining):0} seconds";
            if (remaining <= 0) Finish();
        }
        private void Finish()
        {
            if (finished) return;
            finished = true;
            ticker.Stop();
            StopSource();
            if (watcher != null)
            {
                watcher.Stop();
                watcher.Added -= DeviceAdded;
                watcher.Updated -= DeviceUpdated;
                watcher.Removed -= DeviceRemoved;
                watcher = null;
            }
            // Deliberately NO persistent gaze data storage or forced TD Control restart.
            CoreApplication.Exit();
        }
        private void StopSource()
        {
            if (source != null) { source.GazeMoved -= GazeMoved; source = null; }
            started = false;
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!finished) Finish();
        }
    }
}
