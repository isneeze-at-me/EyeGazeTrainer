using System;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Persistent, provisional dwell-time recommendation based on deliberate
    /// Level 1 target holds. It never changes active target/button dwell timing.
    /// Fixed-duration tasks cannot establish a clinically optimal dwell time.
    /// </summary>
    internal sealed class DwellRecommendation
    {
        private const string Prefix = "EyeGazeTrainer.DwellRecommendation.V1.";
        public const int InitialMilliseconds = 500;
        private const int MinimumMilliseconds = 500;
        private const int MaximumMilliseconds = 1500;
        private const int WindowSize = 12;
        private int recommendation = InitialMilliseconds;
        private int totalSuccesses;
        private int totalInterruptions;
        private int windowSuccesses;
        private int windowInterruptions;
        private readonly Windows.Foundation.Collections.IPropertySet settings;

        public int Milliseconds { get { return recommendation; } }
        public int SuccessfulAttempts { get { return totalSuccesses; } }
        public int InterruptedAttempts { get { return totalInterruptions; } }
        public bool HasEvidence { get { return totalSuccesses + totalInterruptions >= WindowSize; } }

        public DwellRecommendation()
        {
            try
            {
                settings = ApplicationData.Current.LocalSettings.Values;
                recommendation = Read("Milliseconds", InitialMilliseconds, MinimumMilliseconds, MaximumMilliseconds);
                totalSuccesses = Read("Successes", 0, 0, int.MaxValue);
                totalInterruptions = Read("Interruptions", 0, 0, int.MaxValue);
                windowSuccesses = Read("WindowSuccesses", 0, 0, WindowSize - 1);
                windowInterruptions = Read("WindowInterruptions", 0, 0, WindowSize - 1);
                if (windowSuccesses + windowInterruptions >= WindowSize)
                    windowSuccesses = windowInterruptions = 0;
            }
            catch (Exception) { /* Invalid settings never prevent gaze use. */ }
        }

        public void RecordCompletedHold() { Record(true); }
        public void RecordInterruptedHold() { Record(false); }

        private void Record(bool completed)
        {
            if (completed)
            {
                if (totalSuccesses < int.MaxValue) totalSuccesses++;
                windowSuccesses++;
            }
            else
            {
                if (totalInterruptions < int.MaxValue) totalInterruptions++;
                windowInterruptions++;
            }

            if (windowSuccesses + windowInterruptions >= WindowSize)
            {
                double interruptedFraction = windowInterruptions / (double)WindowSize;
                // Conservative 50 ms steps; interruptions suggest more time
                // may help, while reliable holds permit a cautious reduction.
                if (interruptedFraction >= 0.30)
                    recommendation = Math.Min(MaximumMilliseconds, recommendation + 50);
                else if (interruptedFraction <= 0.08)
                    recommendation = Math.Max(MinimumMilliseconds, recommendation - 50);
                windowSuccesses = 0;
                windowInterruptions = 0;
            }
            Save();
        }

        private int Read(string key, int fallback, int minimum, int maximum)
        {
            object value;
            if (settings == null || !settings.TryGetValue(Prefix + key, out value))
                return fallback;
            int number = Convert.ToInt32(value);
            return number >= minimum && number <= maximum ? number : fallback;
        }

        private void Save()
        {
            try
            {
                if (settings == null) return;
                settings[Prefix + "Milliseconds"] = recommendation;
                settings[Prefix + "Successes"] = totalSuccesses;
                settings[Prefix + "Interruptions"] = totalInterruptions;
                settings[Prefix + "WindowSuccesses"] = windowSuccesses;
                settings[Prefix + "WindowInterruptions"] = windowInterruptions;
            }
            catch (Exception) { /* Storage must not block gaze or Exit. */ }
        }
    }
}
