// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Banchosucks: the "no score yet" filter in song select, by keyword and by button, in both looks.
    /// </summary>
    public partial class TestSceneBanchosucksNoScoreFilter : SongSelectTestScene
    {
        [Cached]
        private readonly LocalUserStatisticsProvider statisticsProvider = new LocalUserStatisticsProvider();

        private FilterControl filter => SongSelect.ChildrenOfType<FilterControl>().Single();

        private ShearedToggleButton noScoreButton => filter.ChildrenOfType<ShearedToggleButton>().Single(b => b.Text.ToString().Contains("score"));

        private BeatmapInfo[] beatmaps = null!;

        private void setUpLibrary()
        {
            // two sets with three difficulties each.
            ImportBeatmapForRuleset(0);
            ImportBeatmapForRuleset(0);

            AddStep("log in", () =>
            {
                API.Login("user1", string.Empty);
                API.AuthenticateSecondFactor("abcdefgh");
            });

            AddStep("add scores", () =>
            {
                beatmaps = Beatmaps.GetAllUsableBeatmapSets().OrderBy(s => s.OnlineID).SelectMany(s => s.Beatmaps.OrderBy(b => b.OnlineID)).ToArray();

                // an own score on the first difficulty, somebody else's score on the second one.
                ScoreManager.Import(createScore(beatmaps[0]));
                ScoreManager.Import(createScore(beatmaps[1], new APIUser { Id = 1337, Username = "user2" }));
            });
        }

        [Test]
        public void TestKeywordAndButton()
        {
            setUpLibrary();
            LoadSongSelect();

            AddAssert("everything shown", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(6));
            AddAssert("button off", () => noScoreButton.Active.Value, () => Is.False);

            search("score=no");
            AddAssert("own scored difficulty hidden", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(5));
            AddAssert("button on", () => noScoreButton.Active.Value, () => Is.True);

            search("score=yes");
            AddAssert("only the scored difficulty", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(1));
            AddAssert("button off", () => noScoreButton.Active.Value, () => Is.False);

            search("score=nein");
            AddAssert("german value works", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(5));

            AddStep("switch the button off", () => noScoreButton.TriggerClick());
            AddAssert("keyword removed", () => filter.SearchQuery.Value, () => Is.Empty);
            waitForFilter();
            AddAssert("everything shown", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(6));

            AddStep("type a title", () => filter.SearchQuery.Value = "Some Song");
            AddStep("switch the button on", () => noScoreButton.TriggerClick());
            AddAssert("keyword appended", () => filter.SearchQuery.Value, () => Is.EqualTo("Some Song score=no"));
            waitForFilter();
            AddAssert("own scored difficulty hidden", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(5));
        }

        [Test]
        public void TestListFollowsScores()
        {
            Guid newScoreId = Guid.Empty;

            setUpLibrary();
            LoadSongSelect();

            search("score=no");
            AddAssert("five without a score", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(5));

            AddStep("set a score on another difficulty", () =>
            {
                var score = createScore(beatmaps[4]);
                newScoreId = score.ID;
                ScoreManager.Import(score);
            });
            AddUntilStep("four without a score", () => !SongSelect.IsFiltering && Carousel.MatchedBeatmapsCount == 4);

            AddStep("delete that score", () => ScoreManager.Delete(new ScoreInfo { ID = newScoreId }));
            AddUntilStep("five without a score again", () => !SongSelect.IsFiltering && Carousel.MatchedBeatmapsCount == 5);
        }

        [Test]
        public void TestWorksInStableLook()
        {
            AddStep("load user statistics", () => Add(statisticsProvider));
            AddStep("enable legacy song select", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));

            setUpLibrary();
            LoadSongSelect();

            AddUntilStep("legacy top loaded", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().SingleOrDefault()?.IsLoaded == true);
            AddUntilStep("modern filter hidden", () => !filter.IsPresent);

            // the stable look's search box is bound to the same search text.
            search("score=no");
            AddAssert("own scored difficulty hidden", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(5));

            AddStep("set a score on another difficulty", () => ScoreManager.Import(createScore(beatmaps[4])));
            AddUntilStep("four without a score", () => !SongSelect.IsFiltering && Carousel.MatchedBeatmapsCount == 4);

            search(string.Empty);
            AddAssert("everything shown", () => Carousel.MatchedBeatmapsCount, () => Is.EqualTo(6));
        }

        private void search(string query)
        {
            AddStep($"search \"{query}\"", () => filter.SearchQuery.Value = query);
            waitForFilter();
        }

        private void waitForFilter()
        {
            AddWaitStep("wait for debounce", 5);
            AddUntilStep("wait for filter", () => !SongSelect.IsFiltering);
        }

        private ScoreInfo createScore(BeatmapInfo beatmap, APIUser? user = null)
        {
            var score = TestResources.CreateTestScoreInfo(beatmap);
            score.User = user ?? API.LocalUser.Value;
            return score;
        }
    }
}
