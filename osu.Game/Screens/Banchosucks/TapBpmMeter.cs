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
    /// <remarks>
    /// Taps that would push the stream above <see cref="MaxBpm"/> are refused (<see cref="Tap"/> returns false) and
    /// leave the stream untouched, so an autoclicker set faster than 500 BPM earns nothing at all. The cap is judged
    /// on the window of recent taps rather than on a single gap, because tap times are quantised to frames
    /// (a real 450 BPM stream arrives as 17/33 ms gaps at 60 fps).
    /// </remarks>
    public class TapBpmMeter
    {
        /// <summary>
        /// A stream needs this many taps before it can set a record, so two lucky fast taps do not count.
        /// </summary>
        public const int MIN_TAPS_FOR_RECORD = 10;

        /// <summary>
        /// A pause longer than this ends the stream. The input lock in the clicker uses the same gap.
        /// </summary>
        public const double IDLE_MS = 1000;

        private const int min_taps = 4;
        private const int max_taps = 20;

        private readonly List<double> taps = new List<double>();

        /// <summary>
        /// Streams faster than this are refused; 0 disables the cap.
        /// </summary>
        public double MaxBpm { get; set; } = 500;

        /// <summary>
        /// Highest BPM of a stream with at least <see cref="MIN_TAPS_FOR_RECORD"/> taps.
        /// </summary>
        public double Best { get; set; }

        /// <summary>
        /// Taps refused by the cap since the meter was created.
        /// </summary>
        public int Refused { get; private set; }

        /// <summary>
        /// Registers a tap. Returns false when the tap was refused because the stream exceeds <see cref="MaxBpm"/>.
        /// A refused tap still enters the window, so a stream that stays too fast keeps being refused instead of
        /// being thinned out to the cap; it can never set a record.
        /// </summary>
        public bool Tap(double time)
        {
            // a pause starts a new stream
            if (taps.Count > 0 && time - taps[^1] > IDLE_MS)
                taps.Clear();

            bool refused = false;

            if (MaxBpm > 0 && taps.Count >= min_taps)
            {
                double span = time - taps[Math.Max(taps.Count - max_taps + 1, 0)];
                int count = Math.Min(taps.Count, max_taps - 1) + 1;
                double bpm = span > 0 ? (count - 1) * 1000 / span * 15 : double.PositiveInfinity;
                refused = bpm > MaxBpm;
            }

            taps.Add(time);

            if (taps.Count > max_taps)
                taps.RemoveRange(0, taps.Count - max_taps);

            if (refused)
            {
                Refused++;
                return false;
            }

            if (taps.Count >= MIN_TAPS_FOR_RECORD)
                Best = Math.Max(Best, Math.Min(tapsPerSecond() * 15, MaxBpm > 0 ? MaxBpm : double.MaxValue));

            return true;
        }

        /// <summary>
        /// Taps per second over the last taps, or 0 when there is no stream going on at <paramref name="time"/>.
        /// </summary>
        public double TapsPerSecond(double time)
        {
            if (taps.Count < min_taps || time - taps[^1] > IDLE_MS)
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
