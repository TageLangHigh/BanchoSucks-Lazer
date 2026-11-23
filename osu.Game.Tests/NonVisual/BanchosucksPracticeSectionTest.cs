// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Practice;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksPracticeSectionTest
    {
        [Test]
        public void TestSectionInsideMapIsKept()
        {
            var section = BanchosucksPracticeSection.Create(30000, 45000, 0.75, 1000, 120000);

            Assert.That(section.StartTime, Is.EqualTo(30000));
            Assert.That(section.EndTime, Is.EqualTo(45000));
            Assert.That(section.Rate, Is.EqualTo(0.75));
            Assert.That(section.RatePercent, Is.EqualTo(75));
            Assert.That(section.Attempts, Is.Zero);
        }

        [Test]
        public void TestPointsAreClampedToMap()
        {
            var section = BanchosucksPracticeSection.Create(-5000, 500000, 1, 1000, 120000);

            Assert.That(section.StartTime, Is.EqualTo(1000));
            Assert.That(section.EndTime, Is.EqualTo(120000));
        }

        [Test]
        public void TestEndStaysBehindStart()
        {
            // end before start: the end is moved behind the start
            var section = BanchosucksPracticeSection.Create(30000, 20000, 1, 1000, 120000);
            Assert.That(section.StartTime, Is.EqualTo(30000));
            Assert.That(section.EndTime, Is.EqualTo(30000 + BanchosucksPracticeSection.MIN_LENGTH));

            // start at the very end of the map: the start has to give way
            section = BanchosucksPracticeSection.Create(120000, 120000, 1, 1000, 120000);
            Assert.That(section.EndTime, Is.EqualTo(120000));
            Assert.That(section.StartTime, Is.EqualTo(120000 - BanchosucksPracticeSection.MIN_LENGTH));
        }

        [Test]
        public void TestMapShorterThanMinimumLength()
        {
            var section = BanchosucksPracticeSection.Create(1500, 1600, 1, 1000, 2000);

            Assert.That(section.StartTime, Is.EqualTo(1000));
            Assert.That(section.EndTime, Is.EqualTo(2000));

            // a map of a single circle
            section = BanchosucksPracticeSection.Create(0, 0, 1, 700, 700);
            Assert.That(section.StartTime, Is.EqualTo(700));
            Assert.That(section.EndTime, Is.EqualTo(700));
        }

        [TestCase(0.1, 0.5)]
        [TestCase(0.5, 0.5)]
        [TestCase(0.749, 0.75)]
        [TestCase(1.0, 1.0)]
        [TestCase(1.5, 1.0)]
        [TestCase(double.NaN, 1.0)]
        public void TestRateIsClamped(double requested, double expected)
        {
            Assert.That(BanchosucksPracticeSection.ClampRate(requested), Is.EqualTo(expected));
            Assert.That(BanchosucksPracticeSection.Create(0, 10000, requested, 0, 10000).Rate, Is.EqualTo(expected));
        }

        [Test]
        public void TestFromFailStartsFiveSecondsEarlierAndRunsToTheEnd()
        {
            var section = BanchosucksPracticeSection.FromFail(60000, 1000, 120000);

            Assert.That(section.StartTime, Is.EqualTo(55000));
            Assert.That(section.EndTime, Is.EqualTo(120000));
            Assert.That(section.Rate, Is.EqualTo(1));
            Assert.That(section.RunsToMapEnd(120000), Is.True);
            Assert.That(section.StartsAtMapStart(1000), Is.False);
        }

        [Test]
        public void TestFromFailNearTheStartIsClampedToMapStart()
        {
            var section = BanchosucksPracticeSection.FromFail(3000, 1000, 120000);

            Assert.That(section.StartTime, Is.EqualTo(1000));
            Assert.That(section.StartsAtMapStart(1000), Is.True);
            Assert.That(section.EndTime, Is.EqualTo(120000));
        }

        [Test]
        public void TestFromFailAtTheLastObjectKeepsARunnableSection()
        {
            var section = BanchosucksPracticeSection.FromFail(120000, 1000, 120000);

            Assert.That(section.StartTime, Is.EqualTo(115000));
            Assert.That(section.EndTime, Is.EqualTo(120000));
        }

        [Test]
        public void TestRunsToMapEnd()
        {
            var section = BanchosucksPracticeSection.Create(10000, 20000, 1, 1000, 120000);

            Assert.That(section.RunsToMapEnd(120000), Is.False);
            Assert.That(section.RunsToMapEnd(20000), Is.True);
        }

        [TestCase(0, "00:00")]
        [TestCase(999, "00:00")]
        [TestCase(1000, "00:01")]
        [TestCase(59999, "00:59")]
        [TestCase(60000, "01:00")]
        [TestCase(83456, "01:23")]
        [TestCase(3725000, "62:05")]
        [TestCase(-2500, "00:00")]
        [TestCase(double.NaN, "00:00")]
        public void TestFormatTime(double milliseconds, string expected)
        {
            Assert.That(BanchosucksPracticeSection.FormatTime(milliseconds), Is.EqualTo(expected));
        }

        [Test]
        public void TestNotANumberIsRefused()
        {
            Assert.That(() => BanchosucksPracticeSection.Create(double.NaN, 10, 1, 0, 100), Throws.ArgumentException);
        }
    }
}
