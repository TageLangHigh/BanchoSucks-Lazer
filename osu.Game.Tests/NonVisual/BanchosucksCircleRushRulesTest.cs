// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Banchosucks.CircleRush;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The numbers here are the same as in the server plugin's test_rules.py: both sides must agree on every score.
    /// </summary>
    [TestFixture]
    public class BanchosucksCircleRushRulesTest
    {
        [Test]
        public void TestStartValuesAreTheOsuApproachRates()
        {
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Easy, CircleRushMods.None, 0), Is.EqualTo(750));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 0), Is.EqualTo(600));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Hard, CircleRushMods.None, 0), Is.EqualTo(450));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Extreme, CircleRushMods.None, 0), Is.EqualTo(300));
            Assert.That(CircleRushRules.BaseInterval(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 0), Is.EqualTo(650));
            Assert.That(CircleRushRules.Radius(CircleRushDifficulty.Easy, CircleRushMods.None), Is.EqualTo(50));
            Assert.That(CircleRushRules.Radius(CircleRushDifficulty.Extreme, CircleRushMods.None), Is.EqualTo(28));
        }

        [Test]
        public void TestRampAndSqueeze()
        {
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 720), Is.EqualTo(596));
            Assert.That(CircleRushRules.BaseInterval(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 720), Is.EqualTo(644));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, CircleRushRules.RAMP_MINUTE), Is.EqualTo(360));
            Assert.That(CircleRushRules.BaseInterval(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, CircleRushRules.RAMP_MINUTE), Is.EqualTo(300));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 59_000), Is.EqualTo(360));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Endless, CircleRushDifficulty.Easy, CircleRushMods.None, CircleRushRules.RAMP_ENDLESS), Is.EqualTo(450));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Endless, CircleRushDifficulty.Easy, CircleRushMods.None, CircleRushRules.RAMP_ENDLESS + 25_000), Is.EqualTo(350));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Endless, CircleRushDifficulty.Easy, CircleRushMods.None, CircleRushRules.RAMP_ENDLESS + 500_000), Is.EqualTo(225));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Endless, CircleRushDifficulty.Extreme, CircleRushMods.None, 1_000_000), Is.EqualTo(CircleRushRules.MIN_LIFETIME));
        }

        [Test]
        public void TestModsChangeSizeAndSpeed()
        {
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Hard, CircleRushMods.Easy, 0), Is.EqualTo(720));
            Assert.That(CircleRushRules.Lifetime(CircleRushMode.Minute, CircleRushDifficulty.Hard, CircleRushMods.HardRock, 0), Is.EqualTo(337));
            Assert.That(CircleRushRules.Radius(CircleRushDifficulty.Medium, CircleRushMods.Easy), Is.EqualTo(58));
            Assert.That(CircleRushRules.Radius(CircleRushDifficulty.Medium, CircleRushMods.HardRock), Is.EqualTo(33));
            Assert.That(CircleRushRules.ModsValid(CircleRushMods.Hidden | CircleRushMods.HardRock | CircleRushMods.Flashlight), Is.True);
            Assert.That(CircleRushRules.ModsValid(CircleRushMods.Easy | CircleRushMods.HardRock), Is.False);
            Assert.That(CircleRushRules.ModsAcronyms(CircleRushMods.Hidden | CircleRushMods.HardRock), Is.EqualTo("HDHR"));
            Assert.That(CircleRushRules.ModMultiplier(CircleRushMods.Easy), Is.EqualTo(1.2).Within(0.0001));
        }

        [Test]
        public void TestHitScoreJudgementAndMultipliers()
        {
            Assert.That(CircleRushRules.HitScore(0, 600, 0), Is.EqualTo(200));
            Assert.That(CircleRushRules.HitScore(600, 600, 0), Is.EqualTo(100));
            Assert.That(CircleRushRules.HitScore(300, 600, 100), Is.EqualTo(450));
            Assert.That(CircleRushRules.HitScore(300, 600, 250), Is.EqualTo(450));
            Assert.That(CircleRushRules.Judgement(200, 600), Is.EqualTo(CircleRushJudgement.Perfect));
            Assert.That(CircleRushRules.Judgement(300, 600), Is.EqualTo(CircleRushJudgement.Good));
            Assert.That(CircleRushRules.Judgement(421, 600), Is.EqualTo(CircleRushJudgement.Late));
            Assert.That(CircleRushRules.ApplyMods(1000, CircleRushMods.Hidden), Is.EqualTo(1060));
            Assert.That(CircleRushRules.ApplyMods(1000, CircleRushMods.Hidden | CircleRushMods.HardRock), Is.EqualTo(1123));
            Assert.That(CircleRushRules.ApplyMods(1000, CircleRushMods.Easy), Is.EqualTo(1200));
            Assert.That(CircleRushRules.ApplyMods(1000, CircleRushMods.Easy | CircleRushMods.Hidden | CircleRushMods.Flashlight), Is.EqualTo(1424));
        }

        [Test]
        public void TestScheduleIsSeededAndGrouped()
        {
            var rng = new CircleRushXorshift(1);
            Assert.That(new[] { rng.Next(), rng.Next(), rng.Next(), rng.Next() }, Is.EqualTo(new uint[] { 270369, 67634689, 2647435461, 307599695 }));

            var schedule = new CircleRushSchedule(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 1);
            Assert.That(Enumerable.Range(0, 10).Select(schedule.SpawnTime), Is.EqualTo(new[] { 0, 390, 1037, 1325, 1612, 2248, 2531, 2813, 3094, 3374 }));
            Assert.That(Enumerable.Range(0, 6).Select(i => (schedule.Circle(i).GroupSize, schedule.Circle(i).GroupIndex)),
                Is.EqualTo(new[] { (2, 0), (2, 1), (3, 0), (3, 1), (3, 2), (5, 0) }));
            Assert.That(Enumerable.Range(0, 4).Select(schedule.Lifetime), Is.EqualTo(new[] { 600, 598, 594, 593 }));
            Assert.That(schedule.Deadline(2), Is.EqualTo(1631));

            var other = new CircleRushSchedule(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 12345);
            Assert.That(Enumerable.Range(0, 4).Select(other.SpawnTime), Is.EqualTo(new[] { 0, 650, 1295, 1934 }));
        }

        [Test]
        public void TestScriptedMinuteRun()
        {
            var run = new CircleRushRun(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.None, 1);

            Assert.That(run.Hit(0, 200), Is.EqualTo(CircleRushJudgement.Perfect));
            Assert.That(run.BaseScore, Is.EqualTo(166));
            Assert.That(run.Schedule.SpawnTime(1), Is.EqualTo(390));
            Assert.That(run.Hit(1, 690), Is.EqualTo(CircleRushJudgement.Good));
            Assert.That(run.BaseScore, Is.EqualTo(317));
            Assert.That(run.Combo, Is.EqualTo(2));
            Assert.That(run.Schedule.Deadline(2), Is.EqualTo(1631));
            Assert.That(run.Expire(2), Is.True);
            Assert.That(run.Combo, Is.EqualTo(0));
            Assert.That(run.Misses, Is.EqualTo(1));
            Assert.That(run.Hit(2, 1600), Is.Null, "resolved circles cannot be hit");
            Assert.That(run.Hit(3, 1000), Is.Null, "circles cannot be hit before they appear");
            Assert.That(run.Ended, Is.False, "a minute run only ends with the clock");

            run.FinishMinute();
            Assert.That(run.Ended, Is.True);
            Assert.That(run.DurationMs, Is.EqualTo(CircleRushRules.MINUTE_DURATION));
            Assert.That(run.Score, Is.EqualTo(317));
            Assert.That((run.Perfect, run.Good, run.Late), Is.EqualTo((1, 1, 0)));
            Assert.That(run.Events.Count, Is.EqualTo(3));

            var hidden = new CircleRushRun(CircleRushMode.Minute, CircleRushDifficulty.Medium, CircleRushMods.Hidden, 1);
            hidden.Hit(0, 200);
            hidden.Hit(1, 690);
            Assert.That(hidden.Score, Is.EqualTo(336));
        }

        [Test]
        public void TestEndlessEndsWithTheFifthMiss()
        {
            var run = new CircleRushRun(CircleRushMode.Endless, CircleRushDifficulty.Easy, CircleRushMods.None, 7);
            run.Hit(0, run.Schedule.SpawnTime(0) + 150);
            run.Hit(1, run.Schedule.SpawnTime(1) + 150);

            for (int index = 2; index < 6; index++)
                Assert.That(run.Expire(index), Is.True);

            Assert.That(run.LivesLeft, Is.EqualTo(1));
            Assert.That(run.Ended, Is.False);

            int clickTime = run.Schedule.Deadline(5) + 10;
            Assert.That(run.EmptyClick(clickTime), Is.True);
            Assert.That(run.Ended, Is.True);
            Assert.That(run.DurationMs, Is.EqualTo(clickTime));
            Assert.That(run.Misses, Is.EqualTo(5));
            Assert.That(run.EmptyClick(clickTime + 5), Is.False, "nothing counts after the end");

            var submission = CircleRushSubmission.FromRun(run);
            Assert.That(submission.Mode, Is.EqualTo("endless"));
            Assert.That(submission.Seed, Is.EqualTo(7));
            Assert.That(submission.Events.Count, Is.EqualTo(7));
            Assert.That(submission.Events[0], Is.EqualTo(new[] { 1, 0, 150, 150 }));
        }
    }
}
