// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: where the player stands on one day against their own daily goal.
    /// </summary>
    /// <param name="MinutesPlayed">Active play time of the day in minutes.</param>
    /// <param name="MapsPassed">Passed plays of the day.</param>
    /// <param name="GoalMinutes">Minutes to play per day, 0 = this part is off.</param>
    /// <param name="GoalMaps">Maps to pass per day, 0 = this part is off.</param>
    public readonly record struct BanchosucksDailyGoalProgress(double MinutesPlayed, int MapsPassed, int GoalMinutes, int GoalMaps)
    {
        /// <summary>
        /// Whether the player set a goal at all.
        /// </summary>
        public bool Enabled => GoalMinutes > 0 || GoalMaps > 0;

        public bool MinutesMet => GoalMinutes <= 0 || MinutesPlayed >= GoalMinutes;

        public bool MapsMet => GoalMaps <= 0 || MapsPassed >= GoalMaps;

        /// <summary>
        /// The goal is met when every part that is switched on is met (minutes AND maps when both are set).
        /// </summary>
        public bool Met => Enabled && MinutesMet && MapsMet;

        /// <summary>
        /// 0 to 1 for a progress bar: the part that is furthest behind decides, so the bar is only full when the goal is met.
        /// </summary>
        public double Fraction
        {
            get
            {
                if (!Enabled)
                    return 0;

                double fraction = 1;

                if (GoalMinutes > 0)
                    fraction = Math.Min(fraction, MinutesPlayed / GoalMinutes);
                if (GoalMaps > 0)
                    fraction = Math.Min(fraction, (double)MapsPassed / GoalMaps);

                return Math.Clamp(fraction, 0, 1);
            }
        }
    }

    /// <summary>
    /// Banchosucks: the bookkeeping of the daily goal. Pure functions; the caller supplies "today" from the PC's local time.
    /// </summary>
    public static class BanchosucksDailyGoal
    {
        /// <summary>
        /// Progress of <paramref name="day"/>: play time of every logged play that ended on that day, and the number of passed ones.
        /// </summary>
        public static BanchosucksDailyGoalProgress ProgressOn(IEnumerable<BanchosucksActivityEntry> entries, DateOnly day, int goalMinutes, int goalMaps)
        {
            double seconds = 0;
            int passed = 0;

            foreach (var entry in entries)
            {
                if (entry.Day != day)
                    continue;

                seconds += entry.Seconds;
                if (entry.Passed)
                    passed++;
            }

            return new BanchosucksDailyGoalProgress(seconds / 60, passed, Math.Max(0, goalMinutes), Math.Max(0, goalMaps));
        }

        /// <summary>
        /// Consecutive days on which the goal was met. The streak ends with today when today is already met, otherwise
        /// with yesterday: an unfinished today does not break it, a missed yesterday does.
        /// </summary>
        public static int Streak(IEnumerable<DateOnly> metDays, DateOnly today)
        {
            var met = metDays as ISet<DateOnly> ?? metDays.ToHashSet();

            DateOnly day = met.Contains(today) ? today : today.AddDays(-1);
            int streak = 0;

            while (met.Contains(day))
            {
                streak++;
                day = day.AddDays(-1);
            }

            return streak;
        }

        /// <summary>
        /// The longest run of consecutive met days in the log.
        /// </summary>
        public static int LongestStreak(IEnumerable<DateOnly> metDays)
        {
            int best = 0;
            int run = 0;
            DateOnly? previous = null;

            foreach (var day in metDays.Distinct().OrderBy(d => d))
            {
                run = previous != null && previous.Value.AddDays(1) == day ? run + 1 : 1;
                best = Math.Max(best, run);
                previous = day;
            }

            return best;
        }
    }
}
