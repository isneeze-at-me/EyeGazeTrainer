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

            Canvas.SetLeft(wheel, centerPoint.X - wheel.Width * 0.5);
            Canvas.SetTop(wheel, centerPoint.Y - wheel.Height * 0.5);

            double p = Math.Max(0, Math.Min(1, progress));
            double center = wheel.Width * 0.5;
            if (p <= 0)
            {
                progressArc.Data = null;
            }
            else
            {
                // UWP ArcSegment cannot represent a full circle with one arc.
                // Construct two arcs, preserving a complete visible ring at 100%.
                double firstSweep = Math.Min(Math.PI, p * Math.PI * 2);
                double secondSweep = Math.Max(0, p * Math.PI * 2 - Math.PI);
                var start = new Point(center, center - Radius);
                var firstEnd = PolarPoint(center, firstSweep);
                var figure = new PathFigure { StartPoint = start, IsClosed = false };
                figure.Segments.Add(new ArcSegment
                {
                    Point = firstEnd,
                    Size = new Size(Radius, Radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = false
                });
                if (secondSweep > 0)
                {
                    figure.Segments.Add(new ArcSegment
                    {
                        Point = PolarPoint(center, firstSweep + secondSweep),
                        Size = new Size(Radius, Radius),
                        SweepDirection = SweepDirection.Clockwise,
                        IsLargeArc = secondSweep > Math.PI
                    });
                }
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                progressArc.Data = geometry;
            }
            wheel.Visibility = Visibility.Visible;
        }

        private static Point PolarPoint(double center, double clockwiseRadians)
        {
            return new Point(center + Radius * Math.Sin(clockwiseRadians),
                             center - Radius * Math.Cos(clockwiseRadians));
        }

        public void Hide()
        {
            progressArc.Data = null;
            wheel.Visibility = Visibility.Collapsed;
        }

        public void Reset()
        {
            Hide();
        }
    }
}
