// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Online.Chat;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: the limits on the lookups behind the beatmap previews in chat.
    /// </summary>
    [TestFixture]
    public class BanchosucksLookupThrottleTest
    {
        [Test]
        public void TestFirstRequestGoesOutAtOnce()
        {
            var throttle = new BanchosucksLookupThrottle<int>();

            Assert.That(throttle.TryTake(0, out _), Is.False, "nothing asked yet");
            Assert.That(throttle.Request(5, 1000), Is.True);
            Assert.That(throttle.TryTake(1000, out int key), Is.True);
            Assert.That(key, Is.EqualTo(5));
            Assert.That(throttle.InFlightCount, Is.EqualTo(1));
            Assert.That(throttle.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void TestOnlyAFewPerSecond()
        {
            var throttle = new BanchosucksLookupThrottle<int>(interval: 400, maxInFlight: 2);

            for (int i = 0; i < 20; i++)
                throttle.Request(i, 0);

            int sent = 0;

            // the server answers at once, so only the interval limits
            for (double time = 0; time < 1000; time += 10)
            {
                if (throttle.TryTake(time, out int key))
                {
                    sent++;
                    throttle.Succeed(key);
                }
            }

            Assert.That(sent, Is.EqualTo(3), "at 0, 400 and 800 ms");
        }

        [Test]
        public void TestNewestRequestFirst()
        {
            var throttle = new BanchosucksLookupThrottle<string>(interval: 0, maxInFlight: 10);

            throttle.Request("old", 0);
            throttle.Request("newer", 1);
            throttle.Request("newest", 2);

            var order = new List<string>();
            while (throttle.TryTake(10, out string key))
                order.Add(key);

            Assert.That(order, Is.EqualTo(new[] { "newest", "newer", "old" }));
        }

        [Test]
        public void TestAskingAgainMovesToTheFrontWithoutDoubling()
        {
            var throttle = new BanchosucksLookupThrottle<string>(interval: 0, maxInFlight: 10);

            throttle.Request("a", 0);
            throttle.Request("b", 1);
            throttle.Request("a", 2);

            Assert.That(throttle.PendingCount, Is.EqualTo(2));
            Assert.That(throttle.TryTake(3, out string first), Is.True);
            Assert.That(first, Is.EqualTo("a"));
        }

        [Test]
        public void TestLimitOnUnansweredRequests()
        {
            var throttle = new BanchosucksLookupThrottle<int>(interval: 0, maxInFlight: 2);

            for (int i = 0; i < 5; i++)
                throttle.Request(i, 0);

            Assert.That(throttle.TryTake(0, out int first), Is.True);
            Assert.That(throttle.TryTake(1, out _), Is.True);
            Assert.That(throttle.TryTake(2, out _), Is.False, "two are unanswered");

            throttle.Succeed(first);
            Assert.That(throttle.TryTake(3, out _), Is.True);
        }

        [Test]
        public void TestAnsweredKeyIsNeverAskedAgain()
        {
            var throttle = new BanchosucksLookupThrottle<int>(interval: 0);

            throttle.Request(1, 0);
            throttle.TryTake(0, out _);

            Assert.That(throttle.Request(1, 1), Is.True, "still running: the caller may wait for the answer");
            Assert.That(throttle.PendingCount, Is.EqualTo(0), "but it is not sent twice");

            throttle.Succeed(1);

            Assert.That(throttle.Request(1, 2), Is.False);
            Assert.That(throttle.TryTake(1_000_000, out _), Is.False);
        }

        [Test]
        public void TestFailedKeyIsLeftAloneForAWhile()
        {
            var throttle = new BanchosucksLookupThrottle<int>(interval: 0, retryAfter: 60_000);

            throttle.Request(1, 0);
            throttle.TryTake(0, out _);
            throttle.Fail(1, 100);

            Assert.That(throttle.InFlightCount, Is.EqualTo(0));
            Assert.That(throttle.HasFailed(1, 200), Is.True);
            Assert.That(throttle.Request(1, 200), Is.False);
            Assert.That(throttle.Request(1, 60_000), Is.False);
            Assert.That(throttle.TryTake(60_000, out _), Is.False);

            Assert.That(throttle.Request(1, 60_100), Is.True, "a later message with the same link may try again");
            Assert.That(throttle.TryTake(60_100, out int key), Is.True);
            Assert.That(key, Is.EqualTo(1));
        }

        [Test]
        public void TestCancelledRequestIsNotSent()
        {
            var throttle = new BanchosucksLookupThrottle<int>(interval: 0);

            throttle.Request(1, 0);
            throttle.Request(2, 0);

            Assert.That(throttle.Cancel(2), Is.True);
            Assert.That(throttle.Cancel(2), Is.False);

            Assert.That(throttle.TryTake(0, out int key), Is.True);
            Assert.That(key, Is.EqualTo(1));
            Assert.That(throttle.TryTake(1, out _), Is.False);

            // cancelled is not failed: it can be asked again right away
            Assert.That(throttle.Request(2, 2), Is.True);
        }

        [Test]
        public void TestKeyAnsweredByAnotherRequest()
        {
            var throttle = new BanchosucksLookupThrottle<string>(interval: 0, maxInFlight: 10);

            throttle.Request("beatmap 5", 0);
            throttle.Request("set 1", 0);

            Assert.That(throttle.TryTake(0, out string key), Is.True);
            Assert.That(key, Is.EqualTo("set 1"));

            // the answer for the set also contained beatmap 5
            throttle.Succeed("set 1");
            throttle.MarkKnown("beatmap 5");

            Assert.That(throttle.TryTake(1, out _), Is.False);
            Assert.That(throttle.Request("beatmap 5", 2), Is.False);
        }

        [Test]
        public void TestForgottenKeyCanBeAskedAgain()
        {
            var throttle = new BanchosucksLookupThrottle<int>(interval: 0);

            throttle.Request(1, 0);
            throttle.TryTake(0, out _);
            throttle.Succeed(1);
            throttle.Forget(1);

            Assert.That(throttle.Request(1, 1), Is.True);
            Assert.That(throttle.TryTake(1, out _), Is.True);
        }
    }
}
