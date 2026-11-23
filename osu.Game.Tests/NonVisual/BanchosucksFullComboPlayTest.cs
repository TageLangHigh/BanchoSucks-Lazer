// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: the judgement counts behind the pp preview (95 %, 98 %, 100 % with a full combo) and the preview itself.
    /// </summary>
    [TestFixture]
    public class BanchosucksFullComboPlayTest
    {
        private static Func<HitResult, int> baseScore(Ruleset ruleset) => ruleset.CreateScoreProcessor().GetBaseScoreForResult;

        [Test]
        public void TestRulePerRuleset()
        {
            Assert.That(BanchosucksFullComboPlay.GetRule(new OsuRuleset().ShortName), Is.EqualTo(BanchosucksFullComboPlay.Rule.GreatAndOk));
            Assert.That(BanchosucksFullComboPlay.GetRule(new TaikoRuleset().ShortName), Is.EqualTo(BanchosucksFullComboPlay.Rule.GreatAndOk));
            Assert.That(BanchosucksFullComboPlay.GetRule(new CatchRuleset().ShortName), Is.EqualTo(BanchosucksFullComboPlay.Rule.TinyDroplets));
            Assert.That(BanchosucksFullComboPlay.GetRule(new ManiaRuleset().ShortName), Is.EqualTo(BanchosucksFullComboPlay.Rule.PerfectGreatAndGood));
            Assert.That(BanchosucksFullComboPlay.GetRule("somebody's custom ruleset"), Is.EqualTo(BanchosucksFullComboPlay.Rule.None));
        }

        [Test]
        public void TestOsuUsesGreatAndOkOnly()
        {
            var maximum = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 1000,
                [HitResult.LargeTickHit] = 200,
                [HitResult.SliderTailHit] = 100,
                [HitResult.SmallBonus] = 50,
                [HitResult.IgnoreHit] = 100,
            };

            // accuracy base: 1000 * 300 + 200 * 30 + 100 * 150 = 321000. An OK instead of a GREAT costs 200.
            // 95 %: 16050 / 200 = 80.25 -> 80 OK. 98 %: 6420 / 200 = 32.1 -> 32 OK.
            var at95 = create(new OsuRuleset(), maximum, 0.95)!;
            var at98 = create(new OsuRuleset(), maximum, 0.98)!;

            Assert.That(at95[HitResult.Ok], Is.EqualTo(80));
            Assert.That(at95[HitResult.Great], Is.EqualTo(920));
            Assert.That(at98[HitResult.Ok], Is.EqualTo(32));
            Assert.That(at98[HitResult.Great], Is.EqualTo(968));

            Assert.That(accuracy(new OsuRuleset(), at95, maximum), Is.EqualTo(1 - 16000.0 / 321000).Within(1e-9));
            Assert.That(accuracy(new OsuRuleset(), at98, maximum), Is.EqualTo(1 - 6400.0 / 321000).Within(1e-9));

            foreach (var statistics in new[] { at95, at98 })
            {
                assertFullCombo(statistics, maximum);
                Assert.That(statistics.GetValueOrDefault(HitResult.Meh), Is.Zero);
                // slider ticks, slider ends and bonus stay as in the perfect play.
                Assert.That(statistics[HitResult.LargeTickHit], Is.EqualTo(200));
                Assert.That(statistics[HitResult.SliderTailHit], Is.EqualTo(100));
                Assert.That(statistics[HitResult.SmallBonus], Is.EqualTo(50));
            }
        }

        [Test]
        public void TestTaikoUsesGreatAndOkOnly()
        {
            var maximum = new Dictionary<HitResult, int> { [HitResult.Great] = 500, [HitResult.SmallBonus] = 40, [HitResult.LargeBonus] = 10 };

            // in osu!taiko an OK is worth half a GREAT (150), which the rule takes from the score processor instead of assuming 100.
            // 95 %: 0.05 * 150000 / 150 = 50 OK. 98 %: 20 OK.
            var at95 = create(new TaikoRuleset(), maximum, 0.95)!;
            var at98 = create(new TaikoRuleset(), maximum, 0.98)!;

            Assert.That(at95[HitResult.Ok], Is.EqualTo(50));
            Assert.That(at95[HitResult.Great], Is.EqualTo(450));
            Assert.That(at98[HitResult.Ok], Is.EqualTo(20));
            Assert.That(accuracy(new TaikoRuleset(), at95, maximum), Is.EqualTo(0.95).Within(1e-9));
            assertFullCombo(at95, maximum);
            assertFullCombo(at98, maximum);
        }

        [Test]
        public void TestManiaKeepsTwoPerfectPerGreat()
        {
            var maximum = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1500, [HitResult.IgnoreHit] = 30 };

            // accuracy base 1500 * 305 = 457500; GREAT costs 5, GOOD costs 105.
            // 99.9 %: 457.5 / 5 = 91.5 -> 92 GREAT, far fewer than a third of the notes, so no GOOD is needed.
            var at999 = create(new ManiaRuleset(), maximum, 0.999)!;
            Assert.That(at999[HitResult.Great], Is.EqualTo(92));
            Assert.That(at999[HitResult.Perfect], Is.EqualTo(1408));
            Assert.That(at999.GetValueOrDefault(HitResult.Good), Is.Zero);

            // 98 %: loss 9150. GOOD = (9150 - 1500 / 3 * 5) / (105 - 5 / 3) = 64.4 -> 64, GREAT = (1500 - 64) / 3 = 478.7 -> 479.
            var at98 = create(new ManiaRuleset(), maximum, 0.98)!;
            Assert.That(at98[HitResult.Good], Is.EqualTo(64));
            Assert.That(at98[HitResult.Great], Is.EqualTo(479));
            Assert.That(at98[HitResult.Perfect], Is.EqualTo(957));
            Assert.That(accuracy(new ManiaRuleset(), at98, maximum), Is.EqualTo(1 - 9115.0 / 457500).Within(1e-9));

            // 95 %: loss 22875. GOOD = (22875 - 2500) / 103.33 = 197.2 -> 197, GREAT = 1303 / 3 = 434.3 -> 434.
            var at95 = create(new ManiaRuleset(), maximum, 0.95)!;
            Assert.That(at95[HitResult.Good], Is.EqualTo(197));
            Assert.That(at95[HitResult.Great], Is.EqualTo(434));
            Assert.That(at95[HitResult.Perfect], Is.EqualTo(869));
            Assert.That(accuracy(new ManiaRuleset(), at95, maximum), Is.EqualTo(1 - 22855.0 / 457500).Within(1e-9));

            foreach (var statistics in new[] { at999, at98, at95 })
            {
                assertFullCombo(statistics, maximum);
                Assert.That(statistics.GetValueOrDefault(HitResult.Ok) + statistics.GetValueOrDefault(HitResult.Meh), Is.Zero);
                Assert.That(statistics[HitResult.IgnoreHit], Is.EqualTo(30));
            }

            Assert.That(at95[HitResult.Perfect] / (double)at95[HitResult.Great], Is.EqualTo(BanchosucksFullComboPlay.MANIA_PERFECT_TO_GREAT_RATIO).Within(0.01));
            Assert.That(at98[HitResult.Perfect] / (double)at98[HitResult.Great], Is.EqualTo(BanchosucksFullComboPlay.MANIA_PERFECT_TO_GREAT_RATIO).Within(0.01));
        }

        [Test]
        public void TestCatchOnlyMissesTinyDroplets()
        {
            var maximum = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 300,
                [HitResult.LargeTickHit] = 100,
                [HitResult.SmallTickHit] = 200,
                [HitResult.LargeBonus] = 20,
            };

            // every fruit, droplet and tiny droplet counts the same for accuracy: 600 objects, 95 % = 30 tiny droplets missed.
            var at95 = create(new CatchRuleset(), maximum, 0.95)!;

            Assert.That(at95[HitResult.SmallTickMiss], Is.EqualTo(30));
            Assert.That(at95[HitResult.SmallTickHit], Is.EqualTo(170));
            Assert.That(at95[HitResult.Great], Is.EqualTo(300));
            Assert.That(at95[HitResult.LargeTickHit], Is.EqualTo(100));
            Assert.That(accuracy(new CatchRuleset(), at95, maximum), Is.EqualTo(0.95).Within(1e-9));
            Assert.That(at95.GetValueOrDefault(HitResult.Miss) + at95.GetValueOrDefault(HitResult.LargeTickMiss), Is.Zero, "nothing that breaks the combo");
        }

        [Test]
        public void TestCatchWithoutEnoughTinyDropletsHasNoSuchFullCombo()
        {
            var maximum = new Dictionary<HitResult, int> { [HitResult.Great] = 300, [HitResult.LargeTickHit] = 100, [HitResult.SmallTickHit] = 5 };

            Assert.That(create(new CatchRuleset(), maximum, 0.95), Is.Null);
            Assert.That(create(new CatchRuleset(), maximum, 0.98), Is.Null);
            Assert.That(create(new CatchRuleset(), maximum, 1), Is.EqualTo(maximum));
        }

        [Test]
        public void TestPerfectAccuracyIsThePerfectPlayForEveryRule()
        {
            var maximum = new Dictionary<HitResult, int> { [HitResult.Great] = 123, [HitResult.Perfect] = 45, [HitResult.SmallTickHit] = 6, [HitResult.LargeBonus] = 7 };

            foreach (var rule in Enum.GetValues<BanchosucksFullComboPlay.Rule>())
            {
                var statistics = BanchosucksFullComboPlay.CreateStatistics(rule, maximum, 1, baseScore(new OsuRuleset()));

                Assert.That(statistics, Is.EqualTo(maximum), rule.ToString());
                Assert.That(statistics, Is.Not.SameAs(maximum), "a copy, the caller's counts are never changed");
            }
        }

        [Test]
        public void TestNothingIsInventedWhereTheRuleDoesNotApply()
        {
            var maximum = new Dictionary<HitResult, int> { [HitResult.Great] = 100 };

            Assert.That(BanchosucksFullComboPlay.CreateStatistics(BanchosucksFullComboPlay.Rule.None, maximum, 0.95, baseScore(new OsuRuleset())), Is.Null, "unknown ruleset");
            Assert.That(create(new OsuRuleset(), maximum, double.NaN), Is.Null);
            Assert.That(create(new OsuRuleset(), maximum, 1.2), Is.Null);
            Assert.That(create(new OsuRuleset(), maximum, -0.1), Is.Null);
            // all OK is 33.3 %; nothing lower exists with GREAT and OK.
            Assert.That(create(new OsuRuleset(), maximum, 0.2), Is.Null);
            Assert.That(create(new ManiaRuleset(), new Dictionary<HitResult, int> { [HitResult.Perfect] = 100 }, 0.5), Is.Null);
            Assert.That(maximum, Is.EqualTo(new Dictionary<HitResult, int> { [HitResult.Great] = 100 }));
        }

        [Test]
        public void TestAccuracyIsAsCloseAsTheMapAllows()
        {
            var random = new Random(20261123);

            foreach (double target in new[] { 0.95, 0.98 })
            {
                for (int i = 0; i < 200; i++)
                {
                    int notes = random.Next(200, 4000);

                    var osu = new Dictionary<HitResult, int> { [HitResult.Great] = notes, [HitResult.LargeTickHit] = random.Next(0, notes), [HitResult.SliderTailHit] = random.Next(0, notes) };
                    assertClose(new OsuRuleset(), osu, target, 200);

                    var taiko = new Dictionary<HitResult, int> { [HitResult.Great] = notes };
                    assertClose(new TaikoRuleset(), taiko, target, 150);

                    // GOOD and GREAT are both rounded to whole judgements.
                    var mania = new Dictionary<HitResult, int> { [HitResult.Perfect] = notes };
                    assertClose(new ManiaRuleset(), mania, target, 105 + 5);

                    var fruits = new Dictionary<HitResult, int> { [HitResult.Great] = notes, [HitResult.LargeTickHit] = notes / 4, [HitResult.SmallTickHit] = notes };
                    assertClose(new CatchRuleset(), fruits, target, 300);
                }
            }

            void assertClose(Ruleset ruleset, Dictionary<HitResult, int> maximum, double target, int step)
            {
                var statistics = create(ruleset, maximum, target);
                Assert.That(statistics, Is.Not.Null, ruleset.ShortName);

                double max = maximum.Where(kvp => kvp.Key.AffectsAccuracy()).Sum(kvp => (double)kvp.Value * baseScore(ruleset)(kvp.Key));

                Assert.That(accuracy(ruleset, statistics!, maximum), Is.EqualTo(target).Within(step / 2.0 / max + 1e-9), ruleset.ShortName);
                assertFullCombo(statistics!, maximum);
            }
        }

        [Test]
        public void TestShortMapUsesTheNearestPossibleAccuracy()
        {
            // ten circles: one OK is already 93.3 %. 98 % is nearer to the perfect play, 95 % nearer to one OK.
            var maximum = new Dictionary<HitResult, int> { [HitResult.Great] = 10 };

            Assert.That(create(new OsuRuleset(), maximum, 0.98)!.GetValueOrDefault(HitResult.Ok), Is.Zero);
            Assert.That(create(new OsuRuleset(), maximum, 0.95)![HitResult.Ok], Is.EqualTo(1));
        }

        #region The preview itself, with the rulesets' own calculators

        [Test]
        public void TestPreviewRisesWithAccuracy([Values(0, 1, 3)] int rulesetId)
        {
            var ruleset = createRuleset(rulesetId);
            var values = preview(ruleset, ruleset);

            Assert.That(values.Select(v => v.TargetAccuracy), Is.EqualTo(new[] { 0.95, 0.98, 1.0 }));
            Assert.That(values.Select(v => v.Performance), Has.All.Not.Null);
            Assert.That(values[0].Performance, Is.LessThan(values[1].Performance!.Value));
            Assert.That(values[1].Performance, Is.LessThan(values[2].Performance!.Value));

            Assert.That(values[0].Accuracy, Is.EqualTo(0.95).Within(0.01));
            Assert.That(values[1].Accuracy, Is.EqualTo(0.98).Within(0.01));
            Assert.That(values[2].Accuracy, Is.EqualTo(1));
        }

        [Test]
        public void TestPreviewAtFullAccuracyIsThePerfectPlay([Values(0, 1, 2, 3)] int rulesetId)
        {
            var ruleset = createRuleset(rulesetId);
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            var mods = Array.Empty<Mod>();

            // what BeatmapDifficultyCache calculates for the star rating display ("maximum pp" on the results screen).
            var attributes = ruleset.CreateDifficultyCalculator(working).Calculate(mods);
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(working.GetPlayableBeatmap(ruleset.RulesetInfo, mods));

            var perfectScore = new ScoreInfo(working.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = mods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics
            };

            double expected = ruleset.CreatePerformanceCalculator()!.Calculate(perfectScore, attributes).Total;

            var values = BanchosucksPerformancePreview.Calculate(working, ruleset, mods, attributes);

            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(values[2].Performance, Is.EqualTo(expected));
        }

        [Test]
        public void TestCatchPreviewIsADashWhereNoFullComboExists()
        {
            // whether 95 % and 98 % exist depends on the map's tiny droplets; either a value below the maximum or a dash, never anything else.
            var values = preview(new CatchRuleset(), new CatchRuleset());

            Assert.That(values[2].Performance, Is.Not.Null);

            foreach (var value in values.Take(2))
                Assert.That(value.Performance, Is.Null.Or.LessThan(values[2].Performance!.Value));
        }

        [Test]
        public void TestManiaConvertHasNoPerformance()
        {
            // the server gives no pp for converts, so the preview shows dashes.
            var values = preview(new OsuRuleset(), new ManiaRuleset());

            Assert.That(values.Select(v => v.Performance), Has.All.Null);
        }

        [Test]
        public void TestManiaModWithoutPerformanceIsADash()
        {
            Assert.That(preview(new ManiaRuleset(), new ManiaRuleset(), new ManiaModRandom()).Select(v => v.Performance), Has.All.Null);
        }

        [Test]
        public void TestModsChangeThePreview()
        {
            var noMod = preview(new OsuRuleset(), new OsuRuleset());
            var hardRock = preview(new OsuRuleset(), new OsuRuleset(), new OsuModHardRock());
            var maniaNoMod = preview(new ManiaRuleset(), new ManiaRuleset());
            var maniaNoFail = preview(new ManiaRuleset(), new ManiaRuleset(), new ManiaModNoFail());

            for (int i = 0; i < noMod.Length; i++)
            {
                Assert.That(hardRock[i].Performance, Is.GreaterThan(noMod[i].Performance!.Value));
                Assert.That(maniaNoFail[i].Performance, Is.LessThan(maniaNoMod[i].Performance!.Value));
            }
        }

        private static Ruleset createRuleset(int rulesetId)
        {
            switch (rulesetId)
            {
                case 0:
                    return new OsuRuleset();

                case 1:
                    return new TaikoRuleset();

                case 2:
                    return new CatchRuleset();

                default:
                    return new ManiaRuleset();
            }
        }

        private static BanchosucksPerformancePreview.Value[] preview(Ruleset beatmapRuleset, Ruleset ruleset, params Mod[] mods)
        {
            WorkingBeatmap working = new TestWorkingBeatmap(new TestBeatmap(beatmapRuleset.RulesetInfo));
            var attributes = ruleset.CreateDifficultyCalculator(working).Calculate(mods);

            return BanchosucksPerformancePreview.Calculate(working, ruleset, mods, attributes);
        }

        #endregion

        private static Dictionary<HitResult, int>? create(Ruleset ruleset, Dictionary<HitResult, int> maximum, double accuracy)
            => BanchosucksFullComboPlay.CreateStatistics(BanchosucksFullComboPlay.GetRule(ruleset.ShortName), maximum, accuracy, baseScore(ruleset));

        private static double accuracy(Ruleset ruleset, Dictionary<HitResult, int> statistics, Dictionary<HitResult, int> maximum)
            => BanchosucksFullComboPlay.ComputeAccuracy(statistics, maximum, baseScore(ruleset));

        /// <summary>
        /// Nothing that breaks the combo, nothing negative, and as many judgements as the perfect play has.
        /// </summary>
        private static void assertFullCombo(Dictionary<HitResult, int> statistics, Dictionary<HitResult, int> maximum)
        {
            Assert.That(statistics.Values, Has.All.GreaterThanOrEqualTo(0));
            Assert.That(statistics.Where(kvp => kvp.Key.BreaksCombo()).Sum(kvp => kvp.Value), Is.Zero);
            Assert.That(statistics.Values.Sum(), Is.EqualTo(maximum.Values.Sum()));
        }
    }
}
