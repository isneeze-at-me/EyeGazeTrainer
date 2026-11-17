using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace EyeGazeApiViability
{
    // Developer-only mouse simulation of the first no-dwell/no-click learning skill.
    // Deliberately uses no Windows.Devices.Input.Preview classes.
    public sealed partial class LevelOnePreview : Page
    {
        private const double TargetDiameter = 116;
        private const double DotDiameter = 44;
        private static readonly Point[] TargetFractions =
        {
            new Point(0.25, 0.50),
            new Point(0.75, 0.50),
            new Point(0.50, 0.28),
            new Point(0.50, 0.72)
        };
        private int targetIndex = -1;
        private bool insideTarget;
        private bool completed;

        public LevelOnePreview()
        {
            InitializeComponent();
        }

        private void OnCanvasSized(object sender, SizeChangedEventArgs e)
        {
            RepositionTarget();
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (TrainingCanvas.ActualWidth <= DotDiameter ||
                TrainingCanvas.ActualHeight <= DotDiameter)
                return;

            // This position is Windows pointer input, never an eye-tracker sample.
            Point p = e.GetCurrentPoint(TrainingCanvas).Position;
            if (p.X < 0 || p.Y < 0 ||
                p.X > TrainingCanvas.ActualWidth ||
                p.Y > TrainingCanvas.ActualHeight) return;

            PreviewDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(PreviewDot, Clamp(p.X - DotDiameter / 2, 0,
                TrainingCanvas.ActualWidth - DotDiameter));
            Canvas.SetTop(PreviewDot, Clamp(p.Y - DotDiameter / 2, 0,
                TrainingCanvas.ActualHeight - DotDiameter));

            if (completed) return;

            if (targetIndex < 0)
            {
                targetIndex = 0;
                InstructionText.Text =
                    "Move the green circle into the yellow ring. No click is needed.";
                ProgressText.Text = "Target 1 of 4";
                RepositionTarget();
                return;
            }

            Point target = TargetCenter();
            double dx = p.X - target.X;
            double dy = p.Y - target.Y;
            bool inRing = dx * dx + dy * dy <= 58 * 58;

            // A single entry triggers success. No gaze hold, timer or dwell.
            if (inRing && !insideTarget)
            {
                insideTarget = true;
                targetIndex++;
                if (targetIndex >= TargetFractions.Length)
                {
                    completed = true;
                    TargetRing.Visibility = Visibility.Collapsed;
                    InstructionText.Text =
                        "Excellent. Moving your eyes can make things happen. " +
                        "This mouse preview is complete.";
                    ProgressText.Text = "LEVEL 1 PREVIEW COMPLETE — no eye tracker used";
                    return;
                }
                ProgressText.Text = "Target " + (targetIndex + 1) +
                                    " of " + TargetFractions.Length;
                RepositionTarget();
                // Clear entry state for next target; target position changes.
                insideTarget = false;
            }
            else if (!inRing)
            {
                insideTarget = false;
            }
        }

        private Point TargetCenter()
        {
            Point fraction = TargetFractions[Math.Max(0,
                Math.Min(targetIndex, TargetFractions.Length - 1))];
            double margin = TargetDiameter / 2;
            double x = margin + Math.Max(0, TrainingCanvas.ActualWidth -
                       TargetDiameter) * fraction.X;
            double y = margin + Math.Max(0, TrainingCanvas.ActualHeight -
                       TargetDiameter) * fraction.Y;
            return new Point(x, y);
        }

        private void RepositionTarget()
        {
            if (targetIndex < 0 || completed ||
                TrainingCanvas.ActualWidth < TargetDiameter ||
                TrainingCanvas.ActualHeight < TargetDiameter) return;
            Point p = TargetCenter();
            Canvas.SetLeft(TargetRing, p.X - TargetDiameter / 2);
            Canvas.SetTop(TargetRing, p.Y - TargetDiameter / 2);
            TargetRing.Visibility = Visibility.Visible;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private void OnBack(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
