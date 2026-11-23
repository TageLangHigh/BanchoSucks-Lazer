// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Banchosucks: finds duplicate and broken beatmap sets for "clean up library" (Settings > Maintenance > Beatmaps).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class only decides; it reads no database and deletes nothing. The decisions are deliberately cautious, because
    /// what it proposes for removal is the player's data:
    /// </para>
    /// <list type="bullet">
    /// <item>A difficulty is unplayable when its .osu file is not in the file store, or when it names an audio file that is
    /// not in the file store. A difficulty that names no audio file at all is left alone.</item>
    /// <item>A set is broken when it has no difficulty, or when every difficulty is unplayable. A set where only some
    /// difficulties are unplayable is "partly damaged": it is reported and never proposed for removal.</item>
    /// <item>Duplicates are looked for among the intact sets only: sets with the same online id (above 0), or with exactly the
    /// same difficulties (the same set of .osu hashes). Of a group of duplicates every copy with a local score is kept; if no
    /// copy has one, the most recently imported copy is kept. A set with a local score is therefore never proposed as a duplicate.</item>
    /// </list>
    /// </remarks>
    public static class BanchosucksLibraryCleanup
    {
        /// <summary>
        /// What the scan knows about one difficulty.
        /// </summary>
        /// <param name="Hash">The hash of the .osu file.</param>
        /// <param name="BeatmapFileMissing">Whether the .osu file is missing from the set's files or from the file store.</param>
        /// <param name="AudioMissing">Whether the difficulty names an audio file that is missing from the set's files or from the file store.</param>
        public readonly record struct Difficulty(string Hash, bool BeatmapFileMissing, bool AudioMissing)
        {
            public bool Playable => !BeatmapFileMissing && !AudioMissing;
        }

        /// <summary>
        /// What the scan knows about one beatmap set.
        /// </summary>
        /// <param name="ID">The local id of the set.</param>
        /// <param name="Title">For display.</param>
        /// <param name="OnlineID">The online id, 0 or below when the set has none.</param>
        /// <param name="DateAdded">When the set was imported.</param>
        /// <param name="LocalScoreCount">The number of local scores (of anybody, not deleted) on its difficulties.</param>
        /// <param name="Difficulties">Its difficulties.</param>
        public sealed record Set(Guid ID, string Title, int OnlineID, DateTimeOffset DateAdded, int LocalScoreCount, IReadOnlyList<Difficulty> Difficulties);

        [Flags]
        public enum BrokenReasons
        {
            None = 0,

            /// <summary>The set has no difficulty at all.</summary>
            NoDifficulties = 1,

            /// <summary>The .osu file of at least one difficulty is missing.</summary>
            BeatmapFileMissing = 2,

            /// <summary>The audio file of at least one difficulty is missing.</summary>
            AudioMissing = 4,
        }

        public sealed record BrokenSet(Set Set, BrokenReasons Reasons);

        /// <summary>
        /// Several copies of the same beatmap set.
        /// </summary>
        /// <param name="Keep">The copies that stay: every copy with a local score, or the most recent one.</param>
        /// <param name="Remove">The copies proposed for removal. None of them has a local score.</param>
        public sealed record DuplicateGroup(IReadOnlyList<Set> Keep, IReadOnlyList<Set> Remove);

        public sealed class Result
        {
            /// <summary>
            /// The number of sets that were looked at.
            /// </summary>
            public int ScannedSets { get; init; }

            public IReadOnlyList<DuplicateGroup> DuplicateGroups { get; init; } = Array.Empty<DuplicateGroup>();

            /// <summary>
            /// Sets that cannot be played at all.
            /// </summary>
            public IReadOnlyList<BrokenSet> BrokenSets { get; init; } = Array.Empty<BrokenSet>();

            /// <summary>
            /// Sets where some, but not all, difficulties are unplayable. Reported only.
            /// </summary>
            public IReadOnlyList<BrokenSet> PartlyDamagedSets { get; init; } = Array.Empty<BrokenSet>();

            /// <summary>
            /// The duplicate copies proposed for removal.
            /// </summary>
            public IReadOnlyList<Set> DuplicatesToRemove => DuplicateGroups.SelectMany(g => g.Remove).ToList();

            /// <summary>
            /// How many of the broken sets carry local scores (they become invisible with the set, but stay in the database).
            /// </summary>
            public int BrokenSetsWithScores => BrokenSets.Count(b => b.Set.LocalScoreCount > 0);

            public bool AnythingToRemove => BrokenSets.Count > 0 || DuplicateGroups.Any(g => g.Remove.Count > 0);
        }

        public static Result Analyse(IEnumerable<Set> sets)
        {
            var all = sets.ToList();

            var broken = new List<BrokenSet>();
            var partlyDamaged = new List<BrokenSet>();
            var intact = new List<Set>();

            foreach (var set in all)
            {
                var reasons = BrokenReasons.None;

                if (set.Difficulties.Count == 0)
                    reasons |= BrokenReasons.NoDifficulties;
                if (set.Difficulties.Any(d => d.BeatmapFileMissing))
                    reasons |= BrokenReasons.BeatmapFileMissing;
                if (set.Difficulties.Any(d => d.AudioMissing))
                    reasons |= BrokenReasons.AudioMissing;

                if (reasons == BrokenReasons.None)
                    intact.Add(set);
                else if (set.Difficulties.Any(d => d.Playable))
                    partlyDamaged.Add(new BrokenSet(set, reasons));
                else
                    broken.Add(new BrokenSet(set, reasons));
            }

            return new Result
            {
                ScannedSets = all.Count,
                BrokenSets = broken,
                PartlyDamagedSets = partlyDamaged,
                DuplicateGroups = findDuplicates(intact),
            };
        }

        private static List<DuplicateGroup> findDuplicates(List<Set> sets)
        {
            // two sets are copies of each other when they share an online id or have exactly the same difficulties.
            // a set can be linked to one copy by the id and to another by its difficulties, so the links are merged (union-find).
            int[] parent = Enumerable.Range(0, sets.Count).ToArray();

            int find(int i)
            {
                while (parent[i] != i)
                    i = parent[i] = parent[parent[i]];

                return i;
            }

            void link<TKey>(Dictionary<TKey, int> seen, TKey key, int index)
                where TKey : notnull
            {
                if (seen.TryGetValue(key, out int other))
                    parent[find(index)] = find(other);
                else
                    seen[key] = index;
            }

            var byOnlineId = new Dictionary<int, int>();
            var byDifficulties = new Dictionary<string, int>();

            for (int i = 0; i < sets.Count; i++)
            {
                if (sets[i].OnlineID > 0)
                    link(byOnlineId, sets[i].OnlineID, i);

                string? difficulties = difficultyKey(sets[i]);
                if (difficulties != null)
                    link(byDifficulties, difficulties, i);
            }

            var groups = new List<DuplicateGroup>();

            foreach (var copies in Enumerable.Range(0, sets.Count).GroupBy(find).Where(g => g.Count() > 1))
            {
                // newest first; the id makes the order the same on every run when two copies were imported in the same instant.
                var ordered = copies.Select(i => sets[i]).OrderByDescending(s => s.DateAdded).ThenBy(s => s.ID).ToList();

                var keep = ordered.Where(s => s.LocalScoreCount > 0).ToList();
                if (keep.Count == 0)
                    keep.Add(ordered[0]);

                groups.Add(new DuplicateGroup(keep, ordered.Except(keep).ToList()));
            }

            return groups;
        }

        private static string? difficultyKey(Set set)
        {
            var hashes = set.Difficulties.Select(d => d.Hash).Where(h => !string.IsNullOrEmpty(h)).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();

            // a difficulty without a hash cannot be compared; such a set is never a duplicate by its difficulties.
            if (hashes.Count == 0 || hashes.Count != set.Difficulties.Select(d => d.Hash).Distinct().Count())
                return null;

            return string.Join('|', hashes);
        }
    }
}
