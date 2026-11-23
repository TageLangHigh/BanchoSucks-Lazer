// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: turns the activity log into the numbers the statistics screen and the daily goal show.
    /// Pure functions of the entries; no clock, no storage.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    /// <item>A play counts for the local day it ended on (<see cref="BanchosucksActivityEntry.Day"/>).</item>
    /// <item>Play time is the sum of active gameplay seconds, of passed and unfinished plays alike.</item>
    /// <item>"Played" counts every logged play, "passed" the ones finished without failing or quitting.</item>
    /// <item>The average accuracy of a day is the plain mean over its passed plays; the accuracy of a run that was
    /// abandoned says little, so those are left out. A day without a pass has no accuracy.</item>
    /// <item>A session is a run of plays in which each one starts at most <see cref="SESSION_GAP_SECONDS"/> after the
    /// previous one ended; its length runs from the first start to the last end. Estimated entries are not part of
    /// sessions, the failed attempts between them are unknown.</item>
    /// </list>
    /// </remarks>
    public static class BanchosucksActivityStats
    {
        public const double SESSION_GAP_SECONDS = 600;

        /// <summary>
        /// One calendar day of the log.
        /// </summary>
        /// <param name="Date">The local day.</param>
        /// <param name="Seconds">Active play time.</param>
        /// <param name="Played">Logged plays.</param>
        /// <param name="Passed">Passed plays.</param>
        /// <param name="Accuracy">Mean accuracy of the passed plays (0 to 1), null without a pass.</param>
        /// <param name="Estimated">Whether the day contains entries rebuilt from saved scores.</param>
        public readonly record struct Day(DateOnly Date, double Seconds, int Played, int Passed, double? Accuracy, bool Estimated);

        /// <summary>
        /// A best value together with the day it happened on.
        /// </summary>
        public readonly record struct Best(double Value, DateOnly Date);

        public class Totals
        {
            public double Seconds { get; init; }
            public int Played { get; init; }
            public int Passed { get; init; }

            /// <summary>Mean accuracy over all passed plays (0 to 1), null without a pass.</summary>
            public double? Accuracy { get; init; }

            /// <summary>Days with at least one logged play.</summary>
            public int ActiveDays { get; init; }

            /// <summary>The first day the log knows about.</summary>
            public DateOnly? FirstDay { get; init; }

            /// <summary>Longest session in seconds, dated by the day it ended.</summary>
            public Best? LongestSession { get; init; }

            /// <summary>Most plays on one day.</summary>
            public Best? MostMapsInDay { get; init; }

            /// <summary>Most active play time on one day, in seconds.</summary>
            public Best? MostTimeInDay { get; init; }

            /// <summary>Whether any entry was rebuilt from saved scores.</summary>
            public bool HasEstimated { get; init; }
        }

        /// <summary>
        /// The last <paramref name="count"/> days up to and including <paramref name="today"/>, oldest first, days without plays included.
        /// </summary>
        public static Day[] LastDays(IEnumerable<BanchosucksActivityEntry> entries, DateOnly today, int count = 30)
        {
            if (count < 1)
                return Array.Empty<Day>();

            DateOnly first = today.AddDays(-(count - 1));
            var byDay = entries.Where(e => e.Day >= first && e.Day <= today).GroupBy(e => e.Day).ToDictionary(g => g.Key, summarise);

            var days = new Day[count];

            for (int i = 0; i < count; i++)
            {
                DateOnly date = first.AddDays(i);
                days[i] = byDay.TryGetValue(date, out var day) ? day : new Day(date, 0, 0, 0, null, false);
            }

            return days;
        }

        /// <summary>
        /// Every day that has at least one play, oldest first.
        /// </summary>
        public static List<Day> ActiveDays(IEnumerable<BanchosucksActivityEntry> entries)
            => entries.GroupBy(e => e.Day).OrderBy(g => g.Key).Select(summarise).ToList();

        public static Totals Summarise(IEnumerable<BanchosucksActivityEntry> entries)
        {
            var all = entries as IReadOnlyCollection<BanchosucksActivityEntry> ?? entries.ToList();
            var days = ActiveDays(all);
            var passed = all.Where(e => e.Passed).ToList();

            Best? mostMaps = null;
            Best? mostTime = null;

            foreach (var day in days)
            {
                // the earlier day keeps a record that is only tied later
                if (mostMaps == null || day.Played > mostMaps.Value.Value)
                    mostMaps = new Best(day.Played, day.Date);
                if (day.Seconds > 0 && (mostTime == null || day.Seconds > mostTime.Value.Value))
                    mostTime = new Best(day.Seconds, day.Date);
            }

            return new Totals
            {
                Seconds = all.Sum(e => e.Seconds),
                Played = all.Count,
                Passed = passed.Count,
                Accuracy = passed.Count > 0 ? passed.Average(e => e.Accuracy) : null,
                ActiveDays = days.Count,
                FirstDay = days.Count > 0 ? days[0].Date : null,
                LongestSession = LongestSession(all),
                MostMapsInDay = mostMaps,
                MostTimeInDay = mostTime,
                HasEstimated = all.Any(e => e.Estimated),
            };
        }

        /// <summary>
        /// The longest session (see the rules on the class), null when no measured play exists.
        /// </summary>
        public static Best? LongestSession(IEnumerable<BanchosucksActivityEntry> entries)
        {
            Best? best = null;

            DateTimeOffset sessionStart = default;
            DateTimeOffset sessionEnd = default;
            bool open = false;

            foreach (var entry in entries.Where(e => !e.Estimated).OrderBy(e => e.Start))
            {
                if (open && (entry.Start - sessionEnd).TotalSeconds <= SESSION_GAP_SECONDS)
                {
                    if (entry.At > sessionEnd)
                        sessionEnd = entry.At;
                }
                else
                {
                    close();
                    sessionStart = entry.Start;
                    sessionEnd = entry.At;
                    open = true;
                }
            }

            close();
            return best;

            void close()
            {
                if (!open)
                    return;

                double length = (sessionEnd - sessionStart).TotalSeconds;
                if (length > 0 && (best == null || length > best.Value.Value))
                    best = new Best(length, DateOnly.FromDateTime(sessionEnd.DateTime));
            }
        }

        private static Day summarise(IGrouping<DateOnly, BanchosucksActivityEntry> group)
        {
            double seconds = 0;
            int played = 0;
            int passed = 0;
            double accuracy = 0;
            bool estimated = false;

            foreach (var entry in group)
            {
                seconds += entry.Seconds;
                played++;
                estimated |= entry.Estimated;

                if (entry.Passed)
                {
                    passed++;
                    accuracy += entry.Accuracy;
                }
            }

            return new Day(group.Key, seconds, played, passed, passed > 0 ? accuracy / passed : null, estimated);
        }
    }
}
