// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.Banchosucks;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksScoreOriginCacheTest
    {
        [Test]
        public void TestResponseParsing()
        {
            // the two answers of GET /api/plugins/banchosucks_stablescores/origin/{lazer_score_id}
            Assert.That(JsonConvert.DeserializeObject<BanchosucksScoreOriginResponse>(@"{""stable_score_id"": 4711}")!.StableScoreId, Is.EqualTo(4711));
            Assert.That(JsonConvert.DeserializeObject<BanchosucksScoreOriginResponse>(@"{""stable_score_id"": null}")!.StableScoreId, Is.Null);
            // stable score ids are BIGINT UNSIGNED on the server
            Assert.That(JsonConvert.DeserializeObject<BanchosucksScoreOriginResponse>(@"{""stable_score_id"": 5000000000}")!.StableScoreId, Is.EqualTo(5_000_000_000));
        }

        [Test]
        public void TestOnlyClassicScoresWithoutSettingsCanBeImports()
        {
            Assert.That(BanchosucksScoreOriginCache.CanBeStableImport(10, hasClassic: true, allModsDefault: true), Is.True);
            Assert.That(BanchosucksScoreOriginCache.CanBeStableImport(10, hasClassic: false, allModsDefault: true), Is.False);
            Assert.That(BanchosucksScoreOriginCache.CanBeStableImport(10, hasClassic: true, allModsDefault: false), Is.False);
            Assert.That(BanchosucksScoreOriginCache.CanBeStableImport(0, hasClassic: true, allModsDefault: true), Is.False, "a local score without an online id");
            Assert.That(BanchosucksScoreOriginCache.CanBeStableImport(-1, hasClassic: true, allModsDefault: true), Is.False);
        }

        [Test]
        public void TestAnswersAreKeptIncludingNegativeOnes()
        {
            var cache = new BanchosucksScoreOriginCache();

            Assert.That(cache.Lookup(1), Is.Null);
            Assert.That(cache.Request(1, 0), Is.True);
            Assert.That(cache.Request(2, 0), Is.True);

            Assert.That(cache.Next(0, true), Is.EqualTo(1));
            cache.Complete(1, 900);
            Assert.That(cache.Next(1000, true), Is.EqualTo(2));
            cache.Complete(2, null);

            Assert.That(cache.Lookup(1), Is.True);
            Assert.That(cache.StableScoreId(1), Is.EqualTo(900));
            Assert.That(cache.Lookup(2), Is.False);
            Assert.That(cache.StableScoreId(2), Is.Null);

            // known scores are never asked again
            Assert.That(cache.Request(1, 5000), Is.False);
            Assert.That(cache.Request(2, 5000), Is.False);
            Assert.That(cache.Next(6000, true), Is.Null);
        }

        [Test]
        public void TestAScoreIsOnlyQueuedOnce()
        {
            var cache = new BanchosucksScoreOriginCache();

            Assert.That(cache.Request(1, 0), Is.True);
            Assert.That(cache.Request(1, 0), Is.False);
            Assert.That(cache.Queued, Is.EqualTo(1));

            Assert.That(cache.Next(0, true), Is.EqualTo(1));
            Assert.That(cache.Request(1, 10), Is.False, "already on its way");
            Assert.That(cache.Request(0, 10), Is.False);
            Assert.That(cache.Request(-5, 10), Is.False);
        }

        [Test]
        public void TestPaceAndConcurrency()
        {
            var cache = new BanchosucksScoreOriginCache();

            for (long id = 1; id <= 5; id++)
                cache.Request(id, 0);

            Assert.That(cache.Next(0, true), Is.EqualTo(1));
            Assert.That(cache.Next(100, true), Is.Null, "less than the interval since the last start");
            Assert.That(cache.Next(BanchosucksScoreOriginCache.MIN_INTERVAL, true), Is.EqualTo(2));
            Assert.That(cache.InFlight, Is.EqualTo(BanchosucksScoreOriginCache.MAX_CONCURRENT));

            // two unanswered: nothing more starts, however long it takes
            Assert.That(cache.Next(10_000, true), Is.Null);

            cache.Complete(1, null);
            Assert.That(cache.Next(10_000, true), Is.EqualTo(3));
            Assert.That(cache.Next(10_001, true), Is.Null);

            // at most four starts per second
            Assert.That(1000 / BanchosucksScoreOriginCache.MIN_INTERVAL, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void TestNothingIsSentWhileNotAllowed()
        {
            var cache = new BanchosucksScoreOriginCache();

            cache.Request(1, 0);

            // gameplay or logged out
            Assert.That(cache.Next(0, false), Is.Null);
            Assert.That(cache.Next(60_000, false), Is.Null);
            Assert.That(cache.Queued, Is.EqualTo(1));

            Assert.That(cache.Next(60_001, true), Is.EqualTo(1));
        }

        [Test]
        public void TestWithdrawnScoresAreNotRequested()
        {
            var cache = new BanchosucksScoreOriginCache();

            cache.Request(1, 0);
            cache.Request(2, 0);
            cache.Request(3, 0);

            cache.Withdraw(1);
            cache.Withdraw(99);

            Assert.That(cache.Next(0, true), Is.EqualTo(2));
            Assert.That(cache.Next(1000, true), Is.EqualTo(3));
            Assert.That(cache.Next(2000, true), Is.Null);

            // a withdrawn score can be asked for again later
            Assert.That(cache.Request(1, 3000), Is.True);
        }

        [Test]
        public void TestAFailureIsNotAnAnswerAndIsRetriedOnce()
        {
            var cache = new BanchosucksScoreOriginCache();

            cache.Request(1, 0);
            Assert.That(cache.Next(0, true), Is.EqualTo(1));
            cache.Fail(1, 100);

            Assert.That(cache.Lookup(1), Is.Null, "a failed request says nothing about the score");
            Assert.That(cache.InFlight, Is.Zero);

            Assert.That(cache.Request(1, 1000), Is.False, "not again right away");
            Assert.That(cache.Request(1, 100 + BanchosucksScoreOriginCache.RETRY_AFTER), Is.True);
            Assert.That(cache.Next(100 + BanchosucksScoreOriginCache.RETRY_AFTER, true), Is.EqualTo(1));
            cache.Fail(1, 70_000);

            Assert.That(cache.Request(1, 10_000_000), Is.False, "given up after the second failure");
        }

        [Test]
        public void TestASuccessAfterAFailureCounts()
        {
            var cache = new BanchosucksScoreOriginCache();

            cache.Request(1, 0);
            cache.Next(0, true);
            cache.Fail(1, 0);

            cache.Request(1, BanchosucksScoreOriginCache.RETRY_AFTER);
            cache.Next(BanchosucksScoreOriginCache.RETRY_AFTER, true);
            cache.Complete(1, 55);

            Assert.That(cache.Lookup(1), Is.True);
        }

        [Test]
        public void TestAClockThatJumpsBackDoesNotBlockForever()
        {
            var cache = new BanchosucksScoreOriginCache();

            cache.Request(1, 0);
            cache.Request(2, 0);

            Assert.That(cache.Next(500_000, true), Is.EqualTo(1));
            cache.Complete(1, null);

            Assert.That(cache.Next(10, true), Is.EqualTo(2));
        }
    }
}
