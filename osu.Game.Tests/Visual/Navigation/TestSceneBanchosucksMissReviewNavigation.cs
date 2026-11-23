// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// Banchosucks: the miss review on the results screen of a real play, and its click into the replay.
    /// </summary>
    public partial class TestSceneBanchosucksMissReviewNavigation : OsuGameTestScene
    {
        [Test]
        public void TestMissReviewAfterAPlayOpensTheReplayAtTheMiss()
        {
            SoloSongSelect songSelect = null!;
            Player player = null!;
            ResultsScreen results = null!;
            BanchosucksMissReview review = null!;
            ReplayPlayer replayPlayer = null!;
            BanchosucksMiss chosen = default;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);
            AddStep("select Insane", () => Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(
                Game.Beatmap.Value.BeatmapSetInfo.Beatmaps.Single(b => b.Ruleset.OnlineID == 0 && b.DifficultyName == "Insane")));

            // nobody presses a key: with No Fail the play runs through and every object is a miss
            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail() });
            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return (player = (Game.ScreenStack.CurrentScreen as Player)!) != null;
            });

            // the gameplay clock catches up frame by frame, so everything before the seek target is still judged
            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
            AddStep("seek to near end", () => player.ChildrenOfType<GameplayClockContainer>().Single().Seek(Game.Beatmap.Value.Beatmap.HitObjects[^1].StartTime - 1000));

            AddUntilStep("wait for results", () => (results = (Game.ScreenStack.CurrentScreen as ResultsScreen)!)?.IsLoaded == true);
            AddAssert("score has hit events", () => results.Score!.HitEvents, () => Is.Not.Empty);

            AddStep("open statistics", () => results.ChildrenOfType<StatisticsPanel>().Single().Show());
            AddUntilStep("miss review shown", () => (review = results.ChildrenOfType<BanchosucksMissReview>().SingleOrDefault()!)?.IsLoaded == true);

            AddAssert("lists misses", () => review.Misses, () => Is.Not.Empty);
            AddAssert("every circle and slider head ran out of time", () => review.Misses.Count(m => m.Reason == BanchosucksMissReason.NotHitInTime), () => Is.GreaterThan(10));
            AddAssert("nothing claims a key press", () => review.Misses.Select(m => m.Reason),
                () => Has.None.EqualTo(BanchosucksMissReason.TooEarly).And.None.EqualTo(BanchosucksMissReason.Aim).And.None.EqualTo(BanchosucksMissReason.ClickIgnored));

            AddUntilStep("replay found on this PC, lines clickable", () => review.ReplayAvailable && review.ChildrenOfType<BanchosucksMissReview.MissLine>().All(l => l.Enabled.Value));

            AddStep("click a miss in the second half", () =>
            {
                var line = review.ChildrenOfType<BanchosucksMissReview.MissLine>().First(l => l.Miss.Time > 6000);
                chosen = line.Miss;
                line.TriggerClick();
            });

            AddUntilStep("replay opened", () =>
            {
                DismissAnyNotifications();
                return (replayPlayer = (Game.ScreenStack.CurrentScreen as ReplayPlayer)!)?.IsLoaded == true;
            });

            AddAssert("it was told the miss", () => replayPlayer.BanchosucksInitialMissTime, () => Is.EqualTo(chosen.Time));
            AddUntilStep("it jumped to one second before the miss", () =>
                replayPlayer.BanchosucksSelectedMissTime == chosen.Time
                && replayPlayer.ChildrenOfType<GameplayClockContainer>().Single().CurrentTime >= chosen.Time - 1000 - 1);
            AddUntilStep("the replay shows that miss", () =>
                replayPlayer.ChildrenOfType<ScoreProcessor>().Single().HitEvents.Any(e => e.Result.BreaksCombo() && e.HitObject.StartTime == chosen.Time));
        }
    }
}
