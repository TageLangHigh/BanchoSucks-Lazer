// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: translations from lazer's values to the ones tosu (and osu!stable before it) uses.
    /// </summary>
    public static class BanchosucksOverlayMapping
    {
        /// <summary>
        /// tosu's ranked status number: 0 unknown, 1 not submitted, 2 pending, 4 ranked, 5 approved, 6 qualified, 7 loved.
        /// </summary>
        public static int RankedStatus(BeatmapOnlineStatus status)
        {
            switch (status)
            {
                case BeatmapOnlineStatus.LocallyModified:
                    return 1;

                case BeatmapOnlineStatus.Graveyard:
                case BeatmapOnlineStatus.WIP:
                case BeatmapOnlineStatus.Pending:
                    return 2;

                case BeatmapOnlineStatus.Ranked:
                    return 4;

                case BeatmapOnlineStatus.Approved:
                    return 5;

                case BeatmapOnlineStatus.Qualified:
                    return 6;

                case BeatmapOnlineStatus.Loved:
                    return 7;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Fills the hit counts stable knows (300 / geki / 100 / katu / 50 / miss) from lazer's statistics.
        /// </summary>
        /// <remarks>
        /// osu! and taiko: 300 = Great, 100 = Ok, 50 = Meh. mania: geki = Perfect (the rainbow 300), 300 = Great,
        /// katu = Good (200), 100 = Ok, 50 = Meh. catch: 300 = fruits, 100 = drops (large ticks), 50 = droplets (small
        /// ticks), katu = missed droplets, as on stable's result screen.
        /// </remarks>
        public static void FillHits(BanchosucksOverlayState.HitsState hits, IReadOnlyDictionary<HitResult, int> statistics, int rulesetId)
        {
            hits.Great = statistics.GetValueOrDefault(HitResult.Great);
            hits.Miss = statistics.GetValueOrDefault(HitResult.Miss);
            hits.SliderEndHits = statistics.GetValueOrDefault(HitResult.SliderTailHit);
            hits.SmallTickHits = statistics.GetValueOrDefault(HitResult.SmallTickHit);
            hits.LargeTickHits = statistics.GetValueOrDefault(HitResult.LargeTickHit);

            switch (rulesetId)
            {
                case 2:
                    hits.Geki = 0;
                    hits.Ok = statistics.GetValueOrDefault(HitResult.LargeTickHit);
                    hits.Meh = statistics.GetValueOrDefault(HitResult.SmallTickHit);
                    hits.Katu = statistics.GetValueOrDefault(HitResult.SmallTickMiss);
                    hits.Miss += statistics.GetValueOrDefault(HitResult.LargeTickMiss);
                    break;

                case 3:
                    hits.Geki = statistics.GetValueOrDefault(HitResult.Perfect);
                    hits.Katu = statistics.GetValueOrDefault(HitResult.Good);
                    hits.Ok = statistics.GetValueOrDefault(HitResult.Ok);
                    hits.Meh = statistics.GetValueOrDefault(HitResult.Meh);
                    break;

                default:
                    hits.Geki = 0;
                    hits.Katu = 0;
                    hits.Ok = statistics.GetValueOrDefault(HitResult.Ok);
                    hits.Meh = statistics.GetValueOrDefault(HitResult.Meh);
                    break;
            }
        }
    }
}
