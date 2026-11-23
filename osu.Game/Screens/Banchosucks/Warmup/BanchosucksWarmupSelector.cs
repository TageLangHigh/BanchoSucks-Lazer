// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Banchosucks.Warmup
{
    /// <summary>
    /// Banchosucks: one of the player's own local scores, as far as the warm-up needs it.
    /// </summary>
    /// <param name="BeatmapId">The difficulty the score was set on.</param>
    /// <param name="Stars">Star rating of the difficulty as it was played (with the mods of the score).</param>
    /// <param name="Accuracy">0 to 1.</param>
    /// <param name="Passed">Whether the play was finished without failing.</param>
    /// <param name="Date">When the score was set.</param>
    public readonly record struct BanchosucksWarmupScore(Guid BeatmapId, double Stars, double Accuracy, bool Passed, DateTimeOffset Date);

    /// <summary>
    /// Banchosucks: a difficulty of the local library that could become a warm-up map.
    /// </summary>
    /// <param name="BeatmapId">The difficulty.</param>
    /// <param name="SetId">The beatmap set it belongs to.</param>
    /// <param name="Stars">Stored star rating (without mods).</param>
    /// <param name="LengthMs">Length in milliseconds.</param>
    /// <param name="LastPlayed">When it was last played, if ever.</param>
    public readonly record struct BanchosucksWarmupCandidate(Guid BeatmapId, Guid SetId, double Stars, double LengthMs, DateTimeOffset? LastPlayed);

    public enum BanchosucksWarmupFailure
    {
        None,

        /// <summary>
        /// Too few recent, passed, accurate scores to say what the player's level is.
        /// </summary>
        TooFewScores,

        /// <summary>
        /// The level is known, but the library has no three fitting maps from different sets.
        /// </summary>
        TooFewCandidates,
    }

    /// <summary>
    /// Banchosucks: what <see cref="BanchosucksWarmupSelector.Select"/> decided.
    /// </summary>
    public class BanchosucksWarmupPlan
    {
        public BanchosucksWarmupFailure Failure { get; init; }

        /// <summary>
        /// The estimated level in stars, or null when there were too few scores.
        /// </summary>
        public double? Level { get; init; }

        /// <summary>
        /// Number of different difficulties with a score that counted for the level.
        /// </summary>
        public int ScoresUsed { get; init; }

        /// <summary>
        /// The maps to play, easiest target first. Empty unless <see cref="Failure"/> is <see cref="BanchosucksWarmupFailure.None"/>.
        /// </summary>
        public IReadOnlyList<BanchosucksWarmupCandidate> Picks { get; init; } = Array.Empty<BanchosucksWarmupCandidate>();

        /// <summary>
        /// For <see cref="BanchosucksWarmupFailure.TooFewCandidates"/>: the star rating around which nothing fitting was found.
        /// </summary>
        public double? MissingStars { get; init; }

        public bool Success => Failure == BanchosucksWarmupFailure.None;
    }

    /// <summary>
    /// Banchosucks: picks the three maps of the warm-up button. Pure arithmetic on data read from the local database;
    /// nothing is asked from the server and nothing is sent anywhere.
    /// </summary>
    /// <remarks>
    /// The level is the 75th percentile of the star ratings of what the player passed with decent accuracy in the last
    /// weeks, one value per difficulty. The percentile keeps single lucky passes on far too hard maps from setting the
    /// level, while still sitting in the upper part of what the player really plays. The three maps aim at 70 %, 80 %
    /// and 90 % of that level. If the data is too thin the plan says so instead of guessing.
    /// </remarks>
    public static class BanchosucksWarmupSelector
    {
        /// <summary>
        /// Scores older than this many days do not count for the level.
        /// </summary>
        public const int RECENT_DAYS = 42;

        /// <summary>
        /// Scores below this accuracy do not count for the level.
        /// </summary>
        public const double MIN_ACCURACY = 0.9;

        /// <summary>
        /// Different difficulties with a counting score that are needed before a level is named.
        /// </summary>
        public const int MIN_SCORES = 10;

        public const double LEVEL_PERCENTILE = 0.75;

        /// <summary>
        /// Shares of the level the three maps aim at, in playing order.
        /// </summary>
        public static readonly IReadOnlyList<double> TARGETS = new[] { 0.7, 0.8, 0.9 };

        /// <summary>
        /// A map fits a target when its star rating is within this share of the level around it,
        /// so the three bands (65 to 75 %, 75 to 85 %, 85 to 95 %) touch but never reach the level itself.
        /// </summary>
        public const double TOLERANCE = 0.05;

        public const double MIN_LENGTH_MS = 60_000;
        public const double MAX_LENGTH_MS = 240_000;

        /// <summary>
        /// The player's level in stars, or null when fewer than <see cref="MIN_SCORES"/> difficulties count.
        /// </summary>
        /// <param name="scores">The player's local scores in one ruleset.</param>
        /// <param name="now">The current time.</param>
        /// <param name="used">Number of difficulties that counted.</param>
        public static double? EstimateLevel(IEnumerable<BanchosucksWarmupScore> scores, DateTimeOffset now, out int used)
        {
            DateTimeOffset cutoff = now.AddDays(-RECENT_DAYS);

            // one value per difficulty (the hardest way it was passed), so replaying one map does not shift the level
            double[] stars = scores.Where(s => s.Passed && s.Accuracy >= MIN_ACCURACY && s.Date >= cutoff && s.Date <= now && isUsable(s.Stars))
                                   .GroupBy(s => s.BeatmapId)
                                   .Select(g => g.Max(s => s.Stars))
                                   .OrderBy(s => s)
                                   .ToArray();

            used = stars.Length;

            if (stars.Length < MIN_SCORES)
                return null;

            return percentile(stars, LEVEL_PERCENTILE);
        }

        /// <summary>
        /// Whether a difficulty may be offered at all: 1 to 4 minutes long, a known star rating and not played today.
        /// </summary>
        public static bool IsEligible(BanchosucksWarmupCandidate candidate, DateTimeOffset now)
        {
            if (!isUsable(candidate.Stars))
                return false;

            if (double.IsNaN(candidate.LengthMs) || candidate.LengthMs < MIN_LENGTH_MS || candidate.LengthMs > MAX_LENGTH_MS)
                return false;

            // "today" is the player's calendar day, read in the offset of the given current time
            if (candidate.LastPlayed is DateTimeOffset lastPlayed && lastPlayed.ToOffset(now.Offset).Date == now.Date)
                return false;

            return true;
        }

        /// <summary>
        /// Estimates the level and picks one map per target, from different sets, at random among the fitting ones.
        /// </summary>
        /// <param name="scores">The player's local scores in one ruleset.</param>
        /// <param name="candidates">The difficulties of the local library in that ruleset.</param>
        /// <param name="now">The current time.</param>
        /// <param name="random">Source of randomness; a seeded one gives a repeatable result.</param>
        public static BanchosucksWarmupPlan Select(IEnumerable<BanchosucksWarmupScore> scores, IEnumerable<BanchosucksWarmupCandidate> candidates, DateTimeOffset now, Random random)
        {
            double? level = EstimateLevel(scores, now, out int used);

            if (level == null)
                return new BanchosucksWarmupPlan { Failure = BanchosucksWarmupFailure.TooFewScores, ScoresUsed = used };

            double tolerance = level.Value * TOLERANCE;

            // sorted first so the result depends only on the data and the random source, not on the order the database returned
            var eligible = candidates.Where(c => IsEligible(c, now)).OrderBy(c => c.BeatmapId).ToList();

            var bands = new List<List<BanchosucksWarmupCandidate>>();

            foreach (double share in TARGETS)
            {
                double target = level.Value * share;
                var band = eligible.Where(c => Math.Abs(c.Stars - target) <= tolerance).ToList();

                if (band.Count == 0)
                    return new BanchosucksWarmupPlan { Failure = BanchosucksWarmupFailure.TooFewCandidates, Level = level, ScoresUsed = used, MissingStars = target };

                shuffle(band, random);
                bands.Add(band);
            }

            var picks = new List<BanchosucksWarmupCandidate>();

            if (!pick(bands, 0, picks))
            {
                // every band has maps, but not three from different sets
                return new BanchosucksWarmupPlan { Failure = BanchosucksWarmupFailure.TooFewCandidates, Level = level, ScoresUsed = used };
            }

            return new BanchosucksWarmupPlan { Level = level, ScoresUsed = used, Picks = picks };
        }

        /// <summary>
        /// Takes the first map of each (already shuffled) band that does not repeat a set, going back a band when one runs dry.
        /// </summary>
        private static bool pick(List<List<BanchosucksWarmupCandidate>> bands, int index, List<BanchosucksWarmupCandidate> picks)
        {
            if (index == bands.Count)
                return true;

            foreach (var candidate in bands[index])
            {
                if (picks.Any(p => p.SetId == candidate.SetId || p.BeatmapId == candidate.BeatmapId))
                    continue;

                picks.Add(candidate);

                if (pick(bands, index + 1, picks))
                    return true;

                picks.RemoveAt(picks.Count - 1);
            }

            return false;
        }

        private static void shuffle<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Percentile with linear interpolation between the two neighbouring values of a sorted list.
        /// </summary>
        private static double percentile(double[] sorted, double share)
        {
            double position = share * (sorted.Length - 1);
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(sorted.Length - 1, lower + 1);

            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        private static bool isUsable(double stars) => !double.IsNaN(stars) && !double.IsInfinity(stars) && stars > 0;
    }
}
