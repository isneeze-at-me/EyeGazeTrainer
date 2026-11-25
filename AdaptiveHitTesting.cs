// ============================================================================
// EyeGazeTrainer
// File: AdaptiveHitTesting.cs
// Purpose: Tests gaze against scaled circular and rectangular interaction regions without resizing visible controls.
// ============================================================================

using System;
using Windows.Foundation;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Shared target acquisition geometry using the current learner's
    /// assistance percentage. This does not change visible object sizes.
    /// All gaze buttons use the shared hit region, including Exit. For safety,\n    /// Exit also accepts raw gaze when smoothing cannot keep up.
    /// </summary>
    internal static class AdaptiveHitTesting
    {
        // Compare squared gaze distance with the effective circular hit radius.
        public static bool ContainsCircle(Point gaze, Point center,
                                          double visibleDiameter,
                                          double hitBoxScale)
        {
            if (!IsFinitePositive(visibleDiameter) ||
                !IsFinitePositive(hitBoxScale)) return false;

            double radius = visibleDiameter * hitBoxScale * 0.5;
            double dx = gaze.X - center.X;
            double dy = gaze.Y - center.Y;
            return dx * dx + dy * dy <= radius * radius;
        }

        // Test gaze against the rectangle expanded or contracted about its center.
        public static bool ContainsRectangle(Point gaze, Rect visibleBounds,
                                             double hitBoxScale)
        {
            if (!IsFinitePositive(hitBoxScale) ||
                !IsFinitePositive(visibleBounds.Width) ||
                !IsFinitePositive(visibleBounds.Height))
                return false;

            double w = visibleBounds.Width * hitBoxScale;
            double h = visibleBounds.Height * hitBoxScale;
            double x = visibleBounds.X - (w - visibleBounds.Width) * 0.5;
            double y = visibleBounds.Y - (h - visibleBounds.Height) * 0.5;
            return gaze.X >= x && gaze.X <= x + w &&
                   gaze.Y >= y && gaze.Y <= y + h;
        }

        // Reject invalid dimensions and assistance scale factors.
        private static bool IsFinitePositive(double number)
        {
            return number > 0 && !double.IsNaN(number) &&
                   !double.IsInfinity(number);
        }
    }
}
