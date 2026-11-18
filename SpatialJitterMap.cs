using System;
using System.Globalization;
using Windows.Foundation;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Persistent 7x5 map of raw, unsmoothed settled gaze spread in millimeters.
    /// Only summed jitter and sample counts are retained; no gaze coordinates,
    /// timestamps, or per-attempt gaze traces are persisted.
    /// </summary>
    internal sealed class SpatialJitterMap
    {
        public const int Rows = SpatialAccuracyMap.Rows;
        public const int Columns = SpatialAccuracyMap.Columns;
        private const string StorageKey = "EyeGazeTrainer.SpatialJitter.V1";
        private readonly double[,] totals = new double[Rows, Columns];
        private readonly int[,] samples = new int[Rows, Columns];

        public SpatialJitterMap()
        {
            try
            {
                object saved;
                if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(
                    StorageKey, out saved)) return;
                var cells = Convert.ToString(saved).Split(';');
                if (cells.Length != Rows * Columns) return;

                var loadedTotals = new double[Rows, Columns];
                var loadedCounts = new int[Rows, Columns];
                for (int i = 0; i < cells.Length; i++)
                {
                    string[] fields = cells[i].Split(',');
                    if (fields.Length != 2) return;
                    double total;
                    int count;
                    if (!double.TryParse(fields[0], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out total) ||
                        !int.TryParse(fields[1], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out count) ||
                        count < 0 || double.IsNaN(total) ||
                        double.IsInfinity(total) || total < 0 ||
                        (count == 0 && total != 0)) return;
                    loadedTotals[i / Columns, i % Columns] = total;
                    loadedCounts[i / Columns, i % Columns] = count;
                }
                Array.Copy(loadedTotals, totals, loadedTotals.Length);
                Array.Copy(loadedCounts, samples, loadedCounts.Length);
            }
            catch (Exception)
            {
                // Local settings failures must not block gaze training.
            }
        }

        public double? AverageMillimeters(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns ||
                samples[row, col] == 0) return null;
            return totals[row, col] / samples[row, col];
        }

        public int Count(int row, int col)
        {
            return row >= 0 && row < Rows && col >= 0 && col < Columns
                ? samples[row, col] : 0;
        }

        public void Record(Point center, double width, double height,
                           double jitterMillimeters)
        {
            if (width <= 0 || height <= 0 ||
                double.IsNaN(center.X) || double.IsNaN(center.Y) ||
                double.IsInfinity(center.X) || double.IsInfinity(center.Y) ||
                center.X < 0 || center.Y < 0 ||
                center.X > width || center.Y > height ||
                double.IsNaN(jitterMillimeters) ||
                double.IsInfinity(jitterMillimeters) ||
                jitterMillimeters < 0) return;

            int col = Math.Min(Columns - 1, (int)(center.X * Columns / width));
            int row = Math.Min(Rows - 1, (int)(center.Y * Rows / height));
            if (samples[row, col] == int.MaxValue) return;
            totals[row, col] += jitterMillimeters;
            samples[row, col]++;
            Persist();
        }

        private void Persist()
        {
            try
            {
                var cells = new string[Rows * Columns];
                for (int row = 0; row < Rows; row++)
                    for (int col = 0; col < Columns; col++)
                        cells[row * Columns + col] =
                            totals[row, col].ToString("R", CultureInfo.InvariantCulture) +
                            "," + samples[row, col].ToString(CultureInfo.InvariantCulture);
                ApplicationData.Current.LocalSettings.Values[StorageKey] =
                    string.Join(";", cells);
            }
            catch (Exception) { /* Keep interaction responsive without storage. */ }
        }
    }
}
