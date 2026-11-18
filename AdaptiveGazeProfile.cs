using System;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Persistent approximation of gaze precision based on the raw, unassisted
    /// location during successful training-target acquisition. This is NOT a
    /// clinical accuracy measure and does not track or save gaze coordinates.
    /// Precision, speed, and stability are combined independently of visual smoothing.
    /// </summary>
    internal sealed class AdaptiveGazeProfile
    {
        public const double BeginnerHitBoxScale = 1.02;
        public const double ExperiencedHitBoxScale = 1.00;
        public const int BeginnerSmoothingLevel =
            MasterCursorSmoothingFilter.InitialSmoothingLevel;

        // Version 3 retains existing V2 skill component scores, while
        // switching overall accuracy to asymmetric, level-aware updates.
        private const string ScoreKey = "AdaptiveGaze.AccuracyScoreV3";
        private const string LegacyScoreKey = "AdaptiveGaze.AccuracyScoreV2";
        private const string PrecisionKey = "AdaptiveGaze.PrecisionV2";
        private const string SpeedKey = "AdaptiveGaze.SpeedV2";
        private const string StabilityKey = "AdaptiveGaze.StabilityV2";
        private const string SamplesKey = "AdaptiveGaze.AcquisitionsV2";
        private const int GraceSamples = 8;
        private const int FullAdaptationSamples = 56;

        private double accuracyScore;
        private int acquisitions;
        private double precisionScore;
        private double speedScore;
        private double stabilityScore;
        private readonly Windows.Foundation.Collections.IPropertySet settings;

        public double AccuracyScore { get { return accuracyScore; } }
        public int Acquisitions { get { return acquisitions; } }
        public double PrecisionScore { get { return precisionScore; } }
        public double SpeedScore { get { return speedScore; } }
        public double StabilityScore { get { return stabilityScore; } }

        // Initially retain all assistance. A confidence ramp prevents a few
        // lucky hits from abruptly making early training harder.
        private double AdaptationFraction
        {
            get
            {
                if (acquisitions <= GraceSamples) return 0;
                double confidence = Math.Min(1.0,
                    (acquisitions - GraceSamples) /
                    (double)(FullAdaptationSamples - GraceSamples));
                return confidence * Math.Max(0, Math.Min(1, accuracyScore / 100.0));
            }
        }

        public int SmoothingLevel
        {
            get
            {
                return Math.Max(0, Math.Min(99,
                    (int)Math.Round(BeginnerSmoothingLevel *
                                    (1.0 - AdaptationFraction))));
            }
        }

        public double HitBoxScale
        {
            get
            {
                return BeginnerHitBoxScale -
                    (BeginnerHitBoxScale - ExperiencedHitBoxScale) *
                    AdaptationFraction;
            }
        }

        public AdaptiveGazeProfile()
        {
            try
            {
                settings = ApplicationData.Current.LocalSettings.Values;
                object savedScore;
                object savedSamples;
                // Existing users keep their V2 score until the first new
                // training outcome. Do not reset learned adaptive history.
                bool hasScore = settings.TryGetValue(ScoreKey, out savedScore) ||
                                settings.TryGetValue(LegacyScoreKey, out savedScore);
                if (hasScore && settings.TryGetValue(SamplesKey, out savedSamples))
                {
                    double score = Convert.ToDouble(savedScore);
                    int count = Convert.ToInt32(savedSamples);
                    object p, s, t;
                    if (Valid(score) && count >= 0 &&
                        settings.TryGetValue(PrecisionKey, out p) &&
                        settings.TryGetValue(SpeedKey, out s) &&
                        settings.TryGetValue(StabilityKey, out t) &&
                        Valid(Convert.ToDouble(p)) &&
                        Valid(Convert.ToDouble(s)) &&
                        Valid(Convert.ToDouble(t)))
                    {
                        accuracyScore = score;
                        acquisitions = count;
                        precisionScore = Convert.ToDouble(p);
                        speedScore = Convert.ToDouble(s);
                        stabilityScore = Convert.ToDouble(t);
                    }
                }
            }
            catch (Exception)
            {
                // Missing/corrupt local settings must not block eye-gaze use.
                accuracyScore = 0;
                acquisitions = 0;
                precisionScore = speedScore = stabilityScore = 0;
            }
        }

        // Level 1 deliberately has the smallest potential improvement,
        // strongest decline, and lowest attainable ceiling. Advanced
        // levels make it possible to earn the highest accuracy scores.
        private static double SuccessGain(int level)
        {
            return 0.07 + (ClampLevel(level) - 1) * 0.022;
        }

        private static double WeakPerformanceLoss(int level)
        {
            return 0.22 - (ClampLevel(level) - 1) * 0.024;
        }

        private static double MissLoss(int level)
        {
            return 0.14 - (ClampLevel(level) - 1) * 0.018;
        }

        private static double LevelCeiling(int level)
        {
            return 65 + (ClampLevel(level) - 1) * 7;
        }

        private static int ClampLevel(int level)
        {
            return Math.Max(1, Math.Min(6, level));
        }

        /// <summary>
        /// Record a completed target. Only the composite accuracy display
        /// changes; existing precision/speed/stability aggregates remain
        /// available internally for performance measurement. Gains are
        /// slow and level-capped, while poor completed performances have
        /// a stronger negative influence on the lower levels.
        /// </summary>
        public void RecordTargetPerformance(TargetPerformance result, int level = 1)
        {
            if (result == null) return;
            bool first = acquisitions == 0;
            precisionScore = first ? result.Precision :
                precisionScore * 0.90 + result.Precision * 0.10;
            speedScore = first ? result.Speed :
                speedScore * 0.90 + result.Speed * 0.10;
            stabilityScore = first ? result.Stability :
                stabilityScore * 0.90 + result.Stability * 0.10;

            double observed = result.Overall;
            if (observed > accuracyScore)
            {
                // Never erase an existing high score just because a
                // beginner lesson has a lower allowed gain ceiling.
                double increase = (observed - accuracyScore) * SuccessGain(level);
                accuracyScore += Math.Min(increase,
                    Math.Max(0, LevelCeiling(level) - accuracyScore));
            }
            else
            {
                accuracyScore += (observed - accuracyScore) *
                                 WeakPerformanceLoss(level);
            }
            accuracyScore = ClampScore(accuracyScore);
            if (acquisitions < int.MaxValue) acquisitions++;
            Persist();
        }

        /// <summary>
        /// An interrupted deliberate dwell counts as one unsuccessful
        /// attempt. Multiple out-of-target samples after one break must
        /// never repeat the penalty; the target measurement enforces this.
        /// No raw positions, gaze history or missed coordinates are saved.
        /// </summary>
        public void RecordIncompleteTargetDwell(int level = 1)
        {
            if (acquisitions == 0) return; // no earned score to reduce
            accuracyScore = ClampScore(accuracyScore * (1 - MissLoss(level)));
            Persist();
        }

        private void Persist()
        {
            try
            {
                if (settings != null)
                {
                    settings[ScoreKey] = accuracyScore;
                    settings[PrecisionKey] = precisionScore;
                    settings[SpeedKey] = speedScore;
                    settings[StabilityKey] = stabilityScore;
                    settings[SamplesKey] = acquisitions;
                }
            }
            catch (Exception)
            {
                // Storage failures must never interrupt training or Exit.
            }
        }

        private static double ClampScore(double value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private static bool Valid(double score)
        {
            return !double.IsNaN(score) && !double.IsInfinity(score) &&
                   score >= 0 && score <= 100;
        }
    }
}
