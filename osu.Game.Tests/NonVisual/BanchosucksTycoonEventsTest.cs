// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Graphics;
using osu.Game.Screens.Banchosucks.Tycoon;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Every event of the tycoon can be rolled and applied, with and without the protecting features, without
    /// breaking the state: reputation stays within 0..100, players and donations never go negative.
    /// </summary>
    [TestFixture]
    public class BanchosucksTycoonEventsTest
    {
        private static TycoonState make(bool protectedServer)
        {
            var state = new TycoonState
            {
                Hardware = 2,
                Players = 80,
                Ruf = 70,
                Donations = 5000,
                RunTotal = 20000,
                LifetimeTotal = 20000,
            };
            state.Marketing["discord"] = 5;

            if (protectedServer)
                state.Features.AddRange(new[] { "anticheat", "backups", "cloudflare", "tests", "lazer" });

            return state;
        }

        private static void assertSane(TycoonState state, string context)
        {
            Assert.That(state.Ruf, Is.InRange(0, 100), context);
            Assert.That(state.Players, Is.GreaterThanOrEqualTo(0), context);
            Assert.That(state.Donations, Is.GreaterThanOrEqualTo(0), context);
            Assert.That(state.LifetimeTotal, Is.GreaterThanOrEqualTo(state.RunTotal), context);
        }

        [TestCase(false, 8)]
        [TestCase(true, 9)]
        public void TestEveryEventAppliesCleanly(bool protectedServer, int distinctEvents)
        {
            var colours = new OsuColour();
            var rng = new Random(42);
            var seen = new HashSet<string>();

            for (int i = 0; i < 400; i++)
            {
                var ev = TycoonEvents.Roll(make(protectedServer), rng, colours);
                seen.Add(ev.Key);

                Assert.That(ev.Title.ToString(), Is.Not.Empty, ev.Key);
                Assert.That(ev.Text.ToString(), Is.Not.Empty, ev.Key);
                Assert.That(ev.Choices.Count > 0 || ev.Auto != null || ev.Crisis != null, Is.True, $"{ev.Key} has neither choices nor an effect");

                if (ev.Crisis != null)
                {
                    foreach (double severity in new[] { 1.0, 0.5 })
                    {
                        var state = make(protectedServer);
                        var boosts = new TycoonBoosts();
                        Assert.That(ev.Crisis(state, boosts, severity).ToString(), Is.Not.Empty, ev.Key);
                        assertSane(state, $"{ev.Key} crisis {severity}");
                    }
                }

                if (ev.Auto != null)
                {
                    var state = make(protectedServer);
                    var boosts = new TycoonBoosts();
                    Assert.That(ev.Auto(state, boosts).ToString(), Is.Not.Empty, ev.Key);
                    assertSane(state, $"{ev.Key} auto");
                    boosts.Tick(1000);
                    Assert.That(boosts.Inflow, Is.EqualTo(1));
                    Assert.That(boosts.Donation, Is.EqualTo(1));
                }

                foreach (var choice in ev.Choices)
                {
                    var state = make(protectedServer);
                    Assert.That(choice.Apply(state, new TycoonBoosts()).ToString(), Is.Not.Empty, ev.Key);
                    assertSane(state, $"{ev.Key} {choice.Label}");
                }
            }

            Assert.That(seen.Count, Is.EqualTo(distinctEvents), string.Join(", ", seen));
        }

        [Test]
        public void TestProtectionTurnsBadEventsHarmless()
        {
            var colours = new OsuColour();
            var rng = new Random(7);

            for (int i = 0; i < 400; i++)
            {
                var state = make(true);
                var ev = TycoonEvents.Roll(state, rng, colours);

                if (ev.Key is not ("cheaters" or "ddos" or "dbcrash" or "deploy" or "medal")) continue;

                Assert.That(ev.Choices, Is.Empty, $"{ev.Key} should not ask with protection in place");
                var boosts = new TycoonBoosts();
                ev.Auto!(state, boosts);
                Assert.That(state.Players, Is.GreaterThanOrEqualTo(80), ev.Key);
                Assert.That(state.Ruf, Is.GreaterThanOrEqualTo(70), ev.Key);
                Assert.That(state.Donations, Is.GreaterThanOrEqualTo(5000), ev.Key);
                Assert.That(boosts.Donation, Is.GreaterThanOrEqualTo(1), ev.Key);
            }
        }
    }
}
