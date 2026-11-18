using System;
using System.Globalization;
using System.Text;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Human-readable, plain-text snapshot of available local training data.
    /// Never exports raw gaze coordinates, event traces, or identifying data.
    /// All unimplemented metrics are explicitly listed as not recorded.
    /// </summary>
    internal static class TherapistTextReport
    {
        private const int Width = 78;
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static string Build(
            DateTimeOffset generatedAt,
            TrainingProgress progress,
            TrainingReport report,
            AdaptiveGazeProfile gaze,
            SpatialAccuracyMap accuracyMap,
            SpatialJitterMap jitterMap,
            int smoothingPercent,
            double hitboxScale,
            int targetHoldMilliseconds,
            int controlDwellMilliseconds,
            string[][] skillCategories,
            string[] categoryNames)
        {
            var b = new StringBuilder(12000);
            b.AppendLine(new string('=', Width));
            Center(b, "EYEGAZETRAINER");
            Center(b, "THERAPIST TRAINING REPORT");
            b.AppendLine(new string('=', Width));
            b.AppendLine("Generated: " + generatedAt.ToString("yyyy-MM-dd HH:mm zzz", Culture));
            b.AppendLine("Format: Plain-text training summary | Report version: 1");
            b.AppendLine();
            b.AppendLine("PURPOSE");
            b.AppendLine("  Training observations intended to support AAC access discussions.");
            b.AppendLine("  These are not clinical measurements or a diagnostic assessment.");
            b.AppendLine("  No personally identifying data or raw gaze coordinates are included.");
            b.AppendLine("  NA = not available / no valid recorded measurements.");
            b.AppendLine("  Trends use recent recorded observations; they are not clinical findings.");
            b.AppendLine();

            Section(b, "1. TRAINING OVERVIEW");
            int complete = 0;
            for (int level = 1; level <= 6; level++)
                if (progress.IsCompleted(level)) complete++;
            Field(b, "Unique levels completed", complete + " / 6 current level slots");
            Field(b, "Completed level runs", report.CompletedRuns.ToString(Culture));
            Field(b, "Total recorded practice", report.PracticeHours.ToString("0.00", Culture) + " hours");
            Field(b, "Gaze acquisitions recorded", gaze.Acquisitions.ToString(Culture));
            b.AppendLine("  Note: Older sessions before run/time recording cannot be reconstructed.");
            b.AppendLine();

            Section(b, "2. PERFORMANCE AND TRENDS");
            Field(b, "Overall adaptive score", gaze.Acquisitions > 0 ?
                Pct(gaze.AccuracyScore) : "NA", " " + Trend(report.OverallTrend));
            Field(b, "Latest target precision", report.LatestAccuracy.HasValue ?
                Pct(report.LatestAccuracy.Value) : "NA",
                " " + Trend(report.AccuracyTrend));
            Field(b, "Latest settled gaze jitter", report.LatestJitter.HasValue ?
                report.LatestJitter.Value.ToString("0.0", Culture) + " mm" : "NA",
                " " + Trend(report.JitterTrend));
            Field(b, "Current cursor smoothing", smoothingPercent.ToString(Culture) + "%",
                " " + NumericTrend(report.SmoothingTrend));
            Field(b, "Precision component (smoothed history)",
                gaze.Acquisitions > 0 ? Pct(gaze.PrecisionScore) : "NA");
            Field(b, "Speed component (smoothed history)",
                gaze.Acquisitions > 0 ? Pct(gaze.SpeedScore) : "NA");
            Field(b, "Stability component (smoothed history)",
                gaze.Acquisitions > 0 ? Pct(gaze.StabilityScore) : "NA");
            b.AppendLine("  Trend method: latest 5 observations vs preceding 5; minimum 10.");
            b.AppendLine("  Improving / Declining reflect score or jitter direction.");
            b.AppendLine("  Smoothing Increasing / Decreasing is numeric, not necessarily good/bad.");
            b.AppendLine("  Overall adaptive score is level-capped and level-weighted.");
            b.AppendLine("  Latest target precision is not the same as overall accuracy.");
            b.AppendLine("  Eye jitter is estimated raw gaze-position spread on the screen.");
            b.AppendLine();

            Section(b, "3. CURRENT ASSISTANCE SETTINGS");
            Field(b, "Cursor smoothing", smoothingPercent + "% (range 0-99)");
            Field(b, "Adaptive hitbox scale",
                hitboxScale.ToString("0.000", Culture) + "x");
            Field(b, "Level 1 target hold", targetHoldMilliseconds + " ms");
            Field(b, "Menu/button dwell", controlDwellMilliseconds + " ms");
            b.AppendLine("  Note: These are current settings, not historical session snapshots.");
            b.AppendLine();

            Section(b, "4. SCREEN LOCATION MAPS");
            b.AppendLine("  7 columns x 5 rows; row 1 is the top of the screen.");
            b.AppendLine("  Columns progress left to right. NA means no sample in a cell.");
            b.AppendLine();
            b.AppendLine("  ACCURACY MAP -- average composite training outcome (%)");
            WriteMap(b, (r, c) => accuracyMap.Score(r, c).HasValue
                ? accuracyMap.Score(r, c).Value.ToString("0", Culture) + "%" : "NA");
            b.AppendLine("  Cell observations (attempt counts):");
            WriteMap(b, (r, c) => CountOrNA(accuracyMap.Count(r, c)));
            b.AppendLine("  Completed target outcomes include speed/precision/stability;");
            b.AppendLine("  deliberately interrupted target holds can contribute zero.");
            b.AppendLine();
            b.AppendLine("  JITTER MAP -- mean RMS raw gaze spread (approximate mm)");
            WriteMap(b, (r, c) => jitterMap.AverageMillimeters(r, c).HasValue
                ? jitterMap.AverageMillimeters(r, c).Value.ToString("0.0", Culture) : "NA");
            b.AppendLine("  Cell observations (valid measured holds):");
            WriteMap(b, (r, c) => CountOrNA(jitterMap.Count(r, c)));
            b.AppendLine("  Values reflect tracking noise, display calibration and possible");
            b.AppendLine("  head movement as well as observed gaze control.");
            b.AppendLine();

            Section(b, "5. THERAPIST SKILL METRICS");
            b.AppendLine("  The following measures are planned but NOT YET RECORDED.");
            b.AppendLine("  The app does not infer unavailable measurements from other scores.");
            if (skillCategories != null && categoryNames != null)
            {
                for (int category = 0; category < skillCategories.Length &&
                    category < categoryNames.Length; category++)
                {
                    b.AppendLine();
                    b.AppendLine("  " + categoryNames[category].ToUpperInvariant());
                    foreach (string definition in skillCategories[category])
                    {
                        string[] parts = definition.Split(new[] { '|' }, 2);
                        b.AppendLine("    - " + parts[0] + ": NA (not recorded)");
                        if (parts.Length > 1)
                            Wrap(b, "      ", parts[1], Width - 8);
                    }
                }
            }
            b.AppendLine();

            Section(b, "6. INDIVIDUAL SESSIONS AND CONTEXT");
            Field(b, "Dated session summaries", "NA - not recorded yet");
            Field(b, "Session-to-session comparisons", "NA - not recorded yet");
            Field(b, "Early vs late session change", "NA - not recorded yet");
            Field(b, "Positioning / screen / lighting", "NA - not recorded yet");
            Field(b, "Self-reported comfort / fatigue", "NA - not recorded yet");
            b.AppendLine();

            Section(b, "7. HOW TO INTERPRET THIS REPORT");
            b.AppendLine("  - Low performance may reflect tracker placement, fatigue, lighting");
            b.AppendLine("    or task conditions, not solely eye-gaze skill.");
            b.AppendLine("  - Location-map percentages are descriptive training scores;");
            b.AppendLine("    do not treat them as calibrated gaze-point accuracy.");
            b.AppendLine("  - Recent trend changes should be confirmed in comparable tasks");
            b.AppendLine("    with similar target size, dwell time and filter settings.");
            b.AppendLine("  - The report is a local snapshot created only when requested.");
            b.AppendLine("  - It is not automatically uploaded, emailed or shared.");
            b.AppendLine();
            b.AppendLine(new string('=', Width));
            Center(b, "END OF REPORT");
            b.AppendLine(new string('=', Width));
            return b.ToString();
        }

        private static string Pct(double x) { return x.ToString("0", Culture) + "%"; }

        private static string CountOrNA(int count)
        {
            return count > 0 ? count.ToString(Culture) : "NA";
        }

        private static string Trend(int direction)
        {
            return direction > 0 ? "[Improving]" :
                   direction < 0 ? "[Declining]" : "[Stable / insufficient history]";
        }

        private static string NumericTrend(int direction)
        {
            return direction > 0 ? "[Increasing]" :
                   direction < 0 ? "[Decreasing]" : "[Stable / insufficient history]";
        }

        private static void Section(StringBuilder b, string heading)
        {
            b.AppendLine(heading);
            b.AppendLine(new string('-', Width));
        }

        private static void Center(StringBuilder b, string text)
        {
            b.AppendLine(new string(' ', Math.Max(0, (Width - text.Length) / 2)) + text);
        }

        private static void Field(StringBuilder b, string name, string value,
                                  string suffix = "")
        {
            b.AppendLine("  " + name.PadRight(35) + ": " + value + suffix);
        }

        private static void Wrap(StringBuilder b, string indent, string text, int width)
        {
            string line = indent;
            foreach (string word in text.Split(' '))
            {
                if (line.Length + word.Length + 1 > width && line.Trim().Length > 0)
                {
                    b.AppendLine(line.TrimEnd());
                    line = indent;
                }
                line += word + " ";
            }
            if (line.Trim().Length > 0) b.AppendLine(line.TrimEnd());
        }

        private static void WriteMap(StringBuilder b, Func<int, int, string> value)
        {
            b.AppendLine("        C1      C2      C3      C4      C5      C6      C7");
            for (int row = 0; row < SpatialAccuracyMap.Rows; row++)
            {
                b.Append("  R" + (row + 1) + " ");
                for (int col = 0; col < SpatialAccuracyMap.Columns; col++)
                    b.Append(value(row, col).PadLeft(8));
                b.AppendLine();
            }
        }
    }
}
