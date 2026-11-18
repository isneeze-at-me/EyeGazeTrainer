using System;
using System.Threading.Tasks;
using System.Globalization;
using Windows.Foundation;
using Windows.Media.SpeechSynthesis;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.UI.Xaml.Media;
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
        private enum MovementStep { Discover, Vertical, Horizontal, Circles }
        private MovementStep movementStep;
        private DateTimeOffset movementStepStarted, lastMotionSample, lastMovement, lastMovementCoach;
        private Point previousMotionPoint;
        private bool hasPreviousMotionPoint, hasPreviousCircleAngle, hasActualMovement;
        private double discoveryMotionSeconds, stepMotionSeconds, stepTravel;
        private double circleTravelRadians, previousCircleAngle;
        private int circleDirection, circleSectorMask, axisReversals, lastAxisDirection;
        private int verticalSamples, horizontalSamples, diagonalSamples, circleSamples;
        private LevelZeroStage levelZeroStage;
        private bool levelZeroRenderingSubscribed;
        private DispatcherTimer levelZeroCoachTimer;
        private DateTimeOffset levelZeroStarted;
        private DateTimeOffset levelZeroStageStarted;
        private DateTimeOffset levelZeroLastPrompt;
        private DateTimeOffset levelZeroLastValid;
        private double levelZeroSeconds;
        private double levelZeroTimeToFirstGaze = -1;
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
        private bool levelZeroCoachedHorizontal, levelZeroCoachedVertical, levelZeroCoachedCorner;
        private bool levelZeroCoachedPursuit, levelZeroCoachedButton;
        private bool levelZeroVerticalPraised;
        private bool levelZeroButtonExplanationPending;
        private readonly System.Collections.Generic.Queue<string> levelZeroFeedbackQueue =
            new System.Collections.Generic.Queue<string>();
        private TaskCompletionSource<bool> levelZeroPlaybackCompletion;
        private int levelZeroNarrationVersion;
        private SpeechSynthesizer levelZeroSynth;
        private MediaPlayer levelZeroAudioPlayer;
        private bool levelZeroSpeechUnavailable;
        // Hold the synthesized audio stream open for the entire playback.
        // Closing it immediately after Play can result in silent output.
        private SpeechSynthesisStream levelZeroActiveSpeechStream;

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
            movementStep = MovementStep.Discover;
            hasActualMovement = false;
            ResetMovementStep();
            levelZeroLastPrompt = DateTimeOffset.MinValue;
            levelZeroSeconds = 0;
            levelZeroTimeToFirstGaze = -1;
            levelZeroRegionMask = 0;
            levelZeroCornerCandidateSamples = 0;
            levelZeroLastPursuitSample = DateTimeOffset.MinValue;
            levelZeroPendingInstruction = null;
            levelZeroFeedbackQueue.Clear();
            levelZeroButtonExplanationPending = false;
            levelZeroSpeechUnavailable = false;
            levelZeroCoachedHorizontal = levelZeroCoachedVertical =
                levelZeroCoachedCorner = levelZeroCoachedPursuit =
                levelZeroCoachedButton = levelZeroVerticalPraised = false;
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
            // Coaching continues even when the learner is not moving gaze.
            levelZeroCoachTimer = new DispatcherTimer
                { Interval = TimeSpan.FromSeconds(2) };
            levelZeroCoachTimer.Tick += OnLevelZeroCoachTick;
            levelZeroCoachTimer.Start();
        }

        private void StopLevelZero()
        {
            levelZeroNarrationVersion++;
            if (levelZeroCoachTimer != null)
            {
                levelZeroCoachTimer.Stop();
                levelZeroCoachTimer.Tick -= OnLevelZeroCoachTick;
                levelZeroCoachTimer = null;
            }
            if (levelZeroPlaybackCompletion != null)
            {
                levelZeroPlaybackCompletion.TrySetResult(false);
                levelZeroPlaybackCompletion = null;
            }
            StopLevelZeroRendering();
            // MediaPlayer is independent of the hidden/collapsed XAML visual.
            if (levelZeroAudioPlayer != null)
            {
                levelZeroAudioPlayer.Pause();
                levelZeroAudioPlayer.Source = null;
                levelZeroAudioPlayer.MediaFailed -= OnLevelZeroMediaPlayerFailed;
                levelZeroAudioPlayer.MediaEnded -= OnLevelZeroMediaEnded;
                levelZeroAudioPlayer.Dispose();
                levelZeroAudioPlayer = null;
            }
            if (levelZeroActiveSpeechStream != null)
            {
                levelZeroActiveSpeechStream.Dispose();
                levelZeroActiveSpeechStream = null;
            }
            levelZeroSpeechBusy = false;
            levelZeroPendingInstruction = null;
            levelZeroFeedbackQueue.Clear();
            levelZeroButtonExplanationPending = false;
            levelZeroGuidanceStarted = false;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            LevelZeroEyeCursor.Visibility = Visibility.Collapsed;
        }

        private void OnLevelZeroMediaPlayerFailed(MediaPlayer sender,
                                                  MediaPlayerFailedEventArgs args)
        {
            Trace("Level 0 speech playback failure: " +
                  args.ErrorMessage);
        }

        private void OnLevelZeroMediaEnded(MediaPlayer sender, object args)
        {
            // MediaEnded is raised on a media thread; signal only.
            var completion = levelZeroPlaybackCompletion;
            if (completion == null) return;
            // Resume the narration sequence on the UI dispatcher. A native
            // MediaPlayer callback can arrive on a background thread.
            if (Dispatcher.HasThreadAccess)
                completion.TrySetResult(true);
            else
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => completion.TrySetResult(true));
        }

        // Important stage directions take precedence over ordinary praise.
        // Keep at most one queued feedback phrase to avoid nonstop chatter.
        private void SpeakLevelZero(string words, bool essential = false)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested ||
                levelZeroSpeechUnavailable) return;
            if (essential)
            {
                levelZeroFeedbackQueue.Clear();
                if (levelZeroSpeechBusy)
                {
                    levelZeroPendingInstruction = words;
                    return;
                }
            }
            else
            {
                if (levelZeroSpeechBusy)
                {
                    if (levelZeroFeedbackQueue.Count == 0 &&
                        string.IsNullOrEmpty(levelZeroPendingInstruction))
                        levelZeroFeedbackQueue.Enqueue(words);
                    return;
                }
                if ((DateTimeOffset.UtcNow - levelZeroLastPrompt).TotalSeconds < 10)
                    return;
            }
            _ = PlayLevelZeroSpeechAsync(words);
        }

        private async Task PlayLevelZeroSpeechAsync(string words)
        {
            if (levelZeroSpeechBusy || currentScreen != AppScreen.LevelZero) return;
            int version = levelZeroNarrationVersion;
            levelZeroSpeechBusy = true;
            levelZeroLastPrompt = DateTimeOffset.UtcNow;
            try
            {
                if (levelZeroSynth == null) levelZeroSynth = new SpeechSynthesizer();
                var stream = await levelZeroSynth.SynthesizeTextToStreamAsync(words);
                if (version != levelZeroNarrationVersion ||
                    currentScreen != AppScreen.LevelZero || exitRequested)
                {
                    stream.Dispose();
                    return;
                }
                if (levelZeroAudioPlayer == null)
                {
                    levelZeroAudioPlayer = new MediaPlayer();
                    levelZeroAudioPlayer.MediaFailed += OnLevelZeroMediaPlayerFailed;
                    levelZeroAudioPlayer.MediaEnded += OnLevelZeroMediaEnded;
                }
                levelZeroAudioPlayer.Pause();
                levelZeroAudioPlayer.Source = null;
                if (levelZeroActiveSpeechStream != null)
                    levelZeroActiveSpeechStream.Dispose();
                levelZeroActiveSpeechStream = stream;
                levelZeroAudioPlayer.Volume = 1.0;
                levelZeroAudioPlayer.IsMuted = false;
                levelZeroPlaybackCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                levelZeroAudioPlayer.Source =
                    MediaSource.CreateFromStream(stream, stream.ContentType);
                levelZeroAudioPlayer.Play();
                Trace("Level 0 speaking: " + words);
                await Task.WhenAny(levelZeroPlaybackCompletion.Task,
                                   Task.Delay(18000));
            }
            catch (Exception ex)
            {
                levelZeroSpeechUnavailable = true;
                Trace("Level 0 voice failed: " + ex);
            }
            finally
            {
                if (version == levelZeroNarrationVersion)
                {
                    levelZeroPlaybackCompletion = null;
                    levelZeroSpeechBusy = false;
                    if (currentScreen == AppScreen.LevelZero && !exitRequested)
                    {
                        if (!string.IsNullOrEmpty(levelZeroPendingInstruction))
                        {
                            string next = levelZeroPendingInstruction;
                            levelZeroPendingInstruction = null;
                            _ = PlayLevelZeroSpeechAsync(next);
                        }
                        else if (levelZeroButtonExplanationPending &&
                                 levelZeroStage == LevelZeroStage.Continue)
                        {
                            levelZeroButtonExplanationPending = false;
                            LevelZeroContinueButton.Visibility = Visibility.Visible;
                        }
                        else if (levelZeroFeedbackQueue.Count > 0)
                        {
                            _ = PlayLevelZeroSpeechAsync(
                                levelZeroFeedbackQueue.Dequeue());
                        }
                    }
                }
            }
        }

        private void OnLevelZeroCoachTick(object sender, object args)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (levelZeroStage == LevelZeroStage.Explore)
            {
                if ((now - lastMovementCoach).TotalSeconds < 12) return;
                string coaching = null;
                if (!levelZeroHadGaze ||
                    (now - levelZeroLastValid).TotalSeconds > 6)
                    coaching = "I'm not detecting your eye cursor just yet. Try looking at the middle of the screen.";
                else if (!hasActualMovement)
                    coaching = "Let's get that cursor moving. Look up, down, or across the screen. No need to hold your gaze in one place.";
                else if ((now - lastMovement).TotalSeconds > 7)
                    coaching = "Your cursor has been resting. Whenever you're ready, " +
                        MovementInstruction();
                else if ((now - movementStepStarted).TotalSeconds > 7)
                {
                    // If the requested activity is already happening, stay
                    // quiet. Only redirect when another movement dominates.
                    bool correct = movementStep == MovementStep.Vertical &&
                        verticalSamples >= 8 && verticalSamples >= horizontalSamples ||
                        movementStep == MovementStep.Horizontal &&
                        horizontalSamples >= 8 && horizontalSamples >= verticalSamples ||
                        movementStep == MovementStep.Circles && circleSamples >= 8;
                    if (!correct)
                    {
                        string observed = horizontalSamples >= 5 &&
                            horizontalSamples > verticalSamples * 1.5
                            ? "You're moving side to side. " :
                            verticalSamples >= 5 &&
                            verticalSamples > horizontalSamples * 1.5
                            ? "You're moving up and down. " :
                            diagonalSamples >= 5
                            ? "You're moving diagonally. " : "";
                        coaching = observed + MovementInstruction();
                    }
                }
                if (coaching != null)
                {
                    lastMovementCoach = now;
                    SpeakLevelZero(coaching);
                }
                return;
            }
            if (!levelZeroHadGaze || (now - levelZeroLastValid).TotalSeconds > 8)
                return;
            double elapsed = (now - levelZeroStageStarted).TotalSeconds;
            if (levelZeroStage == LevelZeroStage.Pursue)
            {
                if (elapsed >= 14 && levelZeroCloseSamples < 5 &&
                    !levelZeroCoachedPursuit)
                {
                    levelZeroCoachedPursuit = true;
                    SpeakLevelZero("Watch the glowing circle, not your cursor. Your eyes will guide the cursor as you follow.");
                }
                else if (elapsed >= 31 && levelZeroCloseSamples < 10 &&
                         !levelZeroCoachedButton)
                {
                    levelZeroCoachedButton = true;
                    SpeakLevelZero("Try following just part of the circle's path. Take your time.");
                }
            }
            else if (levelZeroStage == LevelZeroStage.Continue &&
                     !levelZeroButtonExplanationPending)
            {
                if (elapsed >= 14 && levelZeroDwellAttempts == 0 &&
                    !levelZeroCoachedPursuit)
                {
                    levelZeroCoachedPursuit = true;
                    SpeakLevelZero("That large house button opens your Levels screen. Look at its center, and keep looking until the ring fills.");
                }
                else if (elapsed >= 20 && levelZeroDwellAttempts > 0 &&
                         !levelZeroCoachedButton)
                {
                    levelZeroCoachedButton = true;
                    SpeakLevelZero("You're close. Keep your eyes on the house button until the circle finishes.");
                }
            }
        }

        private string MovementInstruction()
        {
            switch (movementStep)
            {
                case MovementStep.Vertical:
                    return "For this exercise, look up toward the top, then down toward the bottom, and repeat.";
                case MovementStep.Horizontal:
                    return "For this exercise, look to the left, then right, and repeat.";
                case MovementStep.Circles:
                    return "For this exercise, move your eyes in a big, comfortable circle around the middle of the screen.";
                default:
                    return "look around the screen and move the eye cursor with you.";
            }
        }

        private void ResetMovementStep()
        {
            movementStepStarted = DateTimeOffset.UtcNow;
            lastMotionSample = DateTimeOffset.MinValue;
            lastMovement = movementStepStarted;
            lastMovementCoach = movementStepStarted;
            hasPreviousMotionPoint = false;
            hasPreviousCircleAngle = false;
            discoveryMotionSeconds = stepMotionSeconds = stepTravel = 0;
            circleTravelRadians = 0;
            circleDirection = circleSectorMask = axisReversals =
                lastAxisDirection = verticalSamples = horizontalSamples =
                diagonalSamples = circleSamples = 0;
        }

        private void NextMovementStep(MovementStep next, string instruction)
        {
            if (levelZeroStage != LevelZeroStage.Explore) return;
            movementStep = next;
            ResetMovementStep();
            levelZeroFeedbackQueue.Clear();
            SpeakLevelZero(instruction, true);
        }

        private void ObserveLevelZeroMovement(Point gaze, double width,
                                              double height, DateTimeOffset now)
        {
            // Sample at 10 Hz, not every tracker frame; ignore jitter.
            if ((now - lastMotionSample).TotalMilliseconds < 100) return;
            if (hasPreviousMotionPoint &&
                (now - lastMotionSample).TotalSeconds > 1.5)
            {
                hasPreviousMotionPoint = false;
                hasPreviousCircleAngle = false;
            }
            double dt = lastMotionSample == DateTimeOffset.MinValue
                ? 0 : Math.Min(.2, (now - lastMotionSample).TotalSeconds);
            lastMotionSample = now;
            if (!hasPreviousMotionPoint)
            {
                previousMotionPoint = gaze;
                hasPreviousMotionPoint = true;
                return;
            }
            double dx = (gaze.X - previousMotionPoint.X) / width;
            double dy = (gaze.Y - previousMotionPoint.Y) / height;
            previousMotionPoint = gaze;
            double displacement = Math.Sqrt(dx * dx + dy * dy);
            if (displacement < .012 || displacement > .32) return;
            hasActualMovement = true;
            lastMovement = now;
            bool vertical = Math.Abs(dy) > 1.4 * Math.Abs(dx);
            bool horizontal = Math.Abs(dx) > 1.4 * Math.Abs(dy);
            if (vertical) verticalSamples++;
            else if (horizontal) horizontalSamples++;
            else diagonalSamples++;

            if (movementStep == MovementStep.Discover)
            {
                discoveryMotionSeconds += dt;
                stepTravel += displacement;
                if (discoveryMotionSeconds >= 1.0 && stepTravel >= .15)
                    NextMovementStep(MovementStep.Vertical,
                        "There you go! You're moving the cursor. Now look toward the top of the screen, then down toward the bottom. Keep practicing up and down.");
                return;
            }

            if (movementStep == MovementStep.Vertical ||
                movementStep == MovementStep.Horizontal)
            {
                int direction = movementStep == MovementStep.Vertical
                    ? (vertical ? Math.Sign(dy) : 0)
                    : (horizontal ? Math.Sign(dx) : 0);
                if (direction == 0) return;
                if (lastAxisDirection != 0 && direction != lastAxisDirection)
                    axisReversals++;
                lastAxisDirection = direction;
                stepMotionSeconds += dt;
                stepTravel += displacement;
                // Fifteen seconds in the activity, with meaningful deliberate
                // movement and multiple direction changes. Idle alone won't pass.
                if ((now - movementStepStarted).TotalSeconds >= 15 &&
                    stepMotionSeconds >= 2.5 && axisReversals >= 2 &&
                    stepTravel >= .35)
                {
                    if (movementStep == MovementStep.Vertical)
                        NextMovementStep(MovementStep.Horizontal,
                            "Great work! You've practiced up and down. Next is side to side. Look to the left, then to the right, and keep repeating.");
                    else
                        NextMovementStep(MovementStep.Circles,
                            "Excellent! You've practiced left and right. Now let's try circles. Slowly trace a big circle with your eyes around the middle of the screen.");
                }
                return;
            }

            // Require angular travel around the center; random small
            // movement in one area must never be scored as a circle.
            double nx = (gaze.X / width - .5) * 2;
            double ny = (gaze.Y / height - .5) * 2;
            double radius = Math.Sqrt(nx * nx + ny * ny);
            if (radius < .23 || radius > 1.3)
            {
                hasPreviousCircleAngle = false;
                return;
            }
            double angle = Math.Atan2(ny, nx);
            int sector = (int)Math.Floor((angle + Math.PI) / (Math.PI / 4));
            circleSectorMask |= 1 << Math.Max(0, Math.Min(7, sector));
            if (hasPreviousCircleAngle)
            {
                double delta = angle - previousCircleAngle;
                while (delta > Math.PI) delta -= 2 * Math.PI;
                while (delta < -Math.PI) delta += 2 * Math.PI;
                if (Math.Abs(delta) >= .035 && Math.Abs(delta) < .9)
                {
                    int sign = Math.Sign(delta);
                    if (circleDirection == 0) circleDirection = sign;
                    if (circleDirection == sign)
                    {
                        circleTravelRadians += Math.Abs(delta);
                        circleSamples++;
                        stepMotionSeconds += dt;
                    }
                }
            }
            previousCircleAngle = angle;
            hasPreviousCircleAngle = true;
            if ((now - movementStepStarted).TotalSeconds >= 15 &&
                circleTravelRadians >= Math.PI * 1.5 && circleSamples >= 10 &&
                CountBits(circleSectorMask) >= 5)
                GoToLevelZeroPursuit();
        }

        // CompositionTarget.Rendering tracks display refresh instead of a
        // 50 ms dispatcher timer (which stepped the marker at only 20 Hz).
        private void StartLevelZeroRendering()
        {
            if (levelZeroRenderingSubscribed) return;
            CompositionTarget.Rendering += OnLevelZeroRendering;
            levelZeroRenderingSubscribed = true;
        }

        private void StopLevelZeroRendering()
        {
            if (!levelZeroRenderingSubscribed) return;
            CompositionTarget.Rendering -= OnLevelZeroRendering;
            levelZeroRenderingSubscribed = false;
        }

        private void OnLevelZeroRendering(object sender, object args)
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

        }

        private void ProcessLevelZeroGaze(Point raw, Point stabilized)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested) return;
            double w = RootLayout.ActualWidth, h = RootLayout.ActualHeight;
            if (w <= 0 || h <= 0 || raw.X < 0 || raw.Y < 0 || raw.X > w || raw.Y > h)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            levelZeroLastValid = now;
            if (!levelZeroHadGaze)
                levelZeroTimeToFirstGaze = (now - levelZeroStarted).TotalSeconds;
            levelZeroHadGaze = true;

            if (!levelZeroGuidanceStarted)
            {
                levelZeroGuidanceStarted = true;
                SpeakLevelZero("Welcome to EyeGazeTrainer! The two eyes on the screen are your cursor. Start by moving your eyes around the screen. Watch the cursor move with you. There is nothing to select yet. Take your time. We will start when you move.", true);
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
                int corner = raw.X < w * .27 ? 0 : raw.X > w * .73 ? 1 : -1;
                int cornerRow = raw.Y < h * .30 ? 0 : raw.Y > h * .70 ? 1 : -1;
                int candidate = corner >= 0 && cornerRow >= 0
                    ? corner + 2 * cornerRow : -1;
                if (candidate != levelZeroCornerCandidate)
                {
                    levelZeroCornerCandidate = candidate;
                    levelZeroCornerCandidateSamples = candidate >= 0 ? 1 : 0;
                    levelZeroCornerEntered = now;
                }
                if (candidate >= 0 && levelZeroCornerCandidateSamples >= 3 &&
                    (now - levelZeroCornerEntered).TotalMilliseconds >= 250)
                    levelZeroCornerMask |= 1 << candidate;

                // Track the same stabilized cursor position the learner sees.
                // Raw gaze jitter alone must never generate praise.
                ObserveLevelZeroMovement(stabilized, w, h, now);
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
                        SpeakLevelZero("That’s the idea! You’re keeping up with the moving circle. Nice work.");
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
            StartLevelZeroRendering();
            LevelZeroPursuitTarget.Visibility = Visibility.Visible;
            LevelZeroPursuitCenter.Visibility = Visibility.Visible;
            levelZeroFeedbackQueue.Clear();
            SpeakLevelZero("Excellent work! You practiced moving up and down, left and right, and drawing circles. Next, follow the glowing circle as it moves around the screen. Keep your eyes on the circle and let the cursor follow.", true);
        }

        private void GoToLevelZeroSelection()
        {
            if (levelZeroStage != LevelZeroStage.Pursue) return;
            levelZeroStage = LevelZeroStage.Continue;
            StopLevelZeroRendering();
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            levelZeroButtonExplanationPending = true;
            levelZeroCoachedPursuit = false;
            levelZeroCoachedButton = false;
            levelZeroFeedbackQueue.Clear();
            SpeakLevelZero("Fantastic! You've learned how to follow a moving target. One final step. A large house button will appear. It takes you to the Levels screen, where you can start Level One. Look at the button and keep looking until the circular ring fills. Give it a try!", true);
        }

        private void OnLevelZeroReplay(object sender, RoutedEventArgs e)
        {
            if (stopping || exitRequested || currentScreen != AppScreen.Levels) return;
            StartLevelZero();
        }

        private static string LevelZeroMetricValue(int index)
        {
            string[] keys = { "FirstGaze", "Coverage", "Corners", "Range",
                              "Pursuit", "Selection" };
            if (index < 0 || index >= keys.Length) return "NA";
            try
            {
                object value;
                return ApplicationData.Current.LocalSettings.Values.TryGetValue(
                    "EyeGazeTrainer.LevelZero.MetricV1." + keys[index], out value)
                    ? Convert.ToString(value) : "NA";
            }
            catch (Exception) { return "NA"; }
        }

        private void OnLevelZeroContinue(object sender, RoutedEventArgs e)
        {
            if (currentScreen != AppScreen.LevelZero ||
                levelZeroStage != LevelZeroStage.Continue) return;
            FinishLevelZero();
        }

        // Save only six aggregate skill observations; never raw positions.
        private void SaveLevelZeroMeasurements()
        {
            try
            {
                var v = ApplicationData.Current.LocalSettings.Values;
                const string p = "EyeGazeTrainer.LevelZero.MetricV1.";
                if (levelZeroTimeToFirstGaze >= 0)
                    v[p + "FirstGaze"] = levelZeroTimeToFirstGaze.ToString("0.0",
                        CultureInfo.InvariantCulture) + " sec";
                v[p + "Coverage"] = (CountBits(levelZeroRegionMask) / 9.0 * 100)
                    .ToString("0", CultureInfo.InvariantCulture) + "% (9 regions)";
                v[p + "Corners"] = CountBits(levelZeroCornerMask).ToString() + " / 4";
                if (levelZeroMinX < double.MaxValue && RootLayout.ActualWidth > 0 &&
                    RootLayout.ActualHeight > 0)
                    v[p + "Range"] =
                        (100 * (levelZeroMaxX - levelZeroMinX) / RootLayout.ActualWidth)
                        .ToString("0", CultureInfo.InvariantCulture) + "% horizontal, " +
                        (100 * (levelZeroMaxY - levelZeroMinY) / RootLayout.ActualHeight)
                        .ToString("0", CultureInfo.InvariantCulture) + "% vertical";
                if (levelZeroPursuitSamples > 0)
                    v[p + "Pursuit"] = (100.0 * levelZeroCloseSamples /
                        levelZeroPursuitSamples).ToString("0",
                        CultureInfo.InvariantCulture) + "% of valid sampled positions";
                v[p + "Selection"] = "Completed (" + levelZeroDwellAttempts + " attempts)";
            }
            catch (Exception) { /* Measurements never block learner navigation. */ }
        }

        private void FinishLevelZero()
        {
            SaveLevelZeroMeasurements();
            try { ApplicationData.Current.LocalSettings.Values[LevelZeroDoneKey] = true; }
            catch (Exception) { /* A storage error never prevents access to Levels. */ }
            StopLevelZero();
            UpdateLevelBadges();
            LevelsNoticeText.Text = "";
            ShowScreen(AppScreen.Levels);
        }
    }
}
