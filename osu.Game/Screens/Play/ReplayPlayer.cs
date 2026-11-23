// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Play.PlayerSettings;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Skinning;
using osu.Game.Users;

namespace osu.Game.Screens.Play
{
    [Cached]
    public partial class ReplayPlayer : Player, IKeyBindingHandler<GlobalAction>
    {
        public const double BASE_SEEK_AMOUNT = 1000;

        private readonly Func<IBeatmap, IReadOnlyList<Mod>, Score> createScore;

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly SoloGameplayLeaderboardProvider leaderboardProvider = new SoloGameplayLeaderboardProvider();

        protected override UserActivity? InitialActivity =>
            // score may be null if LoadedBeatmapSuccessfully is false.
            Score == null ? null : new UserActivity.WatchingReplay(Score.ScoreInfo);

        private bool isAutoplayPlayback => GameplayState.Mods.OfType<ModAutoplay>().Any();

        private double? lastFrameTime;

        private double userPlaybackRateBeforeFastForward;

        private const double miss_seek_lead_in = 1000;

        private readonly SortedSet<double> missTimes = new SortedSet<double>();

        private MissSeekRequest? missSeekRequest;
        private double? selectedMissTime;
        private double selectedMissSeekTime;
        private bool performingMissSeek;

        private ReplayFailIndicator? failIndicator;
        private PlaybackSettings? playbackSettings;

        public ReplayOverlay ReplayOverlay { get; private set; } = null!;

        protected override bool CheckModsAllowFailure()
        {
            // Searching should not fail the replay or interrupt frame-stable catch-up.
            if (missSeekRequest != null)
                return false;

            // autoplay should be able to fail if the beatmap is not humanly beatable
            if (isAutoplayPlayback)
                return base.CheckModsAllowFailure();

            // non-autoplay replays should be able to fail, but only after they've exhausted their frames.
            // note that the rank isn't checked here - that's because it is generally unreliable.
            // stable replays, as well as lazer replays recorded prior to https://github.com/ppy/osu/pull/28058,
            // do not even *contain* the user's rank.
            // not to mention possible gameplay mechanics changes that could make a replay fail sooner than it really should.
            if (GameplayClockContainer.CurrentTime >= lastFrameTime)
                return base.CheckModsAllowFailure();

            return false;
        }

        public ReplayPlayer(Score score, PlayerConfiguration? configuration = null)
            : this((_, _) => score, configuration)
        {
        }

        public ReplayPlayer(Func<IBeatmap, IReadOnlyList<Mod>, Score> createScore, PlayerConfiguration? configuration = null)
            : base(configuration)
        {
            this.createScore = createScore;
            Configuration.ShowLeaderboard = true;
        }

        /// <summary>
        /// Add a settings group to the HUD overlay. Intended to be used by rulesets to add replay-specific settings.
        /// </summary>
        /// <param name="settings">The settings group to be shown.</param>
        public void AddSettings(PlayerSettingsGroup settings) => Schedule(() => ReplayOverlay.Settings.Add(settings));

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            if (!LoadedBeatmapSuccessfully)
                return;

            AddInternal(leaderboardProvider);

            GameplayClockContainer.Add(ReplayOverlay = new ReplayOverlay());

            playbackSettings = new PlaybackSettings
            {
                Depth = float.MaxValue,
                Expanded = { BindTarget = config.GetBindable<bool>(OsuSetting.ReplayPlaybackControlsExpanded) }
            };

            if (GameplayClockContainer is MasterGameplayClockContainer master)
                playbackSettings.UserPlaybackRate.BindTo(master.UserPlaybackRate);

            ReplayOverlay.Settings.AddAtStart(playbackSettings);

            OsuTextFlowContainer message = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Style.Body) { AutoSizeAxes = Axes.Both };
            message.AddText("Watching ");
            message.AddText(Score.ScoreInfo.User.Username, s => s.Font = s.Font.With(weight: FontWeight.SemiBold));
            message.AddText(" play ");
            message.AddText(Beatmap.Value.BeatmapInfo.GetDisplayTitleRomanisable(), s => s.Font = s.Font.With(weight: FontWeight.SemiBold));
            message.AddText(" on ");
            message.AddArbitraryDrawable(new PlayedOnText(Score.ScoreInfo.Date, false)
            {
                Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
            });

            ReplayOverlay.SetMessage(new ScrollingMessage(message)
            {
                Y = 96,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
            });

            RulesetSkinProvidingContainer rulesetSkinProvider;
            AddInternal(rulesetSkinProvider = new RulesetSkinProvidingContainer(GameplayState.Ruleset, GameplayState.Beatmap, Beatmap.Value.Skin)
            {
                Child = failIndicator = new ReplayFailIndicator(GameplayClockContainer)
                {
                    GoToResults = () =>
                    {
                        if (!this.IsCurrentScreen())
                            return;

                        ValidForResume = false;
                        this.Push(new SoloResultsScreen(Score.ScoreInfo));
                    }
                }
            });
            config.BindWith(OsuSetting.BeatmapSkins, rulesetSkinProvider.BeatmapSkins);
            config.BindWith(OsuSetting.BeatmapColours, rulesetSkinProvider.BeatmapColours);
            config.BindWith(OsuSetting.BeatmapHitsounds, rulesetSkinProvider.BeatmapHitsounds);
        }

        protected override void PrepareReplay()
        {
            DrawableRuleset?.SetReplayScore(Score);
            lastFrameTime = Score.Replay.Frames.LastOrDefault()?.Time;

            DrawableRuleset!.NewResult += onReplayResult;
            GameplayClockContainer.OnSeek += onSeek;
        }

        protected override Score CreateScore(IBeatmap beatmap) => createScore(beatmap, Mods.Value);

        // Don't re-import replay scores as they're already present in the database.
        protected override Task ImportScore(Score score) => Task.CompletedTask;

        protected override ResultsScreen CreateResults(ScoreInfo score) => new SoloResultsScreen(score)
        {
            // Only show the relevant button otherwise things look silly.
            AllowWatchingReplay = !isAutoplayPlayback,
            AllowRetry = isAutoplayPlayback,
        };

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!LoadedBeatmapSuccessfully)
                return false;

            switch (e.Action)
            {
                case GlobalAction.StepReplayBackward:
                    StepFrame(-1);
                    return true;

                case GlobalAction.StepReplayForward:
                    StepFrame(1);
                    return true;

                case GlobalAction.SeekReplayBackward:
                    SeekInDirection(-5 * (float)playbackSettings!.UserPlaybackRate.Value);
                    return true;

                case GlobalAction.SeekReplayForward:
                    SeekInDirection(5 * (float)playbackSettings!.UserPlaybackRate.Value);
                    return true;

                case GlobalAction.TogglePauseReplay:
                    if (GameplayClockContainer.IsPaused.Value)
                        GameplayClockContainer.Start();
                    else
                        GameplayClockContainer.Stop();
                    return true;

                case GlobalAction.FastForwardReplay:
                    if (e.Repeat) return false;

                    userPlaybackRateBeforeFastForward = playbackSettings!.UserPlaybackRate.Value;
                    playbackSettings!.UserPlaybackRate.Value *= 2;
                    return true;
            }

            return false;
        }

        public void StepFrame(int direction)
        {
            GameplayClockContainer.Stop();

            var frames = GameplayState.Score.Replay.Frames;

            if (frames.Count == 0)
                return;

            GameplayClockContainer.Seek(direction < 0
                ? (frames.LastOrDefault(f => f.Time < GameplayClockContainer.CurrentTime) ?? frames.First()).Time
                : (frames.FirstOrDefault(f => f.Time > GameplayClockContainer.CurrentTime) ?? frames.Last()).Time
            );
        }

        public void SeekInDirection(float amount)
        {
            double target = Math.Clamp(GameplayClockContainer.CurrentTime + amount * BASE_SEEK_AMOUNT, 0, GameplayState.Beatmap.GetLastObjectTime());

            Seek(target);
        }

        /// <summary>
        /// Seek to one second before the previous or next Miss, retaining the playback state.
        /// </summary>
        /// <param name="direction">A negative value seeks backwards; a positive value seeks forwards.</param>
        public void SeekToMiss(int direction)
        {
            if (!LoadedBeatmapSuccessfully || direction == 0 || missSeekRequest != null || GameplayState.HasFailed)
                return;

            double currentTime = GameplayClockContainer.CurrentTime;
            // Repeated navigation should move between misses, rather than selecting the same miss during its lead-in.
            double referenceTime = selectedMissTime != null && currentTime >= selectedMissSeekTime && currentTime < selectedMissTime
                ? selectedMissTime.Value
                : currentTime;

            double? target = direction < 0
                ? missTimes.Where(time => time < referenceTime).Select(time => (double?)time).LastOrDefault()
                : missTimes.Where(time => time > referenceTime).Select(time => (double?)time).FirstOrDefault();

            if (target != null)
            {
                seekBeforeMiss(target.Value);
                return;
            }

            if (direction < 0)
                return;

            // Replays contain inputs, not judgement timestamps. Seek with frame stability enabled to discover the next miss.
            // Temporarily pause audio and suppress results while searching, including on perfect and failed replays.
            double endTime = GameplayState.Beatmap.HitObjects.Max(h => h.GetEndTime() + h.MaximumJudgementOffset) + miss_seek_lead_in;
            missSeekRequest = new MissSeekRequest(currentTime, referenceTime, endTime, GameplayClockContainer.IsPaused.Value, Configuration.ShowResults);
            Configuration.ShowResults = false;
            GameplayClockContainer.Stop();
            seekForMiss(endTime);
        }

        private void onReplayResult(JudgementResult result)
        {
            if (result.Type != HitResult.Miss)
                return;

            missTimes.Add(result.TimeAbsolute);

            if (missSeekRequest == null || missSeekRequest.RestoreTime != null || missSeekRequest.TargetTime != null || result.TimeAbsolute <= missSeekRequest.ReferenceTime)
                return;

            missSeekRequest.TargetTime = result.TimeAbsolute;
            // Stop catch-up at the frame which produced this miss. Rewind only after the current frame has finished judging.
            seekForMiss(result.RawTime ?? result.TimeAbsolute);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (missSeekRequest == null)
                return;

            var request = missSeekRequest;

            if (request.RestoreTime != null)
            {
                // Keep results and failure suppressed until the judgements from the search have been reverted.
                if (Precision.AlmostEquals(DrawableRuleset!.FrameStableClock.CurrentTime, request.RestoreTime.Value))
                    finishMissSeek();

                return;
            }

            if (request.TargetTime != null)
            {
                seekBeforeMiss(request.TargetTime.Value);
                request.RestoreTime = selectedMissSeekTime;
            }
            else if (Precision.AlmostEquals(DrawableRuleset!.FrameStableClock.CurrentTime, request.EndTime))
            {
                request.RestoreTime = request.OriginalTime;
                seekForMiss(request.OriginalTime);
            }
        }

        private void seekBeforeMiss(double time)
        {
            selectedMissSeekTime = Math.Max(GameplayClockContainer.StartTime, time - miss_seek_lead_in);
            seekForMiss(selectedMissSeekTime);
            selectedMissTime = time;
        }

        private void seekForMiss(double time)
        {
            performingMissSeek = true;

            try
            {
                Seek(time);
            }
            finally
            {
                performingMissSeek = false;
            }
        }

        private void onSeek()
        {
            if (performingMissSeek)
                return;

            selectedMissTime = null;

            // A manual seek cancels the search, but still needs to undo any judgements encountered while searching.
            missSeekRequest?.RestoreTime = GameplayClockContainer.CurrentTime;
        }

        private void finishMissSeek(bool resumePlayback = true)
        {
            if (missSeekRequest == null)
                return;

            var request = missSeekRequest;
            missSeekRequest = null;
            Configuration.ShowResults = request.ShowResults;

            if (resumePlayback && !request.WasPaused)
                GameplayClockContainer.Start();
        }

        private class MissSeekRequest
        {
            public readonly double OriginalTime;
            public readonly double ReferenceTime;
            public readonly double EndTime;
            public readonly bool WasPaused;
            public readonly bool ShowResults;
            public double? TargetTime;
            public double? RestoreTime;

            public MissSeekRequest(double originalTime, double referenceTime, double endTime, bool wasPaused, bool showResults)
            {
                OriginalTime = originalTime;
                ReferenceTime = referenceTime;
                EndTime = endTime;
                WasPaused = wasPaused;
                ShowResults = showResults;
            }
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
            switch (e.Action)
            {
                case GlobalAction.FastForwardReplay:
                    playbackSettings!.UserPlaybackRate.Value = userPlaybackRateBeforeFastForward;
                    return;
            }
        }

        protected override void PerformFail()
        {
            // base logic intentionally suppressed - we have our own custom fail interaction
            ScoreProcessor.FailScore(Score.ScoreInfo);
            failIndicator!.Display();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            stopAllAudioEffects();
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            finishMissSeek(resumePlayback: false);

            // safety against filters or samples from the indicator playing long after the screen is exited
            failIndicator?.RemoveAndDisposeImmediately();
            return base.OnExiting(e);
        }

        private void stopAllAudioEffects()
        {
            finishMissSeek(resumePlayback: false);

            // safety against filters or samples from the indicator playing long after the screen is exited
            failIndicator?.RemoveAndDisposeImmediately();

            if (GameplayClockContainer is MasterGameplayClockContainer master)
            {
                playbackSettings?.UserPlaybackRate.UnbindFrom(master.UserPlaybackRate);
                master.UserPlaybackRate.SetDefault();
            }
        }
    }
}
