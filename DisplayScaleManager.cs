using System;
using Windows.Graphics.Display;
using Windows.Storage;

namespace EyeGazeApiViability
{
    /// <summary>
    /// Shared physical-size conversion, with an optional 85 mm PCEye 5
    /// middle-camera-area reference. The reference length is provisional.
    /// No ruler or Tobii calibration APIs are used.
    /// </summary>
    internal sealed class DisplayScaleManager
    {
        public const double ReferenceWidthMillimeters = 85.0;
        public const double LevelOneDiameterMillimeters = 55.0;
        public const double BullseyeDiameterMillimeters = 3.5;
        private const string SettingPrefix = "DisplaySizeAdjustment.V1.";
        private const double MinimumViewUnitsPerMillimeter = 0.05;
        private const double MaximumViewUnitsPerMillimeter = 30.0;

        private readonly string displayKey;
        private double viewUnitsPerMillimeter;

        public double ViewUnitsPerMillimeter { get { return viewUnitsPerMillimeter; } }
        public double ReferenceBarViewWidth { get { return ReferenceWidthMillimeters * viewUnitsPerMillimeter; } }
        public bool HasSavedAdjustment { get; private set; }

        public DisplayScaleManager()
        {
            DisplayInformation display = DisplayInformation.GetForCurrentView();
            double pixelsPerView = Math.Max(1.0, display.RawPixelsPerViewPixel);
            double rawDpi = display.RawDpiX;
            double dpiInViewUnits = rawDpi > 0 && !double.IsNaN(rawDpi) &&
                                    !double.IsInfinity(rawDpi)
                ? rawDpi / pixelsPerView : 96.0;
            double automatic = dpiInViewUnits / 25.4;
            viewUnitsPerMillimeter = Clamp(automatic, MinimumViewUnitsPerMillimeter,
                                          MaximumViewUnitsPerMillimeter);

            // UWP does not expose a reliable cross-session monitor hardware ID.
            // Approximate a per-display key using pixel dimensions, DPI and scale.
            // Two identical displays can share this setting.
            displayKey = SettingPrefix +
                display.ScreenWidthInRawPixels + "x" +
                display.ScreenHeightInRawPixels + "_" +
                Math.Round(rawDpi) + "_" + Math.Round(pixelsPerView * 100);
            try
            {
                object saved;
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(displayKey, out saved))
                {
                    double value = Convert.ToDouble(saved);
                    if (IsValid(value))
                    {
                        viewUnitsPerMillimeter = value;
                        HasSavedAdjustment = true;
                    }
                }
            }
            catch (Exception) { /* No storage issue should prevent eye-gaze usage. */ }
        }

        public double ToViewUnits(double millimeters)
        {
            return millimeters * viewUnitsPerMillimeter;
        }

        public void SaveReferenceWidth(double displayedWidth)
        {
            double units = displayedWidth / ReferenceWidthMillimeters;
            if (!IsValid(units)) throw new ArgumentOutOfRangeException(nameof(displayedWidth));
            ApplicationData.Current.LocalSettings.Values[displayKey] = units;
            viewUnitsPerMillimeter = units;
            HasSavedAdjustment = true;
        }

        private static bool IsValid(double value)
        {
            return value >= MinimumViewUnitsPerMillimeter &&
                   value <= MaximumViewUnitsPerMillimeter &&
                   !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
