// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Stats;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The bookkeeping behind the daily goal and the personal statistics: the log file, day bucketing, aggregation, streaks.
    /// </summary>
    [TestFixture]
    public class BanchosucksActivityLogTest
    {
        private static readonly TimeSpan cet = TimeSpan.FromHours(1);

        private static BanchosucksActivityEntry play(int year, int month, int day, int hour, int minute, double seconds, bool passed = true, double accuracy = 0.95, string ruleset = "osu",
                                                     bool estimated = false, TimeSpan? offset = null)
            => new BanchosucksActivityEntry
            {
                At = new DateTimeOffset(year, month, day, hour, minute, 0, offset ?? cet),
                Seconds = seconds,
                Ruleset = ruleset,
                Passed = passed,
                Accuracy = accuracy,
                Estimated = estimated,
            };

        // ------------------------------------------------------------------ file

        [Test]
        public void TestRoundTrip()
        {
            var file = new BanchosucksActivityFile { Created = new DateTimeOffset(2026, 11, 20, 18, 0, 0, cet), Backfilled = true };
            file.Add(play(2026, 11, 23, 18, 4, 95.3, true, 0.9812, "mania"));
            file.Add(play(2026, 11, 23, 18, 10, 42, false, 0.7));
            file.MarkGoalDay(new DateOnly(2026, 11, 23));

            string json = file.Serialise();
            Assert.That(json, Does.Contain("\"version\":1"));
            Assert.That(json, Does.Contain("\"at\":\"2026-11-23T18:04:00+01:00\""), "local time with its UTC offset");
            Assert.That(json, Does.Contain("\"goal_days\":[\"2026-11-23\"]"));
            Assert.That(json, Does.Not.Contain("estimated"), "the flag is only written when set");

            var read = BanchosucksActivityFile.Deserialise(json)!;
            Assert.That(read.Version, Is.EqualTo(BanchosucksActivityFile.CURRENT_VERSION));
            Assert.That(read.Created, Is.EqualTo(file.Created));
            Assert.That(read.Backfilled, Is.True);
            Assert.That(read.Entries, Has.Count.EqualTo(2));
            Assert.That(read.Entries[0].Ruleset, Is.EqualTo("mania"));
            Assert.That(read.Entries[0].Seconds, Is.EqualTo(95.3));
            Assert.That(read.Entries[0].Passed, Is.True);
            Assert.That(read.Entries[0].Accuracy, Is.EqualTo(0.9812));
            Assert.That(read.Entries[0].At.Offset, Is.EqualTo(cet));
            Assert.That(read.Entries[1].Passed, Is.False);
            Assert.That(read.GoalDays, Is.EqualTo(new[] { new DateOnly(2026, 11, 23) }));
            Assert.That(read.ReadOnly, Is.False);
        }

        [Test]
        public void TestFieldsOfALaterVersionSurvive()
        {
            const string json = "{\"version\":1,\"created\":\"2026-11-20T18:00:00+01:00\",\"future_setting\":{\"a\":1},"
                                + "\"entries\":[{\"at\":\"2026-11-23T18:04:00+01:00\",\"seconds\":95.3,\"ruleset\":\"osu\",\"passed\":true,\"accuracy\":0.98,\"stars\":5.21}],\"goal_days\":[]}";

            var file = BanchosucksActivityFile.Deserialise(json)!;
            string written = file.Serialise();

            Assert.That(written, Does.Contain("\"future_setting\":{\"a\":1}"));
            Assert.That(written, Does.Contain("\"stars\":5.21"));
        }

        [Test]
        public void TestNewerVersionIsReadOnly()
        {
            var file = BanchosucksActivityFile.Deserialise("{\"version\":2,\"entries\":[{\"at\":\"2026-11-23T18:04:00+01:00\",\"seconds\":60,\"passed\":true,\"accuracy\":1}]}")!;

            Assert.That(file.ReadOnly, Is.True);
            Assert.That(file.Entries, Has.Count.EqualTo(1), "still shown");
        }

        [Test]
        public void TestDamagedContent()
        {
            Assert.That(BanchosucksActivityFile.Deserialise(""), Is.Null);
            Assert.That(BanchosucksActivityFile.Deserialise("{\"version\":1,\"entries\":[{\"at\":\"2026-11-2"), Is.Null, "cut off in the middle");
            Assert.That(BanchosucksActivityFile.Deserialise("[1,2,3]"), Is.Null);
            Assert.That(BanchosucksActivityFile.Deserialise("{\"version\":0}"), Is.Null);

            // nulls, entries without a time, nonsense numbers and wrong order are repaired rather than fatal
            var file = BanchosucksActivityFile.Deserialise("{\"version\":1,\"entries\":[null,{\"seconds\":5},"
                                                           + "{\"at\":\"2026-11-23T19:00:00+01:00\",\"seconds\":-4,\"accuracy\":7,\"ruleset\":null},"
                                                           + "{\"at\":\"2026-11-23T18:00:00+01:00\",\"seconds\":10,\"accuracy\":0.5}],\"goal_days\":null}")!;

            Assert.That(file.Entries, Has.Count.EqualTo(2));
            Assert.That(file.Entries[0].At.Hour, Is.EqualTo(18), "sorted by time");
            Assert.That(file.Entries[1].Seconds, Is.EqualTo(0));
            Assert.That(file.Entries[1].Accuracy, Is.EqualTo(1));
            Assert.That(file.Entries[1].Ruleset, Is.EqualTo(string.Empty));
            Assert.That(file.GoalDays, Is.Empty);
        }

        [Test]
        public void TestShouldLog()
        {
            Assert.That(BanchosucksActivityFile.ShouldLog(0, true), Is.True, "a pass always counts");
            Assert.That(BanchosucksActivityFile.ShouldLog(9.9, false), Is.False, "a retry after a few seconds does not");
            Assert.That(BanchosucksActivityFile.ShouldLog(10, false), Is.True);
        }

        [Test]
        public void TestAddKeepsOrder()
        {
            var file = new BanchosucksActivityFile();
            file.Add(play(2026, 11, 23, 18, 0, 60));
            file.Add(play(2026, 11, 21, 12, 0, 60));
            file.Add(play(2026, 11, 22, 12, 0, 60));
            file.Add(play(2026, 11, 24, 12, 0, 60));

            Assert.That(file.Entries.Select(e => e.At.Day), Is.EqualTo(new[] { 21, 22, 23, 24 }));
        }

        [Test]
        public void TestPrune()
        {
            var today = new DateOnly(2026, 11, 23);
            var file = new BanchosucksActivityFile();

            DateOnly oldestKept = today.AddDays(-(BanchosucksActivityFile.KEEP_DAYS - 1));
            DateOnly tooOld = oldestKept.AddDays(-1);

            file.Add(play(tooOld.Year, tooOld.Month, tooOld.Day, 23, 59, 60));
            file.Add(play(oldestKept.Year, oldestKept.Month, oldestKept.Day, 0, 0, 60));
            file.Add(play(2026, 11, 23, 12, 0, 60));
            file.MarkGoalDay(tooOld);
            file.MarkGoalDay(oldestKept);

            Assert.That(file.Prune(today), Is.True);
            Assert.That(file.Entries.Select(e => e.Day), Is.EqualTo(new[] { oldestKept, today }));
            Assert.That(file.GoalDays, Is.EqualTo(new[] { oldestKept }));
            Assert.That(file.Prune(today), Is.False, "nothing left to remove");
        }

        [Test]
        public void TestPruneCapsTheEntryCount()
        {
            var file = new BanchosucksActivityFile();
            var at = new DateTimeOffset(2026, 11, 23, 0, 0, 0, cet);

            for (int i = 0; i < BanchosucksActivityFile.MAX_ENTRIES + 5; i++)
                file.Entries.Add(new BanchosucksActivityEntry { At = at.AddMilliseconds(i), Seconds = i });

            Assert.That(file.Prune(new DateOnly(2026, 11, 23)), Is.True);
            Assert.That(file.Entries, Has.Count.EqualTo(BanchosucksActivityFile.MAX_ENTRIES));
            Assert.That(file.Entries[0].Seconds, Is.EqualTo(5), "the oldest ones go");
        }

        [Test]
        public void TestMarkGoalDayOnce()
        {
            var file = new BanchosucksActivityFile();

            Assert.That(file.MarkGoalDay(new DateOnly(2026, 11, 23)), Is.True);
            Assert.That(file.MarkGoalDay(new DateOnly(2026, 11, 23)), Is.False);
            Assert.That(file.MarkGoalDay(new DateOnly(2026, 11, 22)), Is.True);
            Assert.That(file.GoalDays, Is.EqualTo(new[] { new DateOnly(2026, 11, 22), new DateOnly(2026, 11, 23) }));
        }

        [Test]
        public void TestWriteIsAtomicAndKeepsABackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "banchosucks-activity-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "sub", "activity-log.json");
            string backup = Path.Combine(directory, "sub", "activity-log.bak.json");

            try
            {
                var file = new BanchosucksActivityFile { Created = new DateTimeOffset(2026, 11, 20, 18, 0, 0, cet) };
                file.Add(play(2026, 11, 23, 18, 0, 60));
                file.Write(path, backup);

                Assert.That(File.Exists(path), Is.True, "the folder is created on the way");
                Assert.That(File.Exists(backup), Is.False, "nothing to back up on the first write");
                Assert.That(File.Exists(path + ".tmp"), Is.False);

                file.Add(play(2026, 11, 23, 19, 0, 60));
                file.Write(path, backup);

                Assert.That(BanchosucksActivityFile.Read(path)!.Entries, Has.Count.EqualTo(2));
                Assert.That(BanchosucksActivityFile.Read(backup)!.Entries, Has.Count.EqualTo(1), "the previous version");
                Assert.That(File.Exists(path + ".tmp"), Is.False);

                // a damaged main file falls back to the backup the way the component reads it
                File.WriteAllText(path, "{\"version\":1,\"entries\":[{\"at\":\"20");
                Assert.That(BanchosucksActivityFile.Read(path), Is.Null);
                Assert.That((BanchosucksActivityFile.Read(path) ?? BanchosucksActivityFile.Read(backup))!.Entries, Has.Count.EqualTo(1));

                Assert.That(BanchosucksActivityFile.Read(Path.Combine(directory, "missing.json")), Is.Null);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void TestSnapshotIsIndependent()
        {
            var file = new BanchosucksActivityFile();
            file.Add(play(2026, 11, 23, 18, 0, 60));

            var snapshot = file.Snapshot();
            file.Add(play(2026, 11, 23, 19, 0, 60));
            file.MarkGoalDay(new DateOnly(2026, 11, 23));

            Assert.That(snapshot.Entries, Has.Count.EqualTo(1));
            Assert.That(snapshot.GoalDays, Is.Empty);
        }

        // ------------------------------------------------------------------ days

        [Test]
        public void TestDayIsTheLocalDayAsWritten()
        {
            // 00:30 on the 24th in Germany is still the 23rd in UTC: the play belongs to the 24th
            var afterMidnight = play(2026, 11, 24, 0, 30, 60);
            Assert.That(afterMidnight.At.UtcDateTime.Day, Is.EqualTo(23));
            Assert.That(afterMidnight.Day, Is.EqualTo(new DateOnly(2026, 11, 24)));

            // the same instant written on a PC in New York stays on the 23rd; each entry keeps the day it was played on
            var newYork = play(2026, 11, 23, 18, 30, 60, offset: TimeSpan.FromHours(-5));
            Assert.That(newYork.At.UtcDateTime, Is.EqualTo(afterMidnight.At.UtcDateTime));
            Assert.That(newYork.Day, Is.EqualTo(new DateOnly(2026, 11, 23)));

            // a play that runs across midnight counts for the day it ended on
            var acrossMidnight = play(2026, 11, 24, 0, 2, 300);
            Assert.That(acrossMidnight.Start.Day, Is.EqualTo(23));
            Assert.That(acrossMidnight.Day, Is.EqualTo(new DateOnly(2026, 11, 24)));
        }

        [Test]
        public void TestLastDays()
        {
            var entries = new[]
            {
                play(2026, 10, 24, 12, 0, 600), // 31 days back: outside
                play(2026, 10, 25, 12, 0, 120, true, 0.90), // 30th day back = first shown
                play(2026, 11, 22, 23, 59, 100, true, 0.98),
                play(2026, 11, 22, 23, 50, 50, false, 0.10),
                play(2026, 11, 22, 20, 0, 150, true, 0.96, estimated: true),
                play(2026, 11, 23, 0, 0, 30, false, 0.50),
                play(2026, 11, 24, 0, 0, 999), // tomorrow: outside
            };

            var days = BanchosucksActivityStats.LastDays(entries, new DateOnly(2026, 11, 23));

            Assert.That(days, Has.Length.EqualTo(30));
            Assert.That(days[0].Date, Is.EqualTo(new DateOnly(2026, 10, 25)));
            Assert.That(days[^1].Date, Is.EqualTo(new DateOnly(2026, 11, 23)));

            Assert.That(days[0], Is.EqualTo(new BanchosucksActivityStats.Day(new DateOnly(2026, 10, 25), 120, 1, 1, 0.90, false)));

            var yesterday = days[^2];
            Assert.That(yesterday.Seconds, Is.EqualTo(300));
            Assert.That(yesterday.Played, Is.EqualTo(3));
            Assert.That(yesterday.Passed, Is.EqualTo(2));
            Assert.That(yesterday.Accuracy, Is.EqualTo(0.97).Within(1e-9), "mean of the passed plays only");
            Assert.That(yesterday.Estimated, Is.True);

            var today = days[^1];
            Assert.That(today.Played, Is.EqualTo(1));
            Assert.That(today.Passed, Is.EqualTo(0));
            Assert.That(today.Accuracy, Is.Null, "no pass, no accuracy");

            Assert.That(days[1], Is.EqualTo(new BanchosucksActivityStats.Day(new DateOnly(2026, 10, 26), 0, 0, 0, null, false)), "empty days are there");
            Assert.That(BanchosucksActivityStats.LastDays(entries, new DateOnly(2026, 11, 23), 0), Is.Empty);
        }

        [Test]
        public void TestTotals()
        {
            var entries = new[]
            {
                play(2026, 11, 20, 12, 0, 600, true, 0.90),
                play(2026, 11, 20, 13, 0, 600, false, 0.20),
                play(2026, 11, 21, 12, 0, 1500, true, 1.00),
                play(2026, 11, 23, 12, 0, 300, true, 0.95),
                play(2026, 11, 23, 12, 10, 300, false, 0.60),
            };

            var totals = BanchosucksActivityStats.Summarise(entries);

            Assert.That(totals.Seconds, Is.EqualTo(3300));
            Assert.That(totals.Played, Is.EqualTo(5));
            Assert.That(totals.Passed, Is.EqualTo(3));
            Assert.That(totals.Accuracy, Is.EqualTo(0.95).Within(1e-9));
            Assert.That(totals.ActiveDays, Is.EqualTo(3));
            Assert.That(totals.FirstDay, Is.EqualTo(new DateOnly(2026, 11, 20)));
            Assert.That(totals.MostMapsInDay, Is.EqualTo(new BanchosucksActivityStats.Best(2, new DateOnly(2026, 11, 20))), "a tie keeps the earlier day");
            Assert.That(totals.MostTimeInDay, Is.EqualTo(new BanchosucksActivityStats.Best(1500, new DateOnly(2026, 11, 21))));
            Assert.That(totals.HasEstimated, Is.False);

            var empty = BanchosucksActivityStats.Summarise(Array.Empty<BanchosucksActivityEntry>());
            Assert.That(empty.Played, Is.EqualTo(0));
            Assert.That(empty.Accuracy, Is.Null);
            Assert.That(empty.FirstDay, Is.Null);
            Assert.That(empty.LongestSession, Is.Null);
            Assert.That(empty.MostMapsInDay, Is.Null);
            Assert.That(empty.MostTimeInDay, Is.Null);
        }

        [Test]
        public void TestLongestSession()
        {
            var entries = new List<BanchosucksActivityEntry>
            {
                // session A: 18:00 to 18:20 (the second play starts 8 minutes after the first ended)
                play(2026, 11, 22, 18, 2, 120), // 18:00 - 18:02
                play(2026, 11, 22, 18, 20, 600), // 18:10 - 18:20
                // 10 minutes and 1 second later: a new session (B), 18:30:01 to 18:33:01
                new BanchosucksActivityEntry { At = new DateTimeOffset(2026, 11, 22, 18, 33, 1, cet), Seconds = 180, Passed = true },
                // session C across midnight: 23:50 to 00:40, dated by its end
                play(2026, 11, 22, 23, 55, 300), // 23:50 - 23:55
                play(2026, 11, 23, 0, 5, 300), // 00:00 - 00:05
                play(2026, 11, 23, 0, 40, 1500), // 00:15 - 00:40
            };

            var best = BanchosucksActivityStats.LongestSession(entries)!.Value;
            Assert.That(best.Value, Is.EqualTo(50 * 60));
            Assert.That(best.Date, Is.EqualTo(new DateOnly(2026, 11, 23)));

            // exactly ten minutes still joins
            var joined = new[] { play(2026, 11, 22, 18, 2, 120), play(2026, 11, 22, 18, 13, 60) }; // 18:00-18:02, 18:12-18:13
            Assert.That(BanchosucksActivityStats.LongestSession(joined)!.Value.Value, Is.EqualTo(13 * 60));

            // estimated entries are no sessions: the attempts between them are unknown
            var estimated = new[] { play(2026, 11, 1, 18, 0, 200, estimated: true), play(2026, 11, 1, 18, 5, 200, estimated: true) };
            Assert.That(BanchosucksActivityStats.LongestSession(estimated), Is.Null);
            Assert.That(BanchosucksActivityStats.Summarise(estimated).HasEstimated, Is.True);

            // order of the input does not matter
            entries.Reverse();
            Assert.That(BanchosucksActivityStats.LongestSession(entries)!.Value.Value, Is.EqualTo(50 * 60));
        }

        // ------------------------------------------------------------------ daily goal

        [Test]
        public void TestGoalProgress()
        {
            var today = new DateOnly(2026, 11, 23);
            var entries = new[]
            {
                play(2026, 11, 22, 23, 59, 3000), // yesterday
                play(2026, 11, 23, 0, 0, 600),
                play(2026, 11, 23, 12, 0, 590, false),
                play(2026, 11, 23, 13, 0, 600),
            };

            var off = BanchosucksDailyGoal.ProgressOn(entries, today, 0, 0);
            Assert.That(off.Enabled, Is.False);
            Assert.That(off.Met, Is.False, "no goal is never met");
            Assert.That(off.Fraction, Is.EqualTo(0));

            var minutes = BanchosucksDailyGoal.ProgressOn(entries, today, 30, 0);
            Assert.That(minutes.MinutesPlayed, Is.EqualTo(1790 / 60.0).Within(1e-9), "failed plays count as play time");
            Assert.That(minutes.MapsPassed, Is.EqualTo(2));
            Assert.That(minutes.Met, Is.False, "29:50 is not 30 minutes");
            Assert.That(minutes.Fraction, Is.EqualTo(1790 / 1800.0).Within(1e-9));
            Assert.That(BanchosucksDailyGoal.ProgressOn(entries, today, 29, 0).Met, Is.True);

            var maps = BanchosucksDailyGoal.ProgressOn(entries, today, 0, 2);
            Assert.That(maps.Met, Is.True);
            Assert.That(maps.Fraction, Is.EqualTo(1));
            Assert.That(BanchosucksDailyGoal.ProgressOn(entries, today, 0, 3).Met, Is.False);

            // both set: both are needed, and the bar follows the part that is behind
            var both = BanchosucksDailyGoal.ProgressOn(entries, today, 20, 4);
            Assert.That(both.MinutesMet, Is.True);
            Assert.That(both.MapsMet, Is.False);
            Assert.That(both.Met, Is.False);
            Assert.That(both.Fraction, Is.EqualTo(0.5));
            Assert.That(BanchosucksDailyGoal.ProgressOn(entries, today, 20, 2).Met, Is.True);

            Assert.That(BanchosucksDailyGoal.ProgressOn(entries, today, -5, -1).Enabled, Is.False, "negative values are off");
        }

        [Test]
        public void TestStreak()
        {
            var today = new DateOnly(2026, 11, 23);
            static DateOnly d(int day) => new DateOnly(2026, 11, day);

            Assert.That(BanchosucksDailyGoal.Streak(Array.Empty<DateOnly>(), today), Is.EqualTo(0));
            Assert.That(BanchosucksDailyGoal.Streak(new[] { d(23) }, today), Is.EqualTo(1));
            Assert.That(BanchosucksDailyGoal.Streak(new[] { d(21), d(22), d(23) }, today), Is.EqualTo(3));

            // today is not done yet: the streak up to yesterday is still alive
            Assert.That(BanchosucksDailyGoal.Streak(new[] { d(20), d(21), d(22) }, today), Is.EqualTo(3));

            // yesterday was missed: gone, whatever came before
            Assert.That(BanchosucksDailyGoal.Streak(new[] { d(19), d(20), d(21) }, today), Is.EqualTo(0));
            Assert.That(BanchosucksDailyGoal.Streak(new[] { d(19), d(20), d(21), d(23) }, today), Is.EqualTo(1));

            // across a month and a year boundary
            var newYear = new DateOnly(2027, 1, 1);
            Assert.That(BanchosucksDailyGoal.Streak(new[] { new DateOnly(2026, 12, 30), new DateOnly(2026, 12, 31), newYear }, newYear), Is.EqualTo(3));

            Assert.That(BanchosucksDailyGoal.LongestStreak(new[] { d(1), d(2), d(3), d(5), d(6), d(10), d(2) }), Is.EqualTo(3));
            Assert.That(BanchosucksDailyGoal.LongestStreak(Array.Empty<DateOnly>()), Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ saved scores

        [Test]
        public void TestEntryFromSavedScore()
        {
            var date = new DateTimeOffset(2026, 9, 1, 21, 30, 12, 345, TimeSpan.FromHours(2));

            var entry = BanchosucksActivityBackfill.FromSavedScore(date, 180_000, 1.5, 0.98765432, "taiko");
            Assert.That(entry.Seconds, Is.EqualTo(120), "three minutes at double time speed");
            Assert.That(entry.Passed, Is.True);
            Assert.That(entry.Estimated, Is.True);
            Assert.That(entry.Accuracy, Is.EqualTo(0.9877));
            Assert.That(entry.Ruleset, Is.EqualTo("taiko"));
            Assert.That(entry.At, Is.EqualTo(new DateTimeOffset(2026, 9, 1, 21, 30, 12, TimeSpan.FromHours(2))), "whole seconds");
            Assert.That(entry.Day, Is.EqualTo(new DateOnly(2026, 9, 1)));

            Assert.That(BanchosucksActivityBackfill.FromSavedScore(date, 180_000, 0, 1, "osu").Seconds, Is.EqualTo(180), "a broken rate is normal speed");
            Assert.That(BanchosucksActivityBackfill.FromSavedScore(date, double.NaN, 1, double.NaN, "osu").Seconds, Is.EqualTo(0));
            Assert.That(BanchosucksActivityBackfill.FromSavedScore(date, 1e12, 1, 2, "osu").Seconds, Is.EqualTo(3600), "capped");
            Assert.That(BanchosucksActivityBackfill.FromSavedScore(date, 1e12, 1, 2, "osu").Accuracy, Is.EqualTo(1));
        }
    }
}
