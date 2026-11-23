// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksRenderQueueTest
    {
        private sealed class Job
        {
            public readonly string Name;

            public Job(string name)
            {
                Name = name;
            }

            public override string ToString() => Name;
        }

        [Test]
        public void TestFirstRequestStartsRightAway()
        {
            var queue = new BanchosucksRenderQueue<Job>();
            var first = new Job("a");

            Assert.That(queue.Add(first, out int position), Is.EqualTo(BanchosucksRenderQueueResult.Started));
            Assert.That(position, Is.EqualTo(0));
            Assert.That(queue.Running, Is.SameAs(first));
            Assert.That(queue.WaitingCount, Is.EqualTo(0));
            Assert.That(queue.IsFull, Is.False);
        }

        [Test]
        public void TestLaterRequestsWaitInOrder()
        {
            var queue = new BanchosucksRenderQueue<Job>();
            var jobs = Enumerable.Range(0, 4).Select(i => new Job(i.ToString())).ToArray();

            queue.Add(jobs[0], out _);

            for (int i = 1; i < jobs.Length; i++)
            {
                Assert.That(queue.Add(jobs[i], out int position), Is.EqualTo(BanchosucksRenderQueueResult.Queued));
                Assert.That(position, Is.EqualTo(i), "the place in the queue is told to the player");
            }

            Assert.That(queue.Waiting, Is.EqualTo(jobs.Skip(1)));

            // finishing one hands out the next, in the order they were added
            Assert.That(queue.Complete(jobs[0]), Is.SameAs(jobs[1]));
            Assert.That(queue.Running, Is.SameAs(jobs[1]));
            Assert.That(queue.PositionOf(jobs[2]), Is.EqualTo(1));
            Assert.That(queue.PositionOf(jobs[3]), Is.EqualTo(2));

            Assert.That(queue.Complete(jobs[1]), Is.SameAs(jobs[2]));
            Assert.That(queue.Complete(jobs[2]), Is.SameAs(jobs[3]));
            Assert.That(queue.Complete(jobs[3]), Is.Null);
            Assert.That(queue.Running, Is.Null);
        }

        [Test]
        public void TestSixthWaitingRequestIsRefused()
        {
            var queue = new BanchosucksRenderQueue<Job>();

            queue.Add(new Job("running"), out _);

            for (int i = 0; i < BanchosucksRenderQueue<Job>.MAX_WAITING; i++)
            {
                Assert.That(queue.IsFull, Is.False);
                Assert.That(queue.Add(new Job($"waiting {i}"), out _), Is.EqualTo(BanchosucksRenderQueueResult.Queued));
            }

            Assert.That(queue.IsFull, Is.True);
            Assert.That(queue.Add(new Job("one too many"), out int position), Is.EqualTo(BanchosucksRenderQueueResult.Full));
            Assert.That(position, Is.EqualTo(-1));
            Assert.That(queue.WaitingCount, Is.EqualTo(5));
        }

        [Test]
        public void TestCancelledRequestLeavesAndOthersMoveUp()
        {
            var queue = new BanchosucksRenderQueue<Job>();
            var running = new Job("running");
            var a = new Job("a");
            var b = new Job("b");
            var c = new Job("c");

            queue.Add(running, out _);
            queue.Add(a, out _);
            queue.Add(b, out _);
            queue.Add(c, out _);

            Assert.That(queue.Remove(b), Is.True);
            Assert.That(queue.Remove(b), Is.False, "only once");
            Assert.That(queue.PositionOf(b), Is.EqualTo(-1));
            Assert.That(queue.PositionOf(a), Is.EqualTo(1));
            Assert.That(queue.PositionOf(c), Is.EqualTo(2));

            // the running render is cancelled through its own notification, not through the queue
            Assert.That(queue.Remove(running), Is.False);
            Assert.That(queue.Running, Is.SameAs(running));

            Assert.That(queue.Complete(running), Is.SameAs(a));
            Assert.That(queue.Complete(a), Is.SameAs(c));
        }

        [Test]
        public void TestFreedPlaceCanBeTakenAgain()
        {
            var queue = new BanchosucksRenderQueue<Job>();
            var running = new Job("running");
            queue.Add(running, out _);

            var waiting = Enumerable.Range(0, 5).Select(i => new Job(i.ToString())).ToArray();
            foreach (var job in waiting)
                queue.Add(job, out _);

            Assert.That(queue.IsFull, Is.True);

            queue.Remove(waiting[0]);
            Assert.That(queue.IsFull, Is.False);
            Assert.That(queue.Add(new Job("late"), out int position), Is.EqualTo(BanchosucksRenderQueueResult.Queued));
            Assert.That(position, Is.EqualTo(5));
        }

        [Test]
        public void TestCompletingSomethingElseChangesNothing()
        {
            var queue = new BanchosucksRenderQueue<Job>();
            var running = new Job("running");
            var waiting = new Job("waiting");

            queue.Add(running, out _);
            queue.Add(waiting, out _);

            Assert.That(queue.Complete(waiting), Is.Null, "a waiting request cannot complete");
            Assert.That(queue.Complete(new Job("stranger")), Is.Null);
            Assert.That(queue.Running, Is.SameAs(running));
            Assert.That(queue.WaitingCount, Is.EqualTo(1));
        }

        [Test]
        public void TestSameRequestIsNotAddedTwice()
        {
            var queue = new BanchosucksRenderQueue<Job>();
            var running = new Job("running");
            var waiting = new Job("waiting");

            queue.Add(running, out _);
            queue.Add(waiting, out _);

            Assert.That(queue.Add(running, out int runningPosition), Is.EqualTo(BanchosucksRenderQueueResult.AlreadyKnown));
            Assert.That(runningPosition, Is.EqualTo(0));
            Assert.That(queue.Add(waiting, out int waitingPosition), Is.EqualTo(BanchosucksRenderQueueResult.AlreadyKnown));
            Assert.That(waitingPosition, Is.EqualTo(1));
            Assert.That(queue.WaitingCount, Is.EqualTo(1));
        }
    }
}
