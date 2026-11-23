// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Banchosucks.Stats;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// The personal statistics screen on its own (no game, so no activity log): fed with a fixed set of plays.
    /// </summary>
    public partial class TestSceneBanchosucksStatsScreen : ScreenTestScene
    {
        private static readonly DateOnly today = new DateOnly(2026, 11, 23);

        private BanchosucksStatsScreen screen = null!;

        [Test]
        public void TestThirtyDays()
        {
            var entries = new List<BanchosucksActivityEntry>();

            // 40 days back to yesterday, every third day off; day n has n % 5 + 1 plays of three minutes, the last one failed
            for (int back = 40; back >= 1; back--)
            {
                if (back % 3 == 0)
                    continue;

                var day = today.AddDays(-back);

                for (int i = 0; i <= back % 5; i++)
                {
                    bool last = i == back % 5;

                    entries.Add(new BanchosucksActivityEntry
                    {
                        At = new DateTimeOffset(day.Year, day.Month, day.Day, 19, i * 5, 0, TimeSpan.FromHours(1)),
                        Seconds = 180,
                        Ruleset = "osu",
                        Passed = !last || back % 5 == 0,
                        Accuracy = 0.9 + 0.002 * back,
                        Estimated = back > 35,
                    });
                }
            }

            load(entries);

            AddAssert("totals of the whole log", () => (screen.ShownTotals!.Played, screen.ShownTotals!.Seconds), () => Is.EqualTo((entries.Count, entries.Count * 180.0)));
            AddAssert("estimated days noted", () => screen.ShownTotals!.HasEstimated);
            AddAssert("a bar for every day played in the window", () => screen.TimeChart.FilledBars,
                () => Is.EqualTo(Enumerable.Range(1, BanchosucksStatsScreen.DAYS_SHOWN - 1).Count(back => back % 3 != 0)));
            AddAssert("texts are in place", () => screen.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Personal statistics"));
            AddAssert("longest session found", () => screen.ShownTotals!.LongestSession, () => Is.Not.Null);
        }

        [Test]
        public void TestEmptyLog()
        {
            load(Array.Empty<BanchosucksActivityEntry>());

            AddAssert("nothing counted", () => screen.ShownTotals!.Played, () => Is.EqualTo(0));
            AddAssert("no bars", () => screen.TimeChart.FilledBars, () => Is.EqualTo(0));
            AddAssert("says so", () => screen.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().StartsWith("No plays recorded yet", StringComparison.Ordinal)));
        }

        [Test]
        public void TestFormats()
        {
            AddAssert("durations", () => new[]
            {
                BanchosucksStatsScreen.Duration(0), BanchosucksStatsScreen.Duration(20), BanchosucksStatsScreen.Duration(89), BanchosucksStatsScreen.Duration(90),
                BanchosucksStatsScreen.Duration(3599), BanchosucksStatsScreen.Duration(3 * 3600 + 5 * 60), BanchosucksStatsScreen.Duration(double.NaN),
            }, () => Is.EqualTo(new[] { "0 min", "< 1 min", "1 min", "2 min", "1 h 00 min", "3 h 05 min", "0 min" }));

            AddAssert("accuracy chart floor", () => new[]
            {
                BanchosucksStatsScreen.AccuracyFloor(Array.Empty<double>()), BanchosucksStatsScreen.AccuracyFloor(new[] { 99.5, 97.2 }),
                BanchosucksStatsScreen.AccuracyFloor(new[] { 100.0 }), BanchosucksStatsScreen.AccuracyFloor(new[] { 61.0, 99.0 }), BanchosucksStatsScreen.AccuracyFloor(new[] { 1.0 }),
            }, () => Is.EqualTo(new[] { 0.0, 95.0, 95.0, 55.0, 0.0 }));
        }

        private void load(IReadOnlyList<BanchosucksActivityEntry> entries)
        {
            AddStep("open the screen", () => LoadScreen(screen = new BanchosucksStatsScreen { EntriesOverride = entries, TodayOverride = today }));
            AddUntilStep("loaded and filled", () => screen.IsLoaded && screen.ShownTotals != null);
        }
    }
}
