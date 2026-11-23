// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Online.Banchosucks;
using Play = osu.Game.Online.Banchosucks.BanchosucksTopPlays.Play;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksTopPlaysTest
    {
        [Test]
        public void TestOnlyMissingSetsAreDownloadedEachOnce()
        {
            var plays = new[]
            {
                new Play(10, "a"), // local
                new Play(20, "b"), // missing
                new Play(20, "c"), // another difficulty of the same set
                new Play(30, "d"), // downloading
                new Play(40, "e"), // missing
                new Play(0, "f"), // no set id from the server
                new Play(-1, null),
            };

            var plan = BanchosucksTopPlays.PlanDownloads(plays, id => id == 10, id => id == 30);

            Assert.That(plan.ToDownload, Is.EqualTo(new[] { 20, 40 }));
            Assert.That(plan.Sets, Is.EqualTo(4));
            Assert.That(plan.AlreadyLocal, Is.EqualTo(1));
            Assert.That(plan.AlreadyDownloading, Is.EqualTo(1));
        }

        [Test]
        public void TestNothingToDownload()
        {
            var plan = BanchosucksTopPlays.PlanDownloads(new[] { new Play(1, "a"), new Play(2, "b") }, _ => true, _ => false);

            Assert.That(plan.ToDownload, Is.Empty);
            Assert.That(plan.AlreadyLocal, Is.EqualTo(2));

            Assert.That(BanchosucksTopPlays.PlanDownloads(Enumerable.Empty<Play>(), _ => false, _ => false).ToDownload, Is.Empty);
        }

        [Test]
        public void TestALocalSetIsNotAskedWhetherItIsDownloading()
        {
            var asked = new List<int>();

            BanchosucksTopPlays.PlanDownloads(new[] { new Play(1, "a"), new Play(2, "b") }, id => id == 1, id =>
            {
                asked.Add(id);
                return false;
            });

            Assert.That(asked, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void TestPagesForAllTopPlays()
        {
            // the section starts with five loaded plays
            Assert.That(BanchosucksTopPlays.PagesForAll(5, 250), Is.EqualTo(new[] { (0, 50), (50, 50) }));
            Assert.That(BanchosucksTopPlays.PagesForAll(5, 100), Is.EqualTo(new[] { (0, 50), (50, 50) }));
            Assert.That(BanchosucksTopPlays.PagesForAll(5, 62), Is.EqualTo(new[] { (0, 50), (50, 12) }));
            Assert.That(BanchosucksTopPlays.PagesForAll(5, 30), Is.EqualTo(new[] { (0, 30) }));

            // everything is loaded already: no request
            Assert.That(BanchosucksTopPlays.PagesForAll(5, 5), Is.Empty);
            Assert.That(BanchosucksTopPlays.PagesForAll(100, 400), Is.Empty);
            Assert.That(BanchosucksTopPlays.PagesForAll(0, 0), Is.Empty);
            Assert.That(BanchosucksTopPlays.PagesForAll(0, -3), Is.Empty);
        }

        [Test]
        public void TestAtMostTwoDownloadsAtATime()
        {
            var slots = new BanchosucksDownloadSlots();

            Assert.That(slots.Enqueue(new[] { 1, 2, 3, 4 }), Is.EqualTo(4));

            Assert.That(slots.Next(), Is.EqualTo(1));
            Assert.That(slots.Next(), Is.EqualTo(2));
            Assert.That(slots.Next(), Is.Null);
            Assert.That(slots.Active, Is.EqualTo(BanchosucksDownloadSlots.MAX_CONCURRENT));
            Assert.That(slots.Waiting, Is.EqualTo(2));

            slots.Finished(1, true);
            Assert.That(slots.Next(), Is.EqualTo(3));
            Assert.That(slots.Next(), Is.Null);

            slots.Finished(2, true);
            slots.Finished(3, true);
            Assert.That(slots.Next(), Is.EqualTo(4));
            slots.Finished(4, true);

            Assert.That(slots.Next(), Is.Null);
            Assert.That(slots.Succeeded, Is.EqualTo(4));
            Assert.That(slots.Active, Is.Zero);
        }

        [Test]
        public void TestSetsAlreadyQueuedOrRunningAreSkipped()
        {
            var slots = new BanchosucksDownloadSlots();

            slots.Enqueue(new[] { 1, 2, 3 });
            slots.Next();

            // 1 is running, 2 and 3 wait, 0 and -1 are no ids, 4 appears twice
            Assert.That(slots.Enqueue(new[] { 1, 2, 3, 0, -1, 4, 4 }), Is.EqualTo(1));
            Assert.That(slots.Waiting, Is.EqualTo(3));
            Assert.That(slots.Contains(1) && slots.Contains(2) && slots.Contains(4), Is.True);
            Assert.That(slots.Contains(5), Is.False);
        }

        [Test]
        public void TestRepeatedFailuresDropTheRest()
        {
            var slots = new BanchosucksDownloadSlots();

            slots.Enqueue(Enumerable.Range(1, 10));

            for (int i = 0; i < BanchosucksDownloadSlots.MAX_CONSECUTIVE_FAILURES; i++)
            {
                int id = slots.Next()!.Value;
                slots.Finished(id, false);
            }

            Assert.That(slots.Next(), Is.Null);
            Assert.That(slots.Waiting, Is.Zero);
            Assert.That(slots.Failed, Is.EqualTo(3));
            Assert.That(slots.Dropped, Is.EqualTo(7));
            Assert.That(slots.Contains(9), Is.False);
        }

        [Test]
        public void TestASuccessResetsTheFailureCount()
        {
            var slots = new BanchosucksDownloadSlots();

            slots.Enqueue(Enumerable.Range(1, 10));

            foreach (bool success in new[] { false, false, true, false, false })
                slots.Finished(slots.Next()!.Value, success);

            Assert.That(slots.Dropped, Is.Zero);
            Assert.That(slots.Waiting, Is.EqualTo(5));
            Assert.That(slots.Next(), Is.Not.Null);
        }

        [Test]
        public void TestCountersStartOverWithANewBatch()
        {
            var slots = new BanchosucksDownloadSlots();

            slots.Enqueue(new[] { 1 });
            slots.Finished(slots.Next()!.Value, false);
            Assert.That(slots.Failed, Is.EqualTo(1));

            slots.Enqueue(new[] { 2 });
            Assert.That(slots.Failed, Is.Zero);
            Assert.That(slots.Succeeded, Is.Zero);

            // finishing something that is not running changes nothing
            slots.Finished(77, true);
            Assert.That(slots.Succeeded, Is.Zero);
        }
    }
}
