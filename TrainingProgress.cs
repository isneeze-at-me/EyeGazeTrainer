using System;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Permanent (per app-user) completion flags. A level is completed only
    /// after all of its training targets are actually acquired.
    /// </summary>
    internal sealed class TrainingProgress
    {
        private const string KeyPrefix = "EyeGazeTrainer.LevelCompleted.V1.";
        public bool IsCompleted(int level)
        {
            if (level < 1 || level > 6) return false;
            try
            {
                object value;
                return ApplicationData.Current.LocalSettings.Values.TryGetValue(
                    KeyPrefix + level, out value) && value is bool && (bool)value;
            }
            catch (Exception) { return false; }
        }

        public void MarkCompleted(int level)
        {
            if (level < 1 || level > 6) return;
            try
            {
                ApplicationData.Current.LocalSettings.Values[KeyPrefix + level] = true;
            }
            catch (Exception) { /* Storage failure must not prevent gaze interaction. */ }
        }
    }
}
