using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace EyeGazeApiViability
{
    // SAFETY HOLD: the previous live gaze diagnostic was followed by a TD Control
    // freeze requiring reboot (see issue #9). This version is deliberately a
    // NO-HARDWARE dry run. It never references or starts the gaze APIs.
    public sealed partial class MainPage : Page
    {
        private readonly Stopwatch clock = new Stopwatch();
        private readonly DispatcherTimer ticker = new DispatcherTimer();
        private int nextStage;
        private TimeSpan nextStageTime;
        private string logPath;
        private bool running;

        private static readonly string[] Stages =
        {
            "01/06 — PREPARE (simulated)",
            "02/06 — UNSUBSCRIBE GazeMoved (simulated)",
            "03/06 — STOP device watcher (simulated)",
            "04/06 — REMOVE watcher events (simulated)",
            "05/06 — RELEASE references (simulated)",
            "06/06 — EXIT APPLICATION (simulated; app remains open)"
        };

        public MainPage()
        {
            InitializeComponent();
            StageText.Text = "Stage: IDLE — NO EYE TRACKER ACCESS";
            ticker.Interval = TimeSpan.FromMilliseconds(100);
            ticker.Tick += OnTick;
            Unloaded += OnUnloaded;
        }

        private void OpenLevelOnePreview(object sender, RoutedEventArgs e)
        {
            // Developer preview only; does not acquire gaze resources.
            ticker.Stop();
            Frame.Navigate(typeof(LevelOnePreview));
        }

        private void StartDryRun(object sender, RoutedEventArgs e)
        {
            if (running) return;
            try
            {
                // Logs contain stage names and timestamps only; no gaze positions.
                logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                       "shutdown-dryrun-trace.txt");
                File.WriteAllText(logPath,
                    "EyeGazeTrainer NO-HARDWARE DRY RUN. No gaze APIs opened.\r\n" +
                    "This test does not validate TD Control handoff or recovery.\r\n");
            }
            catch (Exception ex)
            {
                ResultText.Text = "Dry run unavailable: cannot create trace (" +
                                  ex.GetType().Name + ")";
                return;
            }

            running = true;
            ArmButton.IsEnabled = false;
            clock.Restart();
            nextStage = 0;
            nextStageTime = TimeSpan.Zero;
            StageText.Text = "Stage: starting simulated checkpoints";
            CountdownText.Text = "Dry-run sequence (no gaze access)";
            DeviceText.Text = "NO EYE TRACKER ACCESS — deliberate safety lock";
            DataText.Text = "No gaze samples will be collected";
            CountText.Text = "This is a UI/logging verification only";
            TraceText.Text = "Trace: shutdown-dryrun-trace.txt in UWP LocalFolder";
            ResultText.Text = "SIMULATION — not a hardware test";
            Log("START dry run");
            ticker.Start();
        }

        private void OnTick(object sender, object e)
        {
            if (!running || clock.Elapsed < nextStageTime) return;
            if (nextStage >= Stages.Length)
            {
                ticker.Stop();
                running = false;
                Log("COMPLETE all simulated checkpoints; no app exit invoked");
                StageText.Text = "Stage: COMPLETE — hardware untouched";
                CountdownText.Text = "Dry run complete";
                ResultText.Text = "PASS: visible stages and trace written; TD Control not tested";
                ArmButton.IsEnabled = true;
                return;
            }

            string stage = Stages[nextStage];
            StageText.Text = "Stage: " + stage;
            CountdownText.Text = "Checkpoint " + (nextStage + 1) + " of " + Stages.Length;
            Log("BEGIN " + stage);
            // Intentional NO-OP. Do not insert watcher/stream cleanup here:
            // this is safe simulation, not a hardware experiment.
            Log("END " + stage);
            nextStage++;
            nextStageTime = clock.Elapsed + TimeSpan.FromSeconds(3);
        }

        private void Log(string message)
        {
            try
            {
                File.AppendAllText(logPath,
                    DateTimeOffset.Now.ToString("O") + " | +" +
                    clock.Elapsed.TotalSeconds.ToString("F1") +
                    "s | " + message + "\r\n");
            }
            catch (Exception ex)
            {
                TraceText.Text = "WARNING: trace write failed: " + ex.GetType().Name;
                ticker.Stop();
                running = false;
                ArmButton.IsEnabled = true;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ticker.Stop();
        }
    }
}
