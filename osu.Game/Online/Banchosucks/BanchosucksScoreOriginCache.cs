// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: remembers for the session which lazer scores were imported from the stable server, and decides when the
    /// next lookup may be sent. Pure: time comes in as a number (milliseconds), nothing is requested here.
    /// </summary>
    /// <remarks>
    /// The plugin <c>banchosucks_stablescores</c> answers one score per request (<c>GET /origin/{lazer_score_id}</c>, one
    /// database query), so the lookups are kept few and slow: only scores somebody asked for (visible rows), every answer is
    /// kept (also "not imported"), at most <see cref="MAX_CONCURRENT"/> requests at a time, at least
    /// <see cref="MIN_INTERVAL"/> between two starts, nothing during gameplay. A failed lookup is tried once more after
    /// <see cref="RETRY_AFTER"/> and then left alone.
    /// </remarks>
    public class BanchosucksScoreOriginCache
    {
        public const int MAX_CONCURRENT = 2;

        /// <summary>Milliseconds between two request starts (four per second at most).</summary>
        public const double MIN_INTERVAL = 250;

        /// <summary>Milliseconds until a failed lookup may be asked again.</summary>
        public const double RETRY_AFTER = 60_000;

        public const int MAX_ATTEMPTS = 2;

        /// <summary>Answers kept; a session does not get near this, it only bounds the memory.</summary>
        public const int MAX_ENTRIES = 20_000;

        // null = made in lazer
        private readonly Dictionary<long, long?> known = new Dictionary<long, long?>();
        private readonly LinkedList<long> queue = new LinkedList<long>();
        private readonly HashSet<long> queued = new HashSet<long>();
        private readonly HashSet<long> inFlight = new HashSet<long>();
        private readonly Dictionary<long, (int attempts, double lastFailure)> failures = new Dictionary<long, (int, double)>();

        private double? lastStart;

        public int InFlight => inFlight.Count;

        public int Queued => queue.Count;

        public int Known => known.Count;

        /// <summary>
        /// Every imported stable score carries the Classic mod and no mod settings (the plugin's <c>_lazer_mods</c>), so a score
        /// without Classic or with a changed setting was made in lazer and needs no lookup.
        /// </summary>
        public static bool CanBeStableImport(long onlineId, bool hasClassic, bool allModsDefault) => onlineId > 0 && hasClassic && allModsDefault;

        /// <summary>
        /// What is known about a score: true = imported from stable, false = made in lazer, null = not asked yet.
        /// </summary>
        public bool? Lookup(long scoreId)
        {
            if (known.TryGetValue(scoreId, out long? stableId))
                return stableId != null;

            return null;
        }

        /// <summary>The stable score id behind an imported score, if known.</summary>
        public long? StableScoreId(long scoreId) => known.GetValueOrDefault(scoreId);

        /// <summary>
        /// Asks for a score to be looked up. Returns whether it is now waiting (false: already known, already waiting, on its way, or given up).
        /// </summary>
        public bool Request(long scoreId, double now)
        {
            if (scoreId <= 0 || known.ContainsKey(scoreId) || queued.Contains(scoreId) || inFlight.Contains(scoreId))
                return false;

            if (failures.TryGetValue(scoreId, out var failure) && (failure.attempts >= MAX_ATTEMPTS || now - failure.lastFailure < RETRY_AFTER))
                return false;

            queue.AddLast(scoreId);
            queued.Add(scoreId);
            return true;
        }

        /// <summary>
        /// Takes a waiting score out again (its row is gone). A request already sent is not affected.
        /// </summary>
        public void Withdraw(long scoreId)
        {
            if (queued.Remove(scoreId))
                queue.Remove(scoreId);
        }

        /// <summary>
        /// The score to request now, or null when nothing waits or it is not time yet. The caller has to send the request and
        /// report back with <see cref="Complete"/> or <see cref="Fail"/>.
        /// </summary>
        /// <param name="now">Current time in milliseconds.</param>
        /// <param name="allowed">False while requests must not be sent (gameplay, logged out).</param>
        public long? Next(double now, bool allowed)
        {
            if (!allowed || queue.Count == 0 || inFlight.Count >= MAX_CONCURRENT)
                return null;

            if (lastStart != null && now - lastStart.Value < MIN_INTERVAL && now >= lastStart.Value)
                return null;

            long scoreId = queue.First!.Value;
            queue.RemoveFirst();
            queued.Remove(scoreId);
            inFlight.Add(scoreId);
            lastStart = now;
            return scoreId;
        }

        /// <summary>
        /// The server answered: <paramref name="stableScoreId"/> is the stable score, or null for a play made in lazer.
        /// </summary>
        public void Complete(long scoreId, long? stableScoreId)
        {
            inFlight.Remove(scoreId);
            failures.Remove(scoreId);
            store(scoreId, stableScoreId);
        }

        /// <summary>
        /// The request failed. Nothing is recorded as an answer; the score may be asked once more later.
        /// </summary>
        public void Fail(long scoreId, double now)
        {
            inFlight.Remove(scoreId);

            int attempts = failures.TryGetValue(scoreId, out var failure) ? failure.attempts : 0;
            failures[scoreId] = (attempts + 1, now);
        }

        private void store(long scoreId, long? stableScoreId)
        {
            if (known.Count >= MAX_ENTRIES && !known.ContainsKey(scoreId))
                known.Clear();

            known[scoreId] = stableScoreId;
        }
    }
}
