// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Tycoon;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The numbers here are the same as in the plugin's test_economy.py: client and server must agree on every bound.
    /// </summary>
    [TestFixture]
    public class BanchosucksTycoonRulesTest
    {
        private static TycoonState sampleRun()
        {
            var state = new TycoonState { Hardware = 1 };
            state.Marketing["discord"] = 3;
            state.Features.Add("website");
            TycoonRules.Simulate(state, 600);
            return state;
        }

        [Test]
        public void TestCatalogue()
        {
            Assert.That(Enumerable.Range(0, 9).Select(TycoonRules.Capacity), Is.EqualTo(new[] { 10, 40, 120, 400, 1500, 5000, 15000, 50000, 200000 }));
            Assert.That((TycoonRules.HardwareCost(0), TycoonRules.HardwareCost(1), TycoonRules.HardwareCost(8)), Is.EqualTo((0L, 60L, 60_000_000L)));
            Assert.That((TycoonRules.MarketingCost("discord", 0), TycoonRules.MarketingCost("discord", 1), TycoonRules.MarketingCost("discord", 10)), Is.EqualTo((15L, 17L, 60L)));
            Assert.That(TycoonRules.MarketingTotalCost("discord", 10), Is.EqualTo(299));
            Assert.That((TycoonRules.MarketingCost("reddit", 3), TycoonRules.MarketingCost("youtube", 0)), Is.EqualTo((152L, 1100L)));
            Assert.That(TycoonRules.FeatureCost("tests"), Is.EqualTo(25_000_000));

            string[] features = { "website", "leaderboards", "medals", "anticheat" };
            Assert.That(TycoonRules.DonationMult(features), Is.EqualTo(1.38).Within(1e-9));
            Assert.That(TycoonRules.InflowMult(features), Is.EqualTo(1.25).Within(1e-9));
            Assert.That(TycoonRules.LegacyMult(2), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(TycoonRules.RegenMult(new[] { "discord_bot" }), Is.EqualTo(2.0));
            Assert.That(TycoonRules.RufFactor(0), Is.EqualTo(0.5));
            Assert.That(TycoonRules.RufFactor(100), Is.EqualTo(1.5));
        }

        [Test]
        public void TestOneDiscordOnAPi()
        {
            var state = new TycoonState();
            state.Marketing["discord"] = 1;
            double gained = TycoonRules.Simulate(state, 60);
            Assert.That(state.Players, Is.EqualTo(8.3463).Within(1e-4));
            Assert.That(state.Ruf, Is.EqualTo(100.0).Within(1e-4));
            Assert.That(state.Pending, Is.EqualTo(18.9360).Within(1e-4), "income waits in the collector box");
            Assert.That(state.Donations, Is.EqualTo(0));
            Assert.That(gained, Is.EqualTo(state.Pending).Within(1e-9));
            Assert.That(TycoonRules.Collect(state), Is.EqualTo(18.9360).Within(1e-4));
            Assert.That((Math.Round(state.Donations, 4), state.Pending), Is.EqualTo((18.9360, 0.0)));
            Assert.That(state.PlaySeconds, Is.EqualTo(60.0).Within(1e-9));
        }

        [Test]
        public void TestOverloadedLaptop()
        {
            var state = sampleRun();
            Assert.That(state.Players, Is.EqualTo(43.8999).Within(1e-4));
            Assert.That(state.Ruf, Is.EqualTo(100.0000).Within(1e-4), "just over capacity, within tolerance: the reputation holds");
            Assert.That(state.Pending, Is.EqualTo(2155.5571).Within(1e-4));
            Assert.That(state.MaxPlayers, Is.EqualTo(43.8999).Within(1e-4));
            Assert.That(TycoonRules.Inflow(state), Is.EqualTo(0.450000).Within(1e-6));
            Assert.That(TycoonRules.DonationRate(state), Is.EqualTo(3.950995).Within(1e-6));
            Assert.That(TycoonRules.Spent(state), Is.EqualTo(611.0));
            Assert.That(TycoonRules.MaxPlayersBound(state), Is.EqualTo(323.4848).Within(1e-4));
            Assert.That(TycoonRules.MaxDonationRate(state), Is.EqualTo(58.227273 * 1.15).Within(1e-6), "the comfort cap is part of the bound");
        }

        [Test]
        public void TestOverloadedPi()
        {
            var state = new TycoonState();
            state.Marketing["discord"] = 10;
            TycoonRules.Simulate(state, 300);
            Assert.That(state.Players, Is.EqualTo(15.2959).Within(1e-4), "far over the Pi's ten, the extra players keep leaving");
            Assert.That(state.Ruf, Is.EqualTo(0.0000).Within(1e-4), "lag costs reputation");
            Assert.That(state.Pending, Is.EqualTo(155.9943).Within(1e-4));
        }

        [Test]
        public void TestAutoCollectSkipsTheBox()
        {
            var state = new TycoonState { Players = 10 };
            state.Features.Add("autocollect");
            TycoonRules.Step(state, 1);
            Assert.That(state.Pending, Is.EqualTo(0));
            Assert.That(state.Donations, Is.EqualTo(10 * 0.05 * 1.5).Within(1e-9));
        }

        [Test]
        public void TestBuyingAndClicking()
        {
            var state = new TycoonState();
            state.Marketing["discord"] = 1;
            Assert.That(TycoonRules.Click(state), Is.EqualTo(1.05).Within(1e-9));
            Assert.That((state.Players, state.Clicks), Is.EqualTo((1.05, 1L)));

            state = new TycoonState { Donations = 100 };
            Assert.That(TycoonRules.BuyHardware(state), Is.True);
            Assert.That((state.Hardware, state.Donations), Is.EqualTo((1, 40.0)));
            Assert.That(TycoonRules.BuyHardware(state), Is.False, "the Mini PC costs 450");
            state.Donations = 20;
            Assert.That(TycoonRules.BuyMarketing(state, "discord"), Is.True);
            Assert.That((state.MarketingCount("discord"), state.Donations), Is.EqualTo((1, 5.0)));
            Assert.That(TycoonRules.BuyMarketing(state, "discord"), Is.False);
            state.Donations = 2000;
            Assert.That(TycoonRules.BuyMarketing(state, "youtube"), Is.False, "YouTube videos need the Mini PC, this is a laptop");
            Assert.That(TycoonRules.MarketingOf("influencer").MinHardware, Is.EqualTo(6));
            state.Donations = 600;
            Assert.That(TycoonRules.BuyFeature(state, "website"), Is.True);
            Assert.That(state.Features, Is.EqualTo(new[] { "website" }));
            Assert.That(state.Donations, Is.EqualTo(100.0));
            Assert.That(TycoonRules.BuyFeature(state, "website"), Is.False, "features are bought once");
        }

        [Test]
        public void TestPrestige()
        {
            Assert.That(new[] { 999_999, 1e6, 4e6, 9.5e6 }.Select(TycoonRules.LegacyGain), Is.EqualTo(new[] { 0, 1, 2, 3 }));

            var state = sampleRun();
            state.RunTotal = 4_000_000;
            state.LifetimeTotal = 4_000_000;
            Assert.That(TycoonRules.Prestige(state), Is.EqualTo(2));
            Assert.That((state.Legacy, state.Prestige, state.Hardware, state.Ruf, state.Donations), Is.EqualTo((2, 1, 0, 100.0, 0.0)));
            Assert.That(state.LifetimeTotal, Is.EqualTo(4_000_000.0), "the lifetime total survives the restart");
            Assert.That(state.MarketingCount("discord"), Is.EqualTo(0));
            Assert.That(TycoonRules.Prestige(new TycoonState()), Is.EqualTo(0));
        }

        [Test]
        public void TestOffice()
        {
            var layout = new List<TycoonPlacement>
            {
                new TycoonPlacement { X = 0, Y = 0, Key = "plant" },
                new TycoonPlacement { X = 1, Y = 0, Key = "coffee" },
                new TycoonPlacement { X = 2, Y = 0, Key = "sofa" },
                new TycoonPlacement { X = 3, Y = 0, Key = "carpet" },
            };
            Assert.That(TycoonRules.ComfortPoints(layout), Is.EqualTo(6));
            Assert.That(TycoonRules.ComfortMult(layout), Is.EqualTo(1.03).Within(1e-9));
            Assert.That(TycoonRules.LayoutCost(layout), Is.EqualTo(3350.0));
            var many = Enumerable.Range(0, 40).Select(i => new TycoonPlacement { X = i % 20, Y = i / 20, Key = "arcade" }).ToList();
            Assert.That(TycoonRules.ComfortMult(many), Is.EqualTo(1.15).Within(1e-9), "comfort is capped");

            var state = new TycoonState { Players = 100, Layout = layout };
            Assert.That(TycoonRules.DonationRate(state), Is.EqualTo(100 * 0.05 * 1.5 * 1.03).Within(1e-9));
            Assert.That(TycoonRules.Spent(state), Is.EqualTo(3350.0));

            state = new TycoonState { Donations = 1000 };
            Assert.That(TycoonRules.Place(state, 2, 3, "plant"), Is.True);
            Assert.That(state.Donations, Is.EqualTo(850.0));
            Assert.That(TycoonRules.Place(state, 2, 3, "coffee"), Is.False, "the cell is taken");
            Assert.That(TycoonRules.Place(state, 20, 0, "plant"), Is.False, "outside the plot");
            Assert.That(TycoonRules.Place(state, 0, 0, "sofa"), Is.False, "too expensive");
            Assert.That(TycoonRules.Remove(state, 2, 3), Is.True);
            Assert.That(state.Donations, Is.EqualTo(925.0), "half the price comes back");
            Assert.That(TycoonRules.Remove(state, 2, 3), Is.False);
        }

        [Test]
        public void TestTeam()
        {
            Assert.That(Enumerable.Range(0, 5).Select(i => TycoonRules.StaffCost("developer", i)), Is.EqualTo(new long[] { 5000, 10000, 20000, 40000, 80000 }));
            Assert.That((TycoonRules.StaffCost("moderator", 0), TycoonRules.StaffCost("community", 4)), Is.EqualTo((2000L, 56000L)));
            Assert.That(TycoonRules.StaffTotalCost("developer", 3), Is.EqualTo(35000));

            var state = sampleRun();
            state.Staff["developer"] = 2;
            Assert.That(TycoonRules.StaffDonationMult(state), Is.EqualTo(1.10).Within(1e-9));
            Assert.That(TycoonRules.DonationRate(state), Is.EqualTo(3.950995 * 1.10).Within(1e-5));
            Assert.That(TycoonRules.MaxDonationRate(state), Is.EqualTo(58.227273 * 1.15 * 1.10).Within(1e-5));
            Assert.That(TycoonRules.Spent(state), Is.EqualTo(611.0 + 15000.0));
            state.Staff["community"] = 3;
            state.Staff["moderator"] = 1;
            Assert.That(TycoonRules.StaffInflowMult(state), Is.EqualTo(1.15).Within(1e-9));
            Assert.That(TycoonRules.StaffRegenMult(state), Is.EqualTo(1.25).Within(1e-9));
            Assert.That(TycoonRules.StaffTotal(state), Is.EqualTo(6));

            state = new TycoonState { Donations = 6000 };
            Assert.That(TycoonRules.Hire(state, "developer"), Is.True);
            Assert.That((state.StaffCount("developer"), state.Donations), Is.EqualTo((1, 1000.0)));
            Assert.That(TycoonRules.Hire(state, "developer"), Is.False, "the second developer costs 10000");
            Assert.That(TycoonRules.Hire(state, "intern"), Is.False);
            state.Staff["moderator"] = 5;
            state.Donations = 1e9;
            Assert.That(TycoonRules.Hire(state, "moderator"), Is.False, "five per role");

            state = sampleRun();
            state.Staff["community"] = 1;
            state.Quest = 7;
            state.RunTotal = 4_000_000;
            state.LifetimeTotal = 4_000_000;
            TycoonRules.Prestige(state);
            Assert.That((state.StaffCount("community"), state.Quest), Is.EqualTo((0, 7)), "the team goes, the quest progress stays");
        }

        [Test]
        public void TestSaveRoundTrip()
        {
            var state = sampleRun();
            state.Layout.Add(new TycoonPlacement { X = 4, Y = 2, Key = "coffee" });
            string json = JsonConvert.SerializeObject(state);
            Assert.That(json, Does.Contain("\"lifetime_total\""));
            Assert.That(json, Does.Contain("\"marketing\":{\"discord\":3"));
            Assert.That(json, Does.Contain("\"staff\":{\"moderator\":0,\"developer\":0,\"community\":0}"));
            Assert.That(json, Does.Contain("\"quest\":0"));

            var back = JsonConvert.DeserializeObject<TycoonState>(json)!;
            Assert.That(back.Players, Is.EqualTo(state.Players));
            Assert.That(back.Features, Is.EqualTo(new[] { "website" }));
            Assert.That(back.MarketingCount("reddit"), Is.EqualTo(0));
            Assert.That(json, Does.Contain("\"layout\":[{\"x\":4,\"y\":2,\"k\":\"coffee\"}]"));
            Assert.That(back.Layout.Single().Key, Is.EqualTo("coffee"));
        }
    }
}
