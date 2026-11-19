using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EyeGazeTrainer.OverlayCompanion
{
    // Full-trust packaged companion. Does NOT touch TD Control's processes,
    // settings, windows or the gaze device. Mutates only the target app window.
    internal static class Program
    {
        private const int GwlExStyle = -20;
        // Draft Tolt/Tobii gaze-aware convention. Also WS_EX_NOPARENTNOTIFY.
        private const int GazeAwareBit = 0x2;
        private const int MaxSeconds = 8 * 60 * 60;
        // Published gaze-aware interoperability marker. TD Control may
        // hide its toolbar when the foreground window advertises this bit.
        private const string GazeAwareProperty = "eyegaze:aware";

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetProp(IntPtr hWnd, string property, IntPtr value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr RemoveProp(IntPtr hWnd, string property);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder buffer, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int length);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [STAThread]
        private static void Main()
        {
            IntPtr changed = IntPtr.Zero;
            IntPtr seen = IntPtr.Zero;
             DateTime started = DateTime.UtcNow;
            DateTime missingSince = DateTime.MinValue;
            try
            {
                while ((DateTime.UtcNow - started).TotalSeconds < MaxSeconds)
                {
                    IntPtr foreground = GetForegroundWindow();
                    bool target = IsTarget(foreground);
                    if (changed != IntPtr.Zero && (foreground != changed || !target))
                    {
                        Restore(changed);
                        changed = IntPtr.Zero;
                    }

                    if (target)
                    {
                        // Full-screen sizing is owned by the UWP app.
                        // Do not force a maximized window here; it can
                        // undo UWP's immersive full-screen presentation.
                        seen = foreground;
                        missingSince = DateTime.MinValue;
                        if (changed == IntPtr.Zero)
                        {
                            int style = GetWindowLong(foreground, GwlExStyle);
                            if ((style & GazeAwareBit) == 0)
                            {
                                Marshal.GetLastWin32Error();
                                SetWindowLong(foreground, GwlExStyle, style | GazeAwareBit);
                                int confirmed = GetWindowLong(foreground, GwlExStyle);
                                if ((confirmed & GazeAwareBit) != 0)
                                {
                                    // Advertise both the draft standardized
                                    // marker and Tobii's legacy 0x2 bit.
                                    // Only ever touch our exact app window.
                                    SetProp(foreground, GazeAwareProperty, new IntPtr(1));
                                    changed = foreground;
                                }
                            }
                        }
                    }
                    else if (seen != IntPtr.Zero && !IsWindow(seen))
                    {
                        if (missingSince == DateTime.MinValue) missingSince = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - missingSince).TotalSeconds >= 2) break;
                    }
                    else if (seen != IntPtr.Zero)
                    {
                        missingSince = DateTime.MinValue;
                    }
                    // If packaging fails to give the expected title/class, exit
                    // without touching any other windows.
                    if (seen == IntPtr.Zero &&
                        (DateTime.UtcNow - started).TotalSeconds >= 40) break;
                    Thread.Sleep(250);
                }
            }
            finally
            {
                if (changed != IntPtr.Zero) Restore(changed);
            }
        }

        private static bool IsTarget(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
            var className = new StringBuilder(128);
            var title = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            if (!string.Equals(className.ToString(), "ApplicationFrameWindow",
                               StringComparison.Ordinal)) return false;
            GetWindowText(hWnd, title, title.Capacity);
            // App manifest was renamed to Eye Gaze Trainer. The old
            // hard-coded name prevented this helper from matching at all.
            return string.Equals(title.ToString(), "Eye Gaze Trainer",
                                 StringComparison.Ordinal) ||
                   string.Equals(title.ToString(), "Eye Gaze API Viability",
                                 StringComparison.Ordinal);
        }

        private static void Restore(IntPtr hWnd)
        {
            if (!IsWindow(hWnd)) return;
            RemoveProp(hWnd, GazeAwareProperty);
            int style = GetWindowLong(hWnd, GwlExStyle);
            if ((style & GazeAwareBit) != 0)
                SetWindowLong(hWnd, GwlExStyle, style & ~GazeAwareBit);
        }
    }
}
