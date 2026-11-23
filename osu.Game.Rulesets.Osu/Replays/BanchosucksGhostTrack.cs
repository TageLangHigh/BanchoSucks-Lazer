// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays
{
    /// <summary>
    /// Banchosucks: where the cursor of a replay is at any time, for the ghost cursor of the replay comparison.
    /// Pure: frames in, positions out. Nothing here presses keys or touches the play.
    /// </summary>
    public class BanchosucksGhostTrack
    {
        private readonly double[] times;
        private readonly Vector2[] positions;
        private readonly bool[] pressed;

        /// <param name="frames">The frames of a replay. Frames of other rulesets are ignored; the order does not matter.</param>
        public BanchosucksGhostTrack(IEnumerable<ReplayFrame> frames)
        {
            // a stable sort keeps frames with the same time in the order they were recorded
            var sorted = frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToArray();

            times = sorted.Select(f => f.Time).ToArray();
            positions = sorted.Select(f => f.Position).ToArray();
            pressed = sorted.Select(f => f.Actions.Contains(OsuAction.LeftButton) || f.Actions.Contains(OsuAction.RightButton)).ToArray();
        }

        public int FrameCount => times.Length;

        public bool IsEmpty => times.Length == 0;

        /// <summary>
        /// Time of the first frame (0 without frames).
        /// </summary>
        public double StartTime => IsEmpty ? 0 : times[0];

        /// <summary>
        /// Time of the last frame (0 without frames).
        /// </summary>
        public double EndTime => IsEmpty ? 0 : times[^1];

        /// <summary>
        /// The index of the last frame at or before a time, -1 when the time is before the first frame.
        /// </summary>
        public int IndexAt(double time)
        {
            int low = 0;
            int high = times.Length - 1;
            int found = -1;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;

                if (times[mid] <= time)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                    high = mid - 1;
            }

            return found;
        }

        /// <summary>
        /// Where the cursor is at a time: on the straight line between the two frames around it, at the first frame
        /// before the replay starts and at the last one after it ends.
        /// </summary>
        public Vector2 PositionAt(double time)
        {
            if (IsEmpty)
                return Vector2.Zero;

            int index = IndexAt(time);

            if (index < 0)
                return positions[0];

            if (index >= times.Length - 1)
                return positions[^1];

            double span = times[index + 1] - times[index];

            if (span <= 0)
                return positions[index];

            float progress = (float)Math.Clamp((time - times[index]) / span, 0, 1);
            return Vector2.Lerp(positions[index], positions[index + 1], progress);
        }

        /// <summary>
        /// Whether a button is held at a time (as of the last frame at or before it).
        /// </summary>
        public bool PressedAt(double time)
        {
            int index = IndexAt(time);
            return index >= 0 && pressed[index];
        }

        /// <summary>
        /// The path the cursor took during the last <paramref name="length"/> milliseconds before a time, oldest point first.
        /// </summary>
        /// <param name="time">Where the path ends.</param>
        /// <param name="length">How far back it reaches, in milliseconds.</param>
        /// <param name="into">Receives the points (cleared first). Empty without frames or before the replay starts.</param>
        public void CopyTrail(double time, double length, List<Vector2> into)
        {
            into.Clear();

            if (IsEmpty || time < times[0])
                return;

            double from = Math.Max(times[0], time - length);
            double to = Math.Min(time, times[^1]);

            if (to < from)
            {
                // after the end of the replay the cursor rests
                into.Add(positions[^1]);
                return;
            }

            into.Add(PositionAt(from));

            for (int i = IndexAt(from) + 1; i < times.Length && times[i] < to; i++)
                into.Add(positions[i]);

            into.Add(PositionAt(to));
        }
    }
}
