// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Online.Chat
{
    /// <summary>
    /// Banchosucks: decides which lookup may be sent to the server next, for the beatmap previews in chat.
    /// Every key is asked at most once (until it is forgotten again after a failure), at most a few requests go out
    /// per second and only a couple run at the same time. The newest request goes first, because that is the
    /// message the player is looking at.
    /// </summary>
    /// <remarks>
    /// Pure bookkeeping without clocks or network: the caller passes the time and reports what happened.
    /// Not thread safe; used from the update thread only.
    /// </remarks>
    public class BanchosucksLookupThrottle<TKey>
        where TKey : notnull
    {
        /// <summary>
        /// Minimum time between two requests in milliseconds.
        /// </summary>
        public readonly double Interval;

        /// <summary>
        /// How many requests may be unanswered at the same time.
        /// </summary>
        public readonly int MaxInFlight;

        /// <summary>
        /// How long a failed key is left alone before it may be asked again, in milliseconds.
        /// </summary>
        public readonly double RetryAfter;

        private readonly List<TKey> pending = new List<TKey>();
        private readonly HashSet<TKey> inFlight = new HashSet<TKey>();
        private readonly HashSet<TKey> done = new HashSet<TKey>();
        private readonly Dictionary<TKey, double> failedAt = new Dictionary<TKey, double>();

        private double? lastTaken;

        public BanchosucksLookupThrottle(double interval = 400, int maxInFlight = 2, double retryAfter = 120_000)
        {
            Interval = interval;
            MaxInFlight = maxInFlight;
            RetryAfter = retryAfter;
        }

        public int PendingCount => pending.Count;

        public int InFlightCount => inFlight.Count;

        /// <summary>
        /// Asks for a key to be looked up.
        /// </summary>
        /// <returns>False when nothing will happen for this call: the key is answered already, or failed recently.</returns>
        public bool Request(TKey key, double now)
        {
            if (done.Contains(key))
                return false;

            if (failedAt.TryGetValue(key, out double failed))
            {
                if (now - failed < RetryAfter)
                    return false;

                failedAt.Remove(key);
            }

            if (inFlight.Contains(key))
                return true;

            // asked again: move to the front, the newest request is served first
            pending.Remove(key);
            pending.Add(key);
            return true;
        }

        /// <summary>
        /// Takes a key out of the waiting list (nobody wants its answer any more). Requests already sent are not affected.
        /// </summary>
        public bool Cancel(TKey key) => pending.Remove(key);

        /// <summary>
        /// Hands out the key to send now, if the limits allow one.
        /// </summary>
        public bool TryTake(double now, out TKey key)
        {
            key = default!;

            if (pending.Count == 0 || inFlight.Count >= MaxInFlight)
                return false;

            if (lastTaken is double last && now - last < Interval && now >= last)
                return false;

            key = pending[^1];
            pending.RemoveAt(pending.Count - 1);
            inFlight.Add(key);
            lastTaken = now;
            return true;
        }

        /// <summary>
        /// The request for a key was answered; the key is never asked again.
        /// </summary>
        public void Succeed(TKey key)
        {
            inFlight.Remove(key);
            done.Add(key);
        }

        /// <summary>
        /// Marks a key as answered without a request (the answer came with another key's request).
        /// </summary>
        public void MarkKnown(TKey key)
        {
            pending.Remove(key);
            done.Add(key);
        }

        /// <summary>
        /// The request for a key failed; it may be asked again after <see cref="RetryAfter"/>.
        /// </summary>
        public void Fail(TKey key, double now)
        {
            inFlight.Remove(key);
            failedAt[key] = now;
        }

        /// <summary>
        /// Whether a key failed recently and is left alone for now.
        /// </summary>
        public bool HasFailed(TKey key, double now) => failedAt.TryGetValue(key, out double failed) && now - failed < RetryAfter;

        /// <summary>
        /// Forgets an answered key, so that it would be asked again (its cached answer was dropped).
        /// </summary>
        public void Forget(TKey key) => done.Remove(key);
    }
}
