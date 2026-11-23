// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;
using ModSummary = osu.Game.Scoring.BanchosucksReplayComparison.ModSummary;
using Verdict = osu.Game.Scoring.BanchosucksReplayComparison.Verdict;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: which two replays may be shown on one playfield (replay comparison).
    /// </summary>
    [TestFixture]
    public class BanchosucksReplayComparisonTest
    {
        private static ModSummary[] mods(params Mod[] mods) => mods.Select(m => new ModSummary(m)).ToArray();

        private static ModSummary withSettings(string acronym, params (string key, string value)[] settings) =>
            new ModSummary(acronym, settings.ToDictionary(s => s.key, s => s.value));

        private static BanchosucksReplayComparison.Result check(Mod[] watched, Mod[] ghost) => BanchosucksReplayComparison.CheckMods(mods(watched), mods(ghost));

        [Test]
        public void TestSameModsAreCompatible()
        {
            Assert.That(check(new Mod[] { }, new Mod[] { }).Compatible, Is.True);
            Assert.That(check(new Mod[] { new OsuModHardRock(), new OsuModHidden() }, new Mod[] { new OsuModHidden(), new OsuModHardRock() }).Compatible, Is.True);
            Assert.That(check(new Mod[] { new OsuModDoubleTime() }, new Mod[] { new OsuModDoubleTime() }).Compatible, Is.True);
        }

        [Test]
        public void TestModsThatLeaveThePlayfieldAloneMayDiffer()
        {
            var plain = new Mod[] { };

            foreach (Mod mod in new Mod[]
                     {
                         new OsuModHidden(), new OsuModFlashlight(), new OsuModNoFail(), new OsuModSuddenDeath(), new OsuModPerfect(), new OsuModClassic(),
                         new OsuModRelax(), new OsuModAutopilot(), new OsuModSpunOut(), new OsuModTouchDevice(), new OsuModBlinds(), new OsuModTraceable(), new OsuModMuted(),
                     })
            {
                Assert.That(check(new[] { mod }, plain).Compatible, Is.True, $"{mod.Acronym} in the watched score only");
                Assert.That(check(plain, new[] { mod }).Compatible, Is.True, $"{mod.Acronym} in the ghost score only");
            }
        }

        [Test]
        public void TestHardRockAgainstNoHardRockIsRefused()
        {
            var result = check(new Mod[] { new OsuModHardRock() }, new Mod[] { });

            Assert.That(result.Compatible, Is.False);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.ModInOneScoreOnly));
            Assert.That(result.Acronym, Is.EqualTo("HR"));

            result = check(new Mod[] { new OsuModHidden() }, new Mod[] { new OsuModHidden(), new OsuModHardRock() });

            Assert.That(result.Verdict, Is.EqualTo(Verdict.ModInOneScoreOnly), "also when the ghost is the one with the mod");
            Assert.That(result.Acronym, Is.EqualTo("HR"));
        }

        [Test]
        public void TestOtherPlayfieldModsInOneScoreOnlyAreRefused()
        {
            foreach (Mod mod in new Mod[] { new OsuModEasy(), new OsuModMirror(), new OsuModDifficultyAdjust(), new OsuModDepth(), new OsuModWiggle(), new OsuModTransform() })
            {
                var result = check(new[] { mod }, new Mod[] { });

                Assert.That(result.Verdict, Is.EqualTo(Verdict.ModInOneScoreOnly), mod.Acronym);
                Assert.That(result.Acronym, Is.EqualTo(mod.Acronym));
            }
        }

        [Test]
        public void TestDifferentRateIsRefused()
        {
            Assert.That(check(new Mod[] { new OsuModDoubleTime() }, new Mod[] { }).Verdict, Is.EqualTo(Verdict.RateDiffers));
            Assert.That(check(new Mod[] { }, new Mod[] { new OsuModHalfTime() }).Verdict, Is.EqualTo(Verdict.RateDiffers));
            Assert.That(check(new Mod[] { new OsuModDoubleTime() }, new Mod[] { new OsuModHalfTime() }).Verdict, Is.EqualTo(Verdict.RateDiffers));

            var result = check(new Mod[] { new OsuModDoubleTime() }, new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.3 } } });

            Assert.That(result.Verdict, Is.EqualTo(Verdict.RateDiffers), "DT at 1.5x against DT at 1.3x");
            Assert.That(result.Acronym, Is.EqualTo("DT"));
        }

        [Test]
        public void TestSameRateUnderDifferentNamesIsCompatible()
        {
            Assert.That(check(new Mod[] { new OsuModDoubleTime() }, new Mod[] { new OsuModNightcore() }).Compatible, Is.True, "DT and NC are the same speed");
            Assert.That(check(new Mod[] { new OsuModHalfTime() }, new Mod[] { new OsuModDaycore() }).Compatible, Is.True);
            Assert.That(check(
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.3 } } },
                new Mod[] { new OsuModNightcore { SpeedChange = { Value = 1.3 } } }).Compatible, Is.True);

            // the pitch setting changes the sound, not the speed
            Assert.That(check(
                new Mod[] { new OsuModDoubleTime { AdjustPitch = { Value = true } } },
                new Mod[] { new OsuModDoubleTime() }).Compatible, Is.True);
        }

        [Test]
        public void TestRate()
        {
            Assert.That(BanchosucksReplayComparison.Rate(mods()), Is.EqualTo(1));
            Assert.That(BanchosucksReplayComparison.Rate(mods(new OsuModDoubleTime())), Is.EqualTo(1.5));
            Assert.That(BanchosucksReplayComparison.Rate(mods(new OsuModDaycore())), Is.EqualTo(0.75));
            Assert.That(BanchosucksReplayComparison.Rate(mods(new OsuModDoubleTime { SpeedChange = { Value = 1.25 } }, new OsuModHidden())), Is.EqualTo(1.25).Within(0.0001));
        }

        [Test]
        public void TestRampingRateMustBeTheSame()
        {
            Assert.That(check(new Mod[] { new ModWindUp() }, new Mod[] { }).Verdict, Is.EqualTo(Verdict.RateDiffers));
            Assert.That(check(new Mod[] { new ModWindUp() }, new Mod[] { new ModWindDown() }).Verdict, Is.EqualTo(Verdict.RateDiffers));
            Assert.That(check(new Mod[] { new ModWindUp() }, new Mod[] { new ModWindUp() }).Compatible, Is.True);
            Assert.That(check(new Mod[] { new ModWindUp() }, new Mod[] { new ModWindUp { FinalRate = { Value = 1.8 } } }).Verdict, Is.EqualTo(Verdict.RateDiffers));
        }

        [Test]
        public void TestDifficultyAdjustNeedsTheSameSettings()
        {
            var smallCircles = new OsuModDifficultyAdjust { CircleSize = { Value = 7 } };
            var sameCircles = new OsuModDifficultyAdjust { CircleSize = { Value = 7 } };
            var bigCircles = new OsuModDifficultyAdjust { CircleSize = { Value = 2 } };

            Assert.That(check(new Mod[] { smallCircles }, new Mod[] { sameCircles }).Compatible, Is.True);

            var result = check(new Mod[] { smallCircles }, new Mod[] { bigCircles });

            Assert.That(result.Verdict, Is.EqualTo(Verdict.ModSettingsDiffer));
            Assert.That(result.Acronym, Is.EqualTo("DA"));
        }

        [Test]
        public void TestRandomNeedsTheSameSeed()
        {
            Assert.That(check(new Mod[] { new OsuModRandom { Seed = { Value = 42 } } }, new Mod[] { new OsuModRandom { Seed = { Value = 42 } } }).Compatible, Is.True);
            Assert.That(check(new Mod[] { new OsuModRandom { Seed = { Value = 42 } } }, new Mod[] { new OsuModRandom { Seed = { Value = 43 } } }).Verdict, Is.EqualTo(Verdict.ModSettingsDiffer));

            var result = check(new Mod[] { new OsuModRandom() }, new Mod[] { new OsuModRandom() });

            Assert.That(result.Verdict, Is.EqualTo(Verdict.UnpredictableMod), "without the seed nobody knows whether the maps were the same");
            Assert.That(result.Acronym, Is.EqualTo("RD"));
        }

        [Test]
        public void TestModsThatFollowThePlayAreAlwaysRefused()
        {
            foreach (Mod mod in new Mod[] { new ModAdaptiveSpeed(), new OsuModMagnetised(), new OsuModRepel() })
            {
                Assert.That(check(new[] { mod }, new[] { mod }).Verdict, Is.EqualTo(Verdict.UnpredictableMod), $"{mod.Acronym} in both");
                Assert.That(check(new[] { mod }, new Mod[] { }).Verdict, Is.EqualTo(Verdict.UnpredictableMod), $"{mod.Acronym} in one");
                Assert.That(check(new Mod[] { }, new[] { mod }).Acronym, Is.EqualTo(mod.Acronym));
            }
        }

        [Test]
        public void TestSettingsAreComparedByValue()
        {
            var a = new[] { withSettings("DA", ("circle_size", "7"), ("approach_rate", "9.5")) };
            var b = new[] { withSettings("DA", ("approach_rate", "9.50"), ("circle_size", "7.0")) };
            var c = new[] { withSettings("DA", ("circle_size", "7")) };

            Assert.That(BanchosucksReplayComparison.CheckMods(a, b).Compatible, Is.True, "order and number format do not matter");
            Assert.That(BanchosucksReplayComparison.CheckMods(a, c).Verdict, Is.EqualTo(Verdict.ModSettingsDiffer), "a missing setting is a different setting");
        }

        [Test]
        public void TestRulesetAndBeatmap()
        {
            var none = new List<ModSummary>();

            Assert.That(BanchosucksReplayComparison.Check("osu", "osu", "abc", "abc", 1, 1, none, none).Compatible, Is.True);
            Assert.That(BanchosucksReplayComparison.Check("mania", "mania", "abc", "abc", 1, 1, none, none).Verdict, Is.EqualTo(Verdict.UnsupportedRuleset));
            Assert.That(BanchosucksReplayComparison.Check("osu", "taiko", "abc", "abc", 1, 1, none, none).Verdict, Is.EqualTo(Verdict.UnsupportedRuleset));
            Assert.That(BanchosucksReplayComparison.Check("osu", "osu", "abc", "abd", 1, 1, none, none).Verdict, Is.EqualTo(Verdict.DifferentBeatmap), "same id, different version");
            Assert.That(BanchosucksReplayComparison.Check("osu", "osu", "", "", 1, 2, none, none).Verdict, Is.EqualTo(Verdict.DifferentBeatmap));
            Assert.That(BanchosucksReplayComparison.Check("osu", "osu", "", "abc", 7, 7, none, none).Compatible, Is.True, "ids when a hash is missing");
            Assert.That(BanchosucksReplayComparison.Check("osu", "osu", "", "", 0, 0, none, none).Verdict, Is.EqualTo(Verdict.DifferentBeatmap), "nothing known about the maps");
        }

        private static (ScoreInfo watched, ScoreInfo ghost) createScores()
        {
            BeatmapInfo beatmap = TestResources.CreateTestBeatmapSetInfo(1, new[] { new OsuRuleset().RulesetInfo }).Beatmaps.First();
            beatmap.MD5Hash = "md5 of the map";

            var watched = TestResources.CreateTestScoreInfo(beatmap);
            var ghost = TestResources.CreateTestScoreInfo(beatmap);

            watched.Mods = new Mod[] { new OsuModHidden() };
            ghost.Mods = new Mod[] { new OsuModNoFail() };

            return (watched, ghost);
        }

        [Test]
        public void TestScores()
        {
            var (watched, ghost) = createScores();

            Assert.That(BanchosucksReplayComparison.Check(watched, ghost).Compatible, Is.True);

            ghost.Mods = new Mod[] { new OsuModHardRock() };
            Assert.That(BanchosucksReplayComparison.Check(watched, ghost), Is.EqualTo(new BanchosucksReplayComparison.Result(Verdict.ModInOneScoreOnly, "HR")));
        }

        [Test]
        public void TestScoreFromAnOlderVersionOfTheMap()
        {
            var (watched, ghost) = createScores();

            // local scores remember the hash of the map they were set on
            ghost.BeatmapHash = "hash of an older version";

            Assert.That(BanchosucksReplayComparison.Check(watched, ghost).Verdict, Is.EqualTo(Verdict.DifferentBeatmap));
        }

        [Test]
        public void TestOnlineScoreAgainstLocalScore()
        {
            var (watched, ghost) = createScores();

            // a score from the online leaderboard does not carry the hash it was set on
            ghost.BeatmapHash = string.Empty;

            Assert.That(BanchosucksReplayComparison.Check(watched, ghost).Compatible, Is.True);
        }

        [Test]
        public void TestScoresOfOtherRulesets()
        {
            var (watched, ghost) = createScores();
            ghost.Ruleset = new ManiaRuleset().RulesetInfo;
            ghost.Mods = new Mod[] { };

            Assert.That(BanchosucksReplayComparison.Check(watched, ghost).Verdict, Is.EqualTo(Verdict.UnsupportedRuleset));
            Assert.That(BanchosucksReplayCompare.CanCompare(ghost), Is.False, "the menu entry is hidden for other rulesets");
        }

        [Test]
        public void TestWhichScoresGetTheMenuEntry()
        {
            var (score, _) = createScores();

            score.OnlineID = -1;
            Assert.That(BanchosucksReplayCompare.CanCompare(score), Is.False, "neither a local replay nor one on the server");

            score.OnlineID = 5;
            score.HasOnlineReplay = true;
            Assert.That(BanchosucksReplayCompare.CanCompare(score), Is.True, "replay on the server");

            score.HasOnlineReplay = false;
            Assert.That(BanchosucksReplayCompare.CanCompare(score), Is.False);
        }

        [Test]
        public void TestEveryRefusalHasAMessageNamingTheMod()
        {
            foreach (Verdict verdict in System.Enum.GetValues<Verdict>())
            {
                string message = BanchosucksReplayCompare.Describe(new BanchosucksReplayComparison.Result(verdict, "HR")).ToString();

                Assert.That(message, Is.Not.Empty, verdict.ToString());

                if (verdict is Verdict.ModInOneScoreOnly or Verdict.ModSettingsDiffer or Verdict.RateDiffers or Verdict.UnpredictableMod)
                    Assert.That(message, Does.Contain("HR"), verdict.ToString());
            }
        }
    }
}
