// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksStutterAnalysisTest
    {
        private static BanchosucksFrameTimeHistogram frames(double frameTime, int count, params double[] extra)
        {
            var histogram = new BanchosucksFrameTimeHistogram();

            for (int i = 0; i < count; i++)
                histogram.Add(frameTime);

            foreach (double e in extra)
                histogram.Add(e);

            return histogram;
        }

        [Test]
        public void TestHistogramBasics()
        {
            var histogram = frames(1, 1000, 30, 80, 600, 2000);

            Assert.That(histogram.Count, Is.EqualTo(1004));
            Assert.That(histogram.Longest, Is.EqualTo(2000));
            Assert.That(histogram.Median(), Is.EqualTo(1).Within(BanchosucksFrameTimeHistogram.BUCKET_WIDTH));
            Assert.That(histogram.CountLongerThan(25), Is.EqualTo(4));
            Assert.That(histogram.CountLongerThan(50), Is.EqualTo(3));
            Assert.That(histogram.CountLongerThan(100), Is.EqualTo(2), "frames beyond the last bucket are still counted");
            Assert.That(histogram.CountLongerThan(5000), Is.EqualTo(0));

            histogram.Reset();
            Assert.That(histogram.Count, Is.EqualTo(0));
            Assert.That(histogram.Longest, Is.EqualTo(0));
            Assert.That(histogram.Median(), Is.EqualTo(0));
            Assert.That(histogram.CountLongerThan(0), Is.EqualTo(0));
        }

        [Test]
        public void TestHistogramIgnoresNonsense()
        {
            var histogram = new BanchosucksFrameTimeHistogram();

            histogram.Add(-5);
            histogram.Add(double.NaN);

            Assert.That(histogram.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestThresholdIsAbsoluteAtHighFrameRates()
        {
            // 1000 fps: three times the median would be 3 ms, which nobody notices
            var result = BanchosucksStutterAnalysis.Analyse(frames(1, 5000, 10, 20, 24, 26, 40));

            Assert.That(result.Threshold, Is.EqualTo(BanchosucksStutterAnalysis.MIN_STUTTER_MS));
            Assert.That(result.Stutters, Is.EqualTo(2));
            Assert.That(result.Worst, Is.EqualTo(40));
        }

        [Test]
        public void TestThresholdFollowsMedianAtLowFrameRates()
        {
            // 60 fps: a frame of 30 or 45 ms is a single dropped frame at most, 3x the median is 50 ms
            var result = BanchosucksStutterAnalysis.Analyse(frames(16.6, 3000, 30, 45, 60, 120));

            Assert.That(result.Median, Is.EqualTo(16.6).Within(BanchosucksFrameTimeHistogram.BUCKET_WIDTH));
            Assert.That(result.Threshold, Is.EqualTo(49.8).Within(3 * BanchosucksFrameTimeHistogram.BUCKET_WIDTH));
            Assert.That(result.Stutters, Is.EqualTo(2));
        }

        [Test]
        public void TestTooFewFramesAreIgnored()
        {
            var result = BanchosucksStutterAnalysis.Analyse(frames(1, 20, 200, 300, 400));

            Assert.That(result.Stutters, Is.EqualTo(0));
        }

        [Test]
        public void TestNoReportForSmoothPlay()
        {
            Assert.That(BanchosucksStutterAnalysis.CreateReport(frames(1, 5000), frames(4, 2000), default), Is.Null);
        }

        [Test]
        public void TestNoReportForTwoSmallStutters()
        {
            Assert.That(BanchosucksStutterAnalysis.CreateReport(frames(1, 5000, 30, 40), frames(4, 2000), default), Is.Null);
        }

        [Test]
        public void TestReportForThreeStutters()
        {
            var report = BanchosucksStutterAnalysis.CreateReport(frames(1, 5000, 30, 40, 55), frames(4, 2000, 60), default);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.Stutters, Is.EqualTo(3), "the larger of both threads, not the sum");
            Assert.That(report.Worst, Is.EqualTo(60), "the longest frame of either thread");
            Assert.That(report.Update.Stutters, Is.EqualTo(3));
            Assert.That(report.Draw.Stutters, Is.EqualTo(1));
            Assert.That(report.Hint, Is.EqualTo(BanchosucksStutterHint.None));
        }

        [Test]
        public void TestReportForOneLongStutter()
        {
            var report = BanchosucksStutterAnalysis.CreateReport(frames(1, 5000), frames(4, 2000, 180), default);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.Stutters, Is.EqualTo(1));
            Assert.That(report.Worst, Is.EqualTo(180));
        }

        [Test]
        public void TestEmptyDrawThreadIsFine()
        {
            var report = BanchosucksStutterAnalysis.CreateReport(frames(1, 5000, 30, 40, 55), new BanchosucksFrameTimeHistogram(), default);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.Stutters, Is.EqualTo(3));
            Assert.That(report.Worst, Is.EqualTo(55));
        }

        [Test]
        public void TestHintPriority()
        {
            var everything = new BanchosucksStutterEnvironment
            {
                WindowWasInactive = true,
                SingleThreaded = true,
                UnlimitedFrameLimiter = true,
                InputRateHz = 8000,
                AudioRateHz = 1000,
                StoryboardShown = true,
            };

            Assert.That(BanchosucksStutterAnalysis.PickHint(everything), Is.EqualTo(BanchosucksStutterHint.WindowInactive));
            Assert.That(BanchosucksStutterAnalysis.PickHint(everything with { WindowWasInactive = false }), Is.EqualTo(BanchosucksStutterHint.SingleThreaded));
            Assert.That(BanchosucksStutterAnalysis.PickHint(everything with { WindowWasInactive = false, SingleThreaded = false }), Is.EqualTo(BanchosucksStutterHint.UnlimitedFrameLimiter));
            Assert.That(BanchosucksStutterAnalysis.PickHint(new BanchosucksStutterEnvironment { InputRateHz = 1000, AudioRateHz = 4000, StoryboardShown = true }),
                Is.EqualTo(BanchosucksStutterHint.HighThreadRate));
            Assert.That(BanchosucksStutterAnalysis.PickHint(new BanchosucksStutterEnvironment { InputRateHz = 2000, AudioRateHz = 2000, StoryboardShown = true }),
                Is.EqualTo(BanchosucksStutterHint.Storyboard), "2000 Hz is not \"very high\"");
            Assert.That(BanchosucksStutterAnalysis.PickHint(new BanchosucksStutterEnvironment { InputRateHz = 1000, AudioRateHz = 1000 }), Is.EqualTo(BanchosucksStutterHint.None));
        }

        [Test]
        public void TestDescription()
        {
            var report = BanchosucksStutterAnalysis.CreateReport(frames(1, 5000, 30, 40, 83.6), new BanchosucksFrameTimeHistogram(),
                new BanchosucksStutterEnvironment { InputRateHz = 8000, AudioRateHz = 1000 })!;

            string german = BanchosucksStutterAnalysis.Describe(report, true);
            string english = BanchosucksStutterAnalysis.Describe(report, false);

            Assert.That(german, Does.StartWith("Ruckler-Bericht: 3 Ruckler in dieser Map, längster Frame 84 ms."));
            Assert.That(german, Does.Contain("8000 Hz"));
            Assert.That(english, Does.StartWith("Stutter report: 3 stutters in this map, longest frame 84 ms."));
            Assert.That(english, Does.Contain("8000 Hz"));

            var single = BanchosucksStutterAnalysis.CreateReport(frames(1, 5000, 250), new BanchosucksFrameTimeHistogram(), default)!;
            Assert.That(BanchosucksStutterAnalysis.Describe(single, false), Is.EqualTo("Stutter report: 1 stutter in this map, longest frame 250 ms."), "no hint, no trailing text");
        }
    }
}
