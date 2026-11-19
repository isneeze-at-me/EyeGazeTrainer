using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EyeGazeTrainer.OverlayCompanion
{
    // Narrow, reversible TD Control interoperability helper. Never touches
    // TD Control's process, windows, settings, or eye-tracker device.
    internal static class Program
    {
        private const int GwlExStyle = -20;
        // Tobii's published gaze-aware convention uses the extended 0x2
        // window flag. Because it overlaps WS_EX_NOPARENTNOTIFY, only apply
        // it to the verified EyeGazeTrainer foreground window.
        private const int TobiiGazeAwareBit = 0x2;
        private const string GazeAwareProperty = "eyegaze:aware";
        private const string HeartbeatFile = "eyegazetrainer-gaze-active.signal";
        private const int ErrorInsufficientBuffer = 122;
        private const int AppMaxSeconds = 8 * 60 * 60;
        private const double FreshSampleSeconds = 2.5;
        private const double LossGraceSeconds = 3.5;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int length);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetProp(IntPtr hwnd, string property, IntPtr value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr RemoveProp(IntPtr hwnd, string property);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(
            ref int packageFamilyNameLength, StringBuilder packageFamilyName);

        [STAThread]
        private static void Main()
        {
            string heartbeatPath = FindHeartbeatPath();
            // Fail closed if package identity or shared signal cannot be
            // resolved. Without verified live gaze, never mark the window.
            if (heartbeatPath == null) return;

            IntPtr marked = IntPtr.Zero;
            int originalStyle = 0;
            bool changedStyle = false;
            bool markerPropertySet = false;
            bool blockedForSession = false;
            DateTime started = DateTime.UtcNow;
            DateTime gazeLostSince = DateTime.MinValue;
            DateTime lastSeen = DateTime.MinValue;
            IntPtr appWindow = IntPtr.Zero;
            try
            {
                while ((DateTime.UtcNow - started).TotalSeconds < AppMaxSeconds)
                {
                    IntPtr window = GetForegroundWindow();
                    bool target = IsTarget(window);
                    bool gazeActive = RecentGaze(heartbeatPath);

                    if (marked != IntPtr.Zero && (!target || window != marked))
                    {
                        Restore(marked, originalStyle, changedStyle, markerPropertySet);
                        marked = IntPtr.Zero;
                        changedStyle = false;
                        markerPropertySet = false;
                    }

                    if (target)
                    {
                        lastSeen = DateTime.UtcNow;
                        appWindow = window;
                        if (marked != IntPtr.Zero)
                        {
                            if (gazeActive)
                            {
                                gazeLostSince = DateTime.MinValue;
                            }
                            else
                            {
                                if (gazeLostSince == DateTime.MinValue)
                                    gazeLostSince = DateTime.UtcNow;
                                if ((DateTime.UtcNow - gazeLostSince).TotalSeconds
                                    >= LossGraceSeconds)
                                {
                                    // The marker may have made TD Control
                                    // release the camera. Undo it and DO NOT
                                    // try again this session, preserving input.
                                    Restore(marked, originalStyle,
                                            changedStyle, markerPropertySet);
                                    marked = IntPtr.Zero;
                                    changedStyle = false;
                                    markerPropertySet = false;
                                    blockedForSession = true;
                                }
                            }
                        }
                        else if (!blockedForSession && gazeActive)
                        {
                            originalStyle = GetWindowLong(window, GwlExStyle);
                            changedStyle = (originalStyle & TobiiGazeAwareBit) == 0;
                            if (changedStyle)
                            {
                                SetWindowLong(window, GwlExStyle,
                                              originalStyle | TobiiGazeAwareBit);
                                changedStyle =
                                    (GetWindowLong(window, GwlExStyle) &
                                     TobiiGazeAwareBit) != 0;
                            }
                            markerPropertySet = SetProp(window, GazeAwareProperty,
                                                        new IntPtr(1));
                            // With both markers unavailable, do not record
                            // ownership over an unchanged window.
                            if (changedStyle || markerPropertySet)
                            {
                                marked = window;
                                gazeLostSince = DateTime.MinValue;
                            }
                            else blockedForSession = true;
                        }
                    }
                    else if (appWindow != IntPtr.Zero &&
                             !IsWindow(appWindow))
                    {
                        // Exit when EyeGazeTrainer's window truly closes.
                        // Merely switching apps must not terminate the
                        // helper or prevent automatic restoration on return.
                        break;
                    }
                    if (lastSeen == DateTime.MinValue &&
                        (DateTime.UtcNow - started).TotalSeconds > 45)
                        break;
                    Thread.Sleep(250);
                }
            }
            finally
            {
                if (marked != IntPtr.Zero)
                    Restore(marked, originalStyle, changedStyle, markerPropertySet);
            }
        }

        private static string FindHeartbeatPath()
        {
            try
            {
                int length = 0;
                int result = GetCurrentPackageFamilyName(ref length, null);
                if (result != ErrorInsufficientBuffer || length <= 1) return null;
                var buffer = new StringBuilder(length);
                if (GetCurrentPackageFamilyName(ref length, buffer) != 0)
                    return null;
                string local = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "Packages", buffer.ToString(),
                                    "LocalState", HeartbeatFile);
            }
            catch { return null; }
        }

        private static bool RecentGaze(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                double age = (DateTime.UtcNow - File.GetLastWriteTimeUtc(path))
                    .TotalSeconds;
                return age >= -1 && age <= FreshSampleSeconds;
            }
            catch { return false; }
        }

        private static bool IsTarget(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
            var type = new StringBuilder(128);
            var title = new StringBuilder(256);
            GetClassName(hwnd, type, type.Capacity);
            if (!string.Equals(type.ToString(), "ApplicationFrameWindow",
                               StringComparison.Ordinal)) return false;
            GetWindowText(hwnd, title, title.Capacity);
            return string.Equals(title.ToString(), "Eye Gaze Trainer",
                                 StringComparison.Ordinal) ||
                   string.Equals(title.ToString(), "Eye Gaze API Viability",
                                 StringComparison.Ordinal);
        }

        private static void Restore(IntPtr window, int originalStyle,
                                    bool changedStyle, bool markerPropertySet)
        {
            if (!IsWindow(window)) return;
            if (markerPropertySet) RemoveProp(window, GazeAwareProperty);
            // Only clear the bit we installed, and preserve every other
            // extended-style flag that Windows or another app may have set.
            if (changedStyle)
            {
                int current = GetWindowLong(window, GwlExStyle);
                SetWindowLong(window, GwlExStyle,
                              current & ~TobiiGazeAwareBit);
            }
        }
    }
}
