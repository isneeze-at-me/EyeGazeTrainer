// ============================================================================
// EyeGazeTrainer
// File: SharedButtonDwellWheel.cs
// Purpose: Renders the shared circular dwell indicator; callers own timing and activation.
// ============================================================================

using System;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Shared circular gaze-dwell wheel for buttons and training targets.
    /// Adapted from HomesteadInfluencer ApprovedDwellWheelControl:
    /// one neutral track + clockwise progress from 12 o'clock, no bars.
    /// The wheel is visual only: the page owns dwell timing and activation.
    /// </summary>
    internal sealed class SharedButtonDwellWheel
    {
        private const double Radius = 38;
        private const double Diameter = 2 * Radius;
        private const double TrackThickness = 6;
        private const double ProgressThickness = 7;
        private readonly Canvas overlay;
        private readonly Canvas wheel;
        private readonly Path progressArc;
        // Reuse the path and both arc segments rather than allocate a new
        // visual tree on every eye-tracker sample.
        private readonly PathFigure progressFigure;
        private readonly ArcSegment firstArc;
        private readonly ArcSegment secondArc;
        private double previousProgress = -1;
        private Point previousCenter = new Point(double.NaN, double.NaN);

        // Construct and attach a reusable, non-interactive wheel to the overlay canvas.
        public SharedButtonDwellWheel(Canvas overlay)
        {
            this.overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));

            wheel = new Canvas
            {
                Width = Diameter + 16,
                Height = Diameter + 16,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            double center = wheel.Width / 2;
            var track = new Ellipse
            {
                Width = Diameter,
                Height = Diameter,
                StrokeThickness = TrackThickness,
                Stroke = new SolidColorBrush(Color.FromArgb(235, 15, 18, 20)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(track, center - Radius);
            Canvas.SetTop(track, center - Radius);
            wheel.Children.Add(track);

            progressArc = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(255, 219, 230, 240)),
                StrokeThickness = ProgressThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            double arcCenter = wheel.Width * 0.5;
            firstArc = new ArcSegment
            {
                Size = new Size(Radius, Radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            };
            secondArc = new ArcSegment
            {
                Size = new Size(Radius, Radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            };
            progressFigure = new PathFigure
            {
                StartPoint = new Point(arcCenter, arcCenter - Radius),
                IsClosed = false
            };
            progressFigure.Segments.Add(firstArc);
            progressFigure.Segments.Add(secondArc);
            var progressGeometry = new PathGeometry();
            progressGeometry.Figures.Add(progressFigure);
            progressArc.Data = progressGeometry;
            wheel.Children.Add(progressArc);
            Canvas.SetZIndex(wheel, 100);
            overlay.Children.Add(wheel);
        }

        /// <summary>
        /// Draw the wheel centered on a registered Button in the page overlay.
        /// Uses the button's current layout transform, not a fixed screen point.
        /// </summary>
        public void Present(Button button, double progress)
        {
            if (button == null || !button.IsEnabled ||
                button.Visibility != Visibility.Visible ||
                button.ActualWidth <= 0 || button.ActualHeight <= 0)
            {
                Hide();
                return;
            }

            Point origin = button.TransformToVisual(overlay)
                                 .TransformPoint(new Point(0, 0));
            PresentAt(new Point(origin.X + button.ActualWidth * 0.5,
                                origin.Y + button.ActualHeight * 0.5), progress);
        }

        /// <summary>
        /// Use the identical radial wheel for non-button training targets.
        /// Center coordinates are relative to the shared cursor overlay.
        /// </summary>
        public void PresentAt(Point centerPoint, double progress)
        {
            if (double.IsNaN(centerPoint.X) || double.IsNaN(centerPoint.Y) ||
                double.IsInfinity(centerPoint.X) || double.IsInfinity(centerPoint.Y))
            {
                Hide();
                return;
            }

            if (previousCenter.X != centerPoint.X ||
                previousCenter.Y != centerPoint.Y)
            {
                Canvas.SetLeft(wheel, centerPoint.X - wheel.Width * 0.5);
                Canvas.SetTop(wheel, centerPoint.Y - wheel.Height * 0.5);
                previousCenter = centerPoint;
            }

            // Avoid expensive composition updates for negligible changes.
            // Dwell timing/activation are handled by the caller, not here.
            double p = Math.Max(0, Math.Min(1, progress));
            double rendered = Math.Round(p * 120) / 120;
            if (rendered != previousProgress)
            {
                previousProgress = rendered;
                double center = wheel.Width * 0.5;
                double totalSweep = rendered * Math.PI * 2;
                double firstSweep = Math.Min(Math.PI, totalSweep);
                double secondSweep = Math.Max(0, totalSweep - Math.PI);

                firstArc.Point = PolarPoint(center, firstSweep);
                // Keep two arcs so a full 360-degree dwell stays visible.
                secondArc.Point = PolarPoint(center, firstSweep + secondSweep);
                // At 0 progress, keep the arc path invisible rather than
                // recreating/destroying its geometry each frame.
                progressArc.Visibility = rendered <= 0
                    ? Visibility.Collapsed : Visibility.Visible;
            }
            wheel.Visibility = Visibility.Visible;
        }

        // Convert a clockwise angle from twelve o'clock to a point on the progress ring.
        private static Point PolarPoint(double center, double clockwiseRadians)
        {
            return new Point(center + Radius * Math.Sin(clockwiseRadians),
                             center - Radius * Math.Cos(clockwiseRadians));
        }

        // Hide the indicator and invalidate cached progress and position.
        public void Hide()
        {
            wheel.Visibility = Visibility.Collapsed;
            previousProgress = -1;
            previousCenter = new Point(double.NaN, double.NaN);
        }

        // Clear the visual state for a fresh dwell attempt.
        public void Reset()
        {
            Hide();
        }
    }
}
