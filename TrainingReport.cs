using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Persistent training report. Stores cumulative practice seconds,
    /// completed runs, and small rolling histories of aggregate metrics.
    /// No raw gaze coordinates, per-sample positions, or personal data.
    /// </summary>
    internal sealed class TrainingReport
    {
        private const string Prefix = "EyeGazeTrainer.Report.V1.";
        private const int HistorySize = 20;
        private readonly List<double> overall = new List<double>();
        private readonly List<double> accuracy = new List<double>();
        private readonly List<double> jitter = new List<double>();
        private readonly List<double> smoothing = new List<double>();
        private double practiceSeconds;
        private double unsavedPracticeSeconds;
        private int completedRuns;

        public TrainingReport()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                object value;
                if (settings.TryGetValue(Prefix + "Seconds", out value))
                    practiceSeconds = ValidNonnegative(Convert.ToDouble(value), 0, 1e10);
                if (settings.TryGetValue(Prefix + "Runs", out value))
                    completedRuns = Math.Max(0, Convert.ToInt32(value));
                Load(overall, "Overall", 100);
                Load(accuracy, "Accuracy", 100);
                Load(jitter, "Jitter", 1000);
                Load(smoothing, "Smoothing", 99);
            }
            catch (Exception) { /* Invalid settings do not prevent training. */ }
        }

        public double PracticeHours { get { return practiceSeconds / 3600.0; } }
        public int CompletedRuns { get { return completedRuns; } }
        public double? LatestAccuracy { get { return Last(accuracy); } }
        public double? LatestJitter { get { return Last(jitter); } }

        public void AddPracticeSeconds(double seconds)
        {
            if (seconds <= 0 || seconds > 2 || double.IsNaN(seconds)) return;
            practiceSeconds += seconds;
            unsavedPracticeSeconds += seconds;
            // Coalesce persistent-storage writes; a one-second timer should
            // not write to LocalSettings on every tick.
            if (unsavedPracticeSeconds >= 10)
                FlushPracticeTime();
        }

        public void FlushPracticeTime()
        {
            if (unsavedPracticeSeconds <= 0) return;
            SaveScalar("Seconds", practiceSeconds);
            unsavedPracticeSeconds = 0;
        }

        public void RecordCompletedRun()
        {
            FlushPracticeTime();
            if (completedRuns < int.MaxValue) completedRuns++;
            SaveScalar("Runs", completedRuns);
        }

        public void RecordOverall(double value) { Add(overall, "Overall", value, 100); }
        public void RecordAccuracy(double value) { Add(accuracy, "Accuracy", value, 100); }
        public void RecordJitter(double millimeters) { Add(jitter, "Jitter", millimeters, 1000); }
        public void RecordSmoothing(int percent)
        {
            if (percent < 0 || percent > 99) return;
            Add(smoothing, "Smoothing", percent, 99);
        }

        public int OverallTrend { get { return Trend(overall, false, 0.5); } }
        public int AccuracyTrend { get { return Trend(accuracy, false, 1.0); } }
        // Smaller spatial spread indicates steadier observed gaze.
        public int JitterTrend { get { return Trend(jitter, true, 0.15); } }
        // Direction is numeric: +1 means more filtering, -1 means less.
        public int SmoothingTrend { get { return Trend(smoothing, false, 0.5); } }

        private static double? Last(List<double> items)
        {
            return items.Count > 0 ? (double?)items[items.Count - 1] : null;
        }

        // Compare the latest five observations to the preceding five.
        // At least ten observations are required to show a direction.
        private static int Trend(List<double> items, bool lowerBetter, double threshold)
        {
            if (items.Count < 10) return 0;
            double previous = 0, recent = 0;
            int n = items.Count;
            for (int i = n - 10; i < n - 5; i++) previous += items[i] / 5.0;
            for (int i = n - 5; i < n; i++) recent += items[i] / 5.0;
            double difference = lowerBetter ? previous - recent : recent - previous;
            return difference >= threshold ? 1 : difference <= -threshold ? -1 : 0;
        }

        private void Add(List<double> items, string key, double value, double limit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value < 0 || value > limit) return;
            items.Add(value);
            if (items.Count > HistorySize) items.RemoveAt(0);
            SaveScalar(key, string.Join(";", items.ConvertAll(v =>
                v.ToString("R", CultureInfo.InvariantCulture))));
        }

        private void Load(List<double> items, string key, double limit)
        {
            try
            {
                object stored;
                if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(
                    Prefix + key, out stored)) return;
                string text = Convert.ToString(stored);
                if (string.IsNullOrEmpty(text)) return;
                foreach (string part in text.Split(';'))
                {
                    double value;
                    if (!double.TryParse(part, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out value) ||
                        double.IsNaN(value) || double.IsInfinity(value) ||
                        value < 0 || value > limit) { items.Clear(); return; }
                    items.Add(value);
                }
                while (items.Count > HistorySize) items.RemoveAt(0);
            }
            catch (Exception) { items.Clear(); }
        }

        private static double ValidNonnegative(double x, double fallback, double maximum)
        {
            return double.IsNaN(x) || double.IsInfinity(x) || x < 0 || x > maximum
                ? fallback : x;
        }

        private static void SaveScalar(string key, object value)
        {
            try { ApplicationData.Current.LocalSettings.Values[Prefix + key] = value; }
            catch (Exception) { /* Telemetry persistence cannot block gaze. */ }
        }
    }
}
