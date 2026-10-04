// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko.Mods;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: Double Time is shown as ranked at every speed of its slider. The pp decision itself is the
    /// server's (plugin banchosucks_rankedmods), this only covers the indicator in the mod select overlay.
    /// </summary>
    [TestFixture]
    public class BanchosucksDoubleTimeRankedTest
    {
        private static readonly double[] speeds = { 1.01, 1.2, 1.5, 1.75, 2.0 };

        [Test]
        public void TestDoubleTimeIsRankedAtEverySpeed([ValueSource(nameof(speeds))] double speed)
        {
            ModDoubleTime[] mods = { new OsuModDoubleTime(), new TaikoModDoubleTime(), new CatchModDoubleTime(), new ManiaModDoubleTime() };

            foreach (var mod in mods)
            {
                mod.SpeedChange.Value = speed;
                Assert.That(mod.Ranked, Is.True, $"{mod.GetType().Name} at {speed}x");
            }
        }

        [Test]
        public void TestNightcoreKeepsTheDefaultSpeedRule()
        {
            var nightcore = new OsuModNightcore();
            Assert.That(nightcore.Ranked, Is.True);

            nightcore.SpeedChange.Value = 1.2;
            Assert.That(nightcore.Ranked, Is.False);
        }
    }
}
