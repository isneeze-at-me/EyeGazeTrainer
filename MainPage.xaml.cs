using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;
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
        private bool armed;
        private bool shuttingDown;
        private int shutdownStage;
        private TimeSpan nextStageAt;
        private string logPath;
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
            // Deliberately idle on launch: previous automatic exit disabled TD Control.
            // No device watcher or gaze stream is opened until a supervised arm click.
            StageText.Text = "Stage: IDLE — gaze input NOT started";
        }

        private void ArmDiagnostic(object sender, RoutedEventArgs e)
        {
            if (armed || finished) return;
            armed = true;
            ArmButton.IsEnabled = false;
            logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "shutdown-trace.txt");
            try
            {
                File.WriteAllText(logPath,
                    "EyeGazeTrainer supervised diagnostic (no raw gaze coordinates)\r\n" +
                    "Known risk: TD Control remained visible but stopped responding after prior app exit.\r\n");
                TraceText.Text = "Log: shutdown-trace.txt (UWP LocalFolder)";
            }
            catch (Exception ex)
            {
                TraceText.Text = "WARNING: log unavailable: " + ex.GetType().Name;
                // Do not continue without a durable trace of the high-risk stages.
                armed = false;
                ArmButton.IsEnabled = true;
                return;
            }

            WriteTrace("ARMED; countdown begins; TD Control remains untouched");
            clock.Start();
            ticker.Interval = TimeSpan.FromMilliseconds(100);
            ticker.Tick += Tick;
            ticker.Start();
            StageText.Text = "Stage: ACQUIRING GAZE INPUT";
            try
            {
                WriteTrace("BEGIN watcher create/start");
                watcher = GazeInputSourcePreview.CreateWatcher();
                watcher.Added += DeviceAdded;
                watcher.Updated += DeviceUpdated;
                watcher.Removed += DeviceRemoved;
                watcher.Start();
                WriteTrace("END watcher create/start");
            }
            catch (Exception ex)
            {
                state = "Watcher error: " + ex.GetType().Name;
                DeviceText.Text = state;
                ResultText.Text = "FAIL: Windows gaze watcher unavailable";
                WriteTrace("ERROR watcher create/start: " + ex);
            }
        }

        private void WriteTrace(string entry)
        {
            if (logPath == null) return;
            try
            {
                File.AppendAllText(logPath,
                    DateTimeOffset.Now.ToString("O") + " | +" +
                    clock.Elapsed.TotalSeconds.ToString("F1") + "s | " + entry + "\r\n");
            }
            catch (Exception ex)
            {
                TraceText.Text = "WARNING: log write failed: " + ex.GetType().Name;
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
            if (finished || shuttingDown) return;
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
            if (finished || shuttingDown) return;
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
            if (!armed || finished) return;
            if (shuttingDown)
            {
                if (clock.Elapsed >= nextStageAt)
                    RunShutdownStage();
                return;
            }

            var remaining = Math.Max(0, TestSeconds - clock.Elapsed.TotalSeconds);
            CountdownText.Text = $"Cleanup stages begin in {Math.Ceiling(remaining):0} seconds";
            if (remaining <= 0)
            {
                shuttingDown = true;
                shutdownStage = 0;
                WriteTrace("Gaze observation ended: samples=" + validSamples +
                           " changes=" + coordinateChanges + " (no coordinates stored)");
                RunShutdownStage();
            }
        }

        // Each checkpoint is logged both BEFORE and AFTER its operation. The screen
        // remains present for 3 seconds between operations to help isolate the step
        // at which TD Control stops responding. NOT a guaranteed safe exit.
        private void RunShutdownStage()
        {
            string name;
            switch (shutdownStage)
            {
                case 0: name = "01/06 — PREPARE (no device changes)"; break;
                case 1: name = "02/06 — UNSUBSCRIBE GazeMoved"; break;
                case 2: name = "03/06 — STOP device watcher"; break;
                case 3: name = "04/06 — REMOVE watcher events"; break;
                case 4: name = "05/06 — RELEASE local references"; break;
                case 5: name = "06/06 — EXIT APPLICATION"; break;
                default: return;
            }

            StageText.Text = "Stage: " + name;
            CountdownText.Text = "Shutdown checkpoint " + (shutdownStage + 1) + " of 6";
            WriteTrace("BEGIN " + name);
            try
            {
                switch (shutdownStage)
                {
                    case 0:
                        // No-op baseline; observe TD Control before touching subscriptions.
                        break;
                    case 1:
                        if (source != null) source.GazeMoved -= GazeMoved;
                        started = false;
                        break;
                    case 2:
                        if (watcher != null) watcher.Stop();
                        break;
                    case 3:
                        if (watcher != null)
                        {
                            watcher.Added -= DeviceAdded;
                            watcher.Updated -= DeviceUpdated;
                            watcher.Removed -= DeviceRemoved;
                        }
                        break;
                    case 4:
                        source = null;
                        watcher = null;
                        break;
                    case 5:
                        WriteTrace("CALL CoreApplication.Exit() — final checkpoint");
                        finished = true;
                        ticker.Stop();
                        CoreApplication.Exit();
                        return;
                }
                WriteTrace("END " + name);
            }
            catch (Exception ex)
            {
                WriteTrace("ERROR " + name + ": " + ex);
                StageText.Text = "HALTED at " + name + " — " + ex.GetType().Name;
                ResultText.Text = "Cleanup failed. Do not run again. Recovery assistance required.";
                ticker.Stop();
                return;
            }

            shutdownStage++;
            nextStageAt = clock.Elapsed + TimeSpan.FromSeconds(3);
        }

        private void StopSource()
        {
            if (source != null) { source.GazeMoved -= GazeMoved; source = null; }
            started = false;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Do not recurse into an immediate teardown from Unloaded; manual close
            // is an independently observable lifecycle event.
            WriteTrace("PAGE Unloaded; phase=" + shutdownStage);
            ticker.Stop();
        }
    }
}
