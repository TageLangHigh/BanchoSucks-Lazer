// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.PlayerSettings;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneReplayPlayer : RateAdjustedBeatmapTestScene
    {
        protected TestReplayPlayer Player = null!;

        private readonly SortedSet<double> replayMissTimes = new SortedSet<double>();

        [Test]
        public void TestFailedBeatmapLoad()
        {
            loadPlayerWithBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo, withHitObjects: false));

            AddUntilStep("wait for exit", () => Player.IsCurrentScreen());
        }

        [Test]
        public void TestPauseViaSpace()
        {
            loadPlayerWithBeatmap();

            double? lastTime = null;

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);

            AddStep("Pause playback with space", () => InputManager.Key(Key.Space));

            AddAssert("player not exited", () => Player.IsCurrentScreen());

            AddUntilStep("Time stopped progressing", () =>
            {
                double current = Player.GameplayClockContainer.CurrentTime;
                bool changed = lastTime != current;
                lastTime = current;

                return !changed;
            });

            AddWaitStep("wait some", 10);

            AddAssert("Time still stopped", () => lastTime == Player.GameplayClockContainer.CurrentTime);
        }

        [Test]
        public void TestDoesNotFailOnExit()
        {
            loadPlayerWithBeatmap();

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);
            AddAssert("ensure rank is not fail", () => Player.ScoreProcessor.Rank.Value, () => Is.Not.EqualTo(ScoreRank.F));
            AddStep("exit player", () => Player.Exit());
            AddUntilStep("wait for exit", () => Player.Parent == null);
            AddAssert("ensure rank is not fail", () => Player.ScoreProcessor.Rank.Value, () => Is.Not.EqualTo(ScoreRank.F));
        }

        [Test]
        public void TestPauseViaSpaceWithSkip()
        {
            loadPlayerWithBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                AudioLeadIn = 60000
            });

            AddUntilStep("wait for skip overlay", () => Player.ChildrenOfType<SkipOverlay>().First().IsButtonVisible);

            AddStep("Skip with space", () => InputManager.Key(Key.Space));

            AddAssert("Player not paused", () => !Player.DrawableRuleset.IsPaused.Value);

            double? lastTime = null;

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);

            AddStep("Pause playback with space", () => InputManager.Key(Key.Space));

            AddAssert("player not exited", () => Player.IsCurrentScreen());

            AddUntilStep("Time stopped progressing", () =>
            {
                double current = Player.GameplayClockContainer.CurrentTime;
                bool changed = lastTime != current;
                lastTime = current;

                return !changed;
            });

            AddWaitStep("wait some", 10);

            AddAssert("Time still stopped", () => lastTime == Player.GameplayClockContainer.CurrentTime);
        }

        [Test]
        public void TestPauseViaMiddleMouse()
        {
            loadPlayerWithBeatmap();

            double? lastTime = null;

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);

            AddStep("Pause playback with middle mouse", () => InputManager.Click(MouseButton.Middle));

            AddAssert("player not exited", () => Player.IsCurrentScreen());

            AddUntilStep("Time stopped progressing", () =>
            {
                double current = Player.GameplayClockContainer.CurrentTime;
                bool changed = lastTime != current;
                lastTime = current;

                return !changed;
            });

            AddWaitStep("wait some", 10);

            AddAssert("Time still stopped", () => lastTime == Player.GameplayClockContainer.CurrentTime);
        }

        [Test]
        public void TestSeekBackwards()
        {
            loadPlayerWithBeatmap();

            double? lastTime = null;

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);

            AddStep("Seek backwards", () =>
            {
                lastTime = Player.GameplayClockContainer.CurrentTime;
                InputManager.Key(Key.Left);
            });

            AddAssert("Jumped backwards", () => Player.GameplayClockContainer.CurrentTime - lastTime < 0);
        }

        [Test]
        public void TestSeekForwards()
        {
            loadPlayerWithBeatmap();

            double? lastTime = null;

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);

            AddStep("Seek forwards", () =>
            {
                lastTime = Player.GameplayClockContainer.CurrentTime;
                InputManager.Key(Key.Right);
            });

            AddAssert("Jumped forwards", () => Player.GameplayClockContainer.CurrentTime - lastTime > 500);
        }

        [Test]
        public void TestReplayDoesNotFailUntilRunningOutOfFrames()
        {
            var score = new Score
            {
                ScoreInfo = TestResources.CreateTestScoreInfo(Beatmap.Value.BeatmapInfo),
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(0, Vector2.Zero),
                        new OsuReplayFrame(10000, Vector2.Zero),
                    }
                }
            };
            score.ScoreInfo.Mods = [];
            score.ScoreInfo.Rank = ScoreRank.F;
            AddStep("set global state", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
                Ruleset.Value = Beatmap.Value.BeatmapInfo.Ruleset;
                SelectedMods.Value = score.ScoreInfo.Mods;
            });
            AddStep("create player", () => Player = new TestReplayPlayer(score, showResults: false));
            AddStep("load player", () => LoadScreen(Player));
            AddUntilStep("wait for loaded", () => Player.IsCurrentScreen());
            AddStep("seek to 8000", () => Player.Seek(8000));
            AddUntilStep("fail indicator visible", () => Player.ChildrenOfType<ReplayFailIndicator>().Any(indicator => indicator.IsAlive && indicator.IsPresent));
        }

        [Test]
        public void TestPlayerLoaderSettingsHover()
        {
            loadPlayerWithBeatmap();

            AddUntilStep("wait for settings overlay hidden", () => settingsOverlay().Expanded.Value, () => Is.False);
            AddStep("move mouse to right of screen", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopRight));
            AddUntilStep("wait for settings overlay visible", () => settingsOverlay().Expanded.Value, () => Is.True);
            AddStep("move mouse to centre of screen", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("wait for settings overlay hidden", () => settingsOverlay().Expanded.Value, () => Is.False);

            ReplaySettingsOverlay settingsOverlay() => Player.ChildrenOfType<ReplaySettingsOverlay>().Single();
        }

        [Test]
        public void TestChangePlaybackRateViaHoldingShift()
        {
            loadPlayerWithBeatmap();

            double? lastRate = null;

            AddUntilStep("wait for first hit", () => Player.ScoreProcessor.TotalScore.Value > 0);
            AddStep("Change playback rate with shift", () =>
            {
                lastRate = Player.GameplayClockContainer.Rate;
                InputManager.PressKey(Key.ShiftLeft);
            });

            AddWaitStep("wait some", 5);

            AddAssert("rate changed", () => lastRate != Player.GameplayClockContainer.Rate);

            AddStep("Change playback rate by releasing shift", () =>
            {
                lastRate = Player.GameplayClockContainer.Rate;
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
            AddWaitStep("wait some", 5);
            AddAssert("rate changed", () => lastRate != Player.GameplayClockContainer.Rate);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestSeekToMiss(bool paused)
        {
            loadMissReplay();

            AddStep("set playback state", () =>
            {
                if (!paused)
                    Player.GameplayClockContainer.Start();
            });
            AddStep("seek to next miss", () => Player.SeekToMiss(1));
            AddUntilStep("at first miss lead-in", () => isAtMiss(0, 200));
            AddUntilStep("search finished", () => Player.Configuration.ShowResults);
            AddAssert("playback state preserved", () => Player.GameplayClockContainer.IsPaused.Value == paused);
            AddAssert("results still enabled", () => Player.Configuration.ShowResults);

            AddStep("seek to next miss again", () => Player.SeekToMiss(1));
            AddUntilStep("at second miss lead-in", () => isAtMiss(1, 200));
            AddUntilStep("search finished", () => Player.Configuration.ShowResults);
            AddAssert("playback state preserved", () => Player.GameplayClockContainer.IsPaused.Value == paused);

            AddStep("seek to previous miss", () => Player.SeekToMiss(-1));
            AddUntilStep("back at first miss lead-in", () => isAtMiss(0, 200));
            AddAssert("playback state preserved", () => Player.GameplayClockContainer.IsPaused.Value == paused);
            AddAssert("player has not failed", () => !Player.GameplayState.HasFailed);
            AddAssert("player still current", () => Player.IsCurrentScreen());
        }

        [Test]
        public void TestMissNavigationButtons()
        {
            loadMissReplay();

            AddStep("expand replay settings", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopRight));
            AddUntilStep("settings expanded", () => Precision.AlmostEquals(Player.ReplayOverlay.Settings.DrawWidth, ReplaySettingsOverlay.EXPANDED_WIDTH));
            AddStep("click next miss", () => clickMissButton(PlayerSettingsOverlayStrings.NextMiss));
            AddUntilStep("at first miss lead-in", () => isAtMiss(0));
            AddStep("click next miss again", () => clickMissButton(PlayerSettingsOverlayStrings.NextMiss));
            AddUntilStep("at second miss lead-in", () => isAtMiss(1));
            AddStep("click previous miss", () => clickMissButton(PlayerSettingsOverlayStrings.PreviousMiss));
            AddUntilStep("back at first miss lead-in", () => isAtMiss(0));

            void clickMissButton(LocalisableString text)
            {
                var button = Player.ChildrenOfType<PlaybackSettings>().Single().ChildrenOfType<RoundedButton>().Single(button => button.Text == text);
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            }
        }

        [TestCase(1f)]
        [TestCase(1.5f)]
        public void TestMissNavigationButtonsLayout(float scale)
        {
            loadMissReplay();

            AddStep("expand playback settings", () =>
            {
                var settings = Player.ChildrenOfType<PlaybackSettings>().Single();
                settings.Expanded.Value = true;
                settings.Scale = new Vector2(scale);
            });
            AddWaitStep("wait for layout", 5);
            AddAssert("buttons have row height", () => missButtons().All(button => Precision.AlmostEquals(button.DrawHeight, 30)));
            AddAssert("buttons fit inside playback settings", () => missButtons().All(button =>
                Player.ChildrenOfType<PlaybackSettings>().Single().ScreenSpaceDrawQuad.AABBFloat.Contains(button.ScreenSpaceDrawQuad.AABBFloat)));
            AddAssert("labels fit inside buttons", () => missButtons().All(button =>
                button.ScreenSpaceDrawQuad.AABBFloat.Contains(button.ChildrenOfType<OsuSpriteText>().Single().ScreenSpaceDrawQuad.AABBFloat)));
            AddAssert("buttons do not overlap speed control", () => missButtons().All(button =>
                button.ScreenSpaceDrawQuad.BottomRight.Y < Player.ChildrenOfType<PlaybackSettings>().Single().ChildrenOfType<PlayerSliderBar<double>>().Single().ScreenSpaceDrawQuad.TopLeft.Y));

            RoundedButton[] missButtons() => Player.ChildrenOfType<PlaybackSettings>().Single().ChildrenOfType<RoundedButton>().ToArray();
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void TestSeekToMissWithNoMisses(bool autoplay, bool paused)
        {
            loadMissReplay(miss: false, autoplay: autoplay);

            double originalTime = 0;
            bool restoredOriginalTime = false;
            AddStep("set playback state", () =>
            {
                Player.GameplayClockContainer.OnSeek += () =>
                    restoredOriginalTime |= Precision.AlmostEquals(Player.GameplayClockContainer.CurrentTime, originalTime);
                if (!paused)
                    Player.GameplayClockContainer.Start();
            });
            AddStep("seek to next miss", () =>
            {
                originalTime = Player.GameplayClockContainer.CurrentTime;
                Player.SeekToMiss(1);
            });
            AddUntilStep("search finished", () => Player.Configuration.ShowResults);
            AddAssert("returned to original time", () => restoredOriginalTime);
            if (paused)
                waitForReplayCatchUp();
            AddAssert("playback state preserved", () => Player.GameplayClockContainer.IsPaused.Value == paused);
            AddAssert("player still current", () => Player.IsCurrentScreen());
            AddAssert("player not marked passed", () => !Player.GameplayState.HasPassed);
        }

        [Test]
        public void TestSeekToMissAtBoundaries()
        {
            loadMissReplay();

            AddStep("seek to previous miss before any miss", () => Player.SeekToMiss(-1));
            AddAssert("time unchanged", () => Precision.AlmostEquals(Player.GameplayClockContainer.CurrentTime, 0));
            AddStep("seek to last miss", () => Player.Seek(4500));
            waitForReplayCatchUp();
            AddStep("seek to next miss after last miss", () => Player.SeekToMiss(1));
            AddUntilStep("search finished", () => Player.Configuration.ShowResults);
            AddUntilStep("back at original time", () => Precision.AlmostEquals(Player.GameplayClockContainer.CurrentTime, 4500));
            waitForReplayCatchUp();
            AddAssert("playback still paused", () => Player.GameplayClockContainer.IsPaused.Value);
            AddStep("seek to previous miss", () => Player.SeekToMiss(-1));
            AddUntilStep("at last miss lead-in", () => isAtMiss(1));
        }

        [Test]
        public void TestManualSeekResetsSelectedMiss()
        {
            loadMissReplay();

            AddStep("seek to next miss", () => Player.SeekToMiss(1));
            AddUntilStep("at first miss lead-in", () => isAtMiss(0));
            AddStep("manually seek within lead-in", () => Player.Seek(1200));
            waitForReplayCatchUp();
            AddStep("seek to next miss", () => Player.SeekToMiss(1));
            AddAssert("first miss selected again", () => isAtMiss(0));
        }

        [Test]
        public void TestSeekToMissDoesNotInterruptExistingSearch()
        {
            loadMissReplay();

            AddStep("request multiple seeks", () =>
            {
                Player.SeekToMiss(1);
                Player.SeekToMiss(1);
                Player.SeekToMiss(-1);
            });
            AddUntilStep("at first miss lead-in", () => isAtMiss(0));
            AddAssert("playback still paused", () => Player.GameplayClockContainer.IsPaused.Value);
            AddAssert("results still enabled", () => Player.Configuration.ShowResults);
        }

        [Test]
        public void TestMissSearchDoesNotTriggerFailure()
        {
            loadMissReplay(autoplay: true);

            AddStep("fail on any judgement", () => Player.HealthProcessor.FailConditions += (_, _) => true);
            AddStep("seek to next miss", () => Player.SeekToMiss(1));
            AddUntilStep("search finished", () => Player.Configuration.ShowResults);
            AddAssert("at first miss lead-in", () => isAtMiss(0));
            AddAssert("gameplay not failed", () => !Player.GameplayState.HasFailed);
            AddAssert("health processor not failed", () => !Player.HealthProcessor.HasFailed);
        }

        [Test]
        public void TestManualSeekCancelsMissSearch()
        {
            loadMissReplay();

            AddStep("search then seek manually", () =>
            {
                Player.SeekToMiss(1);
                Player.Seek(500);
            });
            waitForReplayCatchUp();
            AddAssert("manual target preserved", () => Precision.AlmostEquals(Player.GameplayClockContainer.CurrentTime, 500));
            AddAssert("playback still paused", () => Player.GameplayClockContainer.IsPaused.Value);
            AddAssert("results restored", () => Player.Configuration.ShowResults);
        }

        [Test]
        public void TestSeekToMissNearStart()
        {
            loadMissReplay(firstObjectTime: 100);

            AddStep("seek to next miss", () => Player.SeekToMiss(1));
            AddUntilStep("search finished", () => Player.Configuration.ShowResults);
            AddAssert("negative lead-in supported", () => isAtMiss(0));
            AddAssert("lead-in not before playback start", () => Player.GameplayClockContainer.CurrentTime >= Player.GameplayClockContainer.StartTime);
            AddAssert("miss recorded", () => replayMissTimes.Count > 0);
            AddStep("seek to next miss again", () => Player.SeekToMiss(1));
            AddUntilStep("at second miss lead-in", () => isAtMiss(1));
        }

        private void loadMissReplay(bool miss = true, bool autoplay = false, double firstObjectTime = 2000)
        {
            AddStep("create miss replay player", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap
                {
                    HitObjects = Enumerable.Range(1, 3).Select(i => new HitCircle
                    {
                        StartTime = firstObjectTime + (i - 1) * 2000,
                        Position = new Vector2(256, 192),
                    }).ToList<HitObject>(),
                };

                Beatmap.Value = CreateWorkingBeatmap(beatmap);
                Ruleset.Value = ruleset.RulesetInfo;
                SelectedMods.Value = autoplay ? new[] { ruleset.GetAutoplayMod()! } : new Mod[] { new OsuModNoFail() };
                var score = new Score { ScoreInfo = TestResources.CreateTestScoreInfo(Beatmap.Value.BeatmapInfo) };

                // The first two circles miss, but the final circle hits so the search also traverses successful judgements.
                for (int time = 0; time <= 8000; time += 20)
                {
                    bool hit = time is 2000 or 4000 or 6000 && (!miss || time == 6000);
                    score.Replay.Frames.Add(new OsuReplayFrame(time, new Vector2(256, 192), hit ? new[] { OsuAction.LeftButton } : Array.Empty<OsuAction>()));
                }

                Player = new TestReplayPlayer(score);
            });
            AddStep("load player", () => LoadScreen(Player));
            AddUntilStep("player current", () => Player.IsCurrentScreen());
            AddStep("pause at start", () =>
            {
                replayMissTimes.Clear();
                Player.DrawableRuleset.NewResult += result =>
                {
                    if (result.Type == HitResult.Miss)
                        replayMissTimes.Add(result.TimeAbsolute);
                };
                Player.GameplayClockContainer.Stop();
                Player.Seek(0);
            });
            waitForReplayCatchUp();
        }

        private bool isAtMiss(int index, double tolerance = 1) =>
            replayMissTimes.Count > index && Precision.AlmostEquals(Player.GameplayClockContainer.CurrentTime, replayMissTimes.ElementAt(index) - 1000, tolerance);

        private void waitForReplayCatchUp() =>
            AddUntilStep("wait for replay catch-up", () => Precision.AlmostEquals(Player.DrawableRuleset.FrameStableClock.CurrentTime, Player.GameplayClockContainer.CurrentTime));

        private void loadPlayerWithBeatmap(IBeatmap? beatmap = null)
        {
            AddStep("create player", () =>
            {
                CreatePlayer(new OsuRuleset(), beatmap);
            });

            AddStep("Load player", () => LoadScreen(Player));
            AddUntilStep("player loaded", () => Player.IsLoaded);
        }

        protected void CreatePlayer(Ruleset ruleset, IBeatmap? beatmap = null)
        {
            Beatmap.Value = beatmap != null
                ? CreateWorkingBeatmap(beatmap)
                : CreateWorkingBeatmap(ruleset.RulesetInfo);

            SelectedMods.Value = new[] { ruleset.GetAutoplayMod() };

            Player = new TestReplayPlayer(false);
        }
    }
}
