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
        public const int InitialSmoothingLevel = 85;
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

        public void Reset()
        {
            initialized = false;
            lastTime = default(DateTimeOffset);
        }

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
