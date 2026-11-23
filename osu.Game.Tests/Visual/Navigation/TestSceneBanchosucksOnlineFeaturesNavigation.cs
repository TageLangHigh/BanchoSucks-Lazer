// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays.Toolbar;
using osu.Game.Screens.Banchosucks.Stats;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// The online displays in the running game: the weekly tile on the main menu next to the daily goal and the song ticker,
    /// the server status in the toolbar, and the components <see cref="OsuGame"/> provides.
    /// </summary>
    public partial class TestSceneBanchosucksOnlineFeaturesNavigation : OsuGameTestScene
    {
        private BanchosucksWeeklyCard card => Game.ChildrenOfType<BanchosucksWeeklyCard>().Single();

        private BanchosucksDailyGoalDisplay dailyGoal => Game.ChildrenOfType<BanchosucksDailyGoalDisplay>().Single();

        private BanchosucksServerStatusIndicator status => Game.ChildrenOfType<BanchosucksServerStatusIndicator>().Single();

        public override void SetUpSteps()
        {
            // before the game starts: the first requests are made as soon as the main menu and the toolbar appear
            AddStep("fake plugins", () => ((DummyAPIAccess)API).HandleRequest = request =>
            {
                switch (request)
                {
                    case GetBanchosucksMapOfTheWeekRequest map:
                        map.TriggerSuccess(new BanchosucksMapOfTheWeekResponse
                        {
                            Current = new BanchosucksMapOfTheWeek { BeatmapId = 129891, BeatmapSetId = 39804, Title = "xi - FREEDOM DiVE [FOUR DIMENSIONS]", Stars = 7.81 },
                        });
                        return true;

                    case GetBanchosucksCommunityGoalRequest goal:
                        goal.TriggerSuccess(new BanchosucksCommunityGoalResponse { Goal = new BanchosucksCommunityGoal { Target = 2000, Total = 1234, Participants = 45 } });
                        return true;

                    case GetBanchosucksStatusSummaryRequest summary:
                        summary.TriggerSuccess(new BanchosucksStatusSummary { LazerOnline = 7, OpenRooms = 1 });
                        return true;

                    default:
                        return false;
                }
            });

            base.SetUpSteps();
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("remove fake plugins", () => ((DummyAPIAccess)API).HandleRequest = null);
        }

        [Test]
        public void TestWeeklyTileOnTheMainMenu()
        {
            AddUntilStep("tile shown", () => card.Shown && card.Alpha > 0);
            // the game runs in the language of the PC, so only what is the same in both languages is compared
            AddAssert("summary", () => card.HeaderText, () => Does.Contain("xi - FREEDOM DiVE [FOUR DIMENSIONS]").And.EndsWith("61 %"));

            AddStep("set a daily goal", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMinutes, 30));
            AddUntilStep("daily goal shown", () => dailyGoal.Shown && dailyGoal.Alpha > 0);

            // the three texts at the top of the menu each keep their own place
            AddAssert("tile below the daily goal", () => card.ScreenSpaceDrawQuad.AABBFloat.Top, () => Is.GreaterThanOrEqualTo(dailyGoal.ScreenSpaceDrawQuad.AABBFloat.Bottom));
            AddAssert("tile clear of the song ticker", () =>
            {
                var ticker = Game.ChildrenOfType<SongTicker>().Single();
                return !card.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(ticker.ScreenSpaceDrawQuad.AABBFloat);
            });

            AddStep("open the panel", () => card.ChildrenOfType<osu.Game.Graphics.Containers.OsuClickableContainer>().First().TriggerClick());
            AddAssert("panel open", () => card.Expanded);
            AddAssert("goal shown", () => card.GoalText, () => Does.EndWith("(61 %)"));
            AddAssert("map can be downloaded", () => card.MapButtonText, () => Is.EqualTo("Download"));

            // leaving the menu closes the panel
            PushAndConfirm(() => new BanchosucksStatsScreen());
            AddUntilStep("panel closed", () => !card.Expanded);

            AddStep("clear the daily goal", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMinutes, 0));
        }

        [Test]
        public void TestServerStatusInTheToolbar()
        {
            // the toolbar is hidden on the fresh main menu, and a hidden toolbar is not updated: it asks nothing
            AddUntilStep("toolbar loaded", () => Game.ChildrenOfType<BanchosucksServerStatusIndicator>().Any());
            AddStep("show the toolbar", () => Game.Toolbar.Show());

            AddUntilStep("status connected", () => status.Connection, () => Is.EqualTo(BanchosucksConnectionState.Online));
            AddUntilStep("summary arrived", () => status.Summary?.LazerOnline, () => Is.EqualTo(7));
            AddUntilStep("tooltip has the players", () => status.TooltipText.ToString(), () => Does.Contain(": 7"));

            // right of the clock, left of the notification button
            AddAssert("next to the clock", () =>
            {
                var clock = Game.ChildrenOfType<ToolbarClock>().Single();
                var notifications = Game.ChildrenOfType<ToolbarNotificationButton>().Single();

                return status.ScreenSpaceDrawQuad.AABBFloat.Left >= clock.ScreenSpaceDrawQuad.AABBFloat.Right - 1
                       && status.ScreenSpaceDrawQuad.AABBFloat.Right <= notifications.ScreenSpaceDrawQuad.AABBFloat.Left + 1;
            });
        }

        [Test]
        public void TestGameProvidesTheComponents()
        {
            AddAssert("score origins", () => Game.Dependencies.Get<BanchosucksScoreOrigins>(), () => Is.Not.Null);
            AddAssert("download queue", () => Game.Dependencies.Get<BanchosucksBeatmapDownloadQueue>(), () => Is.Not.Null);
            AddAssert("both in the game", () => ((Drawable)Game.Dependencies.Get<BanchosucksScoreOrigins>()).IsLoaded && Game.Dependencies.Get<BanchosucksBeatmapDownloadQueue>().IsLoaded);
        }
    }
}
