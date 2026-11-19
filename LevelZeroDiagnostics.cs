using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Storage;

namespace EyeGazeApiViability
{
    // Aggregated Level 0 observations; no coordinates, samples or identifiers
    // are retained beyond this page's in-memory measurement window.
    public sealed partial class LevelOnePreview
    {
        private const string LevelZeroExtraPrefix = "EyeGazeTrainer.LevelZero.ExtraV1.";
        private const string LevelZeroHistoryFile = "level0-session-history-v1.tsv";
        private DateTimeOffset diagnosticLastGaze;
        private DateTimeOffset diagnosticLastStabilitySample;
        private double diagnosticWindowMeanX, diagnosticWindowMeanY;
        private double diagnosticWindowM2;
        private int diagnosticWindowSamples;
        private double diagnosticStabilitySum;
        private int diagnosticStabilityWindows;
        private int diagnosticTrackingGaps;
        private double diagnosticTrackingGapSeconds;
        private double diagnosticLongestGapSeconds;
        private int diagnosticVerticalSweeps, diagnosticHorizontalSweeps;
        private int diagnosticCircleSamples;
        private int diagnosticInterruptedDwells;
        private bool diagnosticDwellActive;

        private void ResetLevelZeroDiagnostics()
        {
            diagnosticLastGaze = DateTimeOffset.MinValue;
            diagnosticLastStabilitySample = DateTimeOffset.MinValue;
            diagnosticWindowSamples = diagnosticStabilityWindows = 0;
            diagnosticWindowMeanX = diagnosticWindowMeanY = diagnosticWindowM2 = 0;
            diagnosticStabilitySum = 0;
            diagnosticTrackingGaps = diagnosticVerticalSweeps = diagnosticHorizontalSweeps = 0;
            diagnosticTrackingGapSeconds = diagnosticLongestGapSeconds = 0;
            diagnosticCircleSamples = diagnosticInterruptedDwells = 0;
            diagnosticDwellActive = false;
        }

        // Only count a tracking gap after tracking has first become available.
        // It may also be caused by Windows scheduling or tracker conditions.
        private void ObserveLevelZeroDiagnosticGaze(Point raw, DateTimeOffset now)
        {
            if (diagnosticLastGaze != DateTimeOffset.MinValue)
            {
                double gap = (now - diagnosticLastGaze).TotalSeconds;
                if (gap > 1.0 && gap <= 60.0)
                {
                    diagnosticTrackingGaps++;
                    diagnosticTrackingGapSeconds += gap;
                    diagnosticLongestGapSeconds = Math.Max(diagnosticLongestGapSeconds, gap);
                }
            }
            diagnosticLastGaze = now;

            // Sample spread only while actively holding the visible Home
            // target; one-second windows prevent cursor movement between
            // separate attempts from being misreported as instability.
            if (levelZeroStage != LevelZeroStage.Continue ||
                LevelZeroContinueButton.Visibility != Windows.UI.Xaml.Visibility.Visible ||
                gazeControlTarget != LevelZeroContinueButton ||
                (now - diagnosticLastStabilitySample).TotalMilliseconds < 100)
                return;
            if (diagnosticLastStabilitySample != DateTimeOffset.MinValue &&
                (now - diagnosticLastStabilitySample).TotalSeconds > .5)
                FinishStabilityWindow();
            diagnosticLastStabilitySample = now;
            diagnosticWindowSamples++;
            double dx = raw.X - diagnosticWindowMeanX;
            diagnosticWindowMeanX += dx / diagnosticWindowSamples;
            double dy = raw.Y - diagnosticWindowMeanY;
            diagnosticWindowMeanY += dy / diagnosticWindowSamples;
            diagnosticWindowM2 += dx * (raw.X - diagnosticWindowMeanX) +
                                  dy * (raw.Y - diagnosticWindowMeanY);
            if (diagnosticWindowSamples >= 10) FinishStabilityWindow();
        }

        private void FinishStabilityWindow()
        {
            if (diagnosticWindowSamples >= 5)
            {
                diagnosticStabilitySum += Math.Sqrt(
                    Math.Max(0, diagnosticWindowM2 / diagnosticWindowSamples));
                diagnosticStabilityWindows++;
            }
            diagnosticWindowSamples = 0;
            diagnosticWindowMeanX = diagnosticWindowMeanY = diagnosticWindowM2 = 0;
            diagnosticLastStabilitySample = DateTimeOffset.MinValue;
        }

        private void ObserveLevelZeroDwellTarget(bool lookingAtHome)
        {
            if (currentScreen != AppScreen.LevelZero ||
                levelZeroStage != LevelZeroStage.Continue) return;
            if (!lookingAtHome && diagnosticDwellActive)
            {
                diagnosticInterruptedDwells++;
                diagnosticDwellActive = false;
                FinishStabilityWindow();
            }
            else if (lookingAtHome) diagnosticDwellActive = true;
        }

        private void FinishLevelZeroDiagnosticDwell()
        {
            diagnosticDwellActive = false;
            FinishStabilityWindow();
        }

        private string[] LevelZeroExtraCurrentValues()
        {
            return new[]
            {
                diagnosticStabilityWindows == 0 ? "NA" :
                    (diagnosticStabilitySum / diagnosticStabilityWindows).ToString("0.0",
                        CultureInfo.InvariantCulture) + " px RMS (Home hold, " +
                    diagnosticStabilityWindows + " windows)",
                diagnosticLastGaze == DateTimeOffset.MinValue ? "NA" :
                    diagnosticTrackingGaps + " gaps >1 sec; " +
                    diagnosticTrackingGapSeconds.ToString("0.0", CultureInfo.InvariantCulture) +
                    " sec total; longest " + diagnosticLongestGapSeconds.ToString("0.0",
                        CultureInfo.InvariantCulture) + " sec",
                diagnosticVerticalSweeps + " vertical / " +
                    diagnosticHorizontalSweeps + " horizontal accepted sweeps",
                diagnosticCircleSamples + " accepted circular movement samples",
                diagnosticInterruptedDwells + " interrupted Home dwells",
                (DateTimeOffset.UtcNow - levelZeroStarted).TotalMinutes.ToString("0.0",
                    CultureInfo.InvariantCulture) + " minutes elapsed (includes instructions and rest)"
            };
        }

        private static readonly string[] LevelZeroExtraLabels =
        {
            "Gaze stability", "Tracking interruptions",
            "Directional control", "Circular movement",
            "Interrupted dwells", "Session duration"
        };
        private static readonly string[] LevelZeroExtraDetails =
        {
            "Raw gaze spread during stable Home-button holds; lower is less spread, not a diagnosis",
            "Gaps over one second between valid samples; may include device or app delays",
            "Accepted intentional vertical and horizontal sweeps during guided practice",
            "Valid circular movement samples recorded during guided practice",
            "Home-button dwell attempts that ended before selection; rest is never a failure",
            "Elapsed tutorial time including instruction, practice and pauses"
        };

        private static string LevelZeroExtraValue(int index)
        {
            if (index < 0 || index >= LevelZeroExtraLabels.Length) return "NA";
            try
            {
                object value;
                return ApplicationData.Current.LocalSettings.Values.TryGetValue(
                    LevelZeroExtraPrefix + index, out value) ? Convert.ToString(value) : "NA";
            }
            catch { return "NA"; }
        }

        private void SaveLevelZeroExtraDiagnostics()
        {
            FinishLevelZeroDiagnosticDwell();
            string[] values = LevelZeroExtraCurrentValues();
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                for (int i = 0; i < values.Length; i++)
                    settings[LevelZeroExtraPrefix + i] = values[i];
            }
            catch { /* Diagnostics never block tutorial completion. */ }

            // Keep bounded, dated session summaries, not raw gaze recordings.
            // The first six original metrics remain available as before.
            try
            {
                string path = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                           LevelZeroHistoryFile);
                string line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz",
                    CultureInfo.InvariantCulture) + "\t" +
                    string.Join("\t", values.Select(v => v.Replace("\t", " ")));
                string[] previous = File.Exists(path) ? File.ReadAllLines(path) :
                    new string[0];
                string[] retained = previous.Where(x => !string.IsNullOrWhiteSpace(x))
                    .TakeLastCompat(99).Concat(new[] { line }).ToArray();
                File.WriteAllLines(path, retained);
            }
            catch { /* Storage failure never interrupts gaze-only navigation. */ }
        }

        private static string[] LevelZeroHistoryLines()
        {
            try
            {
                string path = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                                           LevelZeroHistoryFile);
                if (!File.Exists(path)) return new string[0];
                return File.ReadAllLines(path).Where(x =>
                    !string.IsNullOrWhiteSpace(x)).ToArray();
            }
            catch { return new string[0]; }
        }

        private static string[] LastLevelZeroSessions(int count)
        {
            string[] lines = LevelZeroHistoryLines();
            return lines.Skip(Math.Max(0, lines.Length - count)).Reverse()
                .Select(x => x.Replace("\t", " | ")).ToArray();
        }
    }

    internal static class LevelZeroHistoryEnumerableExtensions
    {
        internal static System.Collections.Generic.IEnumerable<T> TakeLastCompat<T>(
            this System.Collections.Generic.IEnumerable<T> source, int count)
        {
            var a = source.ToArray();
            return a.Skip(Math.Max(0, a.Length - count));
        }
    }
}
