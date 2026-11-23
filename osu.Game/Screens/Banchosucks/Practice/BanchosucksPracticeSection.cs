// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;

namespace osu.Game.Screens.Banchosucks.Practice
{
    /// <summary>
    /// Banchosucks: the part of a map a practice run loops over, with its playback speed.
    /// </summary>
    /// <remarks>
    /// Pure data and arithmetic, so it can be tested without a game. All times are map times in milliseconds.
    /// </remarks>
    public class BanchosucksPracticeSection
    {
        public const double MIN_RATE = 0.5;
        public const double MAX_RATE = 1.0;

        /// <summary>
        /// A shorter section could restart before the player has seen a single object.
        /// </summary>
        public const double MIN_LENGTH = 2000;

        /// <summary>
        /// How far before the fail point "retry from the miss" starts.
        /// </summary>
        public const double FAIL_LEAD_IN = 5000;

        public double StartTime { get; }

        public double EndTime { get; }

        /// <summary>
        /// Playback speed, 0.5 to 1.0.
        /// </summary>
        public double Rate { get; }

        /// <summary>
        /// Runs started on this section so far. Counted by the player, shown in its HUD line.
        /// </summary>
        public int Attempts { get; set; }

        private BanchosucksPracticeSection(double startTime, double endTime, double rate)
        {
            StartTime = startTime;
            EndTime = endTime;
            Rate = rate;
        }

        /// <summary>
        /// Builds a section that lies inside the map: both points are clamped to the map, the end stays behind the
        /// start by at least <see cref="MIN_LENGTH"/> (or the whole map, if that is shorter) and the rate stays in range.
        /// </summary>
        /// <param name="startTime">Requested start.</param>
        /// <param name="endTime">Requested end.</param>
        /// <param name="rate">Requested playback speed.</param>
        /// <param name="mapStart">Time of the first object.</param>
        /// <param name="mapEnd">End time of the last object.</param>
        public static BanchosucksPracticeSection Create(double startTime, double endTime, double rate, double mapStart, double mapEnd)
        {
            if (double.IsNaN(startTime) || double.IsNaN(endTime) || double.IsNaN(mapStart) || double.IsNaN(mapEnd))
                throw new ArgumentException("Practice section times must be numbers.");

            mapEnd = Math.Max(mapStart, mapEnd);

            double start = Math.Clamp(startTime, mapStart, mapEnd);
            double end = Math.Clamp(endTime, mapStart, mapEnd);

            double minLength = Math.Min(MIN_LENGTH, mapEnd - mapStart);

            if (end - start < minLength)
            {
                // keep the start where the player put it if the map is long enough behind it
                end = Math.Min(mapEnd, start + minLength);
                start = Math.Max(mapStart, end - minLength);
            }

            return new BanchosucksPracticeSection(start, end, ClampRate(rate));
        }

        /// <summary>
        /// The section for "retry from the miss": from <see cref="FAIL_LEAD_IN"/> before the fail point to the end of the map, at normal speed.
        /// </summary>
        public static BanchosucksPracticeSection FromFail(double failTime, double mapStart, double mapEnd)
            => Create(failTime - FAIL_LEAD_IN, mapEnd, MAX_RATE, mapStart, mapEnd);

        public static double ClampRate(double rate)
        {
            if (double.IsNaN(rate))
                return MAX_RATE;

            return Math.Round(Math.Clamp(rate, MIN_RATE, MAX_RATE), 2);
        }

        /// <summary>
        /// Whether the run starts with the map (normal lead-in) instead of jumping into it.
        /// </summary>
        public bool StartsAtMapStart(double firstObjectTime) => StartTime <= firstObjectTime;

        /// <summary>
        /// Whether the run goes on to the last object, so it ends when everything is judged rather than at a point in time.
        /// </summary>
        public bool RunsToMapEnd(double lastObjectEndTime) => EndTime >= lastObjectEndTime;

        /// <summary>
        /// A map time as mm:ss (minutes are not capped at 59; times before zero show as 00:00).
        /// </summary>
        public static string FormatTime(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || milliseconds < 0)
                milliseconds = 0;

            long totalSeconds = (long)Math.Floor(milliseconds / 1000);
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        /// <summary>
        /// The rate as a whole percentage, for display.
        /// </summary>
        public int RatePercent => (int)Math.Round(Rate * 100);
    }
}
