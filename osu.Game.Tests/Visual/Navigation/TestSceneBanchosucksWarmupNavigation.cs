// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Banchosucks.Warmup;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// Banchosucks: a warm-up series in the real game: play, results screen, "next warm-up map", and what ends it.
    /// </summary>
    /// <remarks>
    /// The three maps are the osu! difficulties of the one quick test set, handed to the controller directly: which
    /// maps get picked is covered by <c>BanchosucksWarmupSelectorTest</c> and <c>TestSceneBanchosucksWarmup</c>.
    /// </remarks>
    public partial class TestSceneBanchosucksWarmupNavigation : OsuGameTestScene
    {
        private SoloSongSelect songSelect = null!;
        private BeatmapInfo[] maps = null!;

        private BanchosucksWarmupSeries series => songSelect.BanchosucksWarmup.Series;

        private ResultsScreen? results => Game.ScreenStack.CurrentScreen as ResultsScreen;

        private BanchosucksWarmupNextButton? nextButton => results?.ChildrenOfType<BanchosucksWarmupNextButton>().SingleOrDefault();

        private void startSeries()
        {
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("no fail at double speed", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail(), new OsuModDoubleTime { SpeedChange = { Value = 2 } } });

            AddStep("start a series of three", () =>
            {
                maps = Game.Beatmap.Value.BeatmapSetInfo.Beatmaps.Where(b => b.Ruleset.OnlineID == 0).OrderBy(b => b.StarRating).Take(3).ToArray();
                Assert.That(maps, Has.Length.EqualTo(3));
                Assert.That(songSelect.BanchosucksWarmup.StartSeries(maps, 5));
            });
        }

        private void playToResults(int index)
        {
            Player player = null!;

            AddUntilStep($"player for map {index + 1}", () =>
            {
                DismissAnyNotifications();
                player = (Game.ScreenStack.CurrentScreen as Player)!;
                return player?.IsLoaded == true && Game.Beatmap.Value.BeatmapInfo.ID == maps[index].ID;
            });
            AddAssert("series at this map", () => series.Active && series.Index == index);

            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
            AddStep("seek to near end", () => player.ChildrenOfType<GameplayClockContainer>().First().Seek(Game.Beatmap.Value.Beatmap.HitObjects[^1].StartTime - 1000));
            AddUntilStep("wait for results", () => results?.IsLoaded == true);
        }

        [Test]
        public void TestThreeMapsOneAfterAnother()
        {
            startSeries();

            playToResults(0);
            AddAssert("button names the second map", () => nextButton?.Text.ToString(), () => Is.EqualTo("Next warm-up map (2/3)"));
            AddStep("click it", () => nextButton!.TriggerClick());

            playToResults(1);
            AddAssert("button names the third map", () => nextButton?.Text.ToString(), () => Is.EqualTo("Next warm-up map (3/3)"));
            AddStep("click it", () => nextButton!.TriggerClick());

            playToResults(2);
            AddAssert("no button after the last map", () => nextButton, () => Is.Null);

            AddStep("leave results", () => results!.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen == songSelect);
            AddAssert("series over", () => !series.Active);
            AddAssert("mods as selected before", () => Game.SelectedMods.Value.Select(m => m.Acronym), () => Is.EquivalentTo(new[] { "NF", "DT" }));
        }

        [Test]
        public void TestLeavingEndsTheSeries()
        {
            startSeries();

            playToResults(0);
            AddAssert("button shown", () => nextButton != null);

            AddStep("leave results", () => results!.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen == songSelect);
            AddAssert("series over", () => !series.Active);

            // an ordinary play afterwards has nothing to do with the warm-up
            AddStep("play the same map again", () => songSelect.GetForwardActions(Game.Beatmap.Value.BeatmapInfo).First().Action.Value!.Invoke());
            AddUntilStep("player", () =>
            {
                DismissAnyNotifications();
                return (Game.ScreenStack.CurrentScreen as Player)?.IsLoaded == true;
            });
            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
            AddStep("seek to near end", () => ((Player)Game.ScreenStack.CurrentScreen).ChildrenOfType<GameplayClockContainer>().First().Seek(Game.Beatmap.Value.Beatmap.HitObjects[^1].StartTime - 1000));
            AddUntilStep("wait for results", () => results?.IsLoaded == true);
            AddAssert("no warm-up button", () => nextButton, () => Is.Null);
        }

        [Test]
        public void TestStableStyleResultsOfferTheNextMapToo()
        {
            AddStep("stable-style results", () => Game.LocalConfig.SetValue(OsuSetting.ToriiStableResults, true));

            startSeries();

            playToResults(0);
            AddAssert("button shown next to the stable overlay", () => nextButton?.IsPresent == true && results!.ChildrenOfType<LegacyRankingOverlay>().Any());
            AddStep("click it", () => nextButton!.TriggerClick());

            playToResults(1);

            AddStep("leave results", () => results!.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen == songSelect);
            AddStep("lazer results again", () => Game.LocalConfig.SetValue(OsuSetting.ToriiStableResults, false));
        }
    }
}
