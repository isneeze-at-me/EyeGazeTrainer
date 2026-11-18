using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace EyeGazeApiViability
{
    // Level 0 runs inside the one gaze-owning page. No new watcher or teardown.
    public sealed partial class LevelOnePreview
    {
        private const string LevelZeroDoneKey = "EyeGazeTrainer.LevelZero.CompletedV1";
        private enum LevelZeroStage { Explore, Pursue, Continue }
        private LevelZeroStage levelZeroStage;
        private DispatcherTimer levelZeroClock;
        private DateTimeOffset levelZeroStarted;
        private DateTimeOffset levelZeroStageStarted;
        private DateTimeOffset levelZeroLastPrompt;
        private DateTimeOffset levelZeroLastValid;
        private double levelZeroSeconds;
        private double levelZeroMinX = double.MaxValue, levelZeroMaxX;
        private double levelZeroMinY = double.MaxValue, levelZeroMaxY;
        private int levelZeroRegionMask;
        private int levelZeroCornerMask;
        private int levelZeroCornerCandidate = -1;
        private DateTimeOffset levelZeroCornerEntered;
        private int levelZeroCornerCandidateSamples;
        private DateTimeOffset levelZeroLastPursuitSample;
        private string levelZeroPendingInstruction;
        private int levelZeroPursuitSamples, levelZeroCloseSamples;
        private int levelZeroDwellAttempts;
        private bool levelZeroHadGaze;
        private bool levelZeroGuidanceStarted;
        private bool levelZeroHorizontalPrompted, levelZeroCornerPrompted, levelZeroFollowPrompted;
        private bool levelZeroSpeechBusy;
        private SpeechSynthesizer levelZeroSynth;
        private bool levelZeroSpeechUnavailable;

        private bool ShouldStartLevelZero()
        {
            try
            {
                object done;
                var values = ApplicationData.Current.LocalSettings.Values;
                if (values.TryGetValue(LevelZeroDoneKey, out done) &&
                    done is bool && (bool)done) return false;
                // Do not reroute established Level 1 users into new onboarding.
                return !trainingProgress.IsCompleted(1) &&
                       trainingReport.CompletedRuns == 0 &&
                       gazeProfile.Acquisitions == 0;
            }
            catch (Exception) { return false; }
        }

        private void StartLevelZero()
        {
            if (exitRequested || stopping) return;
            levelZeroStage = LevelZeroStage.Explore;
            levelZeroStarted = DateTimeOffset.UtcNow;
            levelZeroStageStarted = levelZeroStarted;
            levelZeroLastPrompt = DateTimeOffset.MinValue;
            levelZeroSeconds = 0;
            levelZeroRegionMask = 0;
            levelZeroCornerCandidateSamples = 0;
            levelZeroLastPursuitSample = DateTimeOffset.MinValue;
            levelZeroPendingInstruction = null;
            levelZeroCornerMask = 0;
            levelZeroMinX = levelZeroMinY = double.MaxValue;
            levelZeroMaxX = levelZeroMaxY = 0;
            levelZeroHadGaze = false;
            levelZeroGuidanceStarted = false;
            levelZeroHorizontalPrompted = levelZeroCornerPrompted = levelZeroFollowPrompted = false;
            levelZeroPursuitSamples = levelZeroCloseSamples = 0;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            ShowScreen(AppScreen.LevelZero);
            levelZeroClock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            levelZeroClock.Tick += OnLevelZeroTick;
            levelZeroClock.Start();
        }

        private void StopLevelZero()
        {
            if (levelZeroClock != null)
            {
                levelZeroClock.Stop();
                levelZeroClock.Tick -= OnLevelZeroTick;
                levelZeroClock = null;
            }
            // Stop queued speech when leaving the tutorial.
            try { LevelZeroSpeechPlayer.Stop(); }
            catch (Exception) { }
            levelZeroSpeechBusy = false;
            levelZeroPendingInstruction = null;
            levelZeroGuidanceStarted = false;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            LevelZeroEyeCursor.Visibility = Visibility.Collapsed;
        }

        private async void SpeakLevelZero(string text, bool essential = false)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested ||
                levelZeroSpeechUnavailable) return;
            if (levelZeroSpeechBusy)
            {
                if (essential) levelZeroPendingInstruction = text;
                return;
            }
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!essential && (now - levelZeroLastPrompt).TotalSeconds < 15) return;
            levelZeroLastPrompt = now;
            levelZeroSpeechBusy = true;
            try
            {
                if (levelZeroSynth == null) levelZeroSynth = new SpeechSynthesizer();
                using (var stream = await levelZeroSynth.SynthesizeTextToStreamAsync(text))
                {
                    if (currentScreen != AppScreen.LevelZero || exitRequested) return;
                    LevelZeroSpeechPlayer.SetSource(stream, stream.ContentType);
                    LevelZeroSpeechPlayer.Play();
                    // Keep a bounded gap; the player may continue after this
                    // delay, but a new prompt stops it before playing.
                    await Task.Delay(Math.Max(2200, Math.Min(6000, text.Length * 78)));
                }
            }
            catch (Exception)
            {
                levelZeroSpeechUnavailable = true;
                // Missing voice output must not crash gaze or exit handling.
            }
            finally
            {
                levelZeroSpeechBusy = false;
                if (currentScreen == AppScreen.LevelZero &&
                    !string.IsNullOrEmpty(levelZeroPendingInstruction))
                {
                    string next = levelZeroPendingInstruction;
                    levelZeroPendingInstruction = null;
                    SpeakLevelZero(next, true);
                }
            }
        }

        private void OnLevelZeroTick(object sender, object args)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested) return;
            if (levelZeroStage != LevelZeroStage.Pursue) return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            double elapsed = (now - levelZeroStageStarted).TotalSeconds;
            double w = LevelZeroCanvas.ActualWidth, h = LevelZeroCanvas.ActualHeight;
            if (w < 350 || h < 280) return;
            // Wide, predictable sinusoidal sweeps with gentle vertical drift.
            double cx = w * (0.5 + 0.32 * Math.Sin(elapsed * 0.38));
            double cy = h * (0.5 + 0.21 * Math.Sin(elapsed * 0.24));
            Canvas.SetLeft(LevelZeroPursuitTarget, cx - 62);
            Canvas.SetTop(LevelZeroPursuitTarget, cy - 62);
            Canvas.SetLeft(LevelZeroPursuitCenter, cx - 9);
            Canvas.SetTop(LevelZeroPursuitCenter, cy - 9);
            if (elapsed >= 18 && levelZeroCloseSamples >= 20)
                GoToLevelZeroSelection();
            else if (elapsed > 35 && levelZeroCloseSamples >= 6)
                GoToLevelZeroSelection();
            else if (elapsed > 26 && levelZeroCloseSamples < 6 &&
                     !levelZeroFollowPrompted)
            {
                levelZeroFollowPrompted = true;
                SpeakLevelZero("Try keeping your eye cursor near the moving circle.");
            }
        }

        private void ProcessLevelZeroGaze(Point raw, Point stabilized)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested) return;
            double w = RootLayout.ActualWidth, h = RootLayout.ActualHeight;
            if (w <= 0 || h <= 0 || raw.X < 0 || raw.Y < 0 || raw.X > w || raw.Y > h)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            levelZeroLastValid = now;
            levelZeroHadGaze = true;

            if (!levelZeroGuidanceStarted)
            {
                levelZeroGuidanceStarted = true;
                SpeakLevelZero("Your eyes move the cursor. Take a moment to look around the screen.", true);
            }

            if (levelZeroStage == LevelZeroStage.Explore)
            {
                levelZeroMinX = Math.Min(levelZeroMinX, raw.X);
                levelZeroMaxX = Math.Max(levelZeroMaxX, raw.X);
                levelZeroMinY = Math.Min(levelZeroMinY, raw.Y);
                levelZeroMaxY = Math.Max(levelZeroMaxY, raw.Y);
                int col = Math.Min(2, (int)(raw.X / w * 3));
                int row = Math.Min(2, (int)(raw.Y / h * 3));
                levelZeroRegionMask |= 1 << (row * 3 + col);
                int corner = (raw.X < w * 0.27 ? 0 : raw.X > w * 0.73 ? 1 : -1);
                int cornerRow = raw.Y < h * 0.30 ? 0 : raw.Y > h * 0.70 ? 1 : -1;
                int candidate = corner >= 0 && cornerRow >= 0
                    ? corner + 2 * cornerRow : -1;
                if (candidate != levelZeroCornerCandidate)
                {
                    levelZeroCornerCandidate = candidate;
                    levelZeroCornerCandidateSamples = candidate >= 0 ? 1 : 0;
                    levelZeroCornerEntered = now;
                }
                if (candidate >= 0 && candidate == levelZeroCornerCandidate)
                    levelZeroCornerCandidateSamples++;
                if (candidate >= 0 && levelZeroCornerCandidateSamples >= 3 &&
                    (now - levelZeroCornerEntered).TotalMilliseconds >= 250 &&
                    (levelZeroCornerMask & (1 << candidate)) == 0)
                {
                    levelZeroCornerMask |= 1 << candidate;
                    SpeakLevelZero("Good job looking toward the corner.");
                }

                bool horizontal = levelZeroMaxX - levelZeroMinX >= w * .34;
                bool vertical = levelZeroMaxY - levelZeroMinY >= h * .27;
                double seconds = (now - levelZeroStageStarted).TotalSeconds;
                if (horizontal && !levelZeroHorizontalPrompted && seconds >= 6)
                {
                    levelZeroHorizontalPrompted = true;
                    SpeakLevelZero("Nice. You've moved the cursor from side to side.");
                }
                if (horizontal && vertical && !levelZeroCornerPrompted)
                {
                    levelZeroCornerPrompted = true;
                    SpeakLevelZero("Good. Now try looking toward a corner.");
                }
                int regions = CountBits(levelZeroRegionMask);
                if (seconds >= 15 && horizontal && vertical && regions >= 4)
                    GoToLevelZeroPursuit();
                else if (seconds >= 45 && regions >= 3 && (horizontal || vertical))
                    GoToLevelZeroPursuit();
                else if (seconds >= 24 && !horizontal && !levelZeroHorizontalPrompted)
                {
                    levelZeroHorizontalPrompted = true;
                    SpeakLevelZero("Take your time. Try looking to the left or right.");
                }
            }
            else if (levelZeroStage == LevelZeroStage.Pursue)
            {
                if (!levelZeroHadGaze ||
                    (now - levelZeroLastPursuitSample).TotalMilliseconds < 150)
                    return;
                levelZeroLastPursuitSample = now;
                levelZeroPursuitSamples++;
                double x = Canvas.GetLeft(LevelZeroPursuitTarget) + 62;
                double y = Canvas.GetTop(LevelZeroPursuitTarget) + 62;
                double dx = raw.X - x, dy = raw.Y - y;
                if (dx * dx + dy * dy <= 180 * 180)
                {
                    levelZeroCloseSamples++;
                    if (levelZeroCloseSamples == 18 && !levelZeroFollowPrompted)
                    {
                        levelZeroFollowPrompted = true;
                        SpeakLevelZero("You're following it well.");
                    }
                }
            }
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0) { count += mask & 1; mask >>= 1; }
            return count;
        }

        private void GoToLevelZeroPursuit()
        {
            if (levelZeroStage != LevelZeroStage.Explore) return;
            levelZeroStage = LevelZeroStage.Pursue;
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            LevelZeroPursuitTarget.Visibility = Visibility.Visible;
            LevelZeroPursuitCenter.Visibility = Visibility.Visible;
            SpeakLevelZero("Now follow the moving target with your eye cursor.", true);
        }

        private void GoToLevelZeroSelection()
        {
            if (levelZeroStage != LevelZeroStage.Pursue) return;
            levelZeroStage = LevelZeroStage.Continue;
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Visible;
            SpeakLevelZero("Look at the large button to open the main screen. Keep looking until the circle fills.", true);
        }

        private void OnLevelZeroContinue(object sender, RoutedEventArgs e)
        {
            if (currentScreen != AppScreen.LevelZero ||
                levelZeroStage != LevelZeroStage.Continue) return;
            FinishLevelZero();
        }

        private void FinishLevelZero()
        {
            try { ApplicationData.Current.LocalSettings.Values[LevelZeroDoneKey] = true; }
            catch (Exception) { /* A storage error never prevents access to home. */ }
            StopLevelZero();
            ShowScreen(AppScreen.Menu);
        }
    }
}
