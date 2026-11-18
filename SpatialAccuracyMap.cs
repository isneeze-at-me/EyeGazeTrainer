using System;
using System.Globalization;
using Windows.Foundation;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Stores only 49 aggregate location scores; never stores gaze positions.
    /// A measured acquisition contributes its overall performance (0-100).
    /// An interrupted deliberate target dwell contributes zero. Empty cells
    /// remain NA. Scores are descriptive training statistics, not calibrated
    /// clinical gaze-accuracy percentages.
    /// </summary>
    internal sealed class SpatialAccuracyMap
    {
        public const int Rows = 7;
        public const int Columns = 7;
        private const string StorageKey = "EyeGazeTrainer.SpatialAccuracy.V1";
        private readonly double[,] totals = new double[Rows, Columns];
        private readonly int[,] attempts = new int[Rows, Columns];

        public SpatialAccuracyMap()
        {
            try
            {
                object saved;
                if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(
                    StorageKey, out saved)) return;
                string[] cells = Convert.ToString(saved).Split(';');
                if (cells.Length != Rows * Columns) return;
                for (int i = 0; i < cells.Length; i++)
                {
                    string[] parts = cells[i].Split(',');
                    if (parts.Length != 2) return;
                    double total;
                    int count;
                    if (!double.TryParse(parts[0], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out total) ||
                        !int.TryParse(parts[1], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out count) ||
                        count < 0 || double.IsNaN(total) ||
                        double.IsInfinity(total) || total < 0 ||
                        total > count * 100.0 + 0.001) return;
                    totals[i / Columns, i % Columns] = total;
                    attempts[i / Columns, i % Columns] = count;
                }
            }
            catch (Exception) { /* Missing settings must not interrupt training. */ }
        }

        public double? Score(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns ||
                attempts[row, column] == 0) return null;
            return totals[row, column] / attempts[row, column];
        }

        public int Count(int row, int column)
        {
            return attempts[row, column];
        }

        public void Record(Point center, double screenWidth, double screenHeight,
                           double score)
        {
            if (screenWidth <= 0 || screenHeight <= 0 ||
                double.IsNaN(center.X) || double.IsNaN(center.Y) ||
                double.IsInfinity(center.X) || double.IsInfinity(center.Y) ||
                center.X < 0 || center.Y < 0 ||
                center.X > screenWidth || center.Y > screenHeight ||
                double.IsNaN(score) || double.IsInfinity(score)) return;

            int col = Math.Min(Columns - 1, (int)(center.X * Columns / screenWidth));
            int row = Math.Min(Rows - 1, (int)(center.Y * Rows / screenHeight));
            if (attempts[row, col] == int.MaxValue) return;
            totals[row, col] += Math.Max(0, Math.Min(100, score));
            attempts[row, col]++;
            Persist();
        }

        private void Persist()
        {
            try
            {
                var values = new string[Rows * Columns];
                for (int row = 0; row < Rows; row++)
                    for (int col = 0; col < Columns; col++)
                        values[row * Columns + col] =
                            totals[row, col].ToString("R", CultureInfo.InvariantCulture) +
                            "," + attempts[row, col].ToString(CultureInfo.InvariantCulture);
                ApplicationData.Current.LocalSettings.Values[StorageKey] =
                    string.Join(";", values);
            }
            catch (Exception) { /* Failed persistence does not block gaze. */ }
        }
    }
}
