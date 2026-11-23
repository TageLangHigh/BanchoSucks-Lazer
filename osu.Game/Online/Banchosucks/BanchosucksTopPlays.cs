// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: the arithmetic behind "download missing maps" and "top plays as a collection" in the profile overlay.
    /// Pure: what is local and what is downloading comes in as functions.
    /// </summary>
    public static class BanchosucksTopPlays
    {
        /// <summary>The most top plays a profile lists (osu! api: the best 100 count).</summary>
        public const int MAX_TOP_PLAYS = 100;

        /// <summary>Plays fetched per request when all of them are wanted (the size the profile itself pages with).</summary>
        public const int PAGE_SIZE = 50;

        /// <summary>
        /// One top play as far as these features care.
        /// </summary>
        /// <param name="BeatmapSetId">Online id of the beatmap set, 0 or less if the server did not send one.</param>
        /// <param name="MD5Hash">MD5 of the difficulty's .osu file, the way collections store beatmaps.</param>
        public readonly record struct Play(int BeatmapSetId, string? MD5Hash);

        /// <summary>
        /// What "download missing maps" would do with the given plays.
        /// </summary>
        /// <param name="ToDownload">Set ids to queue, in the order of the plays, each once.</param>
        /// <param name="Sets">Different sets among the plays.</param>
        /// <param name="AlreadyLocal">Sets that are in the library.</param>
        /// <param name="AlreadyDownloading">Sets that are downloading or waiting in the queue.</param>
        public readonly record struct DownloadPlan(IReadOnlyList<int> ToDownload, int Sets, int AlreadyLocal, int AlreadyDownloading);

        public static DownloadPlan PlanDownloads(IEnumerable<Play> plays, Func<int, bool> isLocal, Func<int, bool> isDownloading)
        {
            var seen = new HashSet<int>();
            var toDownload = new List<int>();
            int local = 0;
            int downloading = 0;

            foreach (var play in plays)
            {
                // several top plays are often on the same set
                if (play.BeatmapSetId <= 0 || !seen.Add(play.BeatmapSetId))
                    continue;

                if (isLocal(play.BeatmapSetId))
                    local++;
                else if (isDownloading(play.BeatmapSetId))
                    downloading++;
                else
                    toDownload.Add(play.BeatmapSetId);
            }

            return new DownloadPlan(toDownload, seen.Count, local, downloading);
        }

        /// <summary>
        /// The hashes to add to a collection that already holds <paramref name="existing"/>, in the order of the plays, each once.
        /// Difficulties that are not in the library are included: a collection stores hashes, so they appear once downloaded.
        /// </summary>
        /// <param name="plays">The top plays.</param>
        /// <param name="existing">Hashes already in the collection.</param>
        /// <param name="withoutHash">Plays the server sent without a hash; they cannot be stored.</param>
        public static IReadOnlyList<string> HashesToAdd(IEnumerable<Play> plays, IEnumerable<string> existing, out int withoutHash)
        {
            var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var toAdd = new List<string>();
            withoutHash = 0;

            foreach (var play in plays)
            {
                if (string.IsNullOrWhiteSpace(play.MD5Hash))
                {
                    withoutHash++;
                    continue;
                }

                string hash = play.MD5Hash.Trim().ToLowerInvariant();

                if (known.Add(hash))
                    toAdd.Add(hash);
            }

            return toAdd;
        }

        /// <summary>
        /// The collection's name in both languages. A collection made in one language is found again after the client language changed.
        /// </summary>
        public static (string english, string german) CollectionNames(string username) => ($"Top plays of {username}", $"Top-Plays von {username}");

        /// <summary>
        /// The offsets to request for every top play that is not loaded yet, in pages of <see cref="PAGE_SIZE"/>.
        /// </summary>
        /// <param name="loaded">Plays the profile section has loaded.</param>
        /// <param name="total">The player's number of top plays as the profile states it.</param>
        public static IReadOnlyList<(int offset, int limit)> PagesForAll(int loaded, int total)
        {
            int wanted = Math.Min(MAX_TOP_PLAYS, Math.Max(0, total));
            var pages = new List<(int, int)>();

            // whole pages from the start: simpler than stitching onto whatever the section happens to have loaded,
            // and two requests at most
            if (loaded >= wanted)
                return pages;

            for (int offset = 0; offset < wanted; offset += PAGE_SIZE)
                pages.Add((offset, Math.Min(PAGE_SIZE, wanted - offset)));

            return pages;
        }
    }

    /// <summary>
    /// Banchosucks: hands out queued beatmap set downloads so that at most <see cref="MAX_CONCURRENT"/> run at a time.
    /// Pure; <see cref="BanchosucksBeatmapDownloadQueue"/> does the downloading.
    /// </summary>
    public class BanchosucksDownloadSlots
    {
        public const int MAX_CONCURRENT = 2;

        /// <summary>After this many failures in a row the rest of the queue is dropped (rate limit, server away, offline).</summary>
        public const int MAX_CONSECUTIVE_FAILURES = 3;

        private readonly Queue<int> waiting = new Queue<int>();
        private readonly HashSet<int> queued = new HashSet<int>();
        private readonly HashSet<int> active = new HashSet<int>();

        private int consecutiveFailures;

        public int Waiting => waiting.Count;

        public int Active => active.Count;

        /// <summary>Downloads that finished since the queue was last empty.</summary>
        public int Succeeded { get; private set; }

        public int Failed { get; private set; }

        /// <summary>Sets dropped because too many downloads failed in a row.</summary>
        public int Dropped { get; private set; }

        public bool Contains(int setId) => queued.Contains(setId) || active.Contains(setId);

        /// <summary>
        /// Adds sets to the queue and returns how many were new.
        /// </summary>
        public int Enqueue(IEnumerable<int> setIds)
        {
            if (waiting.Count == 0 && active.Count == 0)
            {
                Succeeded = Failed = Dropped = 0;
                consecutiveFailures = 0;
            }

            int added = 0;

            foreach (int id in setIds)
            {
                if (id <= 0 || Contains(id))
                    continue;

                waiting.Enqueue(id);
                queued.Add(id);
                added++;
            }

            return added;
        }

        /// <summary>
        /// The next set to start, or null when two are running or nothing waits. The caller reports back with <see cref="Finished"/>.
        /// </summary>
        public int? Next()
        {
            if (active.Count >= MAX_CONCURRENT || waiting.Count == 0)
                return null;

            int id = waiting.Dequeue();
            queued.Remove(id);
            active.Add(id);
            return id;
        }

        public void Finished(int setId, bool success)
        {
            if (!active.Remove(setId))
                return;

            if (success)
            {
                Succeeded++;
                consecutiveFailures = 0;
                return;
            }

            Failed++;

            if (++consecutiveFailures < MAX_CONSECUTIVE_FAILURES)
                return;

            Dropped += waiting.Count;
            waiting.Clear();
            queued.Clear();
        }
    }
}
