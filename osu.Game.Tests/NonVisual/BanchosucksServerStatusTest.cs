// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.Banchosucks;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksServerStatusTest
    {
        [Test]
        public void TestSummaryAsThePluginSendsIt()
        {
            // banchosucks_status/__init__.py, summary()
            var summary = JsonConvert.DeserializeObject<BanchosucksStatusSummary>(
                @"{""lazer_online"": 7, ""open_rooms"": 2, ""daily_challenge"": ""Camellia - Exit This Earth's Atomosphere [Evolution]""}")!;

            Assert.That(summary.LazerOnline, Is.EqualTo(7));
            Assert.That(summary.OpenRooms, Is.EqualTo(2));
            Assert.That(summary.DailyChallenge, Is.EqualTo("Camellia - Exit This Earth's Atomosphere [Evolution]"));

            // the plugin has no maintenance fields today
            Assert.That(summary.MaintenanceAt, Is.Null);
            Assert.That(summary.MaintenanceMessage, Is.Null);
            Assert.That(BanchosucksServerStatus.MaintenanceIn(summary, DateTimeOffset.UtcNow), Is.Null);
        }

        [Test]
        public void TestSummaryWithoutDailyChallenge()
        {
            var summary = JsonConvert.DeserializeObject<BanchosucksStatusSummary>(@"{""lazer_online"": 0, ""open_rooms"": 0, ""daily_challenge"": null}")!;

            Assert.That(summary.LazerOnline, Is.Zero);
            Assert.That(summary.DailyChallenge, Is.Null);
        }

        [Test]
        public void TestProposedMaintenanceFields()
        {
            var summary = JsonConvert.DeserializeObject<BanchosucksStatusSummary>(
                @"{""lazer_online"": 3, ""open_rooms"": 0, ""daily_challenge"": null, ""maintenance_at"": ""2026-11-23T20:00:00+00:00"", ""maintenance_message"": ""Update""}")!;

            var at = new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero);

            Assert.That(summary.MaintenanceAt, Is.EqualTo(at));
            Assert.That(summary.MaintenanceMessage, Is.EqualTo("Update"));

            Assert.That(BanchosucksServerStatus.MaintenanceIn(summary, at.AddMinutes(-5)), Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(BanchosucksServerStatus.MaintenanceIn(summary, at.AddHours(-23)), Is.EqualTo(TimeSpan.FromHours(23)));
            Assert.That(BanchosucksServerStatus.MaintenanceIn(summary, at.AddHours(-25)), Is.Null, "too far away for a countdown");
            Assert.That(BanchosucksServerStatus.MaintenanceIn(summary, at.AddMinutes(2)), Is.EqualTo(TimeSpan.Zero), "starting");
            Assert.That(BanchosucksServerStatus.MaintenanceIn(summary, at.AddMinutes(16)), Is.Null, "over");
            Assert.That(BanchosucksServerStatus.MaintenanceIn(null, at), Is.Null);
        }

        [Test]
        public void TestCountdownFormat()
        {
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromSeconds(245)), Is.EqualTo("4:05"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromSeconds(244.2)), Is.EqualTo("4:05"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromSeconds(59)), Is.EqualTo("0:59"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromSeconds(0.4)), Is.EqualTo("0:01"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.Zero), Is.EqualTo("0:00"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromSeconds(-5)), Is.EqualTo("0:00"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromMinutes(59.99)), Is.EqualTo("1:00 h"));
            Assert.That(BanchosucksServerStatus.FormatCountdown(TimeSpan.FromMinutes(135)), Is.EqualTo("2:15 h"));
        }

        [Test]
        public void TestConnectionState()
        {
            Assert.That(BanchosucksServerStatus.Connection(APIState.Online, false), Is.EqualTo(BanchosucksConnectionState.Online));
            Assert.That(BanchosucksServerStatus.Connection(APIState.Online, true), Is.EqualTo(BanchosucksConnectionState.Trouble));
            Assert.That(BanchosucksServerStatus.Connection(APIState.Failing, false), Is.EqualTo(BanchosucksConnectionState.Trouble));
            Assert.That(BanchosucksServerStatus.Connection(APIState.Connecting, false), Is.EqualTo(BanchosucksConnectionState.Connecting));
            Assert.That(BanchosucksServerStatus.Connection(APIState.RequiresSecondFactorAuth, false), Is.EqualTo(BanchosucksConnectionState.Connecting));
            Assert.That(BanchosucksServerStatus.Connection(APIState.Offline, false), Is.EqualTo(BanchosucksConnectionState.Offline));
            Assert.That(BanchosucksServerStatus.Connection(APIState.Offline, true), Is.EqualTo(BanchosucksConnectionState.Offline));
        }

        [Test]
        public void TestMilliseconds()
        {
            Assert.That(BanchosucksServerStatus.Milliseconds(TimeSpan.FromMilliseconds(23.4)), Is.EqualTo(23));
            Assert.That(BanchosucksServerStatus.Milliseconds(TimeSpan.FromMilliseconds(23.6)), Is.EqualTo(24));
            Assert.That(BanchosucksServerStatus.Milliseconds(TimeSpan.FromHours(1)), Is.EqualTo(99_999));
            Assert.That(BanchosucksServerStatus.Milliseconds(null), Is.Null);
            Assert.That(BanchosucksServerStatus.Milliseconds(TimeSpan.FromMilliseconds(-1)), Is.Null);
        }

        #region refresh gate

        private const double minute = BanchosucksRefreshGate.ONE_MINUTE;

        [Test]
        public void TestFirstRequestIsAllowedAtOnce()
        {
            var gate = new BanchosucksRefreshGate(2 * minute);

            Assert.That(gate.ShouldRefresh(0), Is.True);
            Assert.That(gate.LastSuccess, Is.Null);
            Assert.That(gate.Age(500), Is.Null);
        }

        [Test]
        public void TestRegularInterval()
        {
            var gate = new BanchosucksRefreshGate(2 * minute);

            gate.Started(0);
            Assert.That(gate.ShouldRefresh(10 * minute), Is.False, "one request at a time");

            gate.Succeeded(300);
            Assert.That(gate.ShouldRefresh(minute), Is.False);
            Assert.That(gate.ShouldRefresh(2 * minute - 1), Is.False);
            Assert.That(gate.ShouldRefresh(2 * minute), Is.True);
            Assert.That(gate.Age(1300), Is.EqualTo(1000));
        }

        [Test]
        public void TestAskingDoesNotGoBelowOneMinute()
        {
            var gate = new BanchosucksRefreshGate(5 * minute);

            gate.Started(0);
            gate.Succeeded(100);

            // the player opens the panel or clicks: sooner than the interval, never sooner than a minute
            Assert.That(gate.ShouldRefresh(1000, wanted: true), Is.False);
            Assert.That(gate.ShouldRefresh(minute - 1, wanted: true), Is.False);
            Assert.That(gate.ShouldRefresh(minute, wanted: true), Is.True);
            Assert.That(gate.ShouldRefresh(minute), Is.False);
        }

        [Test]
        public void TestIntervalsAreNeverBelowOneMinute()
        {
            var gate = new BanchosucksRefreshGate(1000, 10);

            Assert.That(gate.Interval, Is.EqualTo(minute));
            Assert.That(gate.MinimumInterval, Is.EqualTo(minute));

            gate.Started(0);
            gate.Succeeded(1);
            Assert.That(gate.ShouldRefresh(30_000, wanted: true), Is.False);
        }

        [Test]
        public void TestAFailureIsRetriedAfterAMinuteNotAtOnce()
        {
            var gate = new BanchosucksRefreshGate(5 * minute);

            gate.Started(0);
            gate.Failed();

            Assert.That(gate.LastFailed, Is.True);
            Assert.That(gate.ShouldRefresh(1000), Is.False);
            Assert.That(gate.ShouldRefresh(minute), Is.True);

            gate.Started(minute);
            gate.Succeeded(minute + 50);
            Assert.That(gate.LastFailed, Is.False);
            Assert.That(gate.ShouldRefresh(2 * minute), Is.False, "back to the regular interval");
        }

        [Test]
        public void TestAClockThatWentBackwardsDoesNotBlock()
        {
            var gate = new BanchosucksRefreshGate(5 * minute);

            gate.Started(10 * minute);
            gate.Succeeded(10 * minute);

            Assert.That(gate.ShouldRefresh(5), Is.True);
            Assert.That(gate.Age(5), Is.Zero);
        }

        #endregion
    }
}
