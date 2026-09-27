// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Screens.Banchosucks;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksTapBpmMeterTest
    {
        [Test]
        public void TestStreamBpm()
        {
            var meter = new TapBpmMeter();
            const double interval = 1000.0 / 12; // 12 taps per second = 180 BPM

            for (int i = 0; i < 30; i++)
                meter.Tap(i * interval);

            double last = 29 * interval;
            Assert.That(meter.Bpm(last), Is.EqualTo(180).Within(0.01));
            Assert.That(meter.TapsPerSecond(last), Is.EqualTo(12).Within(0.01));
            Assert.That(meter.Best, Is.EqualTo(180).Within(0.01));
            Assert.That(meter.Bpm(last + 1500), Is.EqualTo(0), "stream is over after a pause");
        }

        [Test]
        public void TestFewTapsDoNotSetRecord()
        {
            var meter = new TapBpmMeter();

            // five very fast taps: shown while tapping, but no record
            for (int i = 0; i < 5; i++)
                meter.Tap(i * 20);

            Assert.That(meter.Bpm(80), Is.GreaterThan(0));
            Assert.That(meter.Best, Is.EqualTo(0));
        }

        [Test]
        public void TestPauseStartsNewStream()
        {
            var meter = new TapBpmMeter();

            for (int i = 0; i < 12; i++)
                meter.Tap(i * 100.0); // 10 taps/s = 150 BPM

            // after a pause, a slower stream must not be mixed with the old taps
            double start = 11 * 100.0 + 5000;
            for (int i = 0; i < 6; i++)
                meter.Tap(start + i * 200.0); // 5 taps/s = 75 BPM

            Assert.That(meter.Bpm(start + 5 * 200.0), Is.EqualTo(75).Within(0.01));
            Assert.That(meter.Best, Is.EqualTo(150).Within(0.01));
        }

        [Test]
        public void TestCapRefusesFastStream()
        {
            var meter = new TapBpmMeter { MaxBpm = 500 };
            const double interval = 1000.0 / (600.0 / 15); // 600 BPM = 40 taps per second

            int accepted = 0;
            for (int i = 0; i < 40; i++)
            {
                if (meter.Tap(i * interval))
                    accepted++;
            }

            Assert.That(accepted, Is.EqualTo(4), "only the taps before the window is known get through");
            Assert.That(meter.Refused, Is.EqualTo(36));
            Assert.That(meter.Bpm(39 * interval), Is.EqualTo(600).Within(1), "the display still shows the real speed");
            Assert.That(meter.Best, Is.EqualTo(0), "no record from a refused stream");
        }

        [Test]
        public void TestCapAllowsHumanStream()
        {
            var meter = new TapBpmMeter { MaxBpm = 500 };
            const double interval = 1000.0 / (320.0 / 15); // 320 BPM

            for (int i = 0; i < 40; i++)
                Assert.That(meter.Tap(i * interval), Is.True);

            Assert.That(meter.Best, Is.EqualTo(320).Within(0.5));
            Assert.That(meter.Refused, Is.EqualTo(0));
        }

        [Test]
        public void TestCapWithFrameQuantisedTimes()
        {
            // 450 BPM = 30 taps/s (33 ms) arrives as alternating 17/50 ms gaps at 60 fps; the window average must pass
            var meter = new TapBpmMeter { MaxBpm = 500 };
            double time = 0;

            for (int i = 0; i < 40; i++)
            {
                Assert.That(meter.Tap(time), Is.True, $"tap {i} at {time} ms");
                time += i % 2 == 0 ? 17 : 50;
            }

            // short windows see the quantisation more strongly than long ones; the record stays well under the cap
            Assert.That(meter.Best, Is.EqualTo(450).Within(30));
        }
    }
}
