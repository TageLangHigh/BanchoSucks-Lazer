// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;
using osuTK;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// Banchosucks: replay comparison in the whole game, with two replays imported the way a finished play stores
    /// them: mark one score, compare a second one with it, and the replay player shows the first with the second
    /// as a ghost.
    /// </summary>
    public partial class TestSceneBanchosucksReplayCompareNavigation : OsuGameTestScene
    {
        private SoloSongSelect songSelect = null!;
        private BanchosucksReplayCompare compare = null!;

        private ScoreInfo first = null!;
        private ScoreInfo second = null!;
        private ScoreInfo hardRock = null!;

        private void setUp()
        {
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);
            AddStep("select Insane", () => Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(
                Game.Beatmap.Value.BeatmapSetInfo.Beatmaps.Single(b => b.Ruleset.OnlineID == 0 && b.DifficultyName == "Insane")));

            AddStep("import three replays", () =>
            {
                first = importScore("first player", 1001, new Mod[] { new OsuModNoFail() });
                second = importScore("second player", 1002, new Mod[] { new OsuModHidden() });
                hardRock = importScore("hard rock player", 1003, new Mod[] { new OsuModHardRock() });
            });

            AddStep("get the comparison", () => compare = Game.ChildrenOfType<BanchosucksReplayCompare>().Single());
        }

        /// <summary>
        /// Stores a score with a replay the way <see cref="Player"/> does after a play.
        /// </summary>
        private ScoreInfo importScore(string username, int userId, Mod[] mods)
        {
            var working = Game.Beatmap.Value;
            var ruleset = new OsuRuleset();

            var frames = new List<ReplayFrame>();

            for (int i = 0; i < 400; i++)
                frames.Add(new OsuReplayFrame(i * 50, new Vector2(50 + i, 80 + userId % 100)));

            var score = new Score
            {
                ScoreInfo = new ScoreInfo(working.BeatmapInfo, ruleset.RulesetInfo)
                {
                    User = new APIUser { Id = userId, Username = username },
                    BeatmapHash = working.BeatmapInfo.Hash,
                    Mods = mods,
                    Date = DateTimeOffset.Now,
                    Rank = ScoreRank.A,
                    Accuracy = 0.9,
                    MaxCombo = 10,
                    TotalScore = 1000 + userId,
                    Passed = true,
                    Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = 10, [HitResult.Miss] = 1 },
                },
                Replay = new Replay { Frames = frames },
            };

            ByteArrayArchiveReader reader;

            using (var stream = new MemoryStream())
            {
                new LegacyScoreEncoder(score, working.GetPlayableBeatmap(ruleset.RulesetInfo, mods)).Encode(stream);
                reader = new ByteArrayArchiveReader(stream.ToArray(), "replay.osr");
            }

            var imported = Game.ScoreManager.Import(score.ScoreInfo.DeepClone(), reader);

            Assert.That(imported, Is.Not.Null);

            return imported!.PerformRead(s => s.Detach());
        }

        private static string text(MenuItem item) => item.Text.Value.ToString();

        [Test]
        public void TestMarkAndCompare()
        {
            ReplayPlayer player = null!;

            setUp();

            AddAssert("replays are stored", () => first.Files.Count > 0 && second.Files.Count > 0);
            AddAssert("scores can be compared", () => BanchosucksReplayCompare.CanCompare(first) && BanchosucksReplayCompare.CanCompare(second));

            AddAssert("nothing marked: only the mark entry", () => compare.CreateMenuItems(second).Select(text), () => Is.EqualTo(new[] { "Mark for comparison" }));

            AddStep("mark the first score", () => compare.CreateMenuItems(first).Single().Action.Value!.Invoke());
            AddAssert("first is marked", () => compare.First?.User.Username, () => Is.EqualTo("first player"));
            AddAssert("the marked score offers to unmark", () => compare.CreateMenuItems(first).Select(text), () => Is.EqualTo(new[] { "Unmark for comparison" }));
            AddAssert("the second score offers the comparison", () => compare.CreateMenuItems(second).Select(text),
                () => Is.EqualTo(new[] { "Compare with first player (as ghost)", "Mark for comparison" }));

            AddStep("compare", () => compare.CreateMenuItems(second).First().Action.Value!.Invoke());

            AddUntilStep("replay player opens", () =>
            {
                DismissAnyNotifications();
                return (player = (Game.ScreenStack.CurrentScreen as ReplayPlayer)!)?.IsLoaded == true;
            });

            AddAssert("the first score is watched", () => player.Score.ScoreInfo.User.Username, () => Is.EqualTo("first player"));
            AddAssert("the second score is the ghost", () => player.BanchosucksGhostScore?.ScoreInfo.User.Username, () => Is.EqualTo("second player"));
            AddAssert("the ghost has its replay", () => player.BanchosucksGhostScore!.Replay.Frames.Count, () => Is.GreaterThan(100));
            AddUntilStep("the ghost is drawn", () => player.ChildrenOfType<BanchosucksGhostCursorOverlay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("its track is the second replay", () => player.ChildrenOfType<BanchosucksGhostCursorOverlay>().Single().Track.FrameCount, () => Is.GreaterThan(100));
            AddAssert("the watched score's mods are used", () => Game.SelectedMods.Value.Select(m => m.Acronym), () => Is.EqualTo(new[] { "NF" }));
            AddAssert("the mark is used up", () => compare.First, () => Is.Null);

            AddStep("leave the replay", () => player.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
        }

        [Test]
        public void TestDifferentPlayfieldIsRefused()
        {
            BanchosucksReplayComparison.Result result = default;

            setUp();

            AddStep("mark the first score", () => compare.Mark(first));
            AddStep("clear notifications", () => Game.Notifications.ChildrenOfType<Notification>().ToArray().ForEach(n => n.Close(false)));

            AddStep("compare with the hard rock score", () => result = compare.Compare(first, hardRock));

            AddAssert("refused because of HR", () => result, () => Is.EqualTo(new BanchosucksReplayComparison.Result(BanchosucksReplayComparison.Verdict.ModInOneScoreOnly, "HR")));
            AddUntilStep("the player is told why", () => Game.Notifications.ChildrenOfType<SimpleNotification>().Any(n => n.Text.ToString().Contains("HR") && n.Text.ToString().Contains("Cannot compare")));
            AddWaitStep("wait", 10);
            AddAssert("still at song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddAssert("the mark stays for another try", () => compare.First?.User.Username, () => Is.EqualTo("first player"));
        }
    }
}
