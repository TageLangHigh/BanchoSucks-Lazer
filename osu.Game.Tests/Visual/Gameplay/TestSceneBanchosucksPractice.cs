// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Practice;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Banchosucks: the practice loop ("Abschnitt üben"), from its setup screen through the real player loader and player.
    /// </summary>
    public partial class TestSceneBanchosucksPractice : ScreenTestScene
    {
        private BeatmapManager beatmaps = null!;
        private RulesetStore rulesets = null!;

        private BeatmapSetInfo? importedSet;
        private BanchosucksPracticeSetupScreen setup = null!;

        private readonly List<string> requests = new List<string>();

        private BanchosucksPracticePlayer? player => Stack.CurrentScreen as BanchosucksPracticePlayer;

        private GameplayClockContainer clock => player!.ChildrenOfType<GameplayClockContainer>().Single();

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            // a score manager on the test's own database, so "nothing was saved" can be read back from it
            Dependencies.Cache(rulesets = new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(new ScoreManager(rulesets, () => beatmaps, LocalStorage, Realm, API));
            Dependencies.Cache(Realm);
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("import beatmap", () =>
            {
                if (importedSet != null)
                    return;

                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                importedSet = beatmaps.GetAllUsableBeatmapSets().First();
            });

            AddStep("record API requests", () =>
            {
                requests.Clear();
                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    requests.Add(req.GetType().Name);
                    return false;
                };
            });

            AddStep("reset last played", () => Realm.Write(r =>
            {
                foreach (var b in r.All<BeatmapInfo>())
                    b.LastPlayed = null;
            }));

            selectBeatmap(new OsuRuleset().RulesetInfo, new OsuModNoFail());
        }

        private void selectBeatmap(RulesetInfo ruleset, params Mod[] mods) => AddStep($"select {ruleset.ShortName} beatmap", () =>
        {
            Ruleset.Value = ruleset;
            // the last one of a ruleset is its hardest difficulty in this set
            Beatmap.Value = beatmaps.GetWorkingBeatmap(importedSet!.Beatmaps.Where(b => b.Ruleset.ShortName == ruleset.ShortName).OrderBy(b => b.StarRating).Last());
            SelectedMods.Value = mods;
        });

        private void loadSetup(Func<BanchosucksPracticeSetupScreen>? create = null)
        {
            AddStep("load setup", () => LoadScreen(setup = create?.Invoke() ?? new BanchosucksPracticeSetupScreen()));
            AddUntilStep("setup loaded", () => setup.IsLoaded);
            AddStep("mouse to centre", () => InputManager.MoveMouseTo(Stack.ScreenSpaceDrawQuad.Centre));
        }

        private void quitThroughPauseMenu()
        {
            AddUntilStep("pause", () => player!.Pause() || player!.ChildrenOfType<PauseOverlay>().Single().State.Value == Visibility.Visible);
            AddUntilStep("pause overlay shown", () => player!.ChildrenOfType<PauseOverlay>().Single().State.Value == Visibility.Visible);
            AddStep("quit", () => player!.ChildrenOfType<PauseOverlay>().Single().Buttons.Last().TriggerClick());
            AddUntilStep("back at the setup", () => setup.IsCurrentScreen());
        }

        private void waitForAttempt(int attempt)
            => AddUntilStep($"attempt {attempt} running", () => player?.IsLoaded == true && player.Section.Attempts == attempt && clock.IsRunning);

        [Test]
        public void TestSetupCoversTheMapAndKeepsTheSectionValid()
        {
            loadSetup();

            AddAssert("can start", () => setup.CanStart);
            AddAssert("map bounds read", () => setup.MapStart, () => Is.EqualTo(900));
            AddAssert("map end read", () => setup.MapEnd, () => Is.GreaterThanOrEqualTo(9800).And.LessThan(15000));
            AddAssert("starts with the whole map", () => setup.StartTime.Value == setup.MapStart && setup.EndTime.Value == setup.MapEnd);
            AddAssert("normal speed", () => setup.Rate.Value, () => Is.EqualTo(1));

            AddStep("drag start behind the end", () =>
            {
                setup.EndTime.Value = 5000;
                setup.StartTime.Value = 7000;
            });
            AddAssert("end was pushed along", () => setup.StartTime.Value == 7000 && setup.EndTime.Value == 9000);

            AddStep("drag end before the start", () => setup.EndTime.Value = 3000);
            AddAssert("start was pulled back", () => setup.EndTime.Value == 3000 && setup.StartTime.Value == 1000);

            AddStep("start at the very end", () => setup.StartTime.Value = setup.MapEnd);
            AddAssert("minimum length kept", () => setup.EndTime.Value == setup.MapEnd && setup.StartTime.Value == setup.MapEnd - BanchosucksPracticeSection.MIN_LENGTH);

            AddStep("speed below the range", () => setup.Rate.Value = 0.2);
            AddAssert("speed clamped", () => setup.Rate.Value, () => Is.EqualTo(0.5));
        }

        [Test]
        public void TestSectionLoopsAtEndPointWithSpeed()
        {
            BanchosucksPracticePlayer firstPlayer = null!;

            selectBeatmap(new OsuRuleset().RulesetInfo, new OsuModNoFail(), new OsuModAutoplay());
            loadSetup();

            AddStep("pick section and speed", () =>
            {
                setup.EndTime.Value = 6500;
                setup.StartTime.Value = 4000;
                setup.Rate.Value = 0.5;
            });
            AddStep("start", () => setup.Start());

            waitForAttempt(1);
            AddStep("remember player", () => firstPlayer = player!);

            AddAssert("jumped to the start point", () => firstPlayer.JumpTime, () => Is.EqualTo(4000));
            AddAssert("clock started at the start point", () => clock.CurrentTime, () => Is.GreaterThanOrEqualTo(4000 - 50).And.LessThan(6500));
            AddAssert("half speed", () => clock.GetTrueGameplayRate(), () => Is.EqualTo(0.5).Within(0.001));
            AddAssert("autoplay taken out for the run", () => !SelectedMods.Value.Any(m => m is ModAutoplay));
            AddAssert("the player plays, no replay", () => !firstPlayer.ChildrenOfType<DrawableRuleset>().Single().HasReplayLoaded.Value);
            AddAssert("objects before the start count as hit", () =>
            {
                var processor = firstPlayer.ChildrenOfType<ScoreProcessor>().Single();
                int before = Beatmap.Value.GetPlayableBeatmap(Ruleset.Value).HitObjects.Count(h => h.GetEndTime() < 4000);
                return before > 0 && processor.JudgedHits >= before;
            });

            waitForAttempt(2);
            AddAssert("a new player took over", () => player != firstPlayer && firstPlayer.LoopRestartRequested);
            AddAssert("it runs the same section", () => player!.Section == firstPlayer.Section && player.JumpTime == 4000);
            AddAssert("fail overlay never shown", () => firstPlayer.ChildrenOfType<FailOverlay>().All(o => o.State.Value == Visibility.Hidden));

            quitThroughPauseMenu();
            AddAssert("section still set", () => setup.StartTime.Value == 4000 && setup.EndTime.Value == 6500 && setup.Rate.Value == 0.5);
            AddAssert("mods given back", () => SelectedMods.Value.Any(m => m is ModAutoplay) && SelectedMods.Value.Any(m => m is ModNoFail));
        }

        [Test]
        public void TestFailRestartsTheSection()
        {
            BanchosucksPracticePlayer firstPlayer = null!;

            selectBeatmap(new OsuRuleset().RulesetInfo);
            loadSetup();

            AddStep("start at 3 s", () => setup.StartTime.Value = 3000);
            AddStep("start", () => setup.Start());

            waitForAttempt(1);
            AddStep("remember player", () => firstPlayer = player!);
            AddStep("fail", () => firstPlayer.ChildrenOfType<HealthProcessor>().Single().TriggerFailure());

            waitForAttempt(2);
            AddAssert("a new player took over", () => player != firstPlayer && firstPlayer.LoopRestartRequested);
            AddAssert("fail overlay never shown", () => firstPlayer.ChildrenOfType<FailOverlay>().All(o => o.State.Value == Visibility.Hidden));
            AddAssert("restarted at the start point", () => player!.JumpTime, () => Is.EqualTo(3000));
        }

        [Test]
        public void TestNothingIsSavedOrSubmitted()
        {
            int scoresBefore = 0;

            loadSetup();

            AddStep("count scores", () => scoresBefore = Realm.Run(r => r.All<ScoreInfo>().Count()));
            AddStep("start whole map", () => setup.Start());

            waitForAttempt(1);
            AddAssert("starts with the map", () => player!.JumpTime, () => Is.Null);

            // plays the rest of the map frame by frame (every object is judged), which completes the run
            AddStep("seek to completion", () => clock.Seek(Beatmap.Value.GetPlayableBeatmap(Ruleset.Value).HitObjects.Max(h => h.GetEndTime())));

            waitForAttempt(2);
            AddStep("seek to completion again", () => clock.Seek(Beatmap.Value.GetPlayableBeatmap(Ruleset.Value).HitObjects.Max(h => h.GetEndTime())));

            waitForAttempt(3);
            AddAssert("no results screen", () => Stack.CurrentScreen is not ResultsScreen);

            quitThroughPauseMenu();

            AddAssert("no score in the database", () => Realm.Run(r => r.All<ScoreInfo>().Count()), () => Is.EqualTo(scoresBefore));
            AddAssert("last played untouched", () => Realm.Run(r => r.Find<BeatmapInfo>(Beatmap.Value.BeatmapInfo.ID)!.LastPlayed), () => Is.Null);
            AddAssert("no score token or submission requested", () => requests.Where(isScoreSubmissionRequest), () => Is.Empty);
        }

        /// <summary>
        /// The counterpart of <see cref="TestNothingIsSavedOrSubmitted"/>: in this very setup a normal play does ask for a
        /// score token and does store its score, so the assertions there can fail.
        /// </summary>
        [Test]
        public void TestNormalPlayIsSavedInThisSetup()
        {
            TestPlayer normalPlayer = null!;
            int scoresBefore = 0;

            AddStep("count scores", () => scoresBefore = Realm.Run(r => r.All<ScoreInfo>().Count()));
            AddStep("load normal player", () => LoadScreen(normalPlayer = new TestPlayer(false)));
            AddUntilStep("playing", () => normalPlayer.IsCurrentScreen() && normalPlayer.GameplayClockContainer.IsRunning);
            AddStep("seek to completion", () => normalPlayer.GameplayClockContainer.Seek(normalPlayer.DrawableRuleset.Objects.Max(h => h.GetEndTime())));

            AddUntilStep("results displayed", () => normalPlayer.GetChildScreen() is ResultsScreen);
            AddAssert("score in the database", () => Realm.Run(r => r.All<ScoreInfo>().Count()), () => Is.EqualTo(scoresBefore + 1));
            AddAssert("last played set", () => Realm.Run(r => r.Find<BeatmapInfo>(Beatmap.Value.BeatmapInfo.ID)!.LastPlayed), () => Is.Not.Null);
            AddAssert("score token requested", () => requests.Where(isScoreSubmissionRequest), () => Is.Not.Empty);
        }

        [Test]
        public void TestFromFailStartsFiveSecondsBeforeTheFail()
        {
            loadSetup(() => BanchosucksPracticeSetupScreen.FromFail(8000));

            waitForAttempt(1);
            AddAssert("starts 5 s before the fail", () => player!.JumpTime, () => Is.EqualTo(3000));
            AddAssert("runs to the end at normal speed", () => player!.Section.EndTime == setup.MapEnd && player.Section.Rate == 1);
            AddAssert("normal speed", () => clock.GetTrueGameplayRate(), () => Is.EqualTo(1).Within(0.001));

            quitThroughPauseMenu();
            AddAssert("sliders show the section", () => setup.StartTime.Value == 3000 && setup.EndTime.Value == setup.MapEnd);
        }

        [TestCase("taiko")]
        [TestCase("fruits")]
        [TestCase("mania")]
        public void TestJumpIntoTheMapInOtherRulesets(string shortName)
        {
            BanchosucksPracticePlayer firstPlayer = null!;

            AddStep("select ruleset beatmap", () =>
            {
                var ruleset = rulesets.GetRuleset(shortName)!;
                Ruleset.Value = ruleset;
                Beatmap.Value = beatmaps.GetWorkingBeatmap(importedSet!.Beatmaps.Where(b => b.Ruleset.ShortName == shortName).OrderBy(b => b.StarRating).Last());
                SelectedMods.Value = new[] { ruleset.CreateInstance().CreateAllMods().OfType<ModNoFail>().Single() };
            });
            loadSetup();

            AddStep("pick section", () =>
            {
                setup.EndTime.Value = 7000;
                setup.StartTime.Value = 5000;
            });
            AddStep("start", () => setup.Start());

            waitForAttempt(1);
            AddStep("remember player", () => firstPlayer = player!);
            AddAssert("jumped to the start point", () => firstPlayer.JumpTime, () => Is.EqualTo(5000));

            waitForAttempt(2);
            AddAssert("looped", () => player != firstPlayer && firstPlayer.LoopRestartRequested);
        }

        private static bool isScoreSubmissionRequest(string requestType)
            => requestType.Contains("CreateSoloScore") || requestType.Contains("Submit") || requestType.Contains("Token");
    }
}
