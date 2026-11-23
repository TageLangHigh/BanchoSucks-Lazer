// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: the bookkeeping behind the replay render queue of <see cref="BanchosucksReplayRenderer"/>.
    /// One item runs, up to <see cref="MAX_WAITING"/> more wait in the order they were added.
    /// </summary>
    /// <remarks>
    /// Pure and thread safe: requests come from the update thread, completions from the render task and
    /// cancellations from notifications.
    /// </remarks>
    public class BanchosucksRenderQueue<T>
        where T : class
    {
        /// <summary>
        /// How many requests may wait behind the running one.
        /// </summary>
        public const int MAX_WAITING = 5;

        private readonly object sync = new object();
        private readonly List<T> waiting = new List<T>();
        private T? running;

        /// <summary>
        /// The item being rendered right now, if any.
        /// </summary>
        public T? Running
        {
            get
            {
                lock (sync)
                    return running;
            }
        }

        public int WaitingCount
        {
            get
            {
                lock (sync)
                    return waiting.Count;
            }
        }

        /// <summary>
        /// Whether another request would be refused.
        /// </summary>
        public bool IsFull
        {
            get
            {
                lock (sync)
                    return running != null && waiting.Count >= MAX_WAITING;
            }
        }

        /// <summary>
        /// The waiting items in the order they will run.
        /// </summary>
        public IReadOnlyList<T> Waiting
        {
            get
            {
                lock (sync)
                    return waiting.ToArray();
            }
        }

        /// <summary>
        /// Adds a request.
        /// </summary>
        /// <param name="item">The request.</param>
        /// <param name="position">0 when it runs right away, otherwise its place in the queue (1 = next), -1 when refused.</param>
        public BanchosucksRenderQueueResult Add(T item, out int position)
        {
            lock (sync)
            {
                if (ReferenceEquals(running, item) || waiting.Contains(item))
                {
                    position = positionOf(item);
                    return BanchosucksRenderQueueResult.AlreadyKnown;
                }

                if (running == null)
                {
                    running = item;
                    position = 0;
                    return BanchosucksRenderQueueResult.Started;
                }

                if (waiting.Count >= MAX_WAITING)
                {
                    position = -1;
                    return BanchosucksRenderQueueResult.Full;
                }

                waiting.Add(item);
                position = waiting.Count;
                return BanchosucksRenderQueueResult.Queued;
            }
        }

        /// <summary>
        /// Marks the running item as finished (in whatever way) and hands out the next one, which is running from then on.
        /// </summary>
        /// <returns>The item to start now, or null when the queue is empty or <paramref name="finished"/> was not the running item.</returns>
        public T? Complete(T finished)
        {
            lock (sync)
            {
                if (!ReferenceEquals(running, finished))
                    return null;

                if (waiting.Count == 0)
                {
                    running = null;
                    return null;
                }

                running = waiting[0];
                waiting.RemoveAt(0);
                return running;
            }
        }

        /// <summary>
        /// Takes a waiting item out of the queue (the player cancelled it). The running item is not affected.
        /// </summary>
        /// <returns>Whether the item was waiting.</returns>
        public bool Remove(T item)
        {
            lock (sync)
                return waiting.Remove(item);
        }

        /// <summary>
        /// 0 for the running item, 1 for the next one and so on, -1 when the item is not known.
        /// </summary>
        public int PositionOf(T item)
        {
            lock (sync)
                return positionOf(item);
        }

        private int positionOf(T item)
        {
            if (ReferenceEquals(running, item))
                return 0;

            int index = waiting.IndexOf(item);
            return index < 0 ? -1 : index + 1;
        }
    }

    public enum BanchosucksRenderQueueResult
    {
        /// <summary>
        /// Nothing was running, the item runs now.
        /// </summary>
        Started,

        /// <summary>
        /// The item waits behind the running one.
        /// </summary>
        Queued,

        /// <summary>
        /// The queue is full, the item was refused.
        /// </summary>
        Full,

        /// <summary>
        /// The same item was added before.
        /// </summary>
        AlreadyKnown,
    }
}
