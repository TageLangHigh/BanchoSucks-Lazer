// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Guess;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The numbers here are the same as in the plugin's test_rules.py.
    /// </summary>
    [TestFixture]
    public class BanchosucksGuessRulesTest
    {
        [Test]
        public void TestRoundPoints()
        {
            Assert.That(GuessRules.RoundPoints(15_000, 1), Is.EqualTo(250));
            Assert.That(GuessRules.RoundPoints(7_400, 1), Is.EqualTo(170));
            Assert.That(GuessRules.RoundPoints(0, 1), Is.EqualTo(100));
            Assert.That(GuessRules.RoundPoints(12_000, 3), Is.EqualTo(240));
            Assert.That(GuessRules.RoundPoints(15_000, 10), Is.EqualTo(300), "the streak bonus stops at 50");
            Assert.That(GuessRules.RoundPoints(-5, 0), Is.EqualTo(100));
            Assert.That(GuessRules.MAX_SCORE, Is.EqualTo(3000));
        }

        [Test]
        public void TestRun()
        {
            var run = new GuessRun();
            Assert.That(run.Answer(true, 0), Is.EqualTo(250));
            Assert.That(run.Answer(true, 3_000), Is.EqualTo(230), "220 for twelve seconds left plus 10 streak bonus");
            Assert.That(run.Answer(false, 5_000), Is.EqualTo(0));
            Assert.That(run.Answer(true, 15_000), Is.EqualTo(100), "the streak starts over");
            Assert.That((run.Score, run.Correct, run.Rounds, run.Streak, run.BestStreak), Is.EqualTo((580, 3, 4, 1, 2)));
            Assert.That(run.AverageAnswerMs, Is.EqualTo(6_000));
            Assert.That(run.Finished, Is.False);

            for (int i = 0; i < 6; i++) run.Answer(false, 15_000);
            Assert.That(run.Finished, Is.True);
            Assert.That(run.Rounds, Is.EqualTo(10));
        }

        [Test]
        public void TestEndlessRunEndsWithTheFirstMistake()
        {
            var run = new GuessRun(true);
            Assert.That(run.Answer(true, 0), Is.EqualTo(250));
            Assert.That(run.Answer(true, 1_000), Is.EqualTo(250), "240 for fourteen seconds left plus 10 streak bonus");
            Assert.That(run.Finished, Is.False);
            Assert.That(run.Answer(false, 3_000), Is.EqualTo(0));
            Assert.That(run.Ended, Is.True);
            Assert.That(run.Finished, Is.True);
            Assert.That((run.Score, run.Correct, run.Rounds), Is.EqualTo((500, 2, 3)));
            Assert.That(GuessRules.KindFor("song", GuessMode.Popular), Is.EqualTo("song_endless"));
            Assert.That(GuessRules.KindFor("skin", GuessMode.Library), Is.EqualTo("skin"));
        }
    }
}
