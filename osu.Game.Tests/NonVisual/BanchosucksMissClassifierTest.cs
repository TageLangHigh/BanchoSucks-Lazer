// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksMissClassifierTest
    {
        // the windows of the test objects: hittable within +-150 ms, a press within +-400 ms still gives a judgement
        private const double hit_window = 150;
        private const double miss_window = 400;

        #region Classify

        [TestCase(-151, BanchosucksMissReason.TooEarly)]
        [TestCase(-300, BanchosucksMissReason.TooEarly)]
        [TestCase(-400, BanchosucksMissReason.TooEarly)]
        public void TestPressBeforeTheHitWindowIsTooEarly(double offset, BanchosucksMissReason expected)
        {
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, offset, hit_window, miss_window), Is.EqualTo(expected));
        }

        [Test]
        public void TestMissEarlierThanTheMissWindowWasForced()
        {
            // no press can be judged this early, so nothing is claimed about timing
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, -401, hit_window, miss_window), Is.EqualTo(BanchosucksMissReason.Missed));
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, -5000, hit_window, miss_window), Is.EqualTo(BanchosucksMissReason.Missed));
        }

        [TestCase(150.01)]
        [TestCase(166)]
        [TestCase(399)]
        public void TestWindowRunningOutIsNotHitInTime(double offset)
        {
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, offset, hit_window, miss_window), Is.EqualTo(BanchosucksMissReason.NotHitInTime));
        }

        [TestCase(-150)]
        [TestCase(0)]
        [TestCase(150)]
        public void TestMissInsideTheHitWindow(double offset)
        {
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, offset, hit_window, miss_window), Is.EqualTo(BanchosucksMissReason.InsideWindow));
        }

        [Test]
        public void TestClickBesideTheObjectIsAim()
        {
            // no neighbour at all
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 160, hit_window, miss_window, new BanchosucksMissClick(60, 32, null)),
                Is.EqualTo(BanchosucksMissReason.Aim));

            // nearer to this object than to the neighbour
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 160, hit_window, miss_window, new BanchosucksMissClick(60, 32, 200)),
                Is.EqualTo(BanchosucksMissReason.Aim));
        }

        [Test]
        public void TestClickMeantForTheNeighbourIsNotAim()
        {
            // the stored click has no time: one that was nearer to the neighbouring object says nothing about this one
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 160, hit_window, miss_window, new BanchosucksMissClick(200, 32, 40)),
                Is.EqualTo(BanchosucksMissReason.NotHitInTime));
        }

        [Test]
        public void TestClickOnTheObjectThatDidNotCount()
        {
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 160, hit_window, miss_window, new BanchosucksMissClick(10, 32, 5)),
                Is.EqualTo(BanchosucksMissReason.ClickIgnored));
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 160, hit_window, miss_window, new BanchosucksMissClick(32, 32, null)),
                Is.EqualTo(BanchosucksMissReason.ClickIgnored));
        }

        [Test]
        public void TestClickDoesNotChangeTimingReasons()
        {
            var besideTheCircle = new BanchosucksMissClick(60, 32, null);

            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, -200, hit_window, miss_window, besideTheCircle), Is.EqualTo(BanchosucksMissReason.TooEarly));
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 20, hit_window, miss_window, besideTheCircle), Is.EqualTo(BanchosucksMissReason.InsideWindow));
        }

        [Test]
        public void TestObjectsWithoutAHitWindow()
        {
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, 0, 0, 0), Is.EqualTo(BanchosucksMissReason.Missed));
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.LargeTickMiss, 0, 0, 0), Is.EqualTo(BanchosucksMissReason.TickMissed));
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.ComboBreak, -30, 0, 0), Is.EqualTo(BanchosucksMissReason.TickMissed));
        }

        [Test]
        public void TestTimedTickIsClassifiedByTiming()
        {
            // a slider head in classic scoring: a tick result, but with a hit window
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.LargeTickMiss, -200, hit_window, miss_window), Is.EqualTo(BanchosucksMissReason.TooEarly));
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.LargeTickMiss, 151, hit_window, miss_window), Is.EqualTo(BanchosucksMissReason.NotHitInTime));
        }

        [Test]
        public void TestWithoutAMissWindowTheHitWindowBounds()
        {
            Assert.That(BanchosucksMissClassifier.Classify(HitResult.Miss, -151, hit_window, 0), Is.EqualTo(BanchosucksMissReason.Missed));
        }

        [TestCase(HitResult.Great)]
        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.SmallTickMiss)]
        [TestCase(HitResult.IgnoreMiss)]
        public void TestOnlyComboBreaksAreAccepted(HitResult result)
        {
            Assert.That(() => BanchosucksMissClassifier.Classify(result, 0, hit_window, miss_window), Throws.ArgumentException);
        }

        #endregion

        #region Analyse

        [Test]
        public void TestOnlyMissesAreListedInMapOrder()
        {
            var a = new TestObject(1000);
            var b = new TestObject(2000);
            var c = new TestObject(3000);
            var d = new TestObject(4000);

            // judged out of map order on purpose
            var misses = BanchosucksMissClassifier.Analyse(new[]
            {
                hitEvent(c, HitResult.Miss, -220),
                hitEvent(a, HitResult.Great, 5),
                hitEvent(b, HitResult.Miss, 151),
                hitEvent(d, HitResult.Meh, 120),
            });

            Assert.That(misses.Select(m => m.Time), Is.EqualTo(new[] { 2000.0, 3000.0 }));
            Assert.That(misses.Select(m => m.Reason), Is.EqualTo(new[] { BanchosucksMissReason.NotHitInTime, BanchosucksMissReason.TooEarly }));
            Assert.That(misses[1].TimeOffset, Is.EqualTo(-220));
        }

        [Test]
        public void TestNoMisses()
        {
            Assert.That(BanchosucksMissClassifier.Analyse(new[] { hitEvent(new TestObject(1000), HitResult.Great, 0) }), Is.Empty);
            Assert.That(BanchosucksMissClassifier.Analyse(new List<HitEvent>()), Is.Empty);
        }

        [Test]
        public void TestAimUsesHitAreaAndNeighbours()
        {
            var a = new TestObject(1000);
            var b = new TestObject(2000);
            var c = new TestObject(3000);

            var areas = new TestHitAreas
            {
                [a] = (new Vector2(100, 100), 30),
                [b] = (new Vector2(300, 100), 30),
                [c] = (new Vector2(300, 300), 30),
            };

            var events = new[]
            {
                // clicked 50 px left of a's centre: beside it, and far from b
                hitEvent(a, HitResult.Miss, 151, new Vector2(50, 100)),
                // the same click is the closest one b has seen, but it was meant for a
                hitEvent(b, HitResult.Miss, 151, new Vector2(50, 100)),
                // clicked on c, but it did not count
                hitEvent(c, HitResult.Miss, 151, new Vector2(310, 300)),
            };

            var misses = BanchosucksMissClassifier.Analyse(events, areas);

            Assert.That(misses.Select(m => m.Reason), Is.EqualTo(new[] { BanchosucksMissReason.Aim, BanchosucksMissReason.NotHitInTime, BanchosucksMissReason.ClickIgnored }));
            Assert.That(misses[0].DistanceOutside, Is.EqualTo(20).Within(0.001));
            Assert.That(misses[1].DistanceOutside, Is.Null);

            // without hit areas (every ruleset but osu!) the positions are ignored
            Assert.That(BanchosucksMissClassifier.Analyse(events).Select(m => m.Reason), Is.All.EqualTo(BanchosucksMissReason.NotHitInTime));
        }

        [Test]
        public void TestMissWithoutAStoredClick()
        {
            var a = new TestObject(1000);
            var areas = new TestHitAreas { [a] = (new Vector2(100, 100), 30) };

            var misses = BanchosucksMissClassifier.Analyse(new[] { hitEvent(a, HitResult.Miss, 151) }, areas);

            Assert.That(misses.Single().Reason, Is.EqualTo(BanchosucksMissReason.NotHitInTime));
        }

        [Test]
        public void TestTicksOfOneSliderAreFolded()
        {
            var slider = new TestObject(1000, timed: false);
            var head = slider.Add(new TestObject(1000));
            var tick1 = slider.Add(new TestObject(1200, timed: false));
            var tick2 = slider.Add(new TestObject(1400, timed: false));
            var tick3 = slider.Add(new TestObject(1600, timed: false));

            var otherSlider = new TestObject(5000, timed: false);
            var otherTick = otherSlider.Add(new TestObject(5200, timed: false));

            var misses = BanchosucksMissClassifier.Analyse(new[]
            {
                hitEvent(head, HitResult.Great, 0),
                hitEvent(tick1, HitResult.LargeTickHit, 0),
                hitEvent(tick2, HitResult.LargeTickMiss, 0),
                hitEvent(tick3, HitResult.LargeTickMiss, 0),
                hitEvent(slider, HitResult.IgnoreHit, 0),
                hitEvent(otherTick, HitResult.LargeTickMiss, 0),
                hitEvent(otherSlider, HitResult.IgnoreHit, 0),
            });

            Assert.That(misses, Has.Count.EqualTo(2));
            Assert.That(misses[0], Is.EqualTo(new BanchosucksMiss(1400, BanchosucksMissReason.TickMissed, 0, null, 2)));
            Assert.That(misses[1], Is.EqualTo(new BanchosucksMiss(5200, BanchosucksMissReason.TickMissed, 0)));
        }

        [Test]
        public void TestMissedSliderIsExplainedByItsParts()
        {
            // classic scoring: the head is a tick with a hit window, the slider itself gets a miss at its end
            var slider = new TestObject(1000, timed: false);
            var head = slider.Add(new TestObject(1000));
            var tick = slider.Add(new TestObject(1300, timed: false));

            var misses = BanchosucksMissClassifier.Analyse(new[]
            {
                hitEvent(head, HitResult.LargeTickMiss, 151),
                hitEvent(tick, HitResult.LargeTickMiss, 0),
                hitEvent(slider, HitResult.Miss, 0),
            });

            Assert.That(misses.Select(m => (m.Time, m.Reason)), Is.EqualTo(new[]
            {
                (1000.0, BanchosucksMissReason.NotHitInTime),
                (1300.0, BanchosucksMissReason.TickMissed),
            }));
        }

        [Test]
        public void TestUntimedMissWithoutPartsIsListed()
        {
            // a dropped fruit, a spinner that was not completed
            var misses = BanchosucksMissClassifier.Analyse(new[] { hitEvent(new TestObject(7000, timed: false), HitResult.Miss, 0) });

            Assert.That(misses.Single(), Is.EqualTo(new BanchosucksMiss(7000, BanchosucksMissReason.Missed, 0)));
        }

        #endregion

        #region Describe

        [Test]
        public void TestTextsExistInBothLanguages()
        {
            foreach (BanchosucksMissReason reason in System.Enum.GetValues(typeof(BanchosucksMissReason)))
            {
                foreach (string ruleset in new[] { "osu", "taiko", "fruits", "mania" })
                {
                    (string english, string german) = BanchosucksMissClassifier.Describe(new BanchosucksMiss(1000, reason, -200, 20), ruleset);

                    Assert.That(english, Is.Not.Empty);
                    Assert.That(german, Is.Not.Empty);
                    Assert.That(german, Is.Not.EqualTo(english), $"{reason} in {ruleset}");
                }
            }
        }

        [Test]
        public void TestTextsCarryTheNumbers()
        {
            Assert.That(BanchosucksMissClassifier.Describe(new BanchosucksMiss(1000, BanchosucksMissReason.TooEarly, -212.4), "osu").german, Is.EqualTo("Zu früh (-212 ms)"));
            Assert.That(BanchosucksMissClassifier.Describe(new BanchosucksMiss(1000, BanchosucksMissReason.Aim, 160, 23.6f), "osu").english, Is.EqualTo("Aim: clicked 24 px beside the circle"));
            Assert.That(BanchosucksMissClassifier.Describe(new BanchosucksMiss(1000, BanchosucksMissReason.TickMissed, 0, null, 3), "osu").english, Does.Contain("3x"));
        }

        [Test]
        public void TestInsideWindowWordingDependsOnRuleset()
        {
            var miss = new BanchosucksMiss(1000, BanchosucksMissReason.InsideWindow, 10);

            Assert.That(BanchosucksMissClassifier.Describe(miss, "taiko").german, Is.EqualTo("Falsche Taste"));
            Assert.That(BanchosucksMissClassifier.Describe(miss, "osu").german, Does.StartWith("Übersprungen"));
            Assert.That(BanchosucksMissClassifier.Describe(miss, "mania").german, Does.StartWith("Rechtzeitig gedrückt"));
        }

        #endregion

        private static HitEvent hitEvent(HitObject hitObject, HitResult result, double offset, Vector2? position = null)
            => new HitEvent(offset, 1, result, hitObject, null, position);

        private class TestObject : HitObject
        {
            public TestObject(double startTime, bool timed = true)
            {
                StartTime = startTime;
                HitWindows = timed ? new TestHitWindows() : HitWindows.Empty;
            }

            public TestObject Add(TestObject nested)
            {
                AddNested(nested);
                return nested;
            }
        }

        private class TestHitWindows : HitWindows
        {
            public override bool IsHitResultAllowed(HitResult result) => result == HitResult.Great || result == HitResult.Meh || result == HitResult.Miss;

            public override void SetDifficulty(double difficulty)
            {
            }

            public override double WindowFor(HitResult result)
            {
                switch (result)
                {
                    case HitResult.Great:
                        return 50;

                    case HitResult.Meh:
                        return hit_window;

                    default:
                        return miss_window;
                }
            }
        }

        private class TestHitAreas : Dictionary<HitObject, (Vector2 centre, float radius)>, IBanchosucksHitAreaProvider
        {
            public (Vector2 centre, float radius)? GetHitArea(HitObject hitObject) => TryGetValue(hitObject, out var area) ? area : null;
        }
    }
}
