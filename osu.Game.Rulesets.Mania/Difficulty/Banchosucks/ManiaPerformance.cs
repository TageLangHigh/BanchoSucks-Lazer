// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Banchosucks
{
    /// <summary>
    /// Banchosucks: the server's osu!mania pp formula (bancho.py <c>app/maniadiff/performance.py</c>, version 1):
    /// <c>pp = 8.68 * max(SR - 0.15, 0.05)^2.2 * clamp(5 acc - 4, 0, 1)^2 * (1 + 0.1 min(1, notes / 1500)) * mods</c>,
    /// with the accuracy weighting 320 as the maximum. NF and EZ are multipliers, DT/NC/HT/DC act through the star rating
    /// at their rate, a handful of mods are neutral and everything else (key mods, random, invert, ...) gives no pp, like
    /// converts from other rulesets.
    /// </summary>
    public static class ManiaPerformance
    {
        public const int PERFORMANCE_VERSION = 1;

        public const double MULTIPLIER = 8.68;
        public const double STAR_OFFSET = 0.15;
        public const double STAR_EXPONENT = 2.2;
        public const double ACCURACY_EXPONENT = 2.0;
        public const double LENGTH_BONUS_NOTES = 1500;

        /// <summary>Mods a play may carry and still receive pp (same list as the server's lazer plugin).</summary>
        public static readonly IReadOnlyCollection<string> NEUTRAL_MODS = new HashSet<string>
        {
            "NF", "EZ", "HD", "FI", "FL", "HT", "DC", "DT", "NC", "MR", "CL", "SD", "PF", "HR", "MU", "AC", "CS",
        };

        public static bool ModsGivePerformance(IEnumerable<Mod> mods) => mods.All(m => NEUTRAL_MODS.Contains(m.Acronym));

        public static double ModMultiplier(IEnumerable<Mod> mods)
        {
            double multiplier = 1.0;

            foreach (var mod in mods)
            {
                if (mod is ModNoFail)
                    multiplier *= 0.75;
                else if (mod is ModEasy)
                    multiplier *= 0.5;
            }

            return multiplier;
        }

        /// <summary>The star-rating part with the length bonus, before accuracy and mods.</summary>
        public static double DifficultyValue(double starRating, double notes)
            => MULTIPLIER * Math.Pow(Math.Max(starRating - STAR_OFFSET, 0.05), STAR_EXPONENT) * (1 + 0.1 * Math.Min(1, notes / LENGTH_BONUS_NOTES));

        /// <summary>Nothing below 80 %, quadratic above it; exactly 1 at 100 %.</summary>
        public static double AccuracyTerm(double accuracy) => Math.Pow(Math.Clamp(5 * accuracy - 4, 0, 1), ACCURACY_EXPONENT);
    }
}
