// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Warmup;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksWarmupSelectorTest
    {
        private static readonly DateTimeOffset now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.FromHours(1));

        private static BanchosucksWarmupScore score(double stars, double accuracy = 0.97, bool passed = true, int daysAgo = 3, Guid? beatmap = null)
            => new BanchosucksWarmupScore(beatmap ?? Guid.NewGuid(), stars, accuracy, passed, now.AddDays(-daysAgo));

        private static BanchosucksWarmupCandidate map(double stars, double seconds = 120, DateTimeOffset? lastPlayed = null, Guid? set = null)
            => new BanchosucksWarmupCandidate(Guid.NewGuid(), set ?? Guid.NewGuid(), stars, seconds * 1000, lastPlayed);

        /// <summary>
        /// Twelve scores from 4.0 to 5.1 stars: the 75th percentile is 4.825.
        /// </summary>
        private static List<BanchosucksWarmupScore> scoresAroundFive() => Enumerable.Range(0, 12).Select(i => score(4.0 + i * 0.1)).ToList();

        [Test]
        public void TestLevelIsThe75thPercentile()
        {
            double? level = BanchosucksWarmupSelector.EstimateLevel(scoresAroundFive(), now, out int used);

            Assert.That(used, Is.EqualTo(12));
            Assert.That(level, Is.EqualTo(4.825).Within(1e-9));
        }

        [Test]
        public void TestTooFewScoresGiveNoLevel()
        {
            var scores = Enumerable.Range(0, BanchosucksWarmupSelector.MIN_SCORES - 1).Select(i => score(5)).ToList();

            Assert.That(BanchosucksWarmupSelector.EstimateLevel(scores, now, out int used), Is.Null);
            Assert.That(used, Is.EqualTo(BanchosucksWarmupSelector.MIN_SCORES - 1));

            var plan = BanchosucksWarmupSelector.Select(scores, new[] { map(3.5), map(4), map(4.5) }, now, new Random(1));

            Assert.That(plan.Failure, Is.EqualTo(BanchosucksWarmupFailure.TooFewScores));
            Assert.That(plan.Picks, Is.Empty);
            Assert.That(plan.Level, Is.Null);
        }

        [Test]
        public void TestOnlyRecentPassedAccurateScoresCount()
        {
            var scores = scoresAroundFive();

            // none of these may move the level: failed, sloppy, too old, from the future, without a star rating
            scores.Add(score(9, passed: false));
            scores.Add(score(9, accuracy: 0.85));
            scores.Add(score(9, daysAgo: BanchosucksWarmupSelector.RECENT_DAYS + 1));
            scores.Add(score(9, daysAgo: -2));
            scores.Add(score(-1));
            scores.Add(score(double.NaN));

            double? level = BanchosucksWarmupSelector.EstimateLevel(scores, now, out int used);

            Assert.That(used, Is.EqualTo(12));
            Assert.That(level, Is.EqualTo(4.825).Within(1e-9));
        }

        [Test]
        public void TestOneDifficultyCountsOnce()
        {
            var scores = scoresAroundFive();
            var farmed = Guid.NewGuid();

            // thirty plays of one hard map are one value, taken at the hardest way it was passed
            for (int i = 0; i < 30; i++)
                scores.Add(score(7 + i * 0.01, beatmap: farmed));

            double? level = BanchosucksWarmupSelector.EstimateLevel(scores, now, out int used);

            Assert.That(used, Is.EqualTo(13));
            Assert.That(level, Is.LessThan(5.2));
        }

        [Test]
        public void TestPicksAimAtSeventyEightyNinetyPercent()
        {
            var scores = scoresAroundFive();
            const double level = 4.825;

            var candidates = new List<BanchosucksWarmupCandidate>();

            for (double stars = 1; stars < 8; stars += 0.05)
                candidates.Add(map(stars));

            var plan = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(7));

            Assert.That(plan.Success);
            Assert.That(plan.Level, Is.EqualTo(level).Within(1e-9));
            Assert.That(plan.Picks, Has.Count.EqualTo(3));

            for (int i = 0; i < 3; i++)
            {
                double target = level * BanchosucksWarmupSelector.TARGETS[i];
                Assert.That(plan.Picks[i].Stars, Is.EqualTo(target).Within(level * BanchosucksWarmupSelector.TOLERANCE + 1e-9));
                Assert.That(plan.Picks[i].Stars, Is.LessThan(level), "a warm-up map is never at or above the level");
            }

            Assert.That(plan.Picks.Select(p => p.SetId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TestSameSeedSamePicksWhateverTheInputOrder()
        {
            var scores = scoresAroundFive();
            var candidates = new List<BanchosucksWarmupCandidate>();

            for (double stars = 3; stars < 5; stars += 0.02)
                candidates.Add(map(stars));

            var first = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(42));
            var second = BanchosucksWarmupSelector.Select(scores, Enumerable.Reverse(candidates), now, new Random(42));
            var other = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(43));

            Assert.That(first.Success);
            Assert.That(second.Picks.Select(p => p.BeatmapId), Is.EqualTo(first.Picks.Select(p => p.BeatmapId)));
            Assert.That(other.Picks.Select(p => p.BeatmapId), Is.Not.EqualTo(first.Picks.Select(p => p.BeatmapId)), "another seed draws other maps from this many candidates");
        }

        [Test]
        public void TestLengthAndPlayedTodayAreRespected()
        {
            var scores = scoresAroundFive();
            const double level = 4.825;

            var good = new[] { map(level * 0.7), map(level * 0.8), map(level * 0.9) };

            var candidates = new List<BanchosucksWarmupCandidate>(good);

            // fitting stars, but none of these may be drawn
            foreach (double share in BanchosucksWarmupSelector.TARGETS)
            {
                candidates.Add(map(level * share, seconds: 59));
                candidates.Add(map(level * share, seconds: 241));
                candidates.Add(map(level * share, lastPlayed: now.AddHours(-2)));
                // played today by the player's clock, although it is still yesterday in UTC
                candidates.Add(map(level * share, lastPlayed: new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.Zero)));
            }

            for (int seed = 0; seed < 20; seed++)
            {
                var plan = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(seed));

                Assert.That(plan.Success);
                Assert.That(plan.Picks.Select(p => p.BeatmapId), Is.EqualTo(good.Select(g => g.BeatmapId)));
            }

            // played yesterday is fine
            Assert.That(BanchosucksWarmupSelector.IsEligible(map(3, lastPlayed: now.AddDays(-1)), now));
            Assert.That(BanchosucksWarmupSelector.IsEligible(map(3, seconds: 60), now));
            Assert.That(BanchosucksWarmupSelector.IsEligible(map(3, seconds: 240), now));
        }

        [Test]
        public void TestMissingBandIsReportedNotGuessed()
        {
            var scores = scoresAroundFive();
            const double level = 4.825;

            // nothing around 80 % of the level
            var candidates = new[] { map(level * 0.7), map(level * 0.9), map(1), map(7) };

            var plan = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(3));

            Assert.That(plan.Failure, Is.EqualTo(BanchosucksWarmupFailure.TooFewCandidates));
            Assert.That(plan.Picks, Is.Empty);
            Assert.That(plan.Level, Is.EqualTo(level).Within(1e-9));
            Assert.That(plan.MissingStars, Is.EqualTo(level * 0.8).Within(1e-9));
        }

        [Test]
        public void TestThreeMapsOfOneSetAreNotEnough()
        {
            var scores = scoresAroundFive();
            const double level = 4.825;
            var set = Guid.NewGuid();

            var candidates = new[] { map(level * 0.7, set: set), map(level * 0.8, set: set), map(level * 0.9, set: set) };

            var plan = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(3));

            Assert.That(plan.Failure, Is.EqualTo(BanchosucksWarmupFailure.TooFewCandidates));
            Assert.That(plan.MissingStars, Is.Null);
        }

        [Test]
        public void TestGoesBackWhenAnEarlierPickBlocksASet()
        {
            var scores = scoresAroundFive();
            const double level = 4.825;

            var setA = Guid.NewGuid();
            var setB = Guid.NewGuid();
            var setC = Guid.NewGuid();

            // the 70 % band can take A or B, the 80 % band only A: the first band has to settle for B
            var candidates = new[]
            {
                map(level * 0.7, set: setA),
                map(level * 0.7, set: setB),
                map(level * 0.8, set: setA),
                map(level * 0.9, set: setC),
            };

            for (int seed = 0; seed < 20; seed++)
            {
                var plan = BanchosucksWarmupSelector.Select(scores, candidates, now, new Random(seed));

                Assert.That(plan.Success);
                Assert.That(plan.Picks.Select(p => p.SetId), Is.EqualTo(new[] { setB, setA, setC }));
            }
        }
    }
}
