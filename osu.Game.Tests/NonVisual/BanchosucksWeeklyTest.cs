// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.Banchosucks;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksWeeklyTest
    {
        // banchosucks_motw/__init__.py, current_endpoint(): what pick_winner() stores plus the nominations
        private const string motw_json = @"{
            ""current"": {
                ""beatmap_id"": 129891, ""beatmapset_id"": 39804, ""room_id"": 412,
                ""title"": ""xi - FREEDOM DiVE [FOUR DIMENSIONS]"", ""stars"": 7.81,
                ""nominated_by"": ""Lidl"", ""nominated_by_id"": 3, ""votes"": 4,
                ""picked_at"": ""2026-11-22T18:05:00.123456"", ""ends_at"": ""2026-11-29T17:55:00.123456+00:00""
            },
            ""nominations"": [
                {""beatmap_id"": 75, ""title"": ""Kenji Ninuma - DISCO PRINCE [Normal]"", ""stars"": 2.55, ""by"": ""Hodenlos"", ""votes"": 2},
                {""beatmap_id"": 76, ""title"": null, ""stars"": null, ""by"": null, ""votes"": 0}
            ]
        }";

        // banchosucks_goal/__init__.py, update()
        private const string goal_json = @"{""goal"": {
            ""week_start"": ""2026-11-23T00:00:00+00:00"", ""target"": 2000, ""reached_at"": null,
            ""stable"": 900, ""lazer"": 334, ""total"": 1234, ""participants"": 45,
            ""updated_at"": ""2026-11-25T12:10:00.5+00:00"", ""ends_at"": ""2026-11-30T00:00:00+00:00""
        }}";

        [Test]
        public void TestMapOfTheWeekAsThePluginSendsIt()
        {
            var response = JsonConvert.DeserializeObject<BanchosucksMapOfTheWeekResponse>(motw_json)!;
            var map = response.Current!;

            Assert.That(map.BeatmapId, Is.EqualTo(129891));
            Assert.That(map.BeatmapSetId, Is.EqualTo(39804));
            Assert.That(map.RoomId, Is.EqualTo(412));
            Assert.That(map.Title, Is.EqualTo("xi - FREEDOM DiVE [FOUR DIMENSIONS]"));
            Assert.That(map.Stars, Is.EqualTo(7.81));
            Assert.That(map.NominatedBy, Is.EqualTo("Lidl"));
            Assert.That(map.Votes, Is.EqualTo(4));
            Assert.That(BanchosucksWeekly.ParseTime(map.EndsAt), Is.EqualTo(new DateTimeOffset(2026, 11, 29, 17, 55, 0, TimeSpan.Zero).AddTicks(1234560)));

            Assert.That(response.Nominations, Has.Count.EqualTo(2));
            Assert.That(response.Nominations[0].By, Is.EqualTo("Hodenlos"));
            Assert.That(response.Nominations[1].Title, Is.Null);
            Assert.That(response.Nominations[1].Stars, Is.Null);
        }

        [Test]
        public void TestNoMapOfTheWeek()
        {
            var response = JsonConvert.DeserializeObject<BanchosucksMapOfTheWeekResponse>(@"{""current"": null, ""nominations"": []}")!;

            Assert.That(response.Current, Is.Null);
            Assert.That(response.Nominations, Is.Empty);
            Assert.That(BanchosucksWeekly.IsRunning(response.Current, DateTimeOffset.UtcNow), Is.False);
        }

        [Test]
        public void TestMostPlayedMapHasNoNominator()
        {
            var response = JsonConvert.DeserializeObject<BanchosucksMapOfTheWeekResponse>(
                @"{""current"": {""beatmap_id"": 5, ""beatmapset_id"": 2, ""room_id"": 1, ""title"": ""a - b [c]"", ""stars"": 4.0, ""nominated_by"": null, ""nominated_by_id"": null, ""votes"": 0,
                    ""picked_at"": ""2026-11-22T18:05:00"", ""ends_at"": ""2026-11-29T17:55:00+00:00""}, ""nominations"": []}")!;

            Assert.That(response.Current!.NominatedBy, Is.Null);
            Assert.That(response.Current.Stars, Is.EqualTo(4.0));
        }

        [Test]
        public void TestGoalAsThePluginSendsIt()
        {
            var goal = JsonConvert.DeserializeObject<BanchosucksCommunityGoalResponse>(goal_json)!.Goal!;

            Assert.That(goal.Target, Is.EqualTo(2000));
            Assert.That(goal.Total, Is.EqualTo(1234));
            Assert.That(goal.Stable, Is.EqualTo(900));
            Assert.That(goal.Lazer, Is.EqualTo(334));
            Assert.That(goal.Participants, Is.EqualTo(45));
            Assert.That(goal.ReachedAt, Is.Null);

            Assert.That(BanchosucksWeekly.Fraction(goal), Is.EqualTo(0.617).Within(0.0001));
            Assert.That(BanchosucksWeekly.Percent(goal), Is.EqualTo(61), "rounded down like the server's !goal");
            Assert.That(BanchosucksWeekly.Reached(goal), Is.False);
            Assert.That(BanchosucksWeekly.Remaining(goal.EndsAt, new DateTimeOffset(2026, 11, 29, 0, 0, 0, TimeSpan.Zero)), Is.EqualTo(TimeSpan.FromDays(1)));
        }

        [Test]
        public void TestNoGoalYet()
        {
            var response = JsonConvert.DeserializeObject<BanchosucksCommunityGoalResponse>(@"{""goal"": null}")!;

            Assert.That(response.Goal, Is.Null);
            Assert.That(BanchosucksWeekly.Fraction(null), Is.Zero);
            Assert.That(BanchosucksWeekly.Percent(null), Is.Zero);
            Assert.That(BanchosucksWeekly.Reached(null), Is.False);
        }

        [Test]
        public void TestGoalJustStartedHasOnlyATarget()
        {
            // the plugin creates the goal as {week_start, target, reached_at} and fills in the rest in the same run
            var goal = JsonConvert.DeserializeObject<BanchosucksCommunityGoalResponse>(@"{""goal"": {""week_start"": ""2026-11-23T00:00:00+00:00"", ""target"": 300, ""reached_at"": null}}")!.Goal!;

            Assert.That(goal.Total, Is.Zero);
            Assert.That(BanchosucksWeekly.Fraction(goal), Is.Zero);
            Assert.That(BanchosucksWeekly.Remaining(goal.EndsAt, DateTimeOffset.UtcNow), Is.Null);
        }

        [Test]
        public void TestGoalReachedAndBeyond()
        {
            var goal = new BanchosucksCommunityGoal { Target = 300, Total = 450, ReachedAt = "2026-11-26T20:00:00+00:00" };

            Assert.That(BanchosucksWeekly.Fraction(goal), Is.EqualTo(1));
            Assert.That(BanchosucksWeekly.Percent(goal), Is.EqualTo(100));
            Assert.That(BanchosucksWeekly.Reached(goal), Is.True);

            // reached by the numbers although the plugin has not marked it yet
            Assert.That(BanchosucksWeekly.Reached(new BanchosucksCommunityGoal { Target = 300, Total = 300 }), Is.True);
            Assert.That(BanchosucksWeekly.Reached(new BanchosucksCommunityGoal { Target = 300, Total = 299 }), Is.False);

            // nonsense never divides by zero or runs backwards
            Assert.That(BanchosucksWeekly.Fraction(new BanchosucksCommunityGoal { Target = 0, Total = 5 }), Is.Zero);
            Assert.That(BanchosucksWeekly.Percent(new BanchosucksCommunityGoal { Target = 0, Total = 5 }), Is.Zero);
            Assert.That(BanchosucksWeekly.Fraction(new BanchosucksCommunityGoal { Target = 300, Total = -5 }), Is.Zero);
        }

        [Test]
        public void TestTimesWithoutOffsetAreUtc()
        {
            Assert.That(BanchosucksWeekly.ParseTime("2026-11-22T18:05:00"), Is.EqualTo(new DateTimeOffset(2026, 11, 22, 18, 5, 0, TimeSpan.Zero)));
            Assert.That(BanchosucksWeekly.ParseTime("2026-11-22T18:05:00+00:00"), Is.EqualTo(new DateTimeOffset(2026, 11, 22, 18, 5, 0, TimeSpan.Zero)));
            Assert.That(BanchosucksWeekly.ParseTime("2026-11-22T19:05:00+01:00"), Is.EqualTo(new DateTimeOffset(2026, 11, 22, 18, 5, 0, TimeSpan.Zero)));
            Assert.That(BanchosucksWeekly.ParseTime(null), Is.Null);
            Assert.That(BanchosucksWeekly.ParseTime(""), Is.Null);
            Assert.That(BanchosucksWeekly.ParseTime("soon"), Is.Null);
        }

        [Test]
        public void TestMapRunsUntilItsEnd()
        {
            var map = new BanchosucksMapOfTheWeek { BeatmapId = 5, BeatmapSetId = 2, EndsAt = "2026-11-29T17:55:00+00:00" };
            var end = new DateTimeOffset(2026, 11, 29, 17, 55, 0, TimeSpan.Zero);

            Assert.That(BanchosucksWeekly.IsRunning(map, end.AddDays(-3)), Is.True);
            Assert.That(BanchosucksWeekly.IsRunning(map, end.AddSeconds(1)), Is.False);
            Assert.That(BanchosucksWeekly.Remaining(map.EndsAt, end.AddSeconds(1)), Is.EqualTo(TimeSpan.Zero));

            // without a readable end it is shown, the plugin withholds ended maps itself
            Assert.That(BanchosucksWeekly.IsRunning(new BanchosucksMapOfTheWeek { BeatmapId = 5 }, end), Is.True);
            Assert.That(BanchosucksWeekly.IsRunning(new BanchosucksMapOfTheWeek(), end), Is.False);
        }

        [Test]
        public void TestRemainingTimeFormat()
        {
            Assert.That(BanchosucksWeekly.FormatRemaining(new TimeSpan(3, 4, 59, 0), false), Is.EqualTo("3d 4h"));
            Assert.That(BanchosucksWeekly.FormatRemaining(new TimeSpan(3, 4, 59, 0), true), Is.EqualTo("3 T 4 Std"));
            Assert.That(BanchosucksWeekly.FormatRemaining(new TimeSpan(0, 5, 12, 0), false), Is.EqualTo("5h 12min"));
            Assert.That(BanchosucksWeekly.FormatRemaining(new TimeSpan(0, 5, 12, 0), true), Is.EqualTo("5 Std 12 Min"));
            Assert.That(BanchosucksWeekly.FormatRemaining(new TimeSpan(0, 0, 12, 30), false), Is.EqualTo("12min"));
            Assert.That(BanchosucksWeekly.FormatRemaining(TimeSpan.FromSeconds(20), true), Is.EqualTo("1 Min"));
            Assert.That(BanchosucksWeekly.FormatRemaining(TimeSpan.Zero, false), Is.EqualTo("over"));
            Assert.That(BanchosucksWeekly.FormatRemaining(TimeSpan.FromSeconds(-1), true), Is.EqualTo("vorbei"));
        }
    }
}
