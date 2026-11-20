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
        private const int SwMaximize = 3;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        private delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback,
                                               IntPtr lParam);
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
            IntPtr maximized = IntPtr.Zero;
            bool foregroundSeen = false;
            DateTime started = DateTime.UtcNow;
            DateTime missingSince = DateTime.MinValue;
            try
            {
                while ((DateTime.UtcNow - started).TotalSeconds < MaxSeconds)
                {
                    IntPtr foreground = GetForegroundWindow();
                    bool foregroundIsTrainer = IsTarget(foreground);

                    // Apply the gaze-aware window style as soon as our own
                    // visible ApplicationFrameWindow exists, not only after
                    // Windows grants it foreground focus. On the first launch
                    // the foreground grant can arrive after the gaze page is
                    // already visible, making the overlay disappear only when
                    // the user clicks a menu button.
                    IntPtr target = foregroundIsTrainer
                        ? foreground : (seen == IntPtr.Zero
                            ? FindVisibleTrainerWindow() : seen);
                    if (target != IntPtr.Zero && !IsWindow(target))
                        target = IntPtr.Zero;
                    if (target != IntPtr.Zero && !IsTarget(target))
                        target = IntPtr.Zero;

                    // After foreground has been observed once, honor normal
                    // application switching by restoring the original style.
                    // Before that first activation, don't undo the initial
                    // handoff just because Windows still reports TD Control
                    // (or another application) as foreground.
                    if (changed != IntPtr.Zero &&
                        (target != changed ||
                         (foregroundSeen && !foregroundIsTrainer)))
                    {
                        Restore(changed);
                        changed = IntPtr.Zero;
                    }

                    if (target != IntPtr.Zero &&
                        (!foregroundSeen || foregroundIsTrainer))
                    {
                        if (target != maximized)
                        {
                            ShowWindow(target, SwMaximize);
                            maximized = target;
                        }
                        seen = target;
                        missingSince = DateTime.MinValue;
                        if (changed == IntPtr.Zero)
                        {
                            int style = GetWindowLong(target, GwlExStyle);
                            if ((style & GazeAwareBit) == 0)
                            {
                                SetWindowLong(target, GwlExStyle,
                                              style | GazeAwareBit);
                                int confirmed = GetWindowLong(target, GwlExStyle);
                                if ((confirmed & GazeAwareBit) != 0)
                                    changed = target;
                            }
                        }
                    }
                    if (foregroundIsTrainer)
                        foregroundSeen = true;

                    if (seen != IntPtr.Zero && !IsWindow(seen))
                    {
                        if (missingSince == DateTime.MinValue)
                            missingSince = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - missingSince).TotalSeconds >= 2)
                            break;
                    }
                    else if (seen != IntPtr.Zero)
                    {
                        missingSince = DateTime.MinValue;
                    }

                    if (seen == IntPtr.Zero &&
                        (DateTime.UtcNow - started).TotalSeconds >= 40)
                        break;
                    Thread.Sleep(250);
                }
            }
            finally
            {
                if (changed != IntPtr.Zero) Restore(changed);
            }
        }

        private static IntPtr FindVisibleTrainerWindow()
        {
            IntPtr found = IntPtr.Zero;
            // Only the already allowlisted application frame is eligible.
            // Do not enumerate or alter TD Control windows themselves.
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || !IsTarget(hWnd)) return true;
                found = hWnd;
                return false;
            }, IntPtr.Zero);
            return found;
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
            return string.Equals(title.ToString(), "Eye Gaze Trainer",
                                 StringComparison.Ordinal) ||
                   string.Equals(title.ToString(), "Eye Gaze API Viability",
                                 StringComparison.Ordinal);
        }

        private static void Restore(IntPtr hWnd)
        {
            if (!IsWindow(hWnd)) return;
            int style = GetWindowLong(hWnd, GwlExStyle);
            if ((style & GazeAwareBit) != 0)
                SetWindowLong(hWnd, GwlExStyle, style & ~GazeAwareBit);
        }
    }
}
