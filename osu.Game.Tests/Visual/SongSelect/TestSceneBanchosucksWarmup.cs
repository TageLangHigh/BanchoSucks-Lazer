// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Input.Bindings;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Warmup;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Banchosucks: the warm-up button in solo song select, from the player's local scores to the started series.
    /// </summary>
    /// <remarks>
    /// The library consists of generated sets without files, so a started map never gets past the player loader and
    /// song select comes back by itself. The way through real plays and the results screen is covered by
    /// <c>TestSceneBanchosucksWarmupNavigation</c>.
    /// </remarks>
    public partial class TestSceneBanchosucksWarmup : SongSelectTestScene
    {
        /// <summary>
        /// 75th percentile of twelve scores from 4.0 to 5.1 stars.
        /// </summary>
        private const double level = 4.825;

        private SoloSongSelect solo => (SoloSongSelect)SongSelect;
        private BanchosucksWarmupController controller => solo.BanchosucksWarmup;
        private BanchosucksWarmupSeries series => controller.Series;

        private readonly List<BeatmapInfo> scoredMaps = new List<BeatmapInfo>();
        private readonly List<BeatmapInfo> warmupMaps = new List<BeatmapInfo>();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("log in", () =>
            {
                API.Login("user1", string.Empty);
                API.AuthenticateSecondFactor("abcdefgh");
            });

            AddStep("forget library", () =>
            {
                scoredMaps.Clear();
                warmupMaps.Clear();

                // the database is shared by the tests of this scene and deleting the sets keeps their scores
                Realm.Write(r => r.RemoveAll<ScoreInfo>());
            });
        }

        /// <summary>
        /// Twelve maps the player has scores on (too long to be warm-up maps themselves), one fitting map per target
        /// and a few that fit by stars but must never be drawn.
        /// </summary>
        private void setUpLibrary()
        {
            for (int i = 0; i < 12; i++)
            {
                double stars = 4.0 + i * 0.1;
                importSet(stars, 300, scoredMaps);
            }

            foreach (double share in BanchosucksWarmupSelector.TARGETS)
                importSet(level * share, 120, warmupMaps);

            // fits the 80 % target by stars: too short, too long, played today, hidden
            importSet(level * 0.8, 40);
            importSet(level * 0.8, 400);
            importSet(level * 0.8, 120, apply: b => b.LastPlayed = DateTimeOffset.Now);
            importSet(level * 0.8, 120, apply: b => b.Hidden = true);
        }

        private void importSet(double stars, double seconds, List<BeatmapInfo>? remember = null, Action<BeatmapInfo>? apply = null)
        {
            ImportBeatmapForRuleset(set =>
            {
                var beatmap = set.Beatmaps.Single();
                beatmap.StarRating = stars;
                beatmap.Length = seconds * 1000;
                apply?.Invoke(beatmap);
                remember?.Add(beatmap);
            }, 1, 0);
        }

        private void addScores(int count, APIUser? user = null, double accuracy = 0.97, ScoreRank rank = ScoreRank.A, int daysAgo = 2, Mod[]? mods = null)
        {
            AddStep($"add {count} scores", () =>
            {
                foreach (var id in scoredMaps.Take(count).Select(m => m.ID))
                {
                    var score = TestResources.CreateTestScoreInfo(Beatmaps.QueryBeatmap(b => b.ID == id)!);
                    // scores without files are told apart by their hash
                    score.Hash = Guid.NewGuid().ToString();
                    score.User = user ?? API.LocalUser.Value;
                    score.Accuracy = accuracy;
                    score.Rank = rank;
                    score.Passed = rank != ScoreRank.F;
                    score.Date = DateTimeOffset.Now.AddDays(-daysAgo);
                    score.Mods = mods ?? Array.Empty<Mod>();
                    ScoreManager.Import(score);
                }
            });
        }

        private void pressWarmup()
        {
            AddStep("forget last plan", () => controller.CreateRandom = () => new Random(11));
            AddStep("start warm-up", () => solo.BanchosucksStartWarmup());
            AddUntilStep("scores read", () => !controller.Preparing && controller.LastPlan != null);
        }

        [Test]
        public void TestTooLittleDataSaysSoAndStartsNothing()
        {
            setUpLibrary();

            // only the first three count: the others are somebody else's, failed, sloppy or old
            addScores(3);
            addScores(12, user: new APIUser { Id = 1337, Username = "user2" });
            addScores(12, rank: ScoreRank.F);
            addScores(12, accuracy: 0.8);
            addScores(12, daysAgo: BanchosucksWarmupSelector.RECENT_DAYS + 5);
            // generated sets have no files, so a rating with mods cannot be calculated: such scores are left out, not counted at the stored rating
            addScores(12, mods: new Mod[] { new OsuModDoubleTime() });

            LoadSongSelect();
            pressWarmup();

            AddAssert("too few scores", () => controller.LastPlan!.Failure, () => Is.EqualTo(BanchosucksWarmupFailure.TooFewScores));
            AddAssert("three counted", () => controller.LastPlan!.ScoresUsed, () => Is.EqualTo(3));
            AddAssert("player told", () => controller.LastMessage?.ToString(), () => Does.Contain("too few recent plays"));
            AddAssert("nothing started", () => SongSelect.IsCurrentScreen() && !series.Active);
        }

        [Test]
        public void TestNoFittingMapSaysSoAndStartsNothing()
        {
            for (int i = 0; i < 12; i++)
                importSet(4.0 + i * 0.1, 300, scoredMaps);

            addScores(12);

            LoadSongSelect();
            pressWarmup();

            AddAssert("too few candidates", () => controller.LastPlan!.Failure, () => Is.EqualTo(BanchosucksWarmupFailure.TooFewCandidates));
            AddAssert("level known", () => controller.LastPlan!.Level, () => Is.EqualTo(level).Within(0.001));
            AddAssert("player told", () => controller.LastMessage?.ToString(), () => Does.Contain("no fitting map"));
            AddAssert("nothing started", () => SongSelect.IsCurrentScreen() && !series.Active);
        }

        [Test]
        public void TestSeriesStartsAndEndsWhenLeft()
        {
            setUpLibrary();
            addScores(12);

            LoadSongSelect();
            pressWarmup();

            AddAssert("plan made", () => controller.LastPlan!.Success);
            AddAssert("level from own scores", () => controller.LastPlan!.Level, () => Is.EqualTo(level).Within(0.001));
            AddAssert("the three fitting maps, easiest first", () => controller.LastPlan!.Picks.Select(p => p.BeatmapId), () => Is.EqualTo(warmupMaps.Select(m => m.ID)));

            AddUntilStep("first map started", () => Stack.CurrentScreen is PlayerLoader);
            AddAssert("first map selected", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(warmupMaps[0].ID));
            AddAssert("series runs", () => series.Active && series.Index == 0 && series.Maps.Count == 3);
            AddAssert("player told", () => controller.LastMessage?.ToString(), () => Does.Contain("three maps at"));

            // the generated map cannot be loaded, so the game returns to song select: the player is back without asking for the next map
            AddUntilStep("back at song select", () => SongSelect.IsCurrentScreen());
            AddAssert("series over", () => !series.Active);
        }

        [Test]
        public void TestNextLeadsThroughAllThreeMaps()
        {
            setUpLibrary();
            addScores(12);

            LoadSongSelect();
            pressWarmup();

            AddUntilStep("first map started", () => Stack.CurrentScreen is PlayerLoader);

            // what the button on the results screen does
            AddStep("ask for the next map", () => series.RequestNext());
            AddUntilStep("second map started", () => Stack.CurrentScreen is PlayerLoader && Beatmap.Value.BeatmapInfo.ID == warmupMaps[1].ID);
            AddAssert("second of three", () => series.Active && series.Index == 1 && series.HasNext);

            AddStep("ask for the next map", () => series.RequestNext());
            AddUntilStep("third map started", () => Stack.CurrentScreen is PlayerLoader && Beatmap.Value.BeatmapInfo.ID == warmupMaps[2].ID);
            AddAssert("last one", () => series.Active && series.Index == 2 && !series.HasNext);

            AddStep("ask once more", () => series.RequestNext());
            AddAssert("nothing follows the third map", () => series.Index == 2 && Beatmap.Value.BeatmapInfo.ID == warmupMaps[2].ID);

            AddUntilStep("back at song select", () => SongSelect.IsCurrentScreen());
            AddAssert("series over", () => !series.Active);
        }

        [Test]
        public void TestOptionsEntryAndKeyBinding()
        {
            setUpLibrary();
            addScores(3);

            LoadSongSelect();
            AddUntilStep("a map is selected", () => !Beatmap.IsDefault);

            AddStep("open options", () => InputManager.Key(Key.F3));
            AddUntilStep("warm-up entry shown", () => warmupEntry() != null);
            AddStep("click it", () => warmupEntry()!.TriggerClick());
            AddUntilStep("scores read", () => !controller.Preparing && controller.LastPlan != null);
            AddAssert("too few scores", () => controller.LastPlan!.Failure, () => Is.EqualTo(BanchosucksWarmupFailure.TooFewScores));

            addScores(12);

            // a binding with Ctrl in it must not turn the warm-up into autoplay (Ctrl+Enter starts a map with autoplay)
            AddStep("hold ctrl", () => InputManager.PressKey(Key.ControlLeft));
            AddStep("press the bound key", () => solo.OnPressed(new KeyBindingPressEvent<GlobalAction>(InputManager.CurrentState, GlobalAction.BanchosucksWarmup)));
            AddUntilStep("series started", () => !controller.Preparing && series.Active);
            AddUntilStep("first map started", () => Stack.CurrentScreen is PlayerLoader);
            AddAssert("without autoplay", () => !SelectedMods.Value.Any(m => m is ModAutoplay));
            AddStep("release ctrl", () => InputManager.ReleaseKey(Key.ControlLeft));
            AddUntilStep("back at song select", () => SongSelect.IsCurrentScreen());

            FooterPopoverButton? warmupEntry() => this.ChildrenOfType<FooterPopoverButton>().SingleOrDefault(b => b.Text.ToString() == "Start warm-up");
        }

        [Test]
        public void TestActionHasNoDefaultKeyAndIsListedForSongSelect()
        {
            AddAssert("listed without a key", () =>
                GlobalActionContainer.GetDefaultBindingsFor(GlobalActionCategory.SongSelect).Single(b => (GlobalAction)b.Action == GlobalAction.BanchosucksWarmup).KeyCombination.Keys.Single(),
                () => Is.EqualTo(osu.Framework.Input.Bindings.InputKey.None));
        }

        [Test]
        public void TestWhichModsChangeTheStarRating()
        {
            AddAssert("rate and difficulty mods do", () =>
                BanchosucksWarmupController.ChangesStarRating(new OsuModDoubleTime())
                && BanchosucksWarmupController.ChangesStarRating(new OsuModHardRock())
                && BanchosucksWarmupController.ChangesStarRating(new OsuModHalfTime())
                && BanchosucksWarmupController.ChangesStarRating(new OsuModHidden()));

            AddAssert("fail rules and the classic mod do not", () =>
                !BanchosucksWarmupController.ChangesStarRating(new OsuModNoFail())
                && !BanchosucksWarmupController.ChangesStarRating(new OsuModSuddenDeath())
                && !BanchosucksWarmupController.ChangesStarRating(new OsuModClassic()));
        }
    }
}
