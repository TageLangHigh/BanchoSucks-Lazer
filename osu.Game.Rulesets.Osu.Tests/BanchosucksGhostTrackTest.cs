// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Banchosucks: where the ghost cursor of the replay comparison is at a given time.
    /// </summary>
    [TestFixture]
    public class BanchosucksGhostTrackTest
    {
        private static BanchosucksGhostTrack createTrack() => new BanchosucksGhostTrack(new List<ReplayFrame>
        {
            new OsuReplayFrame(1000, new Vector2(100, 100)),
            new OsuReplayFrame(2000, new Vector2(200, 100), OsuAction.LeftButton),
            new OsuReplayFrame(2100, new Vector2(200, 100)),
            new OsuReplayFrame(3000, new Vector2(200, 300), OsuAction.RightButton),
        });

        [Test]
        public void TestPositionIsInterpolatedBetweenFrames()
        {
            var track = createTrack();

            Assert.That(track.PositionAt(1000), Is.EqualTo(new Vector2(100, 100)));
            Assert.That(track.PositionAt(1500), Is.EqualTo(new Vector2(150, 100)));
            Assert.That(track.PositionAt(1750), Is.EqualTo(new Vector2(175, 100)));
            Assert.That(track.PositionAt(2050), Is.EqualTo(new Vector2(200, 100)));
            Assert.That(track.PositionAt(2550), Is.EqualTo(new Vector2(200, 200)));
            Assert.That(track.PositionAt(3000), Is.EqualTo(new Vector2(200, 300)));
        }

        [Test]
        public void TestCursorRestsBeforeStartAndAfterEnd()
        {
            var track = createTrack();

            Assert.That(track.StartTime, Is.EqualTo(1000));
            Assert.That(track.EndTime, Is.EqualTo(3000));
            Assert.That(track.PositionAt(-5000), Is.EqualTo(new Vector2(100, 100)));
            Assert.That(track.PositionAt(999), Is.EqualTo(new Vector2(100, 100)));
            Assert.That(track.PositionAt(99999), Is.EqualTo(new Vector2(200, 300)));
            Assert.That(track.IndexAt(999), Is.EqualTo(-1));
            Assert.That(track.IndexAt(1000), Is.EqualTo(0));
            Assert.That(track.IndexAt(2099), Is.EqualTo(1));
            Assert.That(track.IndexAt(99999), Is.EqualTo(3));
        }

        [Test]
        public void TestPressedFollowsTheLastFrame()
        {
            var track = createTrack();

            Assert.That(track.PressedAt(500), Is.False, "before the replay");
            Assert.That(track.PressedAt(1500), Is.False);
            Assert.That(track.PressedAt(2000), Is.True);
            Assert.That(track.PressedAt(2099), Is.True);
            Assert.That(track.PressedAt(2100), Is.False);
            Assert.That(track.PressedAt(3500), Is.True, "the last frame holds the right button");
        }

        [Test]
        public void TestFramesAreSortedAndOtherFramesIgnored()
        {
            var track = new BanchosucksGhostTrack(new List<ReplayFrame>
            {
                new OsuReplayFrame(2000, new Vector2(200, 0)),
                new ReplayFrame(1500),
                new OsuReplayFrame(1000, new Vector2(100, 0)),
            });

            Assert.That(track.FrameCount, Is.EqualTo(2));
            Assert.That(track.PositionAt(1500), Is.EqualTo(new Vector2(150, 0)));
        }

        [Test]
        public void TestFramesAtTheSameTime()
        {
            var track = new BanchosucksGhostTrack(new List<ReplayFrame>
            {
                new OsuReplayFrame(1000, new Vector2(0, 0)),
                new OsuReplayFrame(1000, new Vector2(50, 50), OsuAction.LeftButton),
                new OsuReplayFrame(2000, new Vector2(150, 50)),
            });

            Assert.That(track.PositionAt(1000), Is.EqualTo(new Vector2(50, 50)), "the later of two frames at one time counts");
            Assert.That(track.PressedAt(1000), Is.True);
            Assert.That(track.PositionAt(1500), Is.EqualTo(new Vector2(100, 50)));
        }

        [Test]
        public void TestEmptyReplay()
        {
            var track = new BanchosucksGhostTrack(new List<ReplayFrame>());
            var trail = new List<Vector2> { new Vector2(1) };

            Assert.That(track.IsEmpty, Is.True);
            Assert.That(track.PositionAt(1000), Is.EqualTo(Vector2.Zero));
            Assert.That(track.PressedAt(1000), Is.False);

            track.CopyTrail(1000, 200, trail);
            Assert.That(trail, Is.Empty);
        }

        [Test]
        public void TestTrail()
        {
            var track = createTrack();
            var trail = new List<Vector2>();

            // from 1900 to 2400: starts on the line into the second frame, passes two frames, ends on the line to the last
            track.CopyTrail(2400, 500, trail);

            Assert.That(trail, Is.EqualTo(new[]
            {
                new Vector2(190, 100),
                new Vector2(200, 100),
                new Vector2(200, 100),
                new Vector2(200, 300 - 200 * 600f / 900),
            }).Using<Vector2>((a, b) => (a - b).Length < 0.01f));

            track.CopyTrail(500, 500, trail);
            Assert.That(trail, Is.Empty, "nothing before the replay starts");

            track.CopyTrail(1100, 500, trail);
            Assert.That(trail, Is.EqualTo(new[] { new Vector2(100, 100), new Vector2(110, 100) }), "the trail does not reach back before the first frame");

            track.CopyTrail(9000, 500, trail);
            Assert.That(trail, Is.EqualTo(new[] { new Vector2(200, 300) }), "after the end the cursor rests");
        }
    }
}
