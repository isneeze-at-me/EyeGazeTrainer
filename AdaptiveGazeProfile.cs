using System;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Persistent approximation of gaze precision based on the raw, unassisted
    /// location at each successful training-target acquisition. This is NOT a
    /// clinical accuracy measure and does not track or save gaze coordinates.
    /// Smoothing and enlarged hitboxes never inflate the measured distance.
    /// </summary>
    internal sealed class AdaptiveGazeProfile
    {
        public const double BeginnerHitBoxScale = 1.10;
        public const double ExperiencedHitBoxScale = 1.00;
        public const int BeginnerSmoothingLevel =
            MasterCursorSmoothingFilter.InitialSmoothingLevel;

        private const string ScoreKey = "AdaptiveGaze.AccuracyScoreV1";
        private const string SamplesKey = "AdaptiveGaze.AcquisitionsV1";
        private const int GraceSamples = 8;
        private const int FullAdaptationSamples = 56;

        private double accuracyScore;
        private int acquisitions;
        private readonly ApplicationDataContainerSettings settings;

        public double AccuracyScore { get { return accuracyScore; } }
        public int Acquisitions { get { return acquisitions; } }

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
                    if (!double.IsNaN(score) && !double.IsInfinity(score) &&
                        score >= 0 && score <= 100 && count >= 0)
                    {
                        accuracyScore = score;
                        acquisitions = count;
                    }
                }
            }
            catch (Exception)
            {
                // Missing/corrupt local settings must not block eye-gaze use.
                accuracyScore = 0;
                acquisitions = 0;
            }
        }

        /// <summary>
        /// Record one target activation using its RAW distance from the
        /// visible center. Scores 100 at <=25% of visible radius and tends
        /// to zero by 110% of radius. Never records mere hover frames.
        /// </summary>
        public void RecordTargetAcquisition(double rawDistance, double visibleRadius)
        {
            if (double.IsNaN(rawDistance) || double.IsInfinity(rawDistance) ||
                rawDistance < 0 || visibleRadius <= 0)
                return;

            double normalized = rawDistance / visibleRadius;
            double precision = 100.0 *
                (1.0 - Math.Max(0, Math.Min(1, (normalized - 0.25) / 0.85)));

            // Smooth skill estimates over multiple successful targets.
            accuracyScore = acquisitions == 0 ? precision :
                (accuracyScore * 0.90 + precision * 0.10);
            if (acquisitions < int.MaxValue) acquisitions++;

            try
            {
                if (settings != null)
                {
                    settings[ScoreKey] = accuracyScore;
                    settings[SamplesKey] = acquisitions;
                }
            }
            catch (Exception)
            {
                // Storage problems must never block target feedback or Exit.
            }
        }
    }
}
