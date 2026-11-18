using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace EyeGazeApiViability
{
    /// <summary>
    /// One target acquisition measurement. A target is NOT acquired on first
    /// border contact: the gaze must stay in its (adaptive) hit region for
    /// the target hold duration. Raw samples are held only in memory.
    /// </summary>
    internal sealed class TrainingTargetMeasurement
    {
        public const int TargetHoldMilliseconds = 1000;
        private const int LostGazeMilliseconds = 300;
        private const int SettledWindowMilliseconds = 300;
        // Very short border flickers are not deliberate dwell attempts.
        private const int MinimumAttemptMilliseconds = 150;
        private const int MaximumSamples = 120;

        private struct GazeSample
        {
            public DateTimeOffset When;
            public Point Raw;
        }

        private readonly List<GazeSample> samples = new List<GazeSample>();
        private DateTimeOffset shownAt;
        private DateTimeOffset enteredAt;
        private DateTimeOffset lastSampleAt;
        private bool dwelling;
        private bool acquired;

        public double HoldProgress { get; private set; }

        public void BeginTarget(DateTimeOffset timestamp)
        {
            samples.Clear();
            shownAt = timestamp;
            enteredAt = default(DateTimeOffset);
            lastSampleAt = default(DateTimeOffset);
            dwelling = false;
            acquired = false;
            HoldProgress = 0;
        }

        /// <summary>
        /// A started but unfinished dwell is one miss after 150 ms of
        /// continuous inside-target gaze. Calling this repeatedly while
        /// outside cannot score duplicate misses, and ordinary target
        /// searching does not count as an attempt.
        /// </summary>
        public bool CancelIncompleteHold(DateTimeOffset now)
        {
            // Completion is only established by a valid inside-target gaze
            // sample. A long gap followed by leaving cannot count as success.
            bool missed = dwelling && !acquired &&
                (now - enteredAt).TotalMilliseconds >= MinimumAttemptMilliseconds;
            CancelHold();
            return missed;
        }

        public void CancelHold()
        {
            samples.Clear();
            dwelling = false;
            enteredAt = default(DateTimeOffset);
            lastSampleAt = default(DateTimeOffset);
            HoldProgress = 0;
        }

        public bool Observe(Point raw, DateTimeOffset now, bool inside)
        {
            if (shownAt == default(DateTimeOffset)) return false;
            if (acquired)
            {
                // Shared button dwell lasts 1500 ms, longer than target
                // acquisition's 1000 ms. Continue retaining recent raw gaze
                // samples so its eventual click can be measured accurately.
                if (inside && Valid(raw))
                {
                    samples.Add(new GazeSample { When = now, Raw = raw });
                    if (samples.Count > MaximumSamples) samples.RemoveAt(0);
                }
                return false;
            }
            if (!inside || !Valid(raw) ||
                (dwelling && (now - lastSampleAt).TotalMilliseconds > LostGazeMilliseconds))
            {
                CancelHold();
                return false;
            }

            if (!dwelling)
            {
                dwelling = true;
                enteredAt = now;
            }

            lastSampleAt = now;
            samples.Add(new GazeSample { When = now, Raw = raw });
            if (samples.Count > MaximumSamples) samples.RemoveAt(0);

            HoldProgress = Math.Max(0, Math.Min(1,
                (now - enteredAt).TotalMilliseconds / TargetHoldMilliseconds));
            if (HoldProgress < 1) return false;
            acquired = true;
            return true;
        }

        public TargetPerformance Measure(Point center, double radius, DateTimeOffset now)
        {
            if (!acquired || radius <= 0) throw new InvalidOperationException();

            // Score the settled gaze, not the first border crossing.
            // Use only recent raw samples for both precision and stability.
            DateTimeOffset cutoff = now.AddMilliseconds(-SettledWindowMilliseconds);
            int count = 0;
            double sx = 0, sy = 0, xx = 0, yy = 0;
            foreach (GazeSample sample in samples)
            {
                if (sample.When < cutoff) continue;
                count++;
                sx += sample.Raw.X;
                sy += sample.Raw.Y;
                xx += sample.Raw.X * sample.Raw.X;
                yy += sample.Raw.Y * sample.Raw.Y;
            }
            if (count == 0) throw new InvalidOperationException("No settled gaze samples");
            double mx = sx / count, my = sy / count;
            double dx = mx - center.X, dy = my - center.Y;
            double centerOffset = Math.Sqrt(dx * dx + dy * dy) / radius;
            double spread = Math.Sqrt(Math.Max(0,
                xx / count - mx * mx + yy / count - my * my)) / radius;

            // 100 at <=25% radius, 0 at the outer visible edge.
            double precision = 100 * (1 - Clamp01((centerOffset - 0.25) / 0.75));
            // Raw jitter: 100 at <=10% radius RMS, 0 at >=60%.
            double stability = 100 * (1 - Clamp01((spread - 0.10) / 0.50));
            // Includes search and required hold. A 1.5s acquisition earns
            // full speed credit; 8s or longer earns zero.
            double elapsed = Math.Max(0, (now - shownAt).TotalSeconds);
            double speed = 100 * (1 - Clamp01((elapsed - 1.5) / 6.5));

            return new TargetPerformance(precision, speed, stability);
        }

        private static bool Valid(Point p)
        {
            return !double.IsNaN(p.X) && !double.IsNaN(p.Y) &&
                   !double.IsInfinity(p.X) && !double.IsInfinity(p.Y);
        }

        private static double Clamp01(double x) { return Math.Max(0, Math.Min(1, x)); }
    }

    internal sealed class TargetPerformance
    {
        public double Precision { get; private set; }
        public double Speed { get; private set; }
        public double Stability { get; private set; }
        public double Overall { get { return Precision * 0.40 + Speed * 0.30 + Stability * 0.30; } }

        public TargetPerformance(double precision, double speed, double stability)
        {
            Precision = Clamp(precision);
            Speed = Clamp(speed);
            Stability = Clamp(stability);
        }

        private static double Clamp(double n) { return Math.Max(0, Math.Min(100, n)); }
    }
}
