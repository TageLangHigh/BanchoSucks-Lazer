// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.Banchosucks;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksMedalOverviewTest
    {
        // banchosucks_medals/__init__.py, catalogue(): Banchosucks medals first, then by id
        private const string catalogue_json = @"{""medals"": [
            {""id"": 200001, ""name"": ""Lazer Pioneer"", ""description"": ""Dein erster Play auf Banchosucks Lazer."", ""slug"": ""banchosucks_lazer_first"",
             ""url"": ""https://banchosucks.cc/medals/banchosucks_lazer_first.png"", ""url2x"": ""https://banchosucks.cc/medals/banchosucks_lazer_first@2x.png"", ""banchosucks"": true},
            {""id"": 200009, ""name"": ""Marathon"", ""description"": ""Eine Map mit mindestens 10 Minuten Spielzeit gepasst."", ""slug"": ""banchosucks_marathon"",
             ""url"": ""x"", ""url2x"": ""y"", ""banchosucks"": true},
            {""id"": 200012, ""name"": ""Teamplayer"", ""description"": ""Beim erreichten Community-Ziel der Woche mitgespielt."", ""slug"": ""banchosucks_teamplayer"",
             ""url"": ""x"", ""url2x"": ""y"", ""banchosucks"": true},
            {""id"": 1, ""name"": ""500 Combo"", ""description"": ""500 big ones! You're moving up in the world!"", ""slug"": ""osu-combo-500"", ""url"": ""x"", ""url2x"": ""y"", ""banchosucks"": false},
            {""id"": 3, ""name"": ""1,000 Combo"", ""description"": """", ""slug"": ""osu-combo-1000"", ""url"": ""x"", ""url2x"": ""y"", ""banchosucks"": false},
            {""id"": 100001, ""name"": ""First Anniversary"", ""description"": ""g0v0 turns one."", ""slug"": ""g0v0_first_anniversary"", ""url"": ""x"", ""url2x"": ""y"", ""banchosucks"": false}
        ]}";

        private static BanchosucksMedalCatalogueResponse catalogue => JsonConvert.DeserializeObject<BanchosucksMedalCatalogueResponse>(catalogue_json)!;

        private static readonly DateTimeOffset monday = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void TestCatalogueAsThePluginSendsIt()
        {
            var medals = catalogue.Medals;

            Assert.That(medals, Has.Count.EqualTo(6));
            Assert.That(medals[0].Id, Is.EqualTo(200001));
            Assert.That(medals[0].Name, Is.EqualTo("Lazer Pioneer"));
            Assert.That(medals[0].Description, Is.EqualTo("Dein erster Play auf Banchosucks Lazer."));
            Assert.That(medals[0].Slug, Is.EqualTo("banchosucks_lazer_first"));
            Assert.That(medals[0].Banchosucks, Is.True);
            Assert.That(medals[3].Banchosucks, Is.False);

            Assert.That(JsonConvert.DeserializeObject<BanchosucksMedalCatalogueResponse>(@"{""medals"": []}")!.Medals, Is.Empty);
        }

        [Test]
        public void TestGroupsAndCounts()
        {
            var overview = BanchosucksMedalOverview.Build(catalogue.Medals, new[] { (200009, monday), (1, monday.AddDays(-30)), (200001, monday.AddDays(-3)) });

            Assert.That(overview.AchievementsKnown, Is.True);
            Assert.That(overview.Total, Is.EqualTo(6));
            Assert.That(overview.Achieved, Is.EqualTo(3));
            Assert.That(overview.AchievedOutsideCatalogue, Is.Zero);

            Assert.That(overview.Groups.Select(g => g.Kind), Is.EqualTo(new[] { BanchosucksMedalGroup.Banchosucks, BanchosucksMedalGroup.G0v0, BanchosucksMedalGroup.Osu }));

            var ours = overview.Groups[0];
            Assert.That(ours.Achieved, Is.EqualTo(2));
            // achieved first, the newest in front, then the rest in the catalogue's order
            Assert.That(ours.Medals.Select(m => m.Medal.Id), Is.EqualTo(new[] { 200009, 200001, 200012 }));
            Assert.That(ours.Medals.Select(m => m.Achieved), Is.EqualTo(new[] { true, true, false }));
            Assert.That(ours.Medals[0].AchievedAt, Is.EqualTo(monday));

            Assert.That(overview.Groups[1].Medals.Single().Medal.Slug, Is.EqualTo("g0v0_first_anniversary"));
            Assert.That(overview.Groups[1].Achieved, Is.Zero);

            Assert.That(overview.Groups[2].Medals.Select(m => m.Medal.Id), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(overview.Groups[2].Achieved, Is.EqualTo(1));
        }

        [Test]
        public void TestPlayerWithoutMedals()
        {
            var overview = BanchosucksMedalOverview.Build(catalogue.Medals, Array.Empty<(int, DateTimeOffset)>());

            Assert.That(overview.AchievementsKnown, Is.True);
            Assert.That(overview.Achieved, Is.Zero);
            Assert.That(overview.Total, Is.EqualTo(6));
            Assert.That(overview.Groups.SelectMany(g => g.Medals).All(m => !m.Achieved), Is.True);
            // the catalogue's order is kept
            Assert.That(overview.Groups[0].Medals.Select(m => m.Medal.Id), Is.EqualTo(new[] { 200001, 200009, 200012 }));
        }

        [Test]
        public void TestNothingIsClaimedWhenTheServerSentNoMedalsForThePlayer()
        {
            var overview = BanchosucksMedalOverview.Build(catalogue.Medals, null);

            Assert.That(overview.AchievementsKnown, Is.False);
            Assert.That(overview.Achieved, Is.Zero);
            Assert.That(overview.Total, Is.EqualTo(6));
        }

        [Test]
        public void TestMedalsOutsideTheCatalogueAreOnlyCounted()
        {
            var overview = BanchosucksMedalOverview.Build(catalogue.Medals, new[] { (200001, monday), (999999, monday) });

            Assert.That(overview.Achieved, Is.EqualTo(1));
            Assert.That(overview.AchievedOutsideCatalogue, Is.EqualTo(1));
            Assert.That(overview.Total, Is.EqualTo(6));
        }

        [Test]
        public void TestDuplicatesAndEmptyCatalogue()
        {
            var medals = catalogue.Medals;
            medals.Add(medals[0]);

            Assert.That(BanchosucksMedalOverview.Build(medals, null).Total, Is.EqualTo(6));

            var empty = BanchosucksMedalOverview.Build(null, new[] { (1, monday) });
            Assert.That(empty.Total, Is.Zero);
            Assert.That(empty.Groups, Is.Empty);
            Assert.That(empty.AchievedOutsideCatalogue, Is.EqualTo(1));
        }

        [Test]
        public void TestGroupFollowsTheFlagAndTheSlug()
        {
            Assert.That(BanchosucksMedalOverview.GroupOf(new BanchosucksCatalogueMedal { Slug = "banchosucks_motw" }), Is.EqualTo(BanchosucksMedalGroup.Banchosucks));
            Assert.That(BanchosucksMedalOverview.GroupOf(new BanchosucksCatalogueMedal { Slug = "something", Banchosucks = true }), Is.EqualTo(BanchosucksMedalGroup.Banchosucks));
            Assert.That(BanchosucksMedalOverview.GroupOf(new BanchosucksCatalogueMedal { Slug = "g0v0_first" }), Is.EqualTo(BanchosucksMedalGroup.G0v0));
            Assert.That(BanchosucksMedalOverview.GroupOf(new BanchosucksCatalogueMedal { Slug = "all-secret-jackpot" }), Is.EqualTo(BanchosucksMedalGroup.Osu));
            Assert.That(BanchosucksMedalOverview.GroupOf(new BanchosucksCatalogueMedal()), Is.EqualTo(BanchosucksMedalGroup.Osu));
        }

        [Test]
        public void TestCatalogueIsKeptForTheSession()
        {
            var cache = new BanchosucksMedalCatalogueCache();
            int changes = 0;
            cache.Changed += () => changes++;

            Assert.That(cache.Medals, Is.Null);
            Assert.That(cache.ShouldRefresh(0), Is.True);

            cache.Started(0);
            Assert.That(cache.InFlight, Is.True);
            Assert.That(cache.ShouldRefresh(1), Is.False, "a second profile does not ask again while the first request runs");

            cache.Succeeded(200, catalogue.Medals);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(cache.Medals, Has.Count.EqualTo(6));

            Assert.That(cache.ShouldRefresh(10 * 60_000), Is.False);
            Assert.That(cache.ShouldRefresh(BanchosucksMedalCatalogueCache.REFRESH_INTERVAL), Is.True);
        }

        [Test]
        public void TestFailedCatalogueRequestIsRetriedAfterAMinute()
        {
            var cache = new BanchosucksMedalCatalogueCache();
            int changes = 0;
            cache.Changed += () => changes++;

            cache.Started(0);
            cache.Failed();

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(cache.LastFailed, Is.True);
            Assert.That(cache.Medals, Is.Null);
            Assert.That(cache.ShouldRefresh(30_000), Is.False);
            Assert.That(cache.ShouldRefresh(60_000), Is.True);

            cache.Clear();
            Assert.That(cache.LastFailed, Is.False);
            Assert.That(cache.ShouldRefresh(0), Is.True);
        }
    }
}
