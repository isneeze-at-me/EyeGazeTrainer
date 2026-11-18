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

        private const string ScoreKey = "AdaptiveGaze.AccuracyScoreV2";
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
                if (settings.TryGetValue(ScoreKey, out savedScore) &&
                    settings.TryGetValue(SamplesKey, out savedSamples))
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

        /// <summary>
        /// Update each skill metric once per completed training-target hold.
        /// Never save raw gaze coordinates or frame-by-frame measurements.
        /// V2 keys deliberately do not inherit old first-border-entry scores.
        /// </summary>
        public void RecordTargetPerformance(TargetPerformance result)
        {
            if (result == null) return;
            bool first = acquisitions == 0;
            precisionScore = first ? result.Precision :
                precisionScore * 0.90 + result.Precision * 0.10;
            speedScore = first ? result.Speed :
                speedScore * 0.90 + result.Speed * 0.10;
            stabilityScore = first ? result.Stability :
                stabilityScore * 0.90 + result.Stability * 0.10;
            accuracyScore = precisionScore * 0.40 +
                            speedScore * 0.30 +
                            stabilityScore * 0.30;
            if (acquisitions < int.MaxValue) acquisitions++;

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
                // Storage failure must never block training or the Exit button.
            }
        }

        private static bool Valid(double score)
        {
            return !double.IsNaN(score) && !double.IsInfinity(score) &&
                   score >= 0 && score <= 100;
        }
    }
}
