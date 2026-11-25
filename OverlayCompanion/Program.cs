// ============================================================================
// EyeGazeTrainer
// File: OverlayCompanion/Program.cs
// Purpose: Full-trust helper that manages the trainer window's gaze-aware style and explicitly acknowledges restoration; does not control Tobii processes.
// ============================================================================

using System;
using System.IO;
using System.Diagnostics;
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
        private static string restoreRequest;
        private static string restoreAck;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(ref int length, StringBuilder name);

        // Resolve the package-local session and restoration request/acknowledgement files.
        private static void InitializeRestoreChannel()
        {
            try
            {
                int length = 0;
                GetCurrentPackageFamilyName(ref length, null);
                if (length <= 0) return;
                var name = new StringBuilder(length);
                if (GetCurrentPackageFamilyName(ref length, name) != 0) return;
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", name.ToString(), "LocalState");
                string session = File.ReadAllText(Path.Combine(folder, "overlay-session.txt")).Trim();
                if (session.Length == 0 || session.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;
                restoreRequest = Path.Combine(folder, "overlay-restore-" + session + ".request");
                restoreAck = Path.Combine(folder, "overlay-restore-" + session + ".ack");
                // No previous application is activated in this experiment.
                Log("Restore channel ready; session=" + session);
            }
            catch (Exception ex) { Log("Restore channel unavailable: " + ex); }
        }

        // Check for the current session's explicit restore request.
        private static bool RestoreRequested()
        {
            return restoreRequest != null && File.Exists(restoreRequest);
        }

        // Write the restoration outcome for the UWP application's bounded wait.
        private static void AcknowledgeRestore(string result)
        {
            try { File.WriteAllText(restoreAck, result); Log("RESTORE ACK written: " + result); }
            catch (Exception ex) { Log("RESTORE ACK error: " + ex); }
        }
        private static readonly string LogName = "overlay-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Process.GetCurrentProcess().Id + ".txt";

        // Append companion lifecycle observations to its diagnostic trace.
        private static void Log(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EyeGazeTrainer", "Diagnostics");
                Directory.CreateDirectory(directory);
                byte[] bytes = Encoding.UTF8.GetBytes(DateTimeOffset.Now.ToString("O") + " | OVERLAY | " + message + "\r\n");
                using (var stream = new FileStream(Path.Combine(directory, LogName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
            }
            catch (Exception) { /* Diagnostics never alter the handoff. */ }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
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
        // Monitor the trainer window, apply the gaze-aware style, and restore it before ending the session.
        private static void Main()
        {
            Log("Companion started; build=2026-11-24-native-close-v2");
            InitializeRestoreChannel();
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
                    // A shutdown request is terminal for this helper instance.
                    // Never apply the style again after acknowledging restoration.
                    if (RestoreRequested())
                    {
                        Log("Explicit restore request received");
                        string result = "NOT_CHANGED";
                        if (changed != IntPtr.Zero)
                        {
                            result = Restore(changed) ? "RESTORED" : "FAILED";
                            changed = IntPtr.Zero;
                        }
                        AcknowledgeRestore(result);
                        Log("Automatic foreground activation disabled; Windows handles focus");
                        break;
                    }
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
                        Log("Restore requested: trainer foreground=" + foregroundIsTrainer + "; target changed=" + (target != changed));
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
                                Log("BEGIN set gaze-aware bit; original=0x" + style.ToString("X8"));
                                int previous = SetWindowLong(target, GwlExStyle,
                                              style | GazeAwareBit);
                                int setError = Marshal.GetLastWin32Error();
                                Log("END set gaze-aware bit; previous=" + previous + "; lastError=" + setError);
                                int confirmed = GetWindowLong(target, GwlExStyle);
                                if ((confirmed & GazeAwareBit) != 0)
                                    changed = target;
                            }
                        }
                    }
                    if (foregroundIsTrainer && !foregroundSeen)
                    {
                        Log("Trainer foreground first observed");
                        foregroundSeen = true;
                    }

                    if (seen != IntPtr.Zero && !IsWindow(seen))
                    {
                        if (missingSince == DateTime.MinValue)
                        {
                            missingSince = DateTime.UtcNow;
                            Log("Trainer window no longer exists");
                        }
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
            catch (Exception ex)
            {
                Log("Companion exception: " + ex);
                throw;
            }
            finally
            {
                if (changed != IntPtr.Zero) Restore(changed);
                Log("Companion exiting");
            }
        }

        // Locate an eligible visible training window when needed.
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

        // Validate the candidate window against the trainer's identity.
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

        // Restore the original gaze-aware style bit and verify the result before reporting success.
        private static bool Restore(IntPtr hWnd)
        {
            if (!IsWindow(hWnd) || !IsTarget(hWnd)) { Log("Restore skipped: trainer window no longer exists or no longer matches"); return false; }
            int style = GetWindowLong(hWnd, GwlExStyle);
            Log("BEGIN restore gaze-aware bit; style=0x" + style.ToString("X8"));
            if ((style & GazeAwareBit) != 0)
            {
                int previous = SetWindowLong(hWnd, GwlExStyle, style & ~GazeAwareBit);
                int error = Marshal.GetLastWin32Error();
                Log("Restore previous=" + previous + "; lastError=" + error);
            }
            int confirmed = GetWindowLong(hWnd, GwlExStyle);
            bool restored = IsWindow(hWnd) && (confirmed & GazeAwareBit) == 0;
            Log("END restore; style=0x" + confirmed.ToString("X8") + "; verified=" + restored);
            return restored;
        }
    }
}
