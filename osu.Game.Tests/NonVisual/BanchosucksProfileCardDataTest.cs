// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Banchosucks.Share;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: the texts on the profile card, and the memory for the best play.
    /// </summary>
    [TestFixture]
    public class BanchosucksProfileCardDataTest
    {
        private static APIUser createUser() => new APIUser
        {
            Id = 1001,
            Username = "Lidl",
            CountryCode = CountryCode.DE,
            AvatarUrl = "https://example.invalid/avatar.png",
            Statistics = new UserStatistics
            {
                GlobalRank = 1234,
                CountryRank = 56,
                PP = 5432.6m,
                Accuracy = 98.7654,
                PlayCount = 12345,
                Level = new UserStatistics.LevelInfo { Current = 101, Progress = 45 },
            }
        };

        private static SoloScoreInfo createBest() => new SoloScoreInfo
        {
            PP = 523.4,
            Beatmap = new APIBeatmap
            {
                DifficultyName = "Extra",
                BeatmapSet = new APIBeatmapSet { Title = "Best Song", Artist = "Best Artist" },
            }
        };

        private static readonly DateTimeOffset card_date = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 23, 12, 0, 0)));

        [Test]
        public void TestEnglishCard()
        {
            var data = BanchosucksProfileCardData.From(createUser(), new OsuRuleset().RulesetInfo, createBest(), now: card_date);

            Assert.That(data.Username, Is.EqualTo("Lidl"));
            Assert.That(data.CountryCode, Is.EqualTo(CountryCode.DE));
            Assert.That(data.CountryName, Is.EqualTo("Germany"));
            Assert.That(data.RulesetName, Is.EqualTo(new OsuRuleset().RulesetInfo.Name));
            Assert.That(data.GlobalRankText, Is.EqualTo("#1,234"));
            Assert.That(data.CountryRankText, Is.EqualTo("#56"));
            Assert.That(data.PpText, Is.EqualTo("5,433"));
            Assert.That(data.AccuracyText, Is.EqualTo("98.76%"), "never rounded up");
            Assert.That(data.PlayCountText, Is.EqualTo("12,345"));
            Assert.That(data.LevelText, Is.EqualTo("101"));
            Assert.That(data.LevelProgressText, Is.EqualTo("45%"));
            Assert.That(data.BestTitle, Is.EqualTo("Best Artist - Best Song [Extra]"));
            Assert.That(data.BestPpText, Is.EqualTo("523pp"));
            Assert.That(data.AvatarUrl, Is.EqualTo("https://example.invalid/avatar.png"));
            Assert.That(data.DateText, Is.EqualTo("2026-11-23"));
            Assert.That(data.Labels.GlobalRank, Is.EqualTo("GLOBAL"));
            Assert.That(data.FileName, Is.EqualTo("Lidl - osu"));
        }

        [Test]
        public void TestGermanCard()
        {
            var data = BanchosucksProfileCardData.From(createUser(), new OsuRuleset().RulesetInfo, createBest(), german: true, now: card_date);

            Assert.That(data.GlobalRankText, Is.EqualTo("#1.234"));
            Assert.That(data.PpText, Is.EqualTo("5.433"));
            Assert.That(data.AccuracyText, Is.EqualTo("98,76%"));
            Assert.That(data.PlayCountText, Is.EqualTo("12.345"));
            Assert.That(data.DateText, Is.EqualTo("23.11.2026"));
            Assert.That(data.Labels.GlobalRank, Is.EqualTo("WELTWEIT"));
            Assert.That(data.Labels.BestPlay, Is.EqualTo("BESTER SCORE"));
        }

        [Test]
        public void TestUnrankedPlayer()
        {
            var user = createUser();
            user.Statistics = new UserStatistics { PlayCount = 3 };

            var data = BanchosucksProfileCardData.From(user, new OsuRuleset().RulesetInfo);

            Assert.That(data.GlobalRankText, Is.EqualTo("-"));
            Assert.That(data.CountryRankText, Is.EqualTo("-"));
            Assert.That(data.PpText, Is.EqualTo("-"));
            Assert.That(data.PlayCountText, Is.EqualTo("3"));
            Assert.That(data.LevelText, Is.EqualTo("-"));
            Assert.That(data.LevelProgressText, Is.Empty);
            Assert.That(data.BestTitle, Is.Null, "no best play, no line on the card");
            Assert.That(data.BestPpText, Is.Empty);
        }

        [Test]
        public void TestUserWithoutStatisticsOrCountry()
        {
            var data = BanchosucksProfileCardData.From(new APIUser { Id = 5, Username = "new player" }, new ManiaRuleset().RulesetInfo);

            Assert.That(data.Username, Is.EqualTo("new player"));
            Assert.That(data.CountryName, Is.Empty);
            Assert.That(data.GlobalRankText, Is.EqualTo("-"));
            // a user object always has statistics, empty ones in this case
            Assert.That(data.AccuracyText, Is.EqualTo("0.00%"));
            Assert.That(data.PlayCountText, Is.EqualTo("0"));
            Assert.That(data.PpText, Is.EqualTo("-"));
            Assert.That(data.LevelText, Is.EqualTo("-"));
            Assert.That(data.AvatarUrl, Is.Empty);
            Assert.That(data.FileName, Is.EqualTo("new player - mania"));
        }

        [Test]
        public void TestBestPlayWithoutPpOrSet()
        {
            var best = createBest();
            best.PP = null;

            var data = BanchosucksProfileCardData.From(createUser(), new OsuRuleset().RulesetInfo, best);

            Assert.That(data.BestTitle, Is.EqualTo("Best Artist - Best Song [Extra]"));
            Assert.That(data.BestPpText, Is.Empty);

            best.Beatmap!.BeatmapSet = null;

            Assert.That(BanchosucksProfileCardData.From(createUser(), new OsuRuleset().RulesetInfo, best).BestTitle, Is.EqualTo("[Extra]"));
        }

        [Test]
        public void TestBestScoreMemory()
        {
            BanchosucksProfileBestScores.Clear();

            var osuBest = createBest();
            var maniaBest = createBest();

            Assert.That(BanchosucksProfileBestScores.Get(1001, "osu"), Is.Null);

            BanchosucksProfileBestScores.Remember(1001, "osu", osuBest);
            BanchosucksProfileBestScores.Remember(1001, "mania", maniaBest);
            BanchosucksProfileBestScores.Remember(2002, "osu", null);

            Assert.That(BanchosucksProfileBestScores.Get(1001, "osu"), Is.SameAs(osuBest));
            Assert.That(BanchosucksProfileBestScores.Get(1001, "mania"), Is.SameAs(maniaBest), "one entry per ruleset");
            Assert.That(BanchosucksProfileBestScores.Get(2002, "osu"), Is.Null, "a player without scores");
            Assert.That(BanchosucksProfileBestScores.Get(3003, "osu"), Is.Null);

            // a newer list replaces the old entry
            var newer = createBest();
            BanchosucksProfileBestScores.Remember(1001, "osu", newer);
            Assert.That(BanchosucksProfileBestScores.Get(1001, "osu"), Is.SameAs(newer));
        }

        [Test]
        public void TestBestScoreMemoryIsBounded()
        {
            BanchosucksProfileBestScores.Clear();

            var first = createBest();
            BanchosucksProfileBestScores.Remember(1, "osu", first);

            for (int i = 2; i <= 200; i++)
                BanchosucksProfileBestScores.Remember(i, "osu", createBest());

            Assert.That(BanchosucksProfileBestScores.Get(1, "osu"), Is.Null, "the oldest profiles are forgotten");
            Assert.That(BanchosucksProfileBestScores.Get(200, "osu"), Is.Not.Null);

            BanchosucksProfileBestScores.Clear();
        }
    }
}
