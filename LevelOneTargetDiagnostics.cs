// ============================================================================
// EyeGazeTrainer
// File: LevelOneTargetDiagnostics.cs
// Purpose: Store comparable Level 1 target-round summaries and separate trivia
// transfer observations without retaining gaze coordinates or per-event traces.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Storage;

namespace EyeGazeApiViability
{
    internal sealed class LevelOneTargetDiagnostics
    {
        private const string StorageKey = "EyeGazeTrainer.LevelOne.TargetSessions.V1";
        private const int HistoryLimit = 20;
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly string[] DirectionNames =
            { "R", "DR", "D", "DL", "L", "UL", "U", "UR" };
        private static readonly double[] TrendThresholds = { .10, 2, .2, 5, 5, 1 };
        private readonly List<Session> history = new List<Session>();
        private readonly List<double> arrivals = new List<double>();
        private readonly List<double> precisions = new List<double>();
        private readonly List<double> jitters = new List<double>();
        private readonly int[] directionTrials = new int[8];
        private readonly int[] directionSuccesses = new int[8];
        private readonly HashSet<int> reached = new HashSet<int>();
        private int firstTrials, firstSuccesses;
        private Session savedRound;

        private sealed class Session
        {
            public string Id;
            public double[] Values = new double[6];
            public int[] Counts = new int[6];
            public int[] DirectionTrials = new int[8];
            public int[] DirectionSuccesses = new int[8];
            public int BonusCount, BonusJitterCount;
            public double BonusPrecisionTotal, BonusJitterTotal;
        }

        // Restore only valid bounded session summaries. Missing data is NaN
        // internally and NA in the UI, never an invented zero measurement.
        public LevelOneTargetDiagnostics()
        {
            try
            {
                object value;
                if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(StorageKey, out value)) return;
                foreach (string line in Convert.ToString(value).Split(';'))
                {
                    string[] f = line.Split(',');
                    if (f.Length != 33) continue;
                    var s = new Session { Id = f[0] };
                    int p = 1;
                    bool valid = true;
                    for (int i = 0; i < 6; i++)
                    {
                        double v;
                        int n;
                        if (!double.TryParse(f[p++], NumberStyles.Float, Culture, out v) ||
                            !int.TryParse(f[p++], out n) || n < 0 ||
                            (n > 0 && (double.IsNaN(v) || double.IsInfinity(v) || v < 0)))
                        { valid = false; break; }
                        s.Values[i] = v; s.Counts[i] = n;
                    }
                    if (!valid) continue;
                    for (int i = 0; i < 8; i++)
                    {
                        int n, k;
                        if (!int.TryParse(f[p++], out n) || !int.TryParse(f[p++], out k) ||
                            n < 0 || k < 0 || k > n) { valid = false; break; }
                        s.DirectionTrials[i] = n; s.DirectionSuccesses[i] = k;
                    }
                    if (!valid) continue;
                    if (!int.TryParse(f[p++], out s.BonusCount) ||
                        !double.TryParse(f[p++], NumberStyles.Float, Culture, out s.BonusPrecisionTotal) ||
                        !int.TryParse(f[p++], out s.BonusJitterCount) ||
                        !double.TryParse(f[p++], NumberStyles.Float, Culture, out s.BonusJitterTotal) ||
                        s.BonusCount < 0 || s.BonusJitterCount < 0 || s.BonusJitterCount > s.BonusCount ||
                        double.IsNaN(s.BonusPrecisionTotal) || double.IsInfinity(s.BonusPrecisionTotal) ||
                        double.IsNaN(s.BonusJitterTotal) || double.IsInfinity(s.BonusJitterTotal)) continue;
                    history.Add(s);
                }
                while (history.Count > HistoryLimit) history.RemoveAt(0);
            }
            catch (Exception) { /* Storage must not interrupt gaze access. */ }
        }

        // Begin a fresh target round without altering previously saved summaries.
        public void BeginRound()
        {
            arrivals.Clear(); precisions.Clear(); jitters.Clear(); reached.Clear();
            Array.Clear(directionTrials, 0, 8); Array.Clear(directionSuccesses, 0, 8);
            firstTrials = firstSuccesses = 0;
            savedRound = null;
        }

        // A tracking interruption excludes the trial from timing and first-hold
        // comparisons; completed spatial access and valid settled samples remain.
        public void RecordTarget(int cell, int direction, double? arrivalSeconds,
            bool comparableHold, bool firstHold, TargetPerformance performance, double? jitterMm)
        {
            if (!reached.Add(cell)) return;
            if (arrivalSeconds.HasValue && arrivalSeconds.Value >= 0)
                arrivals.Add(arrivalSeconds.Value);
            if (performance != null) precisions.Add(performance.Precision);
            if (jitterMm.HasValue && jitterMm.Value >= 0) jitters.Add(jitterMm.Value);
            if (!comparableHold) return;
            firstTrials++;
            if (firstHold) firstSuccesses++;
            if (direction >= 0 && direction < 8)
            {
                directionTrials[direction]++;
                if (firstHold) directionSuccesses[direction]++;
            }
        }

        // Save only a finished primary round so abandoned rounds cannot inflate
        // reach or create misleading historical comparisons.
        public void CompleteRound()
        {
            if (savedRound != null || reached.Count == 0) return;
            var s = new Session { Id = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", Culture) };
            s.Values[0] = Median(arrivals); s.Counts[0] = arrivals.Count;
            s.Values[1] = precisions.Count > 0 ? precisions.Average() : double.NaN; s.Counts[1] = precisions.Count;
            s.Values[2] = Median(jitters); s.Counts[2] = jitters.Count;
            s.Values[3] = firstTrials > 0 ? 100.0 * firstSuccesses / firstTrials : double.NaN; s.Counts[3] = firstTrials;
            Array.Copy(directionTrials, s.DirectionTrials, 8);
            Array.Copy(directionSuccesses, s.DirectionSuccesses, 8);
            var rates = Enumerable.Range(0, 8).Where(i => directionTrials[i] > 0)
                .Select(i => 100.0 * directionSuccesses[i] / directionTrials[i]).ToArray();
            s.Values[4] = rates.Length > 0 ? rates.Average() : double.NaN;
            s.Counts[4] = directionTrials.Sum();
            s.Values[5] = reached.Count; s.Counts[5] = reached.Count;
            history.Add(s);
            savedRound = s;
            while (history.Count > HistoryLimit) history.RemoveAt(0);
            Persist();
        }

        // Trivia is a transfer exercise, not a comparable circular-target trial.
        // Count valid gaze measurements regardless of factual correctness.
        public void RecordBonus(TargetPerformance performance, double? jitterMm)
        {
            if (savedRound == null || performance == null) return;
            savedRound.BonusCount++;
            savedRound.BonusPrecisionTotal += performance.Precision;
            if (jitterMm.HasValue)
            {
                savedRound.BonusJitterCount++;
                savedRound.BonusJitterTotal += jitterMm.Value;
            }
            Persist();
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return double.NaN;
            var ordered = values.OrderBy(v => v).ToArray();
            return (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) / 2;
        }

        // Latest and historical mean of comparable session summaries. The
        // trend compares the latest session with the preceding recorded one.
        public string Latest(int metric)
        {
            return history.Count == 0 || history.Last().Counts[metric] == 0
                ? "NA" : Format(metric, history.Last().Values[metric]);
        }

        public string Summary(int metric)
        {
            if (Latest(metric) == "NA") return "NOT YET RECORDED";
            var valid = history.Where(s => s.Counts[metric] > 0).ToArray();
            double average = valid.Average(s => s.Values[metric]);
            int n = history.Last().Counts[metric];
            string trend = "Trend NA";
            if (valid.Length >= 2)
            {
                double change = valid.Last().Values[metric] - valid[valid.Length - 2].Values[metric];
                double improvement = metric == 0 || metric == 2 ? -change : change;
                trend = Math.Abs(change) < TrendThresholds[metric] ? "Stable" :
                    improvement > 0 ? "Improving" : "Declining";
            }
            return "Mean " + Format(metric, average) + " | n=" + n + " | " + trend;
        }

        // Per-direction first-hold success, with denominators so a small
        // sample cannot be mistaken for a well-established ability.
        public string Directions()
        {
            if (history.Count == 0) return "Directions: NA";
            Session s = history.Last();
            return string.Join("  ", Enumerable.Range(0, 8).Select(i =>
                DirectionNames[i] + ": " + (s.DirectionTrials[i] == 0 ? "NA" :
                s.DirectionSuccesses[i] + "/" + s.DirectionTrials[i])));
        }

        public string BonusSummary()
        {
            if (history.Count == 0 || history.Last().BonusCount == 0) return "Trivia gaze transfer: NA";
            var s = history.Last();
            return "Trivia gaze transfer: " + s.BonusCount + " selections; precision " +
                Format(1, s.BonusPrecisionTotal / s.BonusCount) + "; mean jitter " +
                (s.BonusJitterCount == 0 ? "NA" : Format(2, s.BonusJitterTotal / s.BonusJitterCount));
        }

        private static string Format(int metric, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "NA";
            if (metric == 0) return value.ToString("0.00", Culture) + " s";
            if (metric == 2) return value.ToString("0.0", Culture) + " mm";
            if (metric == 5) return value.ToString("0.#", Culture) + " / " +
                (SpatialAccuracyMap.Rows * SpatialAccuracyMap.Columns);
            return value.ToString("0", Culture) + "%";
        }

        // Keep the bounded summary in one versioned local-settings string.
        private void Persist()
        {
            try
            {
                var rows = new List<string>();
                foreach (Session s in history)
                {
                    var f = new List<string> { s.Id };
                    for (int i = 0; i < 6; i++)
                    {
                        f.Add(s.Values[i].ToString("R", Culture)); f.Add(s.Counts[i].ToString(Culture));
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        f.Add(s.DirectionTrials[i].ToString(Culture)); f.Add(s.DirectionSuccesses[i].ToString(Culture));
                    }
                    f.Add(s.BonusCount.ToString(Culture)); f.Add(s.BonusPrecisionTotal.ToString("R", Culture));
                    f.Add(s.BonusJitterCount.ToString(Culture)); f.Add(s.BonusJitterTotal.ToString("R", Culture));
                    rows.Add(string.Join(",", f));
                }
                ApplicationData.Current.LocalSettings.Values[StorageKey] = string.Join(";", rows);
            }
            catch (Exception) { /* Storage must not block selection or Exit. */ }
        }
    }
}
