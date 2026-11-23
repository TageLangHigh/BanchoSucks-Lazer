// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Banchosucks.Stats;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// Daily goal and personal statistics in the running game: the activity log in <see cref="OsuGame"/>, the line on
    /// the main menu, the notification, and which plays are recorded.
    /// </summary>
    public partial class TestSceneBanchosucksDailyGoal : OsuGameTestScene
    {
        private BanchosucksActivityLog log => Game.Dependencies.Get<BanchosucksActivityLog>();

        private BanchosucksDailyGoalDisplay display => Game.ChildrenOfType<BanchosucksDailyGoalDisplay>().Single();

        private Storage logStorage => Game.Dependencies.Get<Storage>().GetStorageForDirectory("banchosucks");

        private int goalNotifications => Game.Notifications.AllNotifications.OfType<SimpleNotification>().Count(n => n.Text.ToString().StartsWith("Daily goal reached", StringComparison.Ordinal));

        private DateTimeOffset now;

        private void startLog(int goalMinutes, int goalMaps)
        {
            AddUntilStep("log loaded", () => log.IsLoaded);
            AddStep("start with an empty log on a fixed day", () =>
            {
                now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.FromHours(1));
                log.Now = () => now;
                log.ResetForTests();
            });
            AddStep("set the goal", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMinutes, goalMinutes);
                Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMaps, goalMaps);
            });
        }

        [Test]
        public void TestGoalOnMainMenu()
        {
            AddUntilStep("log loaded", () => log.IsLoaded);
            AddAssert("no goal by default", () => !log.TodayProgress.Enabled);
            AddAssert("nothing on the main menu", () => !display.Shown && display.Alpha == 0);

            startLog(1, 2);
            AddUntilStep("goal shown on the main menu", () => display.Shown && display.Alpha > 0);

            AddStep("pass a map (45 s)", () => log.RecordPlay(45, "osu", true, 0.98));
            AddAssert("one of two maps", () => display.ShownProgress.MapsPassed, () => Is.EqualTo(1));
            AddAssert("not met", () => log.GoalDays, () => Is.Empty);

            AddStep("fail a map (20 s)", () => log.RecordPlay(20, "osu", false, 0.60));
            AddAssert("minutes reached, maps not", () => display.ShownProgress.MinutesMet && !display.ShownProgress.MapsMet);
            AddAssert("a retry after 5 s is not logged", () => !log.RecordPlay(5, "osu", false, 1));
            AddAssert("still not met", () => log.GoalDays, () => Is.Empty);
            AddAssert("no notification yet", () => goalNotifications, () => Is.EqualTo(0));

            AddStep("pass the second map", () => log.RecordPlay(30, "osu", true, 0.95));
            AddAssert("goal met", () => display.ShownProgress.Met);
            AddAssert("day marked", () => log.GoalDays, () => Is.EqualTo(new[] { new DateOnly(2026, 11, 23) }));
            AddAssert("streak of one", () => log.Streak, () => Is.EqualTo(1));
            AddUntilStep("notification posted", () => goalNotifications, () => Is.EqualTo(1));

            AddStep("pass a third map", () => log.RecordPlay(30, "osu", true, 0.95));
            AddStep("lower the goal", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMaps, 1));
            AddWaitStep("wait", 3);
            AddAssert("notified only once that day", () => goalNotifications, () => Is.EqualTo(1));
            AddStep("goal back to two maps", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMaps, 2));

            AddStep("midnight passes", () => now = now.AddDays(1));
            AddUntilStep("the new day starts at zero", () => display.ShownProgress.MapsPassed, () => Is.EqualTo(0));
            AddAssert("yesterday's streak is still alive", () => log.Streak, () => Is.EqualTo(1));

            AddStep("pass two maps", () =>
            {
                log.RecordPlay(40, "mania", true, 0.99);
                log.RecordPlay(40, "mania", true, 0.97);
            });
            AddAssert("streak of two", () => log.Streak, () => Is.EqualTo(2));
            AddUntilStep("second notification", () => goalNotifications, () => Is.EqualTo(2));

            AddStep("a day without playing, then the next", () => now = now.AddDays(2));
            AddAssert("streak gone", () => log.Streak, () => Is.EqualTo(0));

            AddStep("switch the goal off", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMinutes, 0);
                Game.LocalConfig.SetValue(OsuSetting.BanchosucksDailyGoalMaps, 0);
            });
            AddUntilStep("line gone again", () => !display.Shown && display.Alpha == 0);

            AddStep("wait for the file", () => log.Flush());
            AddAssert("log written", () => logStorage.Exists(BanchosucksActivityLog.LOG_FILE));
            AddAssert("backup written", () => logStorage.Exists(BanchosucksActivityLog.BACKUP_FILE));
            AddAssert("file holds the plays and the met days", () =>
            {
                var file = BanchosucksActivityFile.Read(logStorage.GetFullPath(BanchosucksActivityLog.LOG_FILE))!;
                return (file.Version, file.Entries.Count, file.GoalDays.Count, file.Entries[0].At.Offset, file.Entries[^1].Ruleset);
            }, () => Is.EqualTo((1, 6, 2, TimeSpan.FromHours(1), "mania")));
        }

        [Test]
        public void TestRealPlaysAreRecordedAutoplayIsNot()
        {
            Player player = null!;

            startLog(0, 1);

            Screens.Select.SongSelect songSelect = null!;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            // a real play: passed with No Fail, sped up so the test does not take the length of the map
            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail(), new OsuModDoubleTime { SpeedChange = { Value = 2 } } });
            playToResults();

            AddAssert("was a real play", () => BanchosucksActivityLog.IsRealPlay(player));
            AddAssert("one play logged", () => log.Entries, () => Has.Count.EqualTo(1));
            AddAssert("as passed, in osu!", () => (log.Entries[0].Passed, log.Entries[0].Ruleset, log.Entries[0].Estimated), () => Is.EqualTo((true, "osu", false)));
            AddAssert("with play time", () => log.Entries[0].Seconds, () => Is.GreaterThan(0));
            AddAssert("dated now", () => log.Entries[0].Day, () => Is.EqualTo(new DateOnly(2026, 11, 23)));
            AddAssert("goal of one map met", () => log.GoalDays, () => Has.Count.EqualTo(1));
            AddUntilStep("notification posted", () => goalNotifications, () => Is.EqualTo(1));

            AddStep("leave the results", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen is Screens.Select.SongSelect);
            AddAssert("still one play", () => log.Entries, () => Has.Count.EqualTo(1));

            // autoplay runs through to the results as well, and leaves no trace
            AddStep("set autoplay", () => Game.SelectedMods.Value = new Mod[] { new OsuModAutoplay(), new OsuModDoubleTime { SpeedChange = { Value = 2 } } });
            playToResults();

            AddAssert("was no real play", () => !BanchosucksActivityLog.IsRealPlay(player));
            AddStep("leave the results", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen is Screens.Select.SongSelect);
            AddAssert("still one play", () => log.Entries, () => Has.Count.EqualTo(1));

            void playToResults()
            {
                AddStep("press enter", () => InputManager.Key(Key.Enter));
                AddUntilStep("wait for player", () =>
                {
                    DismissAnyNotifications();
                    return Game.ScreenStack.CurrentScreen is Player current && current.IsLoaded && (player = current) != null;
                });
                AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
                AddStep("seek to near end", () => player.ChildrenOfType<GameplayClockContainer>().First().Seek(Game.Beatmap.Value.Beatmap.HitObjects[^1].StartTime - 1000));
                AddUntilStep("wait for complete", () => player.GameplayState.HasPassed);
                AddUntilStep("wait for results", () => (Game.ScreenStack.CurrentScreen as ResultsScreen)?.IsLoaded == true);
            }
        }
    }
}
