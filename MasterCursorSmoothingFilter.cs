// ============================================================================
// EyeGazeTrainer
// File: MasterCursorSmoothingFilter.cs
// Purpose: Stabilizes raw gaze coordinates using time-based adaptive smoothing.
// ============================================================================

using System;
using Windows.Foundation;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Reusable time-based cursor stabilizer. The single SmoothingLevel value
    /// (0..99) governs all normal gaze cursor smoothing. No gaze APIs here.
    /// Raw and smoothed positions are deliberately kept separate.
    /// </summary>
    internal sealed class MasterCursorSmoothingFilter
    {
        public const int InitialSmoothingLevel = 75;
        public const double MaximumHalfLifeSeconds = 0.12;
        public const double IntentionalMoveSnapDistance = 160;
        public const double LostSampleResetSeconds = 0.5;

        private Point lastPoint;
        private DateTimeOffset lastTime;
        private bool initialized;
        private int smoothingLevel = InitialSmoothingLevel;

        public int SmoothingLevel
        {
            get { return smoothingLevel; }
            set
            {
                if (value < 0 || value > 99)
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "Smoothing must be in the 0–99 range.");
                smoothingLevel = value;
            }
        }

        // Assistance controller: compare paired raw and filtered stationary
        // hold windows. These statistics are never exported as eye jitter.
        private object holdKey;
        private DateTimeOffset holdStarted, windowStarted, windowLast;
        private int windowCount;
        private double rawX, rawY, filteredX, filteredY, rawM2, filteredM2;
        private int quietWindows;
        private bool hasJitterEvidence;

        // Restore a learned filter only when it was saved from valid jitter
        // evidence. With no evidence, always start at 75 percent.
        public MasterCursorSmoothingFilter()
        {
            try
            {
                object value;
                if (Windows.Storage.ApplicationData.Current.LocalSettings.Values
                    .TryGetValue("EyeGazeTrainer.JitterFilter.V1", out value))
                {
                    int saved = Convert.ToInt32(value);
                    if (saved >= 0 && saved <= 99)
                    {
                        smoothingLevel = saved;
                        hasJitterEvidence = true;
                    }
                }
            }
            catch { /* A settings failure must not prevent gaze use. */ }
        }

        // Observe only deliberate stationary holds, after a settling period.
        // Raw displacement between samples is bounded to reject gaze shifts.
        // Return true only when assistance changes, for numeric trend logging.
        public bool ObserveHold(object key, Point raw, Point filtered,
                                DateTimeOffset now, double viewUnitsPerMm)
        {
            if (key == null || viewUnitsPerMm <= 0 ||
                double.IsNaN(viewUnitsPerMm) || double.IsInfinity(viewUnitsPerMm))
            {
                EndHold();
                return false;
            }
            if (!ReferenceEquals(holdKey, key) ||
                (windowLast != default(DateTimeOffset) &&
                 (now - windowLast).TotalMilliseconds > 250))
            {
                EndHold();
                holdKey = key;
                holdStarted = now;
            }
            windowLast = now;
            if ((now - holdStarted).TotalMilliseconds < 250) return false;
            if (windowCount == 0) windowStarted = now;
            // Large travel away from the stationary window indicates a new
            // fixation or an outlier, not a useful jitter calibration.
            if (windowCount > 0)
            {
                double dx = raw.X - rawX, dy = raw.Y - rawY;
                if (dx * dx + dy * dy > Math.Pow(20 * viewUnitsPerMm, 2))
                {
                    ClearWindow();
                    holdStarted = now;
                    return false;
                }
            }
            windowCount++;
            double rx = raw.X - rawX, ry = raw.Y - rawY;
            rawX += rx / windowCount; rawY += ry / windowCount;
            rawM2 += rx * (raw.X - rawX) + ry * (raw.Y - rawY);
            double fx = filtered.X - filteredX, fy = filtered.Y - filteredY;
            filteredX += fx / windowCount; filteredY += fy / windowCount;
            filteredM2 += fx * (filtered.X - filteredX) + fy * (filtered.Y - filteredY);
            if ((now - windowStarted).TotalMilliseconds < 500 || windowCount < 8) return false;

            double nativeMm = Math.Sqrt(Math.Max(0, rawM2 / windowCount)) / viewUnitsPerMm;
            double cursorMm = Math.Sqrt(Math.Max(0, filteredM2 / windowCount)) / viewUnitsPerMm;
            ClearWindow();
            if (double.IsNaN(nativeMm) || double.IsInfinity(nativeMm) ||
                double.IsNaN(cursorMm) || double.IsInfinity(cursorMm)) return false;
            int old = smoothingLevel;
            // Use feedback rather than assuming a percent reduces jitter by
            // that same percent. Increase quickly if over 1 mm; decrease
            // slowly only after several quiet windows to avoid oscillation.
            if (cursorMm > 1.0 && nativeMm > 1.0)
            {
                quietWindows = 0;
                smoothingLevel = Math.Min(99, smoothingLevel + (cursorMm > 2 ? 8 : 4));
            }
            else if (cursorMm < .65 && nativeMm < 1.0)
            {
                if (++quietWindows >= 3)
                {
                    smoothingLevel = Math.Max(0, smoothingLevel - 3);
                    quietWindows = 0;
                }
            }
            else quietWindows = 0;

            bool first = !hasJitterEvidence;
            hasJitterEvidence = true;
            if (first || old != smoothingLevel)
            {
                try
                {
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values
                        ["EyeGazeTrainer.JitterFilter.V1"] = smoothingLevel;
                }
                catch { /* Keep the in-memory assistance setting. */ }
            }
            return old != smoothingLevel;
        }

        public void EndHold()
        {
            holdKey = null;
            holdStarted = windowLast = default(DateTimeOffset);
            ClearWindow();
        }

        private void ClearWindow()
        {
            windowCount = 0;
            rawX = rawY = filteredX = filteredY = rawM2 = filteredM2 = 0;
            windowStarted = default(DateTimeOffset);
        }

        // Discard previous timing and position so the next valid sample starts a fresh stream.
        public void Reset()
        {
            initialized = false;
            lastTime = default(DateTimeOffset);
            EndHold();
        }

        // Filter a raw sample using elapsed time; reset after gaps and snap deliberate large movements.
        public Point Update(Point raw, DateTimeOffset timestamp)
        {
            if (double.IsNaN(raw.X) || double.IsNaN(raw.Y) ||
                double.IsInfinity(raw.X) || double.IsInfinity(raw.Y))
                return initialized ? lastPoint : raw;

            if (!initialized || timestamp <= lastTime ||
                (timestamp - lastTime).TotalSeconds > LostSampleResetSeconds ||
                smoothingLevel == 0)
            {
                lastPoint = raw;
                lastTime = timestamp;
                initialized = true;
                return raw;
            }

            double dx = raw.X - lastPoint.X;
            double dy = raw.Y - lastPoint.Y;
            if (dx * dx + dy * dy >=
                IntentionalMoveSnapDistance * IntentionalMoveSnapDistance)
            {
                lastPoint = raw;
            }
            else
            {
                double strength = smoothingLevel / 99.0;
                // Scale squared so early high settings give strong stabilization
                // while the lower portion responds promptly.
                double halfLife = MaximumHalfLifeSeconds * strength * strength;
                double elapsed = (timestamp - lastTime).TotalSeconds;
                double alpha = halfLife <= 0 ? 1.0 :
                    1.0 - Math.Pow(0.5, elapsed / halfLife);
                lastPoint = new Point(lastPoint.X + dx * alpha,
                                      lastPoint.Y + dy * alpha);
            }

            lastTime = timestamp;
            return lastPoint;
        }
    }
}
