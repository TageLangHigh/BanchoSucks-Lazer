// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The rule behind the "pp if FC" counter (<see cref="BanchosucksFullComboProjection"/>), first on plain numbers,
    /// then against real score processors and performance calculators: projecting a run with combo breaks must give
    /// exactly the score of the same run played without them.
    /// </summary>
    [TestFixture]
    public class BanchosucksFullComboProjectionTest
    {
        // the base scores of ScoreProcessor.GetBaseScoreForResult, written out so the rule is tested without a ruleset
        private static int baseScore(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                    return 300;

                case HitResult.Good:
                    return 200;

                case HitResult.Ok:
                    return 100;

                case HitResult.Meh:
                    return 50;

                case HitResult.SliderTailHit:
                    return 150;

                case HitResult.LargeTickHit:
                    return 30;

                case HitResult.SmallTickHit:
                    return 10;

                default:
                    return 0;
            }
        }

        [Test]
        public void TestReplacements()
        {
            Assert.That(BanchosucksFullComboProjection.ReplacementFor(HitResult.Miss, HitResult.Great), Is.EqualTo(HitResult.Great));
            Assert.That(BanchosucksFullComboProjection.ReplacementFor(HitResult.Miss, HitResult.Perfect), Is.EqualTo(HitResult.Perfect));
            Assert.That(BanchosucksFullComboProjection.ReplacementFor(HitResult.LargeTickMiss, HitResult.Great), Is.EqualTo(HitResult.LargeTickHit));
            Assert.That(BanchosucksFullComboProjection.ReplacementFor(HitResult.ComboBreak, HitResult.Perfect), Is.EqualTo(HitResult.IgnoreHit));

            // everything that keeps the combo stays as played
            foreach (var kept in new[]
                     {
                         HitResult.Meh, HitResult.Ok, HitResult.Good, HitResult.Great, HitResult.Perfect, HitResult.SmallTickMiss, HitResult.SmallTickHit,
                         HitResult.LargeTickHit, HitResult.SliderTailHit, HitResult.IgnoreMiss, HitResult.IgnoreHit, HitResult.SmallBonus, HitResult.LargeBonus, HitResult.None
                     })
                Assert.That(BanchosucksFullComboProjection.ReplacementFor(kept, HitResult.Great), Is.Null, kept.ToString());

            // every result that breaks combo has a replacement, and the replacement does not break combo
            foreach (var result in HitResultExtensions.ALL_TYPES.Where(r => r.BreaksCombo()))
            {
                var replacement = BanchosucksFullComboProjection.ReplacementFor(result, HitResult.Great);
                Assert.That(replacement, Is.Not.Null, result.ToString());
                Assert.That(replacement!.Value.BreaksCombo(), Is.False, result.ToString());
            }
        }

        [Test]
        public void TestBestBasicResult()
        {
            Assert.That(BanchosucksFullComboProjection.BestBasicResult(new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 100, [HitResult.LargeTickHit] = 40, [HitResult.SliderTailHit] = 20, [HitResult.SmallBonus] = 9, [HitResult.Perfect] = 0,
            }), Is.EqualTo(HitResult.Great));

            Assert.That(BanchosucksFullComboProjection.BestBasicResult(new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 500, [HitResult.IgnoreHit] = 80,
            }), Is.EqualTo(HitResult.Perfect));

            Assert.That(BanchosucksFullComboProjection.BestBasicResult(new Dictionary<HitResult, int>()), Is.EqualTo(HitResult.Great), "nothing known yet");
        }

        [Test]
        public void TestFullComboSoFarChangesNothing()
        {
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 90, [HitResult.Ok] = 8, [HitResult.Meh] = 2, [HitResult.LargeTickHit] = 30, [HitResult.SliderTailHit] = 18, [HitResult.IgnoreMiss] = 2,
                [HitResult.Miss] = 0,
            };

            const double played = 90 * 300 + 8 * 100 + 2 * 50 + 30 * 30 + 18 * 150;
            const double maximum = 100 * 300 + 30 * 30 + 20 * 150;

            var projection = BanchosucksFullComboProjection.Project(statistics, HitResult.Great, played, maximum, baseScore);

            Assert.That(projection.Replaced, Is.EqualTo(0));
            Assert.That(projection.Statistics, Is.EquivalentTo(statistics));
            Assert.That(projection.Accuracy, Is.EqualTo(played / maximum).Within(1e-12), "the accuracy so far, 100s and dropped slider ends included");
            Assert.That(projection.MaxCombo, Is.EqualTo(90 + 8 + 2 + 30 + 18), "dropped slider ends give no combo");
        }

        [Test]
        public void TestMissesAndSliderBreaksAreReplaced()
        {
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 80, [HitResult.Ok] = 10, [HitResult.Meh] = 4, [HitResult.Miss] = 6,
                [HitResult.LargeTickHit] = 27, [HitResult.LargeTickMiss] = 3,
                [HitResult.SliderTailHit] = 15, [HitResult.IgnoreMiss] = 5,
                [HitResult.SmallBonus] = 12,
            };

            const double played = 80 * 300 + 10 * 100 + 4 * 50 + 27 * 30 + 15 * 150;
            const double maximum = 100 * 300 + 30 * 30 + 20 * 150;

            var projection = BanchosucksFullComboProjection.Project(statistics, HitResult.Great, played, maximum, baseScore);

            Assert.That(projection.Replaced, Is.EqualTo(9));
            Assert.That(projection.Statistics[HitResult.Miss], Is.EqualTo(0));
            Assert.That(projection.Statistics[HitResult.LargeTickMiss], Is.EqualTo(0));
            Assert.That(projection.Statistics[HitResult.Great], Is.EqualTo(86));
            Assert.That(projection.Statistics[HitResult.LargeTickHit], Is.EqualTo(30));

            // untouched: the 100s and 50s, the dropped slider ends, the bonus
            Assert.That(projection.Statistics[HitResult.Ok], Is.EqualTo(10));
            Assert.That(projection.Statistics[HitResult.Meh], Is.EqualTo(4));
            Assert.That(projection.Statistics[HitResult.SliderTailHit], Is.EqualTo(15));
            Assert.That(projection.Statistics[HitResult.IgnoreMiss], Is.EqualTo(5));
            Assert.That(projection.Statistics[HitResult.SmallBonus], Is.EqualTo(12));

            // accuracy rises by what the misses cost and by nothing else
            const double projected = played + 6 * 300 + 3 * 30;
            Assert.That(projection.Accuracy, Is.EqualTo(projected / maximum).Within(1e-12));
            Assert.That(projection.Accuracy, Is.GreaterThan(played / maximum));
            Assert.That(projection.Accuracy, Is.LessThan(1), "a full combo, not a perfect play");

            Assert.That(projection.MaxCombo, Is.EqualTo(86 + 10 + 4 + 30 + 15));

            // the input is not modified
            Assert.That(statistics[HitResult.Miss], Is.EqualTo(6));
        }

        [Test]
        public void TestManiaHoldBreaksAndPerfects()
        {
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 400, [HitResult.Great] = 50, [HitResult.Good] = 5, [HitResult.Miss] = 4,
                [HitResult.IgnoreHit] = 60, [HitResult.ComboBreak] = 2,
            };

            const double played = 450 * 300 + 5 * 200;
            const double maximum = 459 * 300;

            var projection = BanchosucksFullComboProjection.Project(statistics, HitResult.Perfect, played, maximum, baseScore);

            Assert.That(projection.Replaced, Is.EqualTo(6));
            Assert.That(projection.Statistics[HitResult.Perfect], Is.EqualTo(404), "a miss becomes the best result of the ruleset");
            Assert.That(projection.Statistics[HitResult.Miss], Is.EqualTo(0));
            Assert.That(projection.Statistics[HitResult.ComboBreak], Is.EqualTo(0));
            Assert.That(projection.Statistics[HitResult.IgnoreHit], Is.EqualTo(62));
            Assert.That(projection.Accuracy, Is.EqualTo((played + 4 * 300) / maximum).Within(1e-12), "a hold break costs combo, not accuracy");
            Assert.That(projection.MaxCombo, Is.EqualTo(404 + 50 + 5), "hold bodies give no combo");
        }

        [Test]
        public void TestNothingJudgedYet()
        {
            var projection = BanchosucksFullComboProjection.Project(new Dictionary<HitResult, int>(), HitResult.Great, 0, 0, baseScore);

            Assert.That(projection.Accuracy, Is.EqualTo(1));
            Assert.That(projection.MaxCombo, Is.EqualTo(0));
            Assert.That(projection.Replaced, Is.EqualTo(0));
        }

        /// <summary>
        /// osu!: one missed circle, one 100, one slider break (missed tick) and one dropped slider end. The projection has
        /// to equal the same run without the miss and the slider break, in statistics, accuracy, combo and pp.
        /// </summary>
        [Test]
        public void TestOsuMatchesTheRunPlayedAsFullCombo()
        {
            var ruleset = new OsuRuleset();
            var (working, playable) = createBeatmap(ruleset);

            var all = enumerate(playable.HitObjects).ToList();

            HitObject missed = all.First(h => h.Judgement.MaxResult == HitResult.Great && h.NestedHitObjects.Count == 0 && h.Judgement.MinResult == HitResult.Miss);
            HitObject ok = all.Last(h => h.Judgement.MaxResult == HitResult.Great && h.NestedHitObjects.Count == 0 && h.Judgement.MinResult == HitResult.Miss);
            HitObject brokenTick = all.First(h => h.Judgement.MaxResult == HitResult.LargeTickHit);
            HitObject droppedEnd = all.First(h => h.Judgement.MaxResult == HitResult.SliderTailHit);

            Assert.That(missed, Is.Not.SameAs(ok));

            HitResult played(HitObject h)
            {
                if (h == missed || h == brokenTick || h == droppedEnd) return h.Judgement.MinResult;
                if (h == ok) return HitResult.Ok;

                return h.Judgement.MaxResult;
            }

            HitResult fullCombo(HitObject h)
            {
                if (h == droppedEnd) return h.Judgement.MinResult;
                if (h == ok) return HitResult.Ok;

                return h.Judgement.MaxResult;
            }

            Assert.That(droppedEnd.Judgement.MinResult.BreaksCombo(), Is.False, "a dropped slider end is no combo break");

            var (actual, actualProcessor) = play(ruleset, playable, all, played);
            var (reference, _) = play(ruleset, playable, all, fullCombo);

            Assert.That(actual.Statistics[HitResult.Miss], Is.EqualTo(1));
            Assert.That(actual.Statistics[HitResult.LargeTickMiss], Is.EqualTo(1));
            Assert.That(actual.MaxCombo, Is.LessThan(reference.MaxCombo));

            var attributes = ruleset.CreateDifficultyCalculator(working).Calculate();
            var calculator = ruleset.CreatePerformanceCalculator()!;

            double actualPp = calculator.Calculate(actual, attributes).Total;

            var projection = BanchosucksFullComboProjection.ApplyTo(actual, actualProcessor);

            Assert.That(projection.Replaced, Is.EqualTo(2));
            assertSameRun(actual, reference);

            double projectedPp = calculator.Calculate(actual, attributes).Total;
            double referencePp = calculator.Calculate(reference, attributes).Total;

            Assert.That(projectedPp, Is.EqualTo(referencePp).Within(1e-9));
            Assert.That(projectedPp, Is.GreaterThan(actualPp));
            Assert.That(actual.Accuracy, Is.LessThan(1), "the 100 and the dropped slider end are still in");
        }

        /// <summary>
        /// osu!mania (the Banchosucks pp formula comes in through the ruleset's calculator): a missed note and a hold note let go.
        /// </summary>
        [Test]
        public void TestManiaMatchesTheRunPlayedAsFullCombo()
        {
            var ruleset = new ManiaRuleset();
            var (working, playable) = createBeatmap(ruleset);

            var all = enumerate(playable.HitObjects).ToList();

            HitObject missed = all.First(h => h.Judgement.MaxResult == HitResult.Perfect);
            HitObject good = all.Last(h => h.Judgement.MaxResult == HitResult.Perfect);
            HitObject? brokenHold = all.FirstOrDefault(h => h.Judgement.MinResult == HitResult.ComboBreak);

            HitResult played(HitObject h)
            {
                if (h == missed || h == brokenHold) return h.Judgement.MinResult;
                if (h == good) return HitResult.Good;

                return h.Judgement.MaxResult;
            }

            HitResult fullCombo(HitObject h) => h == good ? HitResult.Good : h.Judgement.MaxResult;

            var (actual, actualProcessor) = play(ruleset, playable, all, played);
            var (reference, _) = play(ruleset, playable, all, fullCombo);

            var attributes = ruleset.CreateDifficultyCalculator(working).Calculate();
            var calculator = ruleset.CreatePerformanceCalculator()!;

            double actualPp = calculator.Calculate(actual, attributes).Total;

            var projection = BanchosucksFullComboProjection.ApplyTo(actual, actualProcessor);

            Assert.That(projection.Replaced, Is.EqualTo(brokenHold == null ? 1 : 2));
            Assert.That(actual.Statistics[HitResult.Perfect], Is.EqualTo(reference.Statistics[HitResult.Perfect]), "the miss became a Perfect");
            assertSameRun(actual, reference);

            double projectedPp = calculator.Calculate(actual, attributes).Total;
            double referencePp = calculator.Calculate(reference, attributes).Total;

            Assert.That(projectedPp, Is.EqualTo(referencePp).Within(1e-9));
            Assert.That(projectedPp, Is.GreaterThanOrEqualTo(actualPp));
        }

        private static void assertSameRun(ScoreInfo projected, ScoreInfo reference)
        {
            foreach (var result in HitResultExtensions.ALL_TYPES)
                Assert.That(projected.Statistics.GetValueOrDefault(result), Is.EqualTo(reference.Statistics.GetValueOrDefault(result)), result.ToString());

            Assert.That(projected.Accuracy, Is.EqualTo(reference.Accuracy).Within(1e-12));
            Assert.That(projected.MaxCombo, Is.EqualTo(reference.MaxCombo));
        }

        private static (WorkingBeatmap working, IBeatmap playable) createBeatmap(Ruleset ruleset)
        {
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            return (working, working.GetPlayableBeatmap(ruleset.RulesetInfo));
        }

        // nested objects first, the way the score processor walks a beatmap
        private static IEnumerable<HitObject> enumerate(IEnumerable<HitObject> hitObjects)
        {
            foreach (var hitObject in hitObjects)
            {
                foreach (var nested in enumerate(hitObject.NestedHitObjects))
                    yield return nested;

                yield return hitObject;
            }
        }

        private static (ScoreInfo score, ScoreProcessor processor) play(Ruleset ruleset, IBeatmap playable, IEnumerable<HitObject> hitObjects, Func<HitObject, HitResult> resultFor)
        {
            var processor = ruleset.CreateScoreProcessor();
            processor.ApplyBeatmap(playable);

            foreach (var hitObject in hitObjects)
                processor.ApplyResult(new JudgementResult(hitObject, hitObject.Judgement) { Type = resultFor(hitObject) });

            var score = new ScoreInfo(playable.BeatmapInfo, ruleset.RulesetInfo) { Mods = Array.Empty<Mod>() };
            processor.PopulateScore(score);

            return (score, processor);
        }
    }
}
