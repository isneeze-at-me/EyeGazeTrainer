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
        private static string focusRequest;
        private static string focusAck;
        private static IntPtr priorWindow;
        private static uint priorPid;
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        private static bool EligiblePriorWindow(IntPtr hwnd, uint pid)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd) ||
                IsIconic(hwnd) || IsTarget(hwnd)) return false;
            uint actual;
            GetWindowThreadProcessId(hwnd, out actual);
            if (actual != pid || actual == 0) return false;
            int cloaked;
            if (DwmGetWindowAttribute(hwnd, 14, out cloaked, 4) != 0 || cloaked != 0) return false;
            try
            {
                using (var process = Process.GetProcessById((int)actual))
                {
                    string n = process.ProcessName.ToLowerInvariant();
                    return !n.Contains("tobii") && !n.Contains("dynavox") &&
                        !n.Contains("tdcontrol") && !n.Contains("eyeassist") &&
                        !n.Contains("eyegaze") && n != "explorer" &&
                        n != "applicationframehost" && n != "startmenuexperiencehost" &&
                        n != "shellexperiencehost" && n != "searchhost";
                }
            }
            catch { return false; }
        }

        private static void ReadPriorWindow(string folder)
        {
            try
            {
                string path = Path.Combine(folder, "foreground-before-launch.txt");
                if (!File.Exists(path) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds > 15) return;
                var parts = File.ReadAllText(path).Trim().Split('|');
                long handle; uint pid;
                if (parts.Length == 2 && long.TryParse(parts[0], out handle) &&
                    uint.TryParse(parts[1], out pid) && EligiblePriorWindow(new IntPtr(handle), pid))
                {
                    priorWindow = new IntPtr(handle);
                    priorPid = pid;
                    Log("Prior foreground captured by monitor; hwnd=" + handle + "; pid=" + pid);
                }
            }
            catch (Exception ex) { Log("Prior foreground unavailable: " + ex.Message); }
        }

        private static void CompleteForegroundHandoff()
        {
            // Style is already restored; this helper will never reapply it.
            if (focusRequest == null || focusAck == null) return;
            var wait = Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < 6000 && !File.Exists(focusRequest)) Thread.Sleep(50);
            if (!File.Exists(focusRequest)) { Log("Foreground request not received"); return; }
            string result = "SKIPPED";
            try
            {
                IntPtr current = GetForegroundWindow();
                if (EligiblePriorWindow(priorWindow, priorPid) && IsTarget(current))
                {
                    bool accepted = SetForegroundWindow(priorWindow);
                    Thread.Sleep(100);
                    bool verified = GetForegroundWindow() == priorWindow;
                    result = verified ? "VERIFIED" : "NOT_VERIFIED";
                    Log("Foreground restore; accepted=" + accepted + "; verified=" + verified);
                }
                else Log("Foreground restore skipped: no eligible prior window or trainer no longer foreground");
            }
            catch (Exception ex) { result = "FAILED"; Log("Foreground restore error: " + ex.Message); }
            try { File.WriteAllText(focusAck, result); } catch (Exception ex) { Log("Foreground ack error: " + ex.Message); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(ref int length, StringBuilder name);

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
                focusRequest = Path.Combine(folder, "overlay-focus-" + session + ".request");
                focusAck = Path.Combine(folder, "overlay-focus-" + session + ".ack");
                ReadPriorWindow(folder);
                Log("Restore channel ready; session=" + session);
            }
            catch (Exception ex) { Log("Restore channel unavailable: " + ex); }
        }

        private static bool RestoreRequested()
        {
            return restoreRequest != null && File.Exists(restoreRequest);
        }

        private static void AcknowledgeRestore(string result)
        {
            try { File.WriteAllText(restoreAck, result); Log("RESTORE ACK written: " + result); }
            catch (Exception ex) { Log("RESTORE ACK error: " + ex); }
        }
        private static readonly string LogName = "overlay-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Process.GetCurrentProcess().Id + ".txt";

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
        private static void Main()
        {
            Log("Companion started");
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
                        if (result != "FAILED") CompleteForegroundHandoff();
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
