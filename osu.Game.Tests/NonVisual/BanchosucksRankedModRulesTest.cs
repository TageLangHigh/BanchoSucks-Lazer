// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.Banchosucks;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Difficulty.Banchosucks;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Mods;
using Reason = osu.Game.Online.Banchosucks.BanchosucksRankedModRules.Reason;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The cases are the ones of the server plugin's own tests (<c>banchosucks_rankedmods/test_rules.py</c>, including
    /// <c>rules.SAMPLES</c>): the client's mirror has to answer them the same way.
    /// </summary>
    [TestFixture]
    public class BanchosucksRankedModRulesTest
    {
        private const int std = BanchosucksRankedModRules.RULESET_OSU;
        private const int taiko = BanchosucksRankedModRules.RULESET_TAIKO;
        private const int fruits = BanchosucksRankedModRules.RULESET_CATCH;
        private const int mania = BanchosucksRankedModRules.RULESET_MANIA;

        private static BanchosucksRankedModRules.ModEntry da(params (string name, object value)[] settings)
            => new BanchosucksRankedModRules.ModEntry("DA", settings.ToDictionary(s => s.name, s => s.value), RankedByClient: false, RankedByClientWithDefaults: false);

        private static BanchosucksRankedModRules.ModEntry dt(double? speed = null, bool? pitch = null)
        {
            var settings = new Dictionary<string, object>();
            if (speed != null) settings["speed_change"] = speed.Value;
            if (pitch != null) settings["adjust_pitch"] = pitch.Value;
            return new BanchosucksRankedModRules.ModEntry("DT", settings);
        }

        private static bool ranked(int ruleset, params BanchosucksRankedModRules.ModEntry[] mods) => BanchosucksRankedModRules.Check(ruleset, mods).GivesPerformance;

        // rules.SAMPLES
        private static readonly object[] samples =
        {
            new object[] { std, new (string, object)[] { }, true },
            new object[] { std, new (string, object)[] { ("circle_size", 5.5f), ("approach_rate", 9.5f), ("overall_difficulty", 0.0f), ("drain_rate", 10.0f) }, true },
            new object[] { std, new (string, object)[] { ("circle_size", 7.0f) }, true },
            new object[] { std, new (string, object)[] { ("circle_size", 4.0f), ("extended_limits", true) }, true },
            new object[] { std, new (string, object)[] { ("circle_size", 7.1f) }, false },
            new object[] { std, new (string, object)[] { ("circle_size", 11.0f), ("extended_limits", true) }, false },
            new object[] { std, new (string, object)[] { ("approach_rate", 10.5f), ("extended_limits", true) }, false },
            new object[] { std, new (string, object)[] { ("approach_rate", -3.0f), ("extended_limits", true) }, false },
            new object[] { std, new (string, object)[] { ("overall_difficulty", 11.0f), ("extended_limits", true) }, false },
            new object[] { std, new (string, object)[] { ("scroll_speed", 2.0f) }, false },
            new object[] { taiko, new (string, object)[] { ("scroll_speed", 4.0f), ("overall_difficulty", 3.0f) }, true },
            new object[] { taiko, new (string, object)[] { ("scroll_speed", 4.5f) }, false },
            new object[] { taiko, new (string, object)[] { ("circle_size", 4.0f) }, false },
            new object[] { fruits, new (string, object)[] { ("circle_size", 7.0f), ("approach_rate", 10.0f), ("hard_rock_offsets", true) }, true },
            new object[] { fruits, new (string, object)[] { ("circle_size", 8.0f) }, false },
        };

        [TestCaseSource(nameof(samples))]
        public void TestTheSamplesAreJudgedAsIntended(int ruleset, (string, object)[] settings, bool expected)
        {
            Assert.That(ranked(ruleset, da(settings)), Is.EqualTo(expected));
        }

        [Test]
        public void TestDoubleTimeAllowsTheWholeSliderRange()
        {
            foreach (int ruleset in new[] { std, taiko, fruits, mania })
            {
                Assert.That(ranked(ruleset, dt()), Is.True);
                Assert.That(ranked(ruleset, dt(1.01)), Is.True);
                Assert.That(ranked(ruleset, dt(1.2)), Is.True);
                Assert.That(ranked(ruleset, dt(2.0)), Is.True);
                Assert.That(ranked(ruleset, dt(1.3, true)), Is.True, "adjust_pitch is not checked");
                Assert.That(ranked(ruleset, dt(1.0)), Is.False);
                Assert.That(ranked(ruleset, dt(2.01)), Is.False);
            }

            Assert.That(BanchosucksRankedModRules.Check(std, new[] { dt(2.5) }),
                Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.SettingOutOfRange, "DT", "speed_change", 1.01, 2.0)));
        }

        [Test]
        public void TestOtherModsKeepTheClientRule()
        {
            // Nightcore, Half Time and Easy keep g0v0's rule (default settings only), which the client's Mod.Ranked stands in for
            var halfTimeCustom = new BanchosucksRankedModRules.ModEntry("HT", new Dictionary<string, object> { ["speed_change"] = 0.8 }, false, true);
            var relax = new BanchosucksRankedModRules.ModEntry("RX", null, false, false);

            foreach (int ruleset in new[] { std, taiko, fruits, mania })
            {
                Assert.That(ranked(ruleset, new BanchosucksRankedModRules.ModEntry("HR")), Is.True);
                Assert.That(BanchosucksRankedModRules.Check(ruleset, new[] { halfTimeCustom }).Reason, Is.EqualTo(Reason.ModSettings));
            }

            Assert.That(BanchosucksRankedModRules.Check(std, new[] { relax }), Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.UnrankedMod, "RX")));
        }

        [Test]
        public void TestClassicGivesPerformanceWhateverTheClientSays()
        {
            var classic = new BanchosucksRankedModRules.ModEntry("CL", new Dictionary<string, object> { ["no_slider_head_accuracy"] = false }, false, false);

            foreach (int ruleset in new[] { std, taiko, fruits, mania })
                Assert.That(ranked(ruleset, classic), Is.True);
        }

        [Test]
        public void TestManiaAndCustomRulesetsStayWithoutDifficultyAdjust()
        {
            Assert.That(ranked(mania, da(("overall_difficulty", 5.0f))), Is.False);
            Assert.That(ranked(mania, da()), Is.False);
            Assert.That(BanchosucksRankedModRules.Check(mania, new[] { da() }), Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.ManiaMod, "DA")));

            // a custom ruleset is not touched by the plugin: the client's rule (unranked) stays
            Assert.That(ranked(4, da()), Is.False);
            Assert.That(ranked(4, dt(1.2)), Is.True, "only because the client's own rule says so");
            Assert.That(ranked(4, new BanchosucksRankedModRules.ModEntry("DT", null, false)), Is.False);
        }

        [Test]
        public void TestValuesInsideTheLimitsGivePerformance()
        {
            Assert.That(ranked(std, da()), Is.True);
            Assert.That(ranked(std, da(("circle_size", 0.0f), ("approach_rate", 0.0f), ("overall_difficulty", 0.0f), ("drain_rate", 0.0f))), Is.True);
            Assert.That(ranked(std, da(("circle_size", 7.0f), ("approach_rate", 10.0f), ("overall_difficulty", 10.0f), ("drain_rate", 10.0f))), Is.True);
            Assert.That(ranked(std, da(("circle_size", 4), ("approach_rate", 9))), Is.True, "whole numbers as integers");
        }

        [Test]
        public void TestCircleSizeEndsAtSeven()
        {
            Assert.That(BanchosucksRankedModRules.MAX_CIRCLE_SIZE, Is.EqualTo(7));

            foreach (int ruleset in new[] { std, fruits })
            {
                Assert.That(ranked(ruleset, da(("circle_size", 7.0f))), Is.True);
                Assert.That(ranked(ruleset, da(("circle_size", 7.1f))), Is.False);
                Assert.That(ranked(ruleset, da(("circle_size", 10.0f))), Is.False);
            }

            Assert.That(BanchosucksRankedModRules.Check(std, new[] { da(("circle_size", 7.1f)) }),
                Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.SettingOutOfRange, "DA", "circle_size", 0, 7)));
        }

        [Test]
        public void TestExtendedValuesGiveNoPerformanceWhateverTheSwitchSays()
        {
            var cases = new (string, object)[] { ("circle_size", 11.0f), ("approach_rate", 11.0f), ("approach_rate", -0.1f), ("overall_difficulty", 10.1f), ("drain_rate", 11.0f) };

            foreach (var setting in cases)
            {
                Assert.That(ranked(std, da(setting)), Is.False);
                Assert.That(ranked(std, da(setting, ("extended_limits", true))), Is.False);
                Assert.That(ranked(std, da(setting, ("extended_limits", false))), Is.False);
            }
        }

        [Test]
        public void TestTheSwitchAloneCostsNothing()
        {
            Assert.That(ranked(std, da(("extended_limits", true), ("approach_rate", 9.6f))), Is.True);
        }

        [Test]
        public void TestASettingOfAnotherRulesetGivesNoPerformance()
        {
            Assert.That(ranked(std, da(("scroll_speed", 2.0f))), Is.False);
            Assert.That(ranked(taiko, da(("circle_size", 4.0f))), Is.False);
            Assert.That(ranked(std, da(("hard_rock_offsets", true))), Is.False);
            Assert.That(ranked(fruits, da(("hard_rock_offsets", true))), Is.True);

            Assert.That(BanchosucksRankedModRules.Check(std, new[] { da(("scroll_speed", 2.0f)) }),
                Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.SettingNotAllowed, "DA", "scroll_speed")));
        }

        [Test]
        public void TestTaikoScrollSpeedFollowsTheSlider()
        {
            Assert.That(ranked(taiko, da(("scroll_speed", 0.25f))), Is.True);
            Assert.That(ranked(taiko, da(("scroll_speed", 4.0f))), Is.True);
            Assert.That(ranked(taiko, da(("scroll_speed", 0.2f))), Is.False);
            Assert.That(ranked(taiko, da(("scroll_speed", 4.05f))), Is.False);
        }

        [Test]
        public void TestTogetherWithOtherModsEveryModIsJudgedOnItsOwn()
        {
            Assert.That(ranked(std, da(("circle_size", 5.0f)), dt(1.3)), Is.True);
            Assert.That(ranked(std, da(("circle_size", 8.0f)), dt()), Is.False);
        }

        [Test]
        public void TestManiaOnlyPaysForTheNeutralMods()
        {
            foreach (string acronym in BanchosucksRankedModRules.MANIA_NEUTRAL_MODS)
                Assert.That(ranked(mania, new BanchosucksRankedModRules.ModEntry(acronym)), Is.True, acronym);

            foreach (string acronym in new[] { "4K", "RD", "IN", "HO", "WU", "DS", "RX" })
                Assert.That(BanchosucksRankedModRules.Check(mania, new[] { new BanchosucksRankedModRules.ModEntry(acronym) }).Reason, Is.EqualTo(Reason.ManiaMod), acronym);

            // the same mod is fine elsewhere as far as these rules go
            Assert.That(ranked(std, new BanchosucksRankedModRules.ModEntry("RD")), Is.True);
        }

        [Test]
        public void TestManiaNeutralModListsAreTheSame()
        {
            // osu.Game cannot reference the mania ruleset, so the rules carry their own copy of the list
            Assert.That(BanchosucksRankedModRules.MANIA_NEUTRAL_MODS, Is.EquivalentTo(ManiaPerformance.NEUTRAL_MODS));
        }

        [Test]
        public void TestManiaConvertsGiveNoPerformance()
        {
            Assert.That(BanchosucksRankedModRules.Check(mania, Enumerable.Empty<BanchosucksRankedModRules.ModEntry>(), convert: true).Reason, Is.EqualTo(Reason.ManiaConvert));
            Assert.That(BanchosucksRankedModRules.Check(taiko, Enumerable.Empty<BanchosucksRankedModRules.ModEntry>(), convert: true).GivesPerformance, Is.True);
            Assert.That(BanchosucksRankedModRules.Check(mania, Enumerable.Empty<BanchosucksRankedModRules.ModEntry>()).GivesPerformance, Is.True);
        }

        #region with the game's own mods (what the mod select overlay asks)

        private static BanchosucksRankedModRules.Verdict check(osu.Game.Rulesets.Ruleset ruleset, params Mod[] mods)
            => BanchosucksPerformanceEligibility.Check(ruleset.RulesetInfo, new BeatmapInfo { Ruleset = ruleset.RulesetInfo }, mods);

        [Test]
        public void TestRealModsOsu()
        {
            var osuRuleset = new OsuRuleset();

            Assert.That(check(osuRuleset).GivesPerformance, Is.True);
            Assert.That(check(osuRuleset, new OsuModHidden(), new OsuModHardRock()).GivesPerformance, Is.True);
            Assert.That(check(osuRuleset, new OsuModClassic()).GivesPerformance, Is.True, "Classic gives pp on Banchosucks");
            Assert.That(check(osuRuleset, new OsuModDoubleTime { SpeedChange = { Value = 1.2 } }).GivesPerformance, Is.True);
            Assert.That(check(osuRuleset, new OsuModHalfTime { SpeedChange = { Value = 0.8 } }).Reason, Is.EqualTo(Reason.ModSettings));
            Assert.That(check(osuRuleset, new OsuModRelax()), Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.UnrankedMod, "RX")));

            Assert.That(check(osuRuleset, new OsuModDifficultyAdjust()).GivesPerformance, Is.True);
            Assert.That(check(osuRuleset, new OsuModDifficultyAdjust { CircleSize = { Value = 7 }, ApproachRate = { Value = 10 } }).GivesPerformance, Is.True);
            Assert.That(check(osuRuleset, new OsuModDifficultyAdjust { CircleSize = { Value = 7.1f } }),
                Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.SettingOutOfRange, "DA", "circle_size", 0, 7)));
            Assert.That(check(osuRuleset, new OsuModDifficultyAdjust { ExtendedLimits = { Value = true }, ApproachRate = { Value = 11 } }).Reason, Is.EqualTo(Reason.SettingOutOfRange));
            Assert.That(check(osuRuleset, new OsuModDifficultyAdjust { ExtendedLimits = { Value = true }, ApproachRate = { Value = 9.6f } }).GivesPerformance, Is.True);
        }

        [Test]
        public void TestRealModsTaikoAndCatch()
        {
            Assert.That(check(new TaikoRuleset(), new TaikoModDifficultyAdjust { ScrollSpeed = { Value = 4 } }).GivesPerformance, Is.True);
            Assert.That(check(new TaikoRuleset(), new TaikoModDifficultyAdjust { ExtendedLimits = { Value = true }, OverallDifficulty = { Value = 10.5f } }).GivesPerformance, Is.False);
            Assert.That(check(new CatchRuleset(), new CatchModDifficultyAdjust { CircleSize = { Value = 7 }, HardRockOffsets = { Value = true } }).GivesPerformance, Is.True);
            Assert.That(check(new CatchRuleset(), new CatchModDifficultyAdjust { CircleSize = { Value = 8 } }).GivesPerformance, Is.False);
        }

        [Test]
        public void TestRealModsMania()
        {
            var maniaRuleset = new ManiaRuleset();

            Assert.That(check(maniaRuleset, new ManiaModMirror(), new ManiaModDoubleTime { SpeedChange = { Value = 1.3 } }).GivesPerformance, Is.True);
            Assert.That(check(maniaRuleset, new ManiaModDifficultyAdjust()), Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.ManiaMod, "DA")));
            Assert.That(check(maniaRuleset, new ManiaModRandom()).Reason, Is.EqualTo(Reason.ManiaMod));

            // on the neutral list although the client never ranks them: the list wins, as in the client's mania pp
            Assert.That(new ManiaModHardRock().Ranked, Is.False);
            Assert.That(check(maniaRuleset, new ManiaModHardRock(), new ManiaModConstantSpeed()).GivesPerformance, Is.True);
            // changed settings keep the client's rule
            Assert.That(check(maniaRuleset, new ManiaModHalfTime { SpeedChange = { Value = 0.8 } }), Is.EqualTo(new BanchosucksRankedModRules.Verdict(Reason.ModSettings, "HT")));

            // a map made for osu! played in mania
            var convert = BanchosucksPerformanceEligibility.Check(maniaRuleset.RulesetInfo, new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo }, new Mod[] { new ManiaModMirror() });
            Assert.That(convert.Reason, Is.EqualTo(Reason.ManiaConvert));
        }

        [Test]
        public void TestEveryVerdictHasAText()
        {
            var verdicts = new[]
            {
                new BanchosucksRankedModRules.Verdict(Reason.None),
                new BanchosucksRankedModRules.Verdict(Reason.UnrankedMod, "RX"),
                new BanchosucksRankedModRules.Verdict(Reason.ModSettings, "HT"),
                new BanchosucksRankedModRules.Verdict(Reason.SettingOutOfRange, "DA", "circle_size", 0, 7),
                new BanchosucksRankedModRules.Verdict(Reason.SettingNotAllowed, "DA", "scroll_speed"),
                new BanchosucksRankedModRules.Verdict(Reason.ManiaMod, "RD"),
                new BanchosucksRankedModRules.Verdict(Reason.ManiaConvert),
            };

            foreach (var verdict in verdicts)
            {
                string text = BanchosucksPerformanceEligibility.Describe(verdict).ToString();
                Assert.That(text, verdict.GivesPerformance ? Is.EqualTo("gives pp") : Does.StartWith("no pp on Banchosucks: "));
                Assert.That(text, Does.Not.Contain("{"));
                Assert.That(BanchosucksPerformanceEligibility.Explain(verdict).ToString(), Is.Not.Empty);
            }

            Assert.That(BanchosucksPerformanceEligibility.Describe(verdicts[3]).ToString(), Is.EqualTo("no pp on Banchosucks: DA circle size only from 0 to 7"));
        }

        #endregion
    }
}
