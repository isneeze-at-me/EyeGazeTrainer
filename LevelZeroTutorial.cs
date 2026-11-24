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
        private enum MovementStep { Discover, Vertical, Horizontal, DiagonalDown, DiagonalUp, Circles }
        private MovementStep movementStep;
        private bool movementWaitingForInstructions;
        private string movementInstructionSpeech;
        private DateTimeOffset movementStepStarted, lastMotionSample, lastMovement, lastMovementCoach;
        private Point previousMotionPoint;
        private Point directionWindowStart;
        private DateTimeOffset directionWindowStarted;
        private bool directionWindowInitialized;
        private bool hasPreviousMotionPoint, hasPreviousCircleAngle, hasActualMovement;
        private double discoveryMotionSeconds, stepMotionSeconds, stepTravel;
        private double circleTravelRadians, previousCircleAngle;
        private int circleDirection, circleOppositeTurnWindows, circleSectorMask, axisReversals, lastAxisDirection;
        private int verticalSamples, horizontalSamples, diagonalSamples, circleSamples;
        private DateTimeOffset lastCorrectAxisMovement;
        private DateTimeOffset coachingWindowStarted;
        private int recentCorrectSweeps, recentOtherAxisSweeps, recentDiagonalSweeps;
        private LevelZeroStage levelZeroStage;
        private bool levelZeroRenderingSubscribed;
        private DateTimeOffset levelZeroLastRenderedAt;
        private Point levelZeroLastRenderedTarget = new Point(double.NaN, double.NaN);
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
        private const double LevelZeroPursuitRadius = 110;
        private const double LevelZeroPursuitInsideRadius = 100;
        private double levelZeroPursuitInsideSeconds;
        private DateTimeOffset levelZeroPursuitLastInside;
        private bool levelZeroPursuitPreviouslyInside;
        private bool levelZeroPursuitOutsideCoached;
        // Shared, evidence-based feedback for every Level 0 activity.
        private DateTimeOffset levelZeroFeedbackActivityStarted;
        private DateTimeOffset levelZeroFeedbackLastCorrect;
        private DateTimeOffset levelZeroFeedbackLastPraise;
        private DateTimeOffset levelZeroFeedbackLastCoaching;
        private double levelZeroFeedbackCorrectSeconds;
        private bool levelZeroFeedbackHasCorrectMovement;
        private bool levelZeroFeedbackPraisedThisStep;
        private Point levelZeroPursuitStartPoint;
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
        private bool levelZeroStageWaitingForInstructions;
        private bool levelZeroFinishAfterSpeech;
        private DispatcherTimer levelZeroCompletionSafetyTimer;
        private const string LevelZeroCompletionMessage =
            "Congratulations on completing your introduction! " +
            "You have six more lessons to continue your eye gaze training. " +
            "You can replay any level as often as you like, because practice makes perfect!";
        private bool levelZeroSelectionTimedOut;
        private string levelZeroStageInstructionSpeech;
        private const double LevelZeroMaximumStepSeconds = 60;
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
                // Startup routing is based on Level 0 completion only.
                // Other training activity must not silently skip onboarding.
                return true;
            }
            catch (Exception)
            {
                // If completion storage is unavailable, do not presume the
                // user already learned the prerequisite eye-gaze skills.
                return true;
            }
        }

        private void StartLevelZero()
        {
            if (exitRequested || stopping) return;
            levelZeroStage = LevelZeroStage.Explore;
            ResetLevelZeroDiagnostics();
            levelZeroStarted = DateTimeOffset.UtcNow;
            levelZeroStageStarted = levelZeroStarted;
            movementStep = MovementStep.Discover;
            hasActualMovement = false;
            movementWaitingForInstructions = true;
            movementInstructionSpeech = null;
            ResetMovementStep();
            levelZeroLastPrompt = DateTimeOffset.MinValue;
            levelZeroSeconds = 0;
            levelZeroTimeToFirstGaze = -1;
            levelZeroRegionMask = 0;
            levelZeroCornerCandidateSamples = 0;
            levelZeroLastPursuitSample = DateTimeOffset.MinValue;
            levelZeroPursuitInsideSeconds = 0;
            levelZeroPursuitLastInside = DateTimeOffset.MinValue;
            levelZeroPursuitPreviouslyInside = false;
            levelZeroPursuitOutsideCoached = false;
            levelZeroPendingInstruction = null;
            levelZeroFeedbackQueue.Clear();
            levelZeroButtonExplanationPending = false;
            levelZeroFinishAfterSpeech = false;
            StopLevelZeroCompletionTimer();
            LevelZeroCompletionPanel.Visibility = Visibility.Collapsed;
            HideLevelZeroInstructionText();
            levelZeroSelectionTimedOut = false;
            levelZeroStageWaitingForInstructions = false;
            levelZeroStageInstructionSpeech = null;
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
            levelZeroDwellAttempts = 0;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            ShowScreen(AppScreen.LevelZero);
            UpdateLevelZeroMovementGuide();
            // Coaching continues even when the learner is not moving gaze.
            levelZeroCoachTimer = new DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1) };
            levelZeroCoachTimer.Tick += OnLevelZeroCoachTick;
            levelZeroCoachTimer.Start();
            // Start narration immediately. A first-time eye-gaze user may
            // have no valid gaze points yet, but must still receive guidance.
            levelZeroGuidanceStarted = true;
            string welcome = "Welcome to EyeGazeTrainer! The two eyes on the screen are your cursor. For the first exercise, move your eyes freely around the screen for fifteen seconds of actual movement. Watch the cursor follow you. You can stop and rest whenever you need to.";
            movementInstructionSpeech = welcome;
            SpeakLevelZero(welcome, true);
        }

        private void StopLevelZero()
        {
            StopLevelZeroCompletionTimer();
            LevelZeroCompletionPanel.Visibility = Visibility.Collapsed;
            HideLevelZeroInstructionText();
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
            levelZeroFinishAfterSpeech = false;
            levelZeroStageWaitingForInstructions = false;
            levelZeroStageInstructionSpeech = null;
            movementWaitingForInstructions = false;
            movementInstructionSpeech = null;
            levelZeroGuidanceStarted = false;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            LevelZeroMovementGuide.Visibility = Visibility.Collapsed;
            LevelZeroVerticalGuide.Visibility = Visibility.Collapsed;
            LevelZeroHorizontalGuide.Visibility = Visibility.Collapsed;
            LevelZeroDiagonalDownGuide.Visibility = Visibility.Collapsed;
            LevelZeroDiagonalUpGuide.Visibility = Visibility.Collapsed;
            LevelZeroCircleGuide.Visibility = Visibility.Collapsed;
            LevelZeroEyeCursor.Visibility = Visibility.Collapsed;
        }

        private void OnLevelZeroMediaPlayerFailed(MediaPlayer sender,
                                                  MediaPlayerFailedEventArgs args)
        {
            Trace("Level 0 speech playback failure: " + args.ErrorMessage);
            SignalLevelZeroPlaybackFinished(false);
        }

        private void OnLevelZeroMediaEnded(MediaPlayer sender, object args)
        {
            // Native player events are not guaranteed to run on the UI thread.
            SignalLevelZeroPlaybackFinished(true);
        }

        private void SignalLevelZeroPlaybackFinished(bool playedToEnd)
        {
            var completion = levelZeroPlaybackCompletion;
            if (completion == null) return;
            if (Dispatcher.HasThreadAccess)
                completion.TrySetResult(playedToEnd);
            else
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => completion.TrySetResult(playedToEnd));
        }

        private void ShowLevelZeroInstructionText(string words)
        {
            LevelZeroInstructionText.Text = words;
            LevelZeroInstructionPanel.Visibility = Visibility.Visible;
            // The guidance is a replacement for the movement drawing,
            // never an overlay drawn over the guide while instructions play.
            LevelZeroMovementGuide.Visibility = Visibility.Collapsed;
        }

        private void HideLevelZeroInstructionText()
        {
            LevelZeroInstructionPanel.Visibility = Visibility.Collapsed;
            LevelZeroInstructionText.Text = "";
        }

        // Important stage directions take precedence over ordinary praise.
        // Keep at most one queued feedback phrase to avoid nonstop chatter.
        private void SpeakLevelZero(string words, bool essential = false)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested)
                return;
            bool stageInstruction = essential &&
                ((movementWaitingForInstructions &&
                  words == movementInstructionSpeech) ||
                 (levelZeroStageWaitingForInstructions &&
                  words == levelZeroStageInstructionSpeech));
            if (stageInstruction)
                ShowLevelZeroInstructionText(words);
            if (levelZeroSpeechUnavailable)
            {
                // Speech failure must not strand an eye-gaze-only learner.
                if (movementWaitingForInstructions &&
                    words == movementInstructionSpeech)
                {
                    ResetMovementStep();
                    movementWaitingForInstructions = false;
                    movementInstructionSpeech = null;
                }
                if (levelZeroStageWaitingForInstructions &&
                    words == levelZeroStageInstructionSpeech)
                {
                    levelZeroStageStarted = DateTimeOffset.UtcNow;
                    levelZeroStageWaitingForInstructions = false;
                    levelZeroStageInstructionSpeech = null;
                    if (levelZeroStage == LevelZeroStage.Pursue)
                        BeginLevelZeroPursuitMotion();
                }
                if (levelZeroButtonExplanationPending &&
                    levelZeroStage == LevelZeroStage.Continue)
                    RevealLevelZeroContinueButton();
                if (stageInstruction)
                {
                    HideLevelZeroInstructionText();
                    UpdateLevelZeroMovementGuide();
                }
                return;
            }
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
                // Activity-specific feedback is already throttled by the
                // five-second state checks and twelve-second praise interval.
                // Do not impose another ten-second lockout here: that hid
                // the first correction even after five seconds of no action.
                if ((DateTimeOffset.UtcNow - levelZeroLastPrompt).TotalSeconds < 2)
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
            bool playedToEnd = false;
            try
            {
                if (levelZeroSynth == null) levelZeroSynth = new SpeechSynthesizer();
                // The final Home instruction used to lose the end of "try!"
                // when the app transitioned as MediaEnded fired. Put actual
                // silence in the synthesized stage-instruction stream, so the
                // last spoken syllable has drained before MediaEnded can
                // release the instruction gate and reveal the Home button.
                bool isStageInstruction =
                    (movementWaitingForInstructions &&
                     words == movementInstructionSpeech) ||
                    (levelZeroStageWaitingForInstructions &&
                     words == levelZeroStageInstructionSpeech);
                SpeechSynthesisStream stream;
                if (isStageInstruction)
                {
                    string safeWords = System.Security.SecurityElement.Escape(words);
                    string ssml = "<speak version='1.0' " +
                        "xmlns='http://www.w3.org/2001/10/synthesis' " +
                        "xml:lang='en-US'>" + safeWords +
                        "<break time='650ms'/></speak>";
                    try
                    {
                        stream = await levelZeroSynth.SynthesizeSsmlToStreamAsync(ssml);
                    }
                    catch (Exception ex)
                    {
                        // SSML support must never disable spoken guidance.
                        Trace("Instruction SSML unavailable; using text speech: " +
                            ex.GetType().Name);
                        stream = await levelZeroSynth.SynthesizeTextToStreamAsync(words);
                    }
                }
                else
                {
                    stream = await levelZeroSynth.SynthesizeTextToStreamAsync(words);
                }
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
                // Do not start the next voice until Windows confirms that
                // the whole current utterance finished playing. A fixed delay
                // could overlap with a longer welcome or stage instruction.
                playedToEnd = await levelZeroPlaybackCompletion.Task;
                if (!playedToEnd)
                    throw new InvalidOperationException(
                        "Level 0 voice playback was interrupted or failed.");
                // Brief breathing room between complete spoken messages.
                await Task.Delay(350);
            }
            catch (Exception ex)
            {
                levelZeroSpeechUnavailable = true;
                levelZeroFeedbackQueue.Clear();
                levelZeroPendingInstruction = null;
                Trace("Level 0 voice failed: " + ex);
            }
            finally
            {
                if (version == levelZeroNarrationVersion)
                {
                    // Only the exact introduction for the active movement
                    // step can start its timer. Delayed earlier narration
                    // must never arm a newer exercise.
                    if (movementWaitingForInstructions &&
                        words == movementInstructionSpeech &&
                        (playedToEnd || levelZeroSpeechUnavailable) &&
                        levelZeroStage == LevelZeroStage.Explore)
                    {
                        ResetMovementStep();
                        movementWaitingForInstructions = false;
                        movementInstructionSpeech = null;
                        HideLevelZeroInstructionText();
                        UpdateLevelZeroMovementGuide();
                    }
                    if (levelZeroStageWaitingForInstructions &&
                        words == levelZeroStageInstructionSpeech &&
                        (playedToEnd || levelZeroSpeechUnavailable))
                    {
                        levelZeroStageStarted = DateTimeOffset.UtcNow;
                        levelZeroStageWaitingForInstructions = false;
                        levelZeroStageInstructionSpeech = null;
                        HideLevelZeroInstructionText();
                        if (levelZeroStage == LevelZeroStage.Pursue)
                            BeginLevelZeroPursuitMotion();
                    }
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
                        else if (levelZeroFinishAfterSpeech)
                        {
                            FinishLevelZero();
                        }
                        else if (levelZeroButtonExplanationPending &&
                                 levelZeroStage == LevelZeroStage.Continue)
                        {
                            RevealLevelZeroContinueButton();
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

        private void ResetLevelZeroFeedback()
        {
            var now = DateTimeOffset.UtcNow;
            levelZeroFeedbackActivityStarted = now;
            levelZeroFeedbackLastCorrect = now;
            levelZeroFeedbackLastPraise = now;
            levelZeroFeedbackLastCoaching = DateTimeOffset.MinValue;
            levelZeroFeedbackCorrectSeconds = 0;
            levelZeroFeedbackHasCorrectMovement = false;
            levelZeroFeedbackPraisedThisStep = false;
            // A coaching message from the previous activity must never play
            // after its instructions have been superseded.
            levelZeroFeedbackQueue.Clear();
        }

        private void RecordLevelZeroCorrectMovement(double seconds)
        {
            if (currentScreen != AppScreen.LevelZero || stopping ||
                movementWaitingForInstructions || levelZeroStageWaitingForInstructions)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            levelZeroFeedbackLastCorrect = now;
            levelZeroFeedbackHasCorrectMovement = true;
            // A queued "try again" is no longer relevant after the learner
            // resumes the requested action. Do not let delayed audio
            // incorrectly announce failure during correct movement.
            if (levelZeroFeedbackQueue.Count > 0 &&
                levelZeroFeedbackQueue.Peek() == LevelZeroShortCoaching())
                levelZeroFeedbackQueue.Clear();
            if (seconds > 0 && seconds <= .5)
                levelZeroFeedbackCorrectSeconds += seconds;
        }

        private string LevelZeroShortPraise()
        {
            if (levelZeroStage == LevelZeroStage.Pursue)
                return "Good job! You're following the circle.";
            if (levelZeroStage == LevelZeroStage.Continue)
                return "Good job! Keep looking at the button.";
            switch (movementStep)
            {
                case MovementStep.Vertical:
                    return "Good job! That's up and down.";
                case MovementStep.Horizontal:
                    return "Good job! That's left and right.";
                case MovementStep.DiagonalDown:
                    return "Good job! You're moving from top left to bottom right.";
                case MovementStep.DiagonalUp:
                    return "Good job! You're moving from bottom left to top right.";
                case MovementStep.Circles:
                    return "Good job! Keep those circles going.";
                default:
                    return "Good job! You're moving your eyes.";
            }
        }

        private string LevelZeroShortCoaching()
        {
            if (levelZeroStage == LevelZeroStage.Pursue)
                return "Try following the glowing circle. Keep your cursor inside it.";
            if (levelZeroStage == LevelZeroStage.Continue)
                return "Look at the Home button and hold your gaze until the ring fills.";
            switch (movementStep)
            {
                case MovementStep.Vertical:
                    return "Try looking up, then down, and repeat.";
                case MovementStep.Horizontal:
                    return "Try looking from left to right and back again.";
                case MovementStep.DiagonalDown:
                    return "Try looking diagonally between the top left and bottom right.";
                case MovementStep.DiagonalUp:
                    return "Try looking diagonally between the bottom left and top right.";
                case MovementStep.Circles:
                    return "Try making comfortable circles with your eyes.";
                default:
                    return "Try moving your eyes around the screen.";
            }
        }

        private void OnLevelZeroCoachTick(object sender, object args)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested)
                return;
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Keep the independent one-minute maximum for every activity.
            if (levelZeroStage == LevelZeroStage.Explore)
            {
                if (movementWaitingForInstructions) return;
                if ((now - movementStepStarted).TotalSeconds >=
                    LevelZeroMaximumStepSeconds)
                {
                    SkipLevelZeroMovementStep();
                    return;
                }
            }
            else
            {
                if (levelZeroStageWaitingForInstructions ||
                    levelZeroButtonExplanationPending) return;
                if ((now - levelZeroStageStarted).TotalSeconds >=
                    LevelZeroMaximumStepSeconds)
                {
                    if (levelZeroStage == LevelZeroStage.Pursue)
                        GoToLevelZeroSelection(true);
                    else if (levelZeroStage == LevelZeroStage.Continue &&
                             !levelZeroFinishAfterSpeech)
                    {
                        levelZeroFinishAfterSpeech = true;
                        levelZeroSelectionTimedOut = true;
                        SpeakLevelZero("Good effort, let's move on to the training levels.", true);
                        if (levelZeroSpeechUnavailable) FinishLevelZero();
                    }
                    return;
                }
            }

            // Correct-button activity: use the actual dwell target, not an
            // inference based on a nearby cursor position.
            if (levelZeroStage == LevelZeroStage.Continue &&
                gazeControlTarget == LevelZeroContinueButton &&
                !gazeControlTriggered)
                RecordLevelZeroCorrectMovement(0);

            // Five seconds without the requested action triggers a short
            // coaching reminder. It can repeat, but never interrupt speech.
            if ((now - levelZeroFeedbackLastCorrect).TotalSeconds >= 5)
            {
                if ((now - levelZeroFeedbackLastCoaching).TotalSeconds >= 10)
                {
                    levelZeroFeedbackLastCoaching = now;
                    levelZeroFeedbackCorrectSeconds = 0;
                    // Drop any stale praise before offering a correction.
                    levelZeroFeedbackQueue.Clear();
                    SpeakLevelZero(LevelZeroShortCoaching());
                }
                return;
            }

            // Praise only if the activity has been genuinely demonstrated.
            // Allow at most one praise message per activity; no praise for a single gaze jitter sample.
            if (levelZeroFeedbackHasCorrectMovement &&
                levelZeroFeedbackCorrectSeconds >= 5.0 &&
                // Only praise while the requested behavior is still being
                // observed, never several seconds after it has stopped.
                (now - levelZeroFeedbackLastCorrect).TotalSeconds <= 2 &&
                !levelZeroFeedbackPraisedThisStep)
            {
                levelZeroFeedbackPraisedThisStep = true;
                levelZeroFeedbackLastPraise = now;
                levelZeroFeedbackCorrectSeconds = 0;
                SpeakLevelZero(LevelZeroShortPraise());
            }
        }

        private void SkipLevelZeroMovementStep()
        {
            if (levelZeroStage != LevelZeroStage.Explore ||
                movementWaitingForInstructions) return;

            switch (movementStep)
            {
                case MovementStep.Discover:
                    NextMovementStep(MovementStep.Vertical,
                        "Good effort, let's move on. Practice looking up and down for fifteen seconds.");
                    break;
                case MovementStep.Vertical:
                    NextMovementStep(MovementStep.Horizontal,
                        "Good effort, let's move on. Practice looking left and right for fifteen seconds.");
                    break;
                case MovementStep.Horizontal:
                    NextMovementStep(MovementStep.DiagonalDown,
                        "Good effort! Next, look diagonally from the top left toward the bottom right, then back. Practice for fifteen seconds.");
                    break;
                case MovementStep.DiagonalDown:
                    NextMovementStep(MovementStep.DiagonalUp,
                        "Good effort! Now look diagonally from the bottom left toward the top right, then back. Practice for fifteen seconds.");
                    break;
                case MovementStep.DiagonalUp:
                    NextMovementStep(MovementStep.Circles,
                        "Good effort! Next, practice making comfortable circles with your eyes for fifteen seconds.");
                    break;
                case MovementStep.Circles:
                    GoToLevelZeroPursuit(true);
                    break;
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
                case MovementStep.DiagonalDown:
                    return "For this exercise, look between the top left and bottom right.";
                case MovementStep.DiagonalUp:
                    return "For this exercise, look between the bottom left and top right.";
                case MovementStep.Circles:
                    return "For this exercise, move your eyes in comfortable circles.";
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
            ResetLevelZeroFeedback();
            lastCorrectAxisMovement = DateTimeOffset.MinValue;
            coachingWindowStarted = movementStepStarted;
            recentCorrectSweeps = recentOtherAxisSweeps = recentDiagonalSweeps = 0;
            hasPreviousMotionPoint = false;
            hasPreviousCircleAngle = false;
            directionWindowInitialized = false;
            discoveryMotionSeconds = stepMotionSeconds = stepTravel = 0;
            circleTravelRadians = 0;
            circleDirection = circleOppositeTurnWindows = circleSectorMask =
                axisReversals = lastAxisDirection = verticalSamples = horizontalSamples =
                diagonalSamples = circleSamples = 0;
        }

        private void UpdateLevelZeroMovementGuide()
        {
            // The guides illustrate the motion only: they are never read by
            // gaze hit testing, diagnostics or lesson completion logic.
            bool exploring = levelZeroStage == LevelZeroStage.Explore &&
                !movementWaitingForInstructions &&
                LevelZeroInstructionPanel.Visibility != Visibility.Visible;
            LevelZeroMovementGuide.Visibility =
                exploring && movementStep != MovementStep.Discover
                    ? Visibility.Visible : Visibility.Collapsed;
            LevelZeroVerticalGuide.Visibility =
                exploring && movementStep == MovementStep.Vertical
                    ? Visibility.Visible : Visibility.Collapsed;
            LevelZeroHorizontalGuide.Visibility =
                exploring && movementStep == MovementStep.Horizontal
                    ? Visibility.Visible : Visibility.Collapsed;
            LevelZeroDiagonalDownGuide.Visibility =
                exploring && movementStep == MovementStep.DiagonalDown
                    ? Visibility.Visible : Visibility.Collapsed;
            LevelZeroDiagonalUpGuide.Visibility =
                exploring && movementStep == MovementStep.DiagonalUp
                    ? Visibility.Visible : Visibility.Collapsed;
            LevelZeroCircleGuide.Visibility =
                exploring && movementStep == MovementStep.Circles
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NextMovementStep(MovementStep next, string instruction)
        {
            if (levelZeroStage != LevelZeroStage.Explore) return;
            movementStep = next;
            movementWaitingForInstructions = true;
            movementInstructionSpeech = instruction;
            ResetMovementStep();
            UpdateLevelZeroMovementGuide();
            levelZeroFeedbackQueue.Clear();
            SpeakLevelZero(instruction, true);
        }

        private void ObserveLevelZeroMovement(Point gaze, double width,
                                              double height, DateTimeOffset now)
        {
            // No movement evidence or active time counts until the full
            // instructions for THIS step have finished playing.
            if (movementWaitingForInstructions) return;
            // Free exploration uses a longer measurement window so genuine
            // slow gaze travel can accumulate past the noise threshold.
            // Directional lessons classify a longer sweep, not a noisy
            // single 100 ms step (see ObserveDirectionWindow below).
            int sampleWindowMs = movementStep == MovementStep.Discover ? 250 : 100;
            if ((now - lastMotionSample).TotalMilliseconds < sampleWindowMs)
                return;
            if (hasPreviousMotionPoint &&
                (now - lastMotionSample).TotalSeconds > 1.5)
            {
                hasPreviousMotionPoint = false;
                hasPreviousCircleAngle = false;
                directionWindowInitialized = false;
            }
            double dt = lastMotionSample == DateTimeOffset.MinValue
                ? 0 : Math.Min(movementStep == MovementStep.Discover ? .35 : .2,
                               (now - lastMotionSample).TotalSeconds);
            lastMotionSample = now;
            if (!hasPreviousMotionPoint)
            {
                previousMotionPoint = gaze;
                hasPreviousMotionPoint = true;
                return;
            }
            // Direction classification must use the same physical X/Y scale.
            // Dividing X by screen width and Y by screen height incorrectly
            // labels left/right motion as up/down on wide displays.
            double dxPixels = gaze.X - previousMotionPoint.X;
            double dyPixels = gaze.Y - previousMotionPoint.Y;
            double dx = dxPixels / width;
            double dy = dyPixels / height;
            previousMotionPoint = gaze;
            double displacement = Math.Sqrt(dx * dx + dy * dy);
            if (displacement > .32) return; // Likely a tracking jump.

            // Circles use longer direction windows instead of rejecting
            // gentle movements based on the size of each 100 ms sample.
            if (movementStep == MovementStep.Circles)
            {
                ObserveCircleWindow(gaze, width, height, now);
                return;
            }

            // A tiny change is not evidence of intentional movement.
            // A less stringent threshold is reserved for later directions
            // so slow but deliberate vertical/horizontal motion still counts.
            double threshold = movementStep == MovementStep.Discover
                ? .025 : .012;
            if (displacement < threshold)
            {
                if (movementStep == MovementStep.Vertical ||
                    movementStep == MovementStep.Horizontal)
                    ObserveDirectionWindow(gaze, width, height, now);
                else if (movementStep == MovementStep.DiagonalDown ||
                         movementStep == MovementStep.DiagonalUp)
                    ObserveDiagonalWindow(gaze, width, height, now);
                return;
            }
            hasActualMovement = true;
            lastMovement = now;
            bool vertical = Math.Abs(dyPixels) > 1.8 * Math.Abs(dxPixels);
            bool horizontal = Math.Abs(dxPixels) > 1.8 * Math.Abs(dyPixels);
            // For directed exercises count only classifications from the
            // longer sweep window; instantaneous deltas are too noisy.
            if (movementStep == MovementStep.Discover ||
                movementStep == MovementStep.Circles)
            {
                if (vertical) verticalSamples++;
                else if (horizontal) horizontalSamples++;
                else diagonalSamples++;
            }

            if (movementStep == MovementStep.Discover)
            {
                // Count only sampled intervals with real cursor displacement.
                // Pauses and stationary tracker jitter do not contribute.
                discoveryMotionSeconds += dt;
                stepTravel += displacement;
                RecordLevelZeroCorrectMovement(dt);
                if (discoveryMotionSeconds >= 15.0 && stepTravel >= .50)
                    NextMovementStep(MovementStep.Vertical,
                        "There you go! You're moving the cursor. Now look toward the top, then down toward the bottom. Move your eyes up and down for fifteen seconds.");
                return;
            }

            if (movementStep == MovementStep.Vertical ||
                movementStep == MovementStep.Horizontal)
            {
                ObserveDirectionWindow(gaze, width, height, now);
                return;
            }
            if (movementStep == MovementStep.DiagonalDown ||
                movementStep == MovementStep.DiagonalUp)
            {
                ObserveDiagonalWindow(gaze, width, height, now);
                return;
            }

            // Circular movement is measured in 350 ms direction windows
            // below, not in individual noisy tracker samples.
        }

        // A measured sweep is long enough to recognize gentle eye movement,
        // but short enough to follow a comfortable circle. The guide itself
        // has no scoring role; the user may draw a circle anywhere.
        private void ObserveCircleWindow(Point gaze, double width,
                                         double height, DateTimeOffset now)
        {
            if (!directionWindowInitialized)
            {
                directionWindowStart = gaze;
                directionWindowStarted = now;
                directionWindowInitialized = true;
                return;
            }
            double seconds = (now - directionWindowStarted).TotalSeconds;
            if (seconds < .35) return;
            Point start = directionWindowStart;
            directionWindowStart = gaze;
            directionWindowStarted = now;
            if (seconds > 1.2)
            {
                // Lost tracker samples cannot count toward practice.
                hasPreviousCircleAngle = false;
                return;
            }

            double x = gaze.X - start.X;
            double y = gaze.Y - start.Y;
            double travel = Math.Sqrt(x * x + y * y);
            double minTravel = Math.Max(8.0, Math.Min(width, height) * .007);
            if (travel < minTravel ||
                travel > Math.Max(width, height) * .32)
            {
                // No intentional displacement, or an implausible tracker jump.
                return;
            }

            hasActualMovement = true;
            lastMovement = now;
            double heading = Math.Atan2(y, x);
            int sector = (int)Math.Floor((heading + Math.PI) / (Math.PI / 4));
            circleSectorMask |= 1 << Math.Max(0, Math.Min(7, sector));

            if (hasPreviousCircleAngle)
            {
                double turn = heading - previousCircleAngle;
                while (turn > Math.PI) turn -= 2 * Math.PI;
                while (turn < -Math.PI) turn += 2 * Math.PI;
                // A gentle circle may turn only a few degrees per window.
                // Reject near-straight travel, sudden direction reversals,
                // and tracker noise without requiring a particular radius.
                double magnitude = Math.Abs(turn);
                if (magnitude >= .06 && magnitude <= 1.65)
                {
                    int sign = Math.Sign(turn);
                    if (circleDirection == 0) circleDirection = sign;
                    if (circleDirection != sign)
                    {
                        // One bad turn must not lock a learner into the wrong
                        // direction for the entire lesson. Two consecutive
                        // opposite turns permit a genuine direction change.
                        circleOppositeTurnWindows++;
                        if (circleOppositeTurnWindows >= 2)
                        {
                            circleDirection = sign;
                            circleOppositeTurnWindows = 0;
                        }
                    }
                    else circleOppositeTurnWindows = 0;
                    if (circleDirection == sign)
                    {
                        double creditedSeconds = Math.Min(.5, seconds);
                        circleTravelRadians += magnitude;
                        circleSamples++;
                        diagnosticCircleSamples++;
                        stepMotionSeconds += creditedSeconds;
                        RecordLevelZeroCorrectMovement(creditedSeconds);
                    }
                }
            }
            previousCircleAngle = heading;
            hasPreviousCircleAngle = true;
            // Fifteen seconds of detected turning movement, not time since
            // the instruction ended. A complete pattern must cover different
            // headings and a meaningful amount of total angular travel.
            if (stepMotionSeconds >= 15.0 &&
                circleTravelRadians >= Math.PI * 1.5 &&
                circleSamples >= 10 && CountBits(circleSectorMask) >= 5)
                GoToLevelZeroPursuit();
        }

        // Judge the direction over a 350 ms gaze sweep rather than the
        // instantaneous 100 ms tracker delta. This tolerates tiny sideways
        // fluctuations during a real up/down movement (and vice versa).
        private void ObserveDirectionWindow(Point gaze, double width,
                                            double height, DateTimeOffset now)
        {
            if (!directionWindowInitialized)
            {
                directionWindowStart = gaze;
                directionWindowStarted = now;
                directionWindowInitialized = true;
                return;
            }
            double seconds = (now - directionWindowStarted).TotalSeconds;
            if (seconds < .35) return;
            if (seconds > 1.2)
            {
                // Do not credit gaps in input to correct movement time.
                directionWindowStart = gaze;
                directionWindowStarted = now;
                return;
            }

            // Coaching only considers the most recent five seconds.
            if ((now - coachingWindowStarted).TotalSeconds >= 5)
            {
                coachingWindowStarted = now;
                recentCorrectSweeps = recentOtherAxisSweeps = recentDiagonalSweeps = 0;
            }
            double dx = gaze.X - directionWindowStart.X;
            double dy = gaze.Y - directionWindowStart.Y;
            directionWindowStart = gaze;
            directionWindowStarted = now;

            double primary = movementStep == MovementStep.Vertical ? dy : dx;
            double secondary = movementStep == MovementStep.Vertical ? dx : dy;
            // Detect even gentle sweeps, but require displacement large
            // enough to exceed residual tracker wobble.
            double minimumPixels = Math.Max(8, Math.Min(width, height) * .007);
            if (Math.Abs(primary) < minimumPixels ||
                Math.Abs(primary) < 1.35 * Math.Abs(secondary))
            {
                // Record substantive wrong-axis sweeps for specific coaching;
                // neither wrong-way nor tiny movement advances the exercise.
                if (Math.Abs(secondary) >= minimumPixels &&
                    Math.Abs(secondary) > Math.Abs(primary) * 1.35)
                {
                    if (movementStep == MovementStep.Vertical) horizontalSamples++;
                    else verticalSamples++;
                    recentOtherAxisSweeps++;
                }
                else if (Math.Max(Math.Abs(primary), Math.Abs(secondary)) >= minimumPixels)
                {
                    diagonalSamples++;
                    recentDiagonalSweeps++;
                }
                return;
            }

            int direction = Math.Sign(primary);
            if (lastAxisDirection != 0 && direction != lastAxisDirection)
                axisReversals++;
            lastAxisDirection = direction;
            lastCorrectAxisMovement = now;
            if (movementStep == MovementStep.Vertical) diagnosticVerticalSweeps++;
            else if (movementStep == MovementStep.Horizontal) diagnosticHorizontalSweeps++;
            recentCorrectSweeps++;
            stepMotionSeconds += Math.Min(.5, seconds);
            RecordLevelZeroCorrectMovement(Math.Min(.5, seconds));
            stepTravel += Math.Abs(primary) /
                (movementStep == MovementStep.Vertical ? height : width);
            lastMovement = now;
            if (movementStep == MovementStep.Vertical) verticalSamples++;
            else horizontalSamples++;

            // Only correct-axis, above-noise sweeps count; a timer running
            // while stationary or looking in the other direction cannot pass.
            if (stepMotionSeconds >= 15.0 && axisReversals >= 2 &&
                stepTravel >= .35)
            {
                if (movementStep == MovementStep.Vertical)
                    NextMovementStep(MovementStep.Horizontal,
                        "Great work! You've completed fifteen seconds of up and down movement. Next, look left and right for fifteen seconds.");
                else
                    NextMovementStep(MovementStep.DiagonalDown,
                        "Excellent! You've practiced left and right. Now look diagonally from the top left toward the bottom right, then back again. Practice for fifteen seconds.");
            }
        }

        // The two diagonal lessons measure the direction of gaze over 350 ms,
        // not where the eye cursor crosses the decorative arrow. Screen
        // proportions are normalized so the same diagonal is usable on wide
        // and narrow displays. Both outward and return sweeps count.
        private void ObserveDiagonalWindow(Point gaze, double width,
                                            double height, DateTimeOffset now)
        {
            if (!directionWindowInitialized)
            {
                directionWindowStart = gaze;
                directionWindowStarted = now;
                directionWindowInitialized = true;
                return;
            }
            double seconds = (now - directionWindowStarted).TotalSeconds;
            if (seconds < .35) return;
            Point start = directionWindowStart;
            directionWindowStart = gaze;
            directionWindowStarted = now;
            if (seconds > 1.2) return; // A tracker gap is not exercise time.

            double dx = (gaze.X - start.X) / width;
            double dy = (gaze.Y - start.Y) / height;
            double absX = Math.Abs(dx), absY = Math.Abs(dy);
            // Two substantive components, with comparable normalized travel.
            // Perpendicular diagonals, horizontal/vertical and jitter do not
            // qualify. A deliberate 180-degree return is valid.
            const double minComponent = .008;
            bool correctSlope = movementStep == MovementStep.DiagonalDown
                ? dx * dy > 0 : dx * dy < 0;
            if (!correctSlope || absX < minComponent || absY < minComponent ||
                absX < absY * .50 || absY < absX * .50 ||
                absX > .32 || absY > .32)
            {
                return;
            }

            int direction = Math.Sign(dx);
            if (lastAxisDirection != 0 && direction != lastAxisDirection)
                axisReversals++;
            lastAxisDirection = direction;
            lastCorrectAxisMovement = now;
            recentCorrectSweeps++;
            diagonalSamples++;
            lastMovement = now;
            double creditedSeconds = Math.Min(.5, seconds);
            stepMotionSeconds += creditedSeconds;
            stepTravel += Math.Sqrt(dx * dx + dy * dy);
            RecordLevelZeroCorrectMovement(creditedSeconds);

            // Count measured correct-direction movement for 15 seconds,
            // including at least two reversals and meaningful gaze travel.
            if (stepMotionSeconds >= 15.0 && axisReversals >= 2 &&
                stepTravel >= .35)
            {
                if (movementStep == MovementStep.DiagonalDown)
                    NextMovementStep(MovementStep.DiagonalUp,
                        "Great work! You've completed the first diagonal. Now look from the bottom left toward the top right, then back again. Practice for fifteen seconds.");
                else
                    NextMovementStep(MovementStep.Circles,
                        "Excellent! You've practiced both diagonal directions. Now make comfortable circles with your eyes for fifteen seconds.");
            }
        }

        // CompositionTarget.Rendering tracks display refresh instead of a
        // 50 ms dispatcher timer (which stepped the marker at only 20 Hz).
        private void StartLevelZeroRendering()
        {
            if (levelZeroRenderingSubscribed) return;
            levelZeroLastRenderedAt = default(DateTimeOffset);
            levelZeroLastRenderedTarget = new Point(double.NaN, double.NaN);
            CompositionTarget.Rendering += OnLevelZeroRendering;
            levelZeroRenderingSubscribed = true;
        }

        private void StopLevelZeroRendering()
        {
            if (!levelZeroRenderingSubscribed) return;
            CompositionTarget.Rendering -= OnLevelZeroRendering;
            levelZeroRenderingSubscribed = false;
        }

        private void PositionLevelZeroPursuitTarget(double x, double y)
        {
            // Keep the full moving target away from the display edges.
            double w = LevelZeroCanvas.ActualWidth;
            double h = LevelZeroCanvas.ActualHeight;
            x = Math.Max(LevelZeroPursuitRadius,
                Math.Min(w - LevelZeroPursuitRadius, x));
            y = Math.Max(LevelZeroPursuitRadius,
                Math.Min(h - LevelZeroPursuitRadius, y));
            // Avoid four XAML layout-property writes for subpixel motion.
            // Keep gaze collision/diagnostic math independent of this visual.
            if (Math.Abs(x - levelZeroLastRenderedTarget.X) < 0.35 &&
                Math.Abs(y - levelZeroLastRenderedTarget.Y) < 0.35) return;
            levelZeroLastRenderedTarget = new Point(x, y);
            Canvas.SetLeft(LevelZeroPursuitTarget, x - LevelZeroPursuitRadius);
            Canvas.SetTop(LevelZeroPursuitTarget, y - LevelZeroPursuitRadius);
            Canvas.SetLeft(LevelZeroPursuitCenter, x - 9);
            Canvas.SetTop(LevelZeroPursuitCenter, y - 9);
        }

        private void BeginLevelZeroPursuitMotion()
        {
            if (levelZeroStage != LevelZeroStage.Pursue) return;
            double w = LevelZeroCanvas.ActualWidth;
            double h = LevelZeroCanvas.ActualHeight;
            if (w < 350 || h < 280)
            {
                w = Math.Max(350, RootLayout.ActualWidth);
                h = Math.Max(280, RootLayout.ActualHeight);
            }

            // Match the visible stabilized eye cursor rather than beginning
            // at an unrelated fixed center position.
            Point gaze = new Point(w / 2, h / 2);
            if (levelZeroHadGaze &&
                (DateTimeOffset.UtcNow - levelZeroLastValid).TotalSeconds <= 3)
            {
                double x = Canvas.GetLeft(LevelZeroEyeCursor);
                double y = Canvas.GetTop(LevelZeroEyeCursor);
                if (!double.IsNaN(x) && !double.IsNaN(y) &&
                    LevelZeroEyeCursor.Visibility == Visibility.Visible)
                    gaze = new Point(x + 56, y + 32.5);
                else
                    gaze = mostRecentRawGazePosition;
            }
            levelZeroPursuitStartPoint = new Point(
                Math.Max(LevelZeroPursuitRadius,
                    Math.Min(w - LevelZeroPursuitRadius, gaze.X)),
                Math.Max(LevelZeroPursuitRadius,
                    Math.Min(h - LevelZeroPursuitRadius, gaze.Y)));
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            levelZeroLastPursuitSample = DateTimeOffset.MinValue;
            levelZeroPursuitInsideSeconds = 0;
            levelZeroPursuitLastInside = levelZeroStageStarted;
            levelZeroPursuitPreviouslyInside = false;
            levelZeroPursuitOutsideCoached = false;
            ResetLevelZeroFeedback();
            PositionLevelZeroPursuitTarget(
                levelZeroPursuitStartPoint.X, levelZeroPursuitStartPoint.Y);
            LevelZeroPursuitTarget.Visibility = Visibility.Visible;
            LevelZeroPursuitCenter.Visibility = Visibility.Visible;
            StartLevelZeroRendering();
        }

        private void OnLevelZeroRendering(object sender, object args)
        {
            if (currentScreen != AppScreen.LevelZero || stopping || exitRequested) return;
            if (levelZeroStage != LevelZeroStage.Pursue ||
                levelZeroStageWaitingForInstructions) return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            // A 120/144 Hz screen must not force pursuit layout updates at
            // every refresh. Approximately 60 Hz retains smooth motion.
            if (levelZeroLastRenderedAt != default(DateTimeOffset) &&
                (now - levelZeroLastRenderedAt).TotalMilliseconds < 15) return;
            levelZeroLastRenderedAt = now;
            double elapsed = (now - levelZeroStageStarted).TotalSeconds;
            double w = LevelZeroCanvas.ActualWidth, h = LevelZeroCanvas.ActualHeight;
            if (w < 350 || h < 280) return;
            // Begin at the eye cursor, then smoothly blend into the
            // familiar broad pursuit path. No discontinuity when the
            // narration finishes or the first rendering frame arrives.
            double desiredX = w * (0.5 + 0.32 * Math.Sin(elapsed * 0.38));
            double desiredY = h * (0.5 + 0.21 * Math.Sin(elapsed * 0.24));
            double blend = Math.Min(1.0, Math.Max(0.0, elapsed / 5.0));
            blend = blend * blend * (3.0 - 2.0 * blend);
            double cx = levelZeroPursuitStartPoint.X +
                (desiredX - levelZeroPursuitStartPoint.X) * blend;
            double cy = levelZeroPursuitStartPoint.Y +
                (desiredY - levelZeroPursuitStartPoint.Y) * blend;
            PositionLevelZeroPursuitTarget(cx, cy);
            // Require fifteen cumulative seconds of measured gaze
            // inside the larger circle before moving to button practice.
            if (levelZeroPursuitInsideSeconds >= 15.0)
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
            ObserveLevelZeroDiagnosticGaze(raw, now);
            if (!levelZeroHadGaze)
                levelZeroTimeToFirstGaze = (now - levelZeroStarted).TotalSeconds;
            levelZeroHadGaze = true;

            // The welcome is spoken on screen entry, not delayed until
            // the first gaze event. That also starts the 60-second fallback
            // for learners who are not yet producing gaze input.
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
                if (levelZeroStageWaitingForInstructions ||
                    (now - levelZeroLastPursuitSample).TotalMilliseconds < 80)
                    return;
                // Tracking gaps do not count as continuous inside time.
                double sampleSeconds = levelZeroLastPursuitSample ==
                    DateTimeOffset.MinValue ? 0 :
                    (now - levelZeroLastPursuitSample).TotalSeconds;
                levelZeroLastPursuitSample = now;
                levelZeroPursuitSamples++;
                double cx = Canvas.GetLeft(LevelZeroPursuitTarget) +
                    LevelZeroPursuitRadius;
                double cy = Canvas.GetTop(LevelZeroPursuitTarget) +
                    LevelZeroPursuitRadius;
                double dx = stabilized.X - cx, dy = stabilized.Y - cy;
                bool inside = dx * dx + dy * dy <=
                    LevelZeroPursuitInsideRadius * LevelZeroPursuitInsideRadius;
                if (inside)
                {
                    levelZeroCloseSamples++;
                    levelZeroPursuitLastInside = now;
                    levelZeroPursuitOutsideCoached = false;
                    if (levelZeroPursuitPreviouslyInside && sampleSeconds <= .3)
                    {
                        levelZeroPursuitInsideSeconds += sampleSeconds;
                        RecordLevelZeroCorrectMovement(sampleSeconds);
                    }
                    if (levelZeroPursuitInsideSeconds >= 15.0)
                        GoToLevelZeroSelection();
                }
                levelZeroPursuitPreviouslyInside = inside;
            }
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0) { count += mask & 1; mask >>= 1; }
            return count;
        }

        private void GoToLevelZeroPursuit(bool skipped = false)
        {
            if (levelZeroStage != LevelZeroStage.Explore) return;
            levelZeroStage = LevelZeroStage.Pursue;
            UpdateLevelZeroMovementGuide();
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            levelZeroStageWaitingForInstructions = true;
            // Do not show or animate until the spoken directions complete.
            // The target will first appear at the CURRENT cursor position.
            StopLevelZeroRendering();
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            levelZeroFeedbackQueue.Clear();
            string explanation = skipped
                ? "Good effort, let's move on. A large glowing circle will move around the screen. Keep your eye cursor inside the circle for fifteen seconds of practice."
                : "Excellent work! You practiced moving up and down, left and right, both diagonal directions, and drawing circles. Next, a large glowing circle will move around the screen. Follow it with your eyes and keep your eye cursor inside the circle for fifteen seconds.";
            levelZeroStageInstructionSpeech = explanation;
            SpeakLevelZero(explanation, true);
        }

        private void GoToLevelZeroSelection(bool skipped = false)
        {
            if (levelZeroStage != LevelZeroStage.Pursue) return;
            levelZeroStage = LevelZeroStage.Continue;
            StopLevelZeroRendering();
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            levelZeroStageWaitingForInstructions = true;
            LevelZeroPursuitTarget.Visibility = Visibility.Collapsed;
            LevelZeroPursuitCenter.Visibility = Visibility.Collapsed;
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            levelZeroButtonExplanationPending = true;
            levelZeroCoachedPursuit = false;
            levelZeroCoachedButton = false;
            levelZeroFeedbackQueue.Clear();
            string explanation = skipped
                ? "Good effort, let's move on. One final step. A large Home button will appear. Look toward it and keep looking until the circular ring fills to open your training levels."
                : "Fantastic! You've learned how to follow a moving target. One final step. A large Home button will appear. It takes you to the Level Menu, where you can start Level One. Look at the button and keep looking until the circular ring fills. Give it a try!";
            levelZeroStageInstructionSpeech = explanation;
            SpeakLevelZero(explanation, true);
        }

        // Choose the furthest corner from the current gaze. The button is
        // kept comfortably inside the view and is only positioned when the
        // complete spoken explanation has finished, so gaze cannot already
        // be dwelling on a previously visible button.
        private void RevealLevelZeroContinueButton()
        {
            if (levelZeroStage != LevelZeroStage.Continue) return;
            double w = LevelZeroPanel.ActualWidth;
            double h = LevelZeroPanel.ActualHeight;
            const double buttonW = 330, buttonH = 210;
            const double padding = 36;
            if (w <= 0 || h <= 0)
            {
                // If layout has not reported its size, still allow the
                // learner to reach the button rather than wait indefinitely.
                w = Math.Max(800, RootLayout.ActualWidth);
                h = Math.Max(600, RootLayout.ActualHeight);
            }

            double maxLeft = Math.Max(0, w - buttonW - padding);
            double maxTop = Math.Max(0, h - buttonH - padding);
            double leftEdge = Math.Min(padding, Math.Max(0, w - buttonW));
            double topEdge = Math.Min(padding, Math.Max(0, h - buttonH));
            double rightEdge = Math.Max(leftEdge, maxLeft);
            double bottomEdge = Math.Max(topEdge, maxTop);

            Point gaze = mostRecentRawGazePosition;
            if (double.IsNaN(gaze.X) || double.IsNaN(gaze.Y) ||
                gaze.X < 0 || gaze.Y < 0 || gaze.X > w || gaze.Y > h)
                gaze = new Point(w / 2, h / 2);

            double bestX = leftEdge, bestY = topEdge, best = -1;
            double[] xs = { leftEdge, rightEdge };
            double[] ys = { topEdge, bottomEdge };
            foreach (double x in xs)
                foreach (double y in ys)
                {
                    // Maximize the distance from the current gaze to the
                    // nearest edge of the button, not just its center.
                    double dx = Math.Max(x - gaze.X,
                        Math.Max(0, gaze.X - (x + buttonW)));
                    double dy = Math.Max(y - gaze.Y,
                        Math.Max(0, gaze.Y - (y + buttonH)));
                    double distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > best)
                    {
                        best = distanceSquared;
                        bestX = x;
                        bestY = y;
                    }
                }
            LevelZeroContinueButton.Margin =
                new Thickness(bestX, bestY, 0, 0);
            levelZeroButtonExplanationPending = false;
            levelZeroStageStarted = DateTimeOffset.UtcNow;
            levelZeroStageWaitingForInstructions = false;
            ResetLevelZeroFeedback();
            // Never carry over dwell already collected at the prior screen
            // position, even if the user was staring at that position.
            gazeControlTarget = null;
            gazeControlTriggered = false;
            buttonDwellWheel.Hide();
            LevelZeroContinueButton.Visibility = Visibility.Visible;
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
                levelZeroStage != LevelZeroStage.Continue ||
                levelZeroFinishAfterSpeech || stopping || exitRequested) return;

            // Selecting Home completes the introduction. Show and speak the
            // congratulations before automatically opening the Level Menu.
            // There is no additional button or caregiver action.
            LevelZeroContinueButton.Visibility = Visibility.Collapsed;
            buttonDwellWheel.Hide();
            gazeControlTarget = null;
            gazeControlTriggered = false;
            levelZeroFeedbackQueue.Clear();
            levelZeroPendingInstruction = null;
            LevelZeroCompletionPanel.Visibility = Visibility.Visible;
            levelZeroFinishAfterSpeech = true;

            // Do not strand an eye-gaze-only learner if speech playback stalls.
            StopLevelZeroCompletionTimer();
            levelZeroCompletionSafetyTimer = new DispatcherTimer
                { Interval = TimeSpan.FromSeconds(35) };
            levelZeroCompletionSafetyTimer.Tick += OnLevelZeroCompletionTimeout;
            levelZeroCompletionSafetyTimer.Start();

            if (levelZeroSpeechUnavailable)
            {
                // Leave the written message visible briefly even without audio.
                StopLevelZeroCompletionTimer();
                levelZeroCompletionSafetyTimer = new DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(9) };
                levelZeroCompletionSafetyTimer.Tick += OnLevelZeroCompletionTimeout;
                levelZeroCompletionSafetyTimer.Start();
                return;
            }
            SpeakLevelZero(LevelZeroCompletionMessage, true);
        }

        private void OnLevelZeroCompletionTimeout(object sender, object args)
        {
            StopLevelZeroCompletionTimer();
            if (currentScreen == AppScreen.LevelZero && levelZeroFinishAfterSpeech &&
                !stopping && !exitRequested)
                FinishLevelZero();
        }

        private void StopLevelZeroCompletionTimer()
        {
            if (levelZeroCompletionSafetyTimer == null) return;
            levelZeroCompletionSafetyTimer.Stop();
            levelZeroCompletionSafetyTimer.Tick -= OnLevelZeroCompletionTimeout;
            levelZeroCompletionSafetyTimer = null;
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
                v[p + "Selection"] = levelZeroSelectionTimedOut
                    ? "Moved on after 60 sec (" + levelZeroDwellAttempts + " attempts)"
                    : "Completed (" + levelZeroDwellAttempts + " attempts)";
            }
            catch (Exception) { /* Measurements never block learner navigation. */ }
        }

        private void FinishLevelZero()
        {
            SaveLevelZeroMeasurements();
            SaveLevelZeroExtraDiagnostics();
            try { ApplicationData.Current.LocalSettings.Values[LevelZeroDoneKey] = true; }
            catch (Exception) { /* A storage error never prevents access to Levels. */ }
            StopLevelZero();
            UpdateLevelBadges();
            LevelsNoticeText.Text = "";
            ShowScreen(AppScreen.Levels);
        }
    }
}
