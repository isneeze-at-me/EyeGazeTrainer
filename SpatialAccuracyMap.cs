using System;
using System.Globalization;
using Windows.Foundation;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Stores only 35 aggregate location scores; never stores gaze positions.
    /// A measured acquisition contributes its overall performance (0-100).
    /// An interrupted deliberate target dwell contributes zero. Empty cells
    /// remain NA. Scores are descriptive training statistics, not calibrated
    /// clinical gaze-accuracy percentages.
    /// </summary>
    internal sealed class SpatialAccuracyMap
    {
        public const int Rows = 5;
        public const int Columns = 7;
        private const string StorageKey = "EyeGazeTrainer.SpatialAccuracy.V2";
        private const string LegacyStorageKey = "EyeGazeTrainer.SpatialAccuracy.V1";
        private readonly double[,] totals = new double[Rows, Columns];
        private readonly int[,] attempts = new int[Rows, Columns];

        public SpatialAccuracyMap()
        {
            try
            {
                object saved;
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.TryGetValue(StorageKey, out saved))
                {
                    Load(Convert.ToString(saved), Rows);
                }
                else if (settings.TryGetValue(LegacyStorageKey, out saved) &&
                         Load(Convert.ToString(saved), 7))
                {
                    // Legacy scores had seven rows. Without historical raw gaze
                    // coordinates, place each old cell's aggregate in the new
                    // row containing its center; preserve totals and counts.
                    Persist();
                }
            }
            catch (Exception) { /* Missing settings must not interrupt training. */ }
        }

        private bool Load(string saved, int sourceRows)
        {
            if (string.IsNullOrEmpty(saved)) return false;
            string[] cells = saved.Split(';');
            if (cells.Length != sourceRows * Columns) return false;
            var newTotals = new double[Rows, Columns];
            var newAttempts = new int[Rows, Columns];
            for (int i = 0; i < cells.Length; i++)
            {
                string[] parts = cells[i].Split(',');
                if (parts.Length != 2) return false;
                double total;
                int count;
                if (!double.TryParse(parts[0], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out total) ||
                    !int.TryParse(parts[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out count) ||
                    count < 0 || double.IsNaN(total) ||
                    double.IsInfinity(total) || total < 0 ||
                    total > count * 100.0 + 0.001) return false;

                int oldRow = i / Columns;
                int col = i % Columns;
                int row = Math.Min(Rows - 1,
                    (int)((oldRow + 0.5) * Rows / sourceRows));
                if (newAttempts[row, col] > int.MaxValue - count) return false;
                newAttempts[row, col] += count;
                newTotals[row, col] += total;
            }
            Array.Copy(newTotals, totals, newTotals.Length);
            Array.Copy(newAttempts, attempts, newAttempts.Length);
            return true;
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
