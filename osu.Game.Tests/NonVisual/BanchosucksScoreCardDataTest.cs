// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Share;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: the texts on the score card.
    /// </summary>
    [TestFixture]
    public class BanchosucksScoreCardDataTest
    {
        private static ScoreInfo createScore()
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);

            score.BeatmapInfo!.Metadata.Title = "Some Song";
            score.BeatmapInfo.Metadata.TitleUnicode = "ある曲";
            score.BeatmapInfo.Metadata.Artist = "Some Artist";
            score.BeatmapInfo.Metadata.ArtistUnicode = string.Empty;
            score.BeatmapInfo.Metadata.Author.Username = "Some Mapper";
            score.BeatmapInfo.DifficultyName = "Insane";
            score.BeatmapInfo.StarRating = 4.5678;

            score.Mods = new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.3 } }, new OsuModHidden(), new OsuModHardRock() };
            score.Accuracy = 0.987654;
            score.MaxCombo = 1234;
            score.TotalScore = 1234567;
            score.Rank = ScoreRank.SH;
            score.Date = new DateTimeOffset(2026, 11, 23, 18, 4, 5, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 23, 18, 4, 5)));
            score.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 1500,
                [HitResult.Ok] = 12,
                [HitResult.Meh] = 3,
                [HitResult.Miss] = 1,
                [HitResult.LargeTickHit] = 80,
                [HitResult.LargeBonus] = 7,
            };
            score.MaximumStatistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 1516,
                [HitResult.LargeTickHit] = 84,
                [HitResult.LargeBonus] = 9,
            };
            score.PP = null;

            return score;
        }

        [Test]
        public void TestEnglishCard()
        {
            var data = BanchosucksScoreCardData.From(createScore());

            Assert.That(data.Title, Is.EqualTo("Some Song"));
            Assert.That(data.TitleUnicode, Is.EqualTo("ある曲"));
            Assert.That(data.Artist, Is.EqualTo("Some Artist"));
            Assert.That(data.ArtistUnicode, Is.EqualTo("Some Artist"), "falls back to the romanised artist");
            Assert.That(data.Difficulty, Is.EqualTo("Insane"));
            Assert.That(data.Mapper, Is.EqualTo("Some Mapper"));
            Assert.That(data.Player, Is.EqualTo("peppy"));
            Assert.That(data.Rank, Is.EqualTo(ScoreRank.SH));
            Assert.That(data.AccuracyText, Is.EqualTo("98.76%"), "never rounded up");
            Assert.That(data.ComboText, Is.EqualTo("1,234x"));
            Assert.That(data.MaxComboText, Is.EqualTo("/ 1,600x"));
            Assert.That(data.ScoreText, Is.EqualTo("1,234,567"));
            Assert.That(data.DateText, Is.EqualTo("2026-11-23 18:04"));
            Assert.That(data.RulesetName, Is.EqualTo(new OsuRuleset().RulesetInfo.Name));
            Assert.That(data.Labels.Accuracy, Is.EqualTo("ACCURACY"));
        }

        [Test]
        public void TestGermanCard()
        {
            var data = BanchosucksScoreCardData.From(createScore(), german: true);

            Assert.That(data.AccuracyText, Is.EqualTo("98,76%"));
            Assert.That(data.ComboText, Is.EqualTo("1.234x"));
            Assert.That(data.MaxComboText, Is.EqualTo("/ 1.600x"));
            Assert.That(data.ScoreText, Is.EqualTo("1.234.567"));
            Assert.That(data.DateText, Is.EqualTo("23.11.2026 18:04"));
            Assert.That(data.Labels.Accuracy, Is.EqualTo("GENAUIGKEIT"));
            Assert.That(data.Labels.PlayedBy, Is.EqualTo("gespielt von"));
        }

        [Test]
        public void TestMods()
        {
            var data = BanchosucksScoreCardData.From(createScore());

            Assert.That(data.Mods.Select(m => m.Acronym), Is.EquivalentTo(new[] { "DT", "HD", "HR" }));
            Assert.That(data.Mods.Single(m => m.Acronym == "DT").Text, Is.EqualTo("DT 1.30x"), "a changed rate is on the card");
            Assert.That(data.Mods.Single(m => m.Acronym == "HD").Text, Is.EqualTo("HD"));
            Assert.That(data.Mods.Single(m => m.Acronym == "HR").Type, Is.EqualTo(ModType.DifficultyIncrease));

            var noMod = createScore();
            noMod.Mods = Array.Empty<Mod>();

            Assert.That(BanchosucksScoreCardData.From(noMod).Mods, Is.Empty);
        }

        [Test]
        public void TestHitCounts()
        {
            var data = BanchosucksScoreCardData.From(createScore());

            Assert.That(data.Hits.Select(h => h.Result), Is.EquivalentTo(new[] { HitResult.Great, HitResult.Ok, HitResult.Meh, HitResult.Miss, HitResult.LargeTickHit }),
                "judgements and slider ticks, no spinner bonus");
            Assert.That(data.Hits.Single(h => h.Result == HitResult.Great).Count, Is.EqualTo("1,500"));
            Assert.That(data.Hits.Single(h => h.Result == HitResult.Miss).Count, Is.EqualTo("1"));
            Assert.That(data.Hits.Single(h => h.Result == HitResult.LargeTickHit).Count, Is.EqualTo("80/84"), "ticks are shown as hit out of possible");
            Assert.That(data.Hits[0].Result, Is.EqualTo(HitResult.Great), "best judgement first");
            Assert.That(data.Hits.All(h => !string.IsNullOrEmpty(h.Name.ToString())), Is.True);
        }

        [Test]
        public void TestStarRating()
        {
            Assert.That(BanchosucksScoreCardData.From(createScore()).StarText, Is.EqualTo("4.56"), "the stored rating, cut off like everywhere in the game");
            Assert.That(BanchosucksScoreCardData.From(createScore(), starRating: 6.789).StarText, Is.EqualTo("6.78"), "the rating with mods wins");
            Assert.That(BanchosucksScoreCardData.From(createScore(), starRating: 6.789, german: true).StarText, Is.EqualTo("6,78"));

            var unknown = createScore();
            unknown.BeatmapInfo!.StarRating = -1;

            Assert.That(BanchosucksScoreCardData.From(unknown).StarText, Is.Empty);
        }

        [Test]
        public void TestPerformance()
        {
            Assert.That(BanchosucksScoreCardData.From(createScore()).PpText, Is.Null, "unknown pp is left off the card");
            Assert.That(BanchosucksScoreCardData.From(createScore(), pp: 321.5).PpText, Is.EqualTo("322"));

            var withPp = createScore();
            withPp.PP = 1234.4;

            Assert.That(BanchosucksScoreCardData.From(withPp, pp: 5).PpText, Is.EqualTo("1,234"), "the score's own value wins over the local calculation");
            Assert.That(BanchosucksScoreCardData.From(withPp, german: true).PpText, Is.EqualTo("1.234"));
        }

        [Test]
        public void TestUnknownMaxCombo()
        {
            var score = createScore();
            score.MaximumStatistics = new Dictionary<HitResult, int>();

            Assert.That(BanchosucksScoreCardData.From(score).MaxComboText, Is.Empty);
        }

        [Test]
        public void TestFileName()
        {
            var score = createScore();
            score.BeatmapInfo!.Metadata.Title = "What? Why: \"this\"";

            var data = BanchosucksScoreCardData.From(score);

            Assert.That(data.FileName, Is.EqualTo("peppy - What_ Why_ _this_ [Insane]"));
        }
    }
}
