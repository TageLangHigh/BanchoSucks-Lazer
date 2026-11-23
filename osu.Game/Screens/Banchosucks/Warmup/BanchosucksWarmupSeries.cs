// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Banchosucks.Warmup
{
    /// <summary>
    /// Banchosucks: the running warm-up series of a solo song select: which maps, and which one is being played.
    /// </summary>
    /// <remarks>
    /// Song select caches one instance for the screens it pushes, so the results screen can show "next warm-up map".
    /// The series only lives in memory and ends as soon as the player is back at song select by any other way.
    /// </remarks>
    public class BanchosucksWarmupSeries
    {
        /// <summary>
        /// Fired when the player asks for the next map on the results screen.
        /// </summary>
        public event Action? NextRequested;

        private IReadOnlyList<BeatmapInfo> maps = Array.Empty<BeatmapInfo>();

        public IReadOnlyList<BeatmapInfo> Maps => maps;

        /// <summary>
        /// Index of the map being played (0 for the first).
        /// </summary>
        public int Index { get; private set; }

        public bool Active { get; private set; }

        /// <summary>
        /// The level in stars the series was built for.
        /// </summary>
        public double Level { get; private set; }

        public BeatmapInfo? Current => Active ? maps[Index] : null;

        public bool HasNext => Active && Index + 1 < maps.Count;

        public void Start(IReadOnlyList<BeatmapInfo> maps, double level)
        {
            if (maps.Count == 0)
                throw new ArgumentException("A warm-up series needs at least one map.", nameof(maps));

            this.maps = maps;
            Level = level;
            Index = 0;
            Active = true;
        }

        /// <summary>
        /// Whether a results screen for the given difficulty belongs to the map of the series that is being played.
        /// </summary>
        public bool IsCurrent(BeatmapInfo? beatmap) => beatmap != null && Current?.ID == beatmap.ID;

        /// <summary>
        /// Asks whoever runs the series to go on with the next map. Does nothing after the last map.
        /// </summary>
        public void RequestNext()
        {
            if (HasNext)
                NextRequested?.Invoke();
        }

        /// <summary>
        /// Moves on to the next map and returns it, or null when the series is over.
        /// </summary>
        public BeatmapInfo? Advance()
        {
            if (!HasNext)
            {
                End();
                return null;
            }

            Index++;
            return maps[Index];
        }

        public void End()
        {
            Active = false;
            Index = 0;
            maps = Array.Empty<BeatmapInfo>();
        }
    }
}
