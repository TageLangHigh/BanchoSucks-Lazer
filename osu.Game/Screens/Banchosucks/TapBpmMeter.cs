// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: measures tapping speed like osu! tapping tests do, as the BPM of a 1/4 stream
    /// (taps per second * 15, so 12 taps per second are 180 BPM).
    /// </summary>
    public class TapBpmMeter
    {
        /// <summary>
        /// A stream needs this many taps before it can set a record, so two lucky fast taps do not count.
        /// </summary>
        public const int MIN_TAPS_FOR_RECORD = 10;

        private const int min_taps = 4;
        private const int max_taps = 20;
        private const double idle_ms = 1000;

        private readonly List<double> taps = new List<double>();

        /// <summary>
        /// Highest BPM of a stream with at least <see cref="MIN_TAPS_FOR_RECORD"/> taps.
        /// </summary>
        public double Best { get; set; }

        public void Tap(double time)
        {
            // a pause starts a new stream
            if (taps.Count > 0 && time - taps[^1] > idle_ms)
                taps.Clear();

            taps.Add(time);

            if (taps.Count > max_taps)
                taps.RemoveRange(0, taps.Count - max_taps);

            if (taps.Count >= MIN_TAPS_FOR_RECORD)
                Best = Math.Max(Best, tapsPerSecond() * 15);
        }

        /// <summary>
        /// Taps per second over the last taps, or 0 when there is no stream going on at <paramref name="time"/>.
        /// </summary>
        public double TapsPerSecond(double time)
        {
            if (taps.Count < min_taps || time - taps[^1] > idle_ms)
                return 0;

            return tapsPerSecond();
        }

        public double Bpm(double time) => TapsPerSecond(time) * 15;

        private double tapsPerSecond()
        {
            double span = taps[^1] - taps[0];
            return span > 0 ? (taps.Count - 1) * 1000 / span : 0;
        }
    }
}
